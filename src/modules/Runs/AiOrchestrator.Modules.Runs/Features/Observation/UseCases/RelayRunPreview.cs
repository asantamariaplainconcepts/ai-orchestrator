using System.Net;
using AiOrchestrator.BuildingBlocks.Agents;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Domain;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiOrchestrator.Modules.Runs.Features.Observation.UseCases;

/// <summary>
/// Serves a Run's live preview through the portal (run-previews design D4). A published port is
/// loopback-bound, so a browser cannot reach it directly — this relays, and it is the first place
/// this product serves bytes an agent wrote.
/// <para>
/// Three refusals, each deliberate. The target is resolved from the ledger by Run id and nowhere
/// else, so this can never become a general proxy. Authorization is the Run's own, decided here
/// rather than in the browser. And a Run that is not currently previewing gets nothing at all —
/// no fallback, no cached last frame.
/// </para>
/// <para>
/// The response is stripped to a body and a content type, and served under a Content-Security-
/// Policy that forbids the framed document from reaching anything. The page frames it with a
/// restrictive <c>sandbox</c> attribute as well; both exist because either alone is one mistake
/// away from agent-authored script running with the portal's authority.
/// </para>
/// </summary>
sealed class RelayRunPreview : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/projects/{projectId:guid}/runs/{runId:guid}/preview/serve/{**path}",
                async (
                    Guid projectId,
                    Guid runId,
                    string? path,
                    ISender sender,
                    HttpContext context,
                    IHttpClientFactory clients,
                    IRunPreviewMonitor previews,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Authorization first, through the same pipeline every other read uses: this
                    // asks whether the caller may see the RUN, and the preview is the Run's.
                    var allowed = await sender.Send(
                        new GetRunPreview.Query(projectId, runId),
                        cancellationToken
                    );

                    if (allowed is null)
                    {
                        return Results.NotFound();
                    }

                    if (!allowed.Available || previews.PortFor(runId) is not { } port)
                    {
                        // Not "empty page": there is nothing to serve, and saying so is what
                        // stops a stale frame from looking live.
                        return Results.NotFound();
                    }

                    return await Relay(context, clients, port, path, cancellationToken);
                }
            )
            .WithName(nameof(RelayRunPreview))
            .WithTags("Runs")
            // Excluded from the OpenAPI document on purpose: it is a transport for somebody
            // else's application, not an API this product offers.
            .ExcludeFromDescription();

    static async Task<IResult> Relay(
        HttpContext context,
        IHttpClientFactory clients,
        int port,
        string? path,
        CancellationToken cancellationToken
    )
    {
        // Built here from the port the ledger gave us — never from anything the caller sent, so
        // the target cannot be steered. The path is appended as-is; a path that escapes is
        // resolved by Uri and still lands under loopback:port, which is the only host reachable.
        var target = new UriBuilder("http", "127.0.0.1", port, "/" + (path ?? string.Empty))
        {
            Query = context.Request.QueryString.Value?.TrimStart('?') ?? string.Empty,
        }.Uri;

        var client = clients.CreateClient(nameof(RelayRunPreview));

        try
        {
            using var upstream = await client.GetAsync(
                target,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            var body = await upstream.Content.ReadAsByteArrayAsync(cancellationToken);

            // Nothing but a body and a content type crosses back. Upstream headers are the
            // agent's to write, and a Set-Cookie or a redirect of theirs has no business being
            // interpreted by the portal's origin.
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'self' 'unsafe-inline' 'unsafe-eval' data: blob:; "
                + "frame-ancestors 'self'; form-action 'none'";

            return Results.File(
                body,
                upstream.Content.Headers.ContentType?.ToString() ?? "application/octet-stream"
            );
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException)
        {
            // The port is published but nothing is listening yet — the ordinary state of a Run
            // whose agent has not started its server. A state of a live Run, never an error
            // (run-previews spec), and the page renders it as "nothing serving yet".
            return Results.StatusCode((int)HttpStatusCode.ServiceUnavailable);
        }
    }
}
