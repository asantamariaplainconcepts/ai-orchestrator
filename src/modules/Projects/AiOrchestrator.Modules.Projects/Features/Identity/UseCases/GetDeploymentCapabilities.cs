using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Secrets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Projects.Features.Identity.UseCases;

/// <summary>
/// What this deployment can offer (#222). Beside <see cref="GetCurrentPrincipal"/> because that
/// answers "what is this habitat, for this caller" and this answers "what is this habitat" — both
/// derive from <see cref="IdentityHabitat"/>, in one module, so the portal and the API cannot
/// disagree about which deployment this is.
/// <para>
/// It replaces an inference: until now the portal learned the posture by sending a deliberately
/// invalid validate-path and reading the 404 (#211). That worked and read like a trick.
/// </para>
/// <para>
/// <b>Capabilities, not configuration</b> (design D2). No mode string, no vault URI: a client that
/// learns <i>what it may offer</i> stays right when the underlying condition changes, and one that
/// learns the mode re-derives the rules and drifts.
/// </para>
/// </summary>
sealed class GetDeploymentCapabilities : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/capabilities",
                (IConfiguration configuration, ISecretStore secrets) =>
                    Results.Ok(
                        new Response(
                            // A folder on this machine is only nameable where the identity says
                            // this machine is somebody's own (#210).
                            HasCodeSource: IdentityHabitat.IsSelfHost(configuration),
                            // Deliberately NOT the posture (design D3). Naming a secret works in
                            // every habitat — a resolver is always composed. Storing one needs a
                            // store that accepts writes, and a deployment without one composes
                            // the unavailable store, whose every write throws. A self-host
                            // deployment configured with a directory stores perfectly well, so
                            // gating this on the posture would remove a working option from the
                            // habitat it exists to serve.
                            CanStoreSecret: secrets is not UnavailableSecretStore,
                            // The remedy the store itself names, so the portal states how to gain
                            // the option rather than only that it is missing.
                            StoreRemedy: (secrets as UnavailableSecretStore)?.Remedy,
                            // The Local locus, as the habitat declares it (#247): self-host with
                            // no declared reason offers it; compose self-host declares why not.
                            CanUseLocalFolder: IdentityHabitat.IsSelfHost(configuration)
                                && IdentityHabitat.LocalFolderUnavailableReason(configuration)
                                    is null,
                            LocalFolderReason: IdentityHabitat.LocalFolderUnavailableReason(
                                configuration
                            )
                        )
                    )
            )
            .WithName(nameof(GetDeploymentCapabilities))
            .WithTags("Identity")
            // Answerable before anyone signs in (design D4): a sign-in screen has to know what
            // kind of deployment it is on, and this discloses no project, person or value.
            .AllowAnonymous();

    /// <summary>
    /// <paramref name="StoreRemedy"/> is non-null exactly when <paramref name="CanStoreSecret"/>
    /// is false — the sentence the unavailable store carries about how to gain the ability.
    /// <paramref name="LocalFolderReason"/> follows the same pattern for the Local locus (#247):
    /// the declared sentence, present exactly where <paramref name="CanUseLocalFolder"/> is
    /// false on a self-host deployment.
    /// </summary>
    internal sealed record Response(
        bool HasCodeSource,
        bool CanStoreSecret,
        string? StoreRemedy,
        bool CanUseLocalFolder,
        string? LocalFolderReason
    );
}
