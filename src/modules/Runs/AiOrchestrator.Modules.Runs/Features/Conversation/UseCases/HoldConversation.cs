using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Backlog.Contracts;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using DomainConversation = AiOrchestrator.Modules.Runs.Domain.Conversation;

namespace AiOrchestrator.Modules.Runs.Features.Conversation.UseCases;

/// <summary>
/// #166 — a Member talks to an agent about a project, or about one of its Stories.
/// <para>
/// Three operations in one slice because they are one capability, and separating them would put
/// the same permission and the same "does this project exist to you" question in three files.
/// </para>
/// <para>
/// <b>Nothing here creates a Run.</b> No cap check, no story lock, no dispatch: that absence is the
/// feature, and it is why an Automation on a Story with an open conversation still fires (BR-001
/// untouched).
/// </para>
/// </summary>
sealed class HoldConversation : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/conversations",
                async (
                    Guid projectId,
                    StartRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Start(projectId, request.VendorStoryId),
                        cancellationToken
                    );

                    return result.Match(
                        response =>
                            Results.Created(
                                $"/api/projects/{projectId}/conversations/{response.Id}",
                                response
                            ),
                        ApiResults.Problem
                    );
                }
            )
            .WithName(nameof(Start))
            .WithTags("Runs");

        endpoints
            .MapPost(
                "/api/projects/{projectId:guid}/conversations/{conversationId:guid}/messages",
                async (
                    Guid projectId,
                    Guid conversationId,
                    MessageRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Say(projectId, conversationId, request.Body),
                        cancellationToken
                    );

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(Say))
            .WithTags("Runs");

        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/conversations/{conversationId:guid}",
                async (
                    Guid projectId,
                    Guid conversationId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(
                        new Read(projectId, conversationId),
                        cancellationToken
                    );

                    return result.Match(response => Results.Ok(response), ApiResults.Problem);
                }
            )
            .WithName(nameof(Read))
            .WithTags("Runs");
    }

    internal sealed record StartRequest(string? VendorStoryId = null);

    internal sealed record MessageRequest(string Body);

    /// <summary>
    /// <paramref name="SpendIsComplete"/> is BR-011 made visible: false means some pass reported no
    /// usage, so the total is a floor. A surface that showed the number alone would be presenting a
    /// guess as a fact (design D4).
    /// </summary>
    internal sealed record Response(
        Guid Id,
        Guid ProjectId,
        string? VendorStoryId,
        DateTimeOffset StartedAt,
        DateTimeOffset LastActivityAt,
        decimal SpendUsd,
        bool SpendIsComplete,
        IReadOnlyList<MessageResponse> Messages
    );

    internal sealed record MessageResponse(
        Guid Id,
        string Role,
        string Body,
        DateTimeOffset CreatedAt,
        bool Failed,
        long? InputTokens,
        long? OutputTokens,
        decimal? CostUsd
    );

    [Requires(RunPermissions.HoldConversation)]
    internal sealed record Start(Guid ProjectId, string? VendorStoryId)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    [Requires(RunPermissions.HoldConversation)]
    internal sealed record Say(Guid ProjectId, Guid ConversationId, string Body)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject;

    [Requires(RunPermissions.HoldConversation)]
    internal sealed record Read(Guid ProjectId, Guid ConversationId)
        : IQuery<ErrorOr<Response>>,
            IScopedToProject;

    internal sealed class SayValidator : AbstractValidator<Say>
    {
        public SayValidator() => RuleFor(command => command.Body).NotEmpty().MaximumLength(10_000);
    }

    internal sealed class StartValidator : AbstractValidator<Start>
    {
        public StartValidator() => RuleFor(command => command.VendorStoryId).MaximumLength(200);
    }

    internal sealed class StartHandler(RunsDbContext database, TimeProvider clock)
        : IAppCommandHandler<Start, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Start command,
            CancellationToken cancellationToken
        )
        {
            var conversation = DomainConversation.Start(
                command.ProjectId,
                string.IsNullOrWhiteSpace(command.VendorStoryId) ? null : command.VendorStoryId,
                clock.GetUtcNow()
            );

            database.Conversations.Add(conversation);
            await database.SaveChangesAsync(cancellationToken);

            return ToResponse(conversation);
        }
    }

    internal sealed class SayHandler(
        RunsDbContext database,
        IConnectorReader connectors,
        IStoryReader stories,
        IConversationRuntime runtime,
        TimeProvider clock
    ) : IAppCommandHandler<Say, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Say command,
            CancellationToken cancellationToken
        )
        {
            var conversation = await Find(
                database,
                command.ProjectId,
                command.ConversationId,
                cancellationToken
            );
            if (conversation is null)
            {
                return ConversationErrors.NotFound(command.ConversationId);
            }

            // The agent is given the project's repository, so answers are grounded in the code and
            // not only in the mirror (#166). Without a Connector there is no repository and no
            // credential, so there is nothing to ground anything in — said plainly rather than
            // answered from the mirror alone and quietly worse.
            var connector = await connectors.Find(command.ProjectId, cancellationToken);
            if (connector is null)
            {
                return ConversationErrors.NoConnector();
            }

            // Recorded before the pass runs: a crash mid-pass leaves the question in the
            // conversation rather than losing what somebody typed.
            // Added explicitly, not left to graph discovery. BaseEntity sets its own GUID v7 in the
            // constructor, so a child reached through a tracked parent's navigation already has a key
            // — and EF reads that as "this row exists", turning the insert into an update of nothing.
            // It surfaced as DbUpdateConcurrencyException about a row nobody had written.
            database.Add(conversation.Ask(command.Body, clock.GetUtcNow()));
            await database.SaveChangesAsync(cancellationToken);

            var storyContext = conversation.VendorStoryId is null
                ? null
                : await StoryContext(stories, conversation, cancellationToken);

            var reply = await runtime.Answer(
                conversation.Id,
                new ConversationContext(
                    command.ProjectId,
                    connector.SecretName,
                    new CodeCoordinates(connector.Owner, connector.Repository),
                    storyContext
                ),
                command.Body,
                cancellationToken
            );

            // A failure is a message, not an ending: the conversation stays open and takes another
            // (#166). Nothing about this returns an error to the caller — the exchange is the
            // answer, and it now contains a failure the person can read.
            database.Add(
                reply.Succeeded
                    ? conversation.Answer(
                        reply.Body,
                        clock.GetUtcNow(),
                        reply.Usage?.InputTokens,
                        reply.Usage?.OutputTokens,
                        reply.Usage?.CostUsd
                    )
                    : conversation.Fail(reply.Body, clock.GetUtcNow())
            );

            await database.SaveChangesAsync(cancellationToken);

            return ToResponse(conversation);
        }

        /// <summary>
        /// The Story as the mirror holds it (BR-008). A Story the mirror no longer has is not a
        /// failure: the conversation carries on about the project, which is what it would have been
        /// had nobody named a subject.
        /// </summary>
        static async Task<string?> StoryContext(
            IStoryReader stories,
            DomainConversation conversation,
            CancellationToken cancellationToken
        )
        {
            var story = await stories.Find(
                conversation.ProjectId,
                conversation.VendorStoryId!,
                cancellationToken
            );

            return story is null ? null : $"{story.Title}\n\n{story.Body}";
        }
    }

    internal sealed class ReadHandler(RunsDbContext database)
        : IAppQueryHandler<Read, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Read query, CancellationToken cancellationToken)
        {
            var conversation = await Find(
                database,
                query.ProjectId,
                query.ConversationId,
                cancellationToken
            );

            return conversation is null
                ? ConversationErrors.NotFound(query.ConversationId)
                : ToResponse(conversation);
        }
    }

    /// <summary>
    /// Scoped to the project in the route, always. The pipeline has already checked the caller holds
    /// the permission on <i>that</i> project, so looking the conversation up by id alone would let a
    /// Member of one project read another's by guessing an id.
    /// </summary>
    static Task<DomainConversation?> Find(
        RunsDbContext database,
        Guid projectId,
        Guid conversationId,
        CancellationToken cancellationToken
    ) =>
        database.Conversations.FirstOrDefaultAsync(
            entity => entity.Id == conversationId && entity.ProjectId == projectId,
            cancellationToken
        );

    static Response ToResponse(DomainConversation conversation)
    {
        var (spend, complete) = conversation.Spend();

        return new Response(
            conversation.Id,
            conversation.ProjectId,
            conversation.VendorStoryId,
            conversation.StartedAt,
            conversation.LastActivityAt,
            spend,
            complete,
            [
                .. conversation
                    .Messages.OrderBy(message => message.CreatedAt)
                    .Select(message => new MessageResponse(
                        message.Id,
                        message.Role.ToString(),
                        message.Body,
                        message.CreatedAt,
                        message.Failed,
                        message.InputTokens,
                        message.OutputTokens,
                        message.CostUsd
                    )),
            ]
        );
    }
}
