using System.Reflection;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AiOrchestrator.ArchTests;

/// <summary>
/// #13 — the pipeline's authorization decorator, and the property that makes it worth having:
/// forgetting to declare must fail closed. Composition-level, with the real
/// <c>AddVsaCqsArchitecture</c> and this assembly's own handlers, so the decorator under test is
/// the one the product composes and not a copy of its logic.
/// <para>
/// The reflection sweep at the bottom is the other half. The runtime tests prove an undeclared
/// operation is refused; the sweep proves no real operation is relying on that refusal, which is a
/// different claim and the one a reviewer actually wants.
/// </para>
/// </summary>
public class ProjectRoles_Should_Constraint
{
    static IServiceProvider Pipeline(ProjectRole? role) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProjectPermissions>(new FixedPermissions(role))
            .AddVsaCqsArchitecture(Assembly.GetExecutingAssembly())
            .BuildServiceProvider();

    static async Task<TResponse> Send<TResponse>(ProjectRole? role, ICommand<TResponse> command)
    {
        await using var scope = Pipeline(role).CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    [Fact]
    public async Task AnUndeclaredOperation_Should_BeRefusedEvenFromAnAdmin()
    {
        // The default-deny that design D1 exists for: a use case added without thinking is locked,
        // not open. Admin is the caller here on purpose — if the omission were treated as "no
        // requirement", this would pass and the test would prove nothing.
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            Send(ProjectRole.Admin, new Undeclared())
        );
    }

    [Fact]
    public async Task AMemberOnTheProject_Should_BeRefusedAnAdminOperation()
    {
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            Send(ProjectRole.Member, new NeedsAdmin(Guid.NewGuid()))
        );
    }

    [Fact]
    public async Task AnAdminOnTheProject_Should_ReachTheHandler()
    {
        (await Send(ProjectRole.Admin, new NeedsAdmin(Guid.NewGuid()))).ShouldBe("handled");
    }

    [Fact]
    public async Task AMemberOnTheProject_Should_ReachAMemberOperation()
    {
        (await Send(ProjectRole.Member, new NeedsMember(Guid.NewGuid()))).ShouldBe("handled");
    }

    [Fact]
    public async Task SomebodyHoldingNoRole_Should_BeRefusedEvenAMemberOperation()
    {
        // Null is "no row", which is also the answer for a project that does not exist — the two
        // must be indistinguishable or a refusal becomes a way to enumerate projects.
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            Send(null, new NeedsMember(Guid.NewGuid()))
        );
    }

    [Fact]
    public async Task AProjectScopedDeclarationWithNoProject_Should_FailLoudlyRatherThanRefuse()
    {
        // A wiring mistake, not a permission decision. Refusing would hide it behind a 403 that
        // looks like an ordinary refusal to whoever hits it.
        var mistake = await Should.ThrowAsync<InvalidOperationException>(() =>
            Send(ProjectRole.Admin, new ScopelessButScoped())
        );

        mistake.Message.ShouldContain(nameof(IScopedToProject));
    }

    [Fact]
    public async Task AQuery_Should_BeCheckedToo()
    {
        // The query chain is decorated separately, and its position matters more than the command
        // one: authorization sits OUTSIDE caching, so a response cached for somebody allowed cannot
        // be handed to somebody who is not.
        await using var refused = Pipeline(null).CreateAsyncScope();
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            refused
                .ServiceProvider.GetRequiredService<ISender>()
                .Send(new MemberQuery(Guid.NewGuid()))
        );

        await using var allowed = Pipeline(ProjectRole.Member).CreateAsyncScope();
        (
            await allowed
                .ServiceProvider.GetRequiredService<ISender>()
                .Send(new MemberQuery(Guid.NewGuid()))
        ).ShouldBe("handled");
    }

    [Fact]
    public void EveryRequestInTheProduct_Should_DeclareWhatItRequires()
    {
        var undeclared = Requests()
            .Where(request => request.GetCustomAttribute<RequiresAttribute>() is null)
            .Select(request => request.FullName!)
            .ToList();

        // The pipeline would refuse these at runtime, which is the safe failure — but "every button
        // in this feature is broken" is a worse way to find out than a red test.
        undeclared.ShouldBeEmpty();
    }

    [Fact]
    public void EveryProjectScopedRequest_Should_NameItsProject()
    {
        var scoped = new[] { Access.AdminOfProject, Access.MemberOfProject };

        var unpaired = Requests()
            .Where(request =>
                request.GetCustomAttribute<RequiresAttribute>() is { } declared
                && scoped.Contains(declared.Access)
                && !typeof(IScopedToProject).IsAssignableFrom(request)
            )
            .Select(request => request.FullName!)
            .ToList();

        unpaired.ShouldBeEmpty();
    }

    /// <summary>Every command and query the product actually dispatches.</summary>
    static IEnumerable<Type> Requests() =>
        ModuleAssemblies
            .Implementations.SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && (
                            contract.GetGenericTypeDefinition() == typeof(ICommand<>)
                            || contract.GetGenericTypeDefinition() == typeof(IQuery<>)
                        )
                    )
            );

    sealed class FixedPermissions(ProjectRole? role) : IProjectPermissions
    {
        public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(role);

        public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(null);
    }

    // Deliberately carries no [Requires]: this type IS the test.
    internal sealed record Undeclared : ICommand<string>;

    [Requires(Access.AdminOfProject)]
    internal sealed record NeedsAdmin(Guid ProjectId) : ICommand<string>, IScopedToProject;

    [Requires(Access.MemberOfProject)]
    internal sealed record NeedsMember(Guid ProjectId) : ICommand<string>, IScopedToProject;

    // The pairing mistake: declares a project-scoped requirement, names no project.
    [Requires(Access.AdminOfProject)]
    internal sealed record ScopelessButScoped : ICommand<string>;

    // A query as well as commands: AddVsaCqsArchitecture decorates both open handler interfaces and
    // Scrutor refuses to decorate one with no registrations, so an assembly of commands alone cannot
    // compose the real pipeline at all. Found by composing it.
    [Requires(Access.MemberOfProject)]
    internal sealed record MemberQuery(Guid ProjectId) : IQuery<string>, IScopedToProject;

    internal sealed class UndeclaredHandler : IAppCommandHandler<Undeclared, string>
    {
        public Task<string> Handle(Undeclared command, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }

    internal sealed class NeedsAdminHandler : IAppCommandHandler<NeedsAdmin, string>
    {
        public Task<string> Handle(NeedsAdmin command, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }

    internal sealed class NeedsMemberHandler : IAppCommandHandler<NeedsMember, string>
    {
        public Task<string> Handle(NeedsMember command, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }

    internal sealed class ScopelessHandler : IAppCommandHandler<ScopelessButScoped, string>
    {
        public Task<string> Handle(
            ScopelessButScoped command,
            CancellationToken cancellationToken
        ) => Task.FromResult("handled");
    }

    internal sealed class MemberQueryHandler : IAppQueryHandler<MemberQuery, string>
    {
        public Task<string> Handle(MemberQuery query, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }
}
