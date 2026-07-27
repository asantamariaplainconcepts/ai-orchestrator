using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Modules.Backlog.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Backlog.Features.Backlog.UseCases;

/// <summary>
/// UC-010 — a vendor event makes the product look now rather than at the next poll.
/// <para>
/// The payload is a <b>hint</b>, never data (design D1): nothing is read from it but the
/// repository it names, and the reconciliation that follows is the same one the poller runs.
/// That is how BR-015's "identical events" becomes structural instead of two implementations
/// promising to agree.
/// </para>
/// <para>
/// The endpoint is unauthenticated by necessity and triggers work, so the signature check is
/// mandatory and constant-time (D2), and every refusal answers alike so an unauthenticated
/// caller learns nothing about which repositories exist (D3).
/// </para>
/// </summary>
sealed class IngestWebhook : IUseCase
{
    public const string SignatureHeader = "X-Hub-Signature-256";

    public const string EventHeader = "X-GitHub-Event";

    /// <summary>Events worth a look. Anything else is acknowledged and ignored (design D4).</summary>
    static readonly HashSet<string> Interesting = new(StringComparer.Ordinal)
    {
        "issues",
        "issue_comment",
        "label",
    };

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/webhooks/github",
                async (HttpRequest request, HttpContext context, CancellationToken cancellation) =>
                {
                    var services = context.RequestServices;
                    var logger = services
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger(nameof(IngestWebhook));

                    // A vendor that receives errors eventually stops delivering, so anything we
                    // simply do not act on is a success (design D4).
                    var eventName = request.Headers[EventHeader].ToString();
                    if (!Interesting.Contains(eventName))
                    {
                        return Results.Ok();
                    }

                    request.EnableBuffering();
                    using var reader = new StreamReader(request.Body, leaveOpen: true);
                    var body = await reader.ReadToEndAsync(cancellation);
                    request.Body.Position = 0;

                    var repository = RepositoryFrom(body);
                    if (repository is null)
                    {
                        return Refused();
                    }

                    var database = services.GetRequiredService<BacklogDbContext>();
                    var connector = await database.Connectors.FirstOrDefaultAsync(
                        entity =>
                            entity.Owner == repository.Value.Owner
                            && entity.Repository == repository.Value.Name
                            && entity.WebhookSecretName != null,
                        cancellation
                    );

                    if (connector?.WebhookSecretName is null)
                    {
                        // Same answer as a bad signature: distinguishing them would tell an
                        // unauthenticated caller which repositories this installation watches.
                        return Refused();
                    }

                    string secret;
                    try
                    {
                        secret = await services
                            .GetRequiredService<ISecretResolver>()
                            .Resolve(connector.WebhookSecretName, cancellation);
                    }
                    catch (SecretNotFoundException)
                    {
                        WebhookLog.SecretMissing(logger, connector.WebhookSecretName);
                        return Refused();
                    }

                    if (
                        !SignatureMatches(request.Headers[SignatureHeader].ToString(), body, secret)
                    )
                    {
                        WebhookLog.SignatureRejected(logger, repository.Value.Owner);
                        return Refused();
                    }

                    // The same call the poller makes — one producer of story events (design D1).
                    var synchroniser = services.GetRequiredService<BacklogSynchroniser>();
                    await synchroniser.Synchronise(connector.ProjectId, cancellation);

                    return Results.Ok();
                }
            )
            .WithName(nameof(IngestWebhook))
            .WithTags("Backlog")
            .AllowAnonymous();

    /// <summary>One answer for every refusal — see design D3.</summary>
    static IResult Refused() => Results.Unauthorized();

    static (string Owner, string Name)? RepositoryFrom(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            if (
                !document.RootElement.TryGetProperty("repository", out var repository)
                || !repository.TryGetProperty("name", out var name)
                || !repository.TryGetProperty("owner", out var owner)
                || !owner.TryGetProperty("login", out var login)
            )
            {
                return null;
            }

            return (login.GetString() ?? string.Empty, name.GetString() ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static bool SignatureMatches(string header, string body, string secret)
    {
        const string Prefix = "sha256=";

        if (!header.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var expected = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body))
        );

        // Fixed-time: a comparison that returns early turns this endpoint into an oracle.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(header[Prefix.Length..])
        );
    }
}

static partial class WebhookLog
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "Rejected a webhook for {Owner}: the signature did not match"
    )]
    public static partial void SignatureRejected(ILogger logger, string owner);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Error,
        Message = "A Connector names webhook secret {SecretName}, which does not exist — its webhooks are all being refused"
    )]
    public static partial void SecretMissing(ILogger logger, string secretName);
}
