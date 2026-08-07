namespace AiOrchestrator.AppHost;

static class AppHostHabitats
{
    // The dev loop: a machine one person owns, worked on from the keyboard. Everything here exists
    // to make the first `aspire run` clickable and the local loop exercisable.
    public static void DeclareDevLoop(IResourceBuilder<ProjectResource> server)
    {
        // The demo seeder runs only here (local-agent-loop design D3). No deployed template sets
        // this, and the seeder refuses without it — a property rather than a promise. A dev-loop
        // declaration, not a run-mode one: rehearsing the server shape means seeing the empty
        // first boot an operator sees.
        server.WithEnvironment("LocalLoop:Seed", "true");

        // `aspire run` is a machine somebody owns, so the person at the keyboard is the owner
        // (#119). Set here rather than asked of the user: DEC-049's promise is that running this
        // costs one command, and a required identity setting would be a second one.
        server.WithEnvironment("Identity__Mode", "LocalOwner");

        // A machine somebody owns can also *store* a credential (#124), so pasting a token works
        // under `aspire run` exactly as it does in a deployment. Without this the only habitat that
        // could accept a pasted value would be Azure, which would leave the feature unexercisable
        // by the person writing it — the failure ADR-0001 exists to prevent.
        //
        // Two paths, never one: values and the key that protects them together in one directory is
        // obfuscation, and the host refuses to start that way.
        var secrets = Path.Combine(Path.GetTempPath(), "ai-orchestrator", "secrets");
        server.WithEnvironment("Secrets__LocalStorePath", Path.Combine(secrets, "values"));
        server.WithEnvironment("Secrets__LocalKeyRingPath", Path.Combine(secrets, "keys"));
    }

    // The server shape: what an operator's `docker compose up` runs, and since #250 also what
    // `Parameters:habitat=server` rehearses under `aspire run`. One block, both routes — the two
    // cannot drift because they are one method.
    public static void DeclareServerShape(IResourceBuilder<ProjectResource> server)
    {
        // The self-host compose is also a machine somebody owns (#119): the operator who ran
        // `docker compose up` is the owner, and asking them to configure an identity would be the
        // second command DEC-049 promises they will not need. Azure gets neither branch — Terraform
        // composes that deployment and never sets this.
        server.WithEnvironment("Identity__Mode", "LocalOwner");

        // …but its Server is a container, and a folder on the operator's machine is not reachable
        // from it (#247). Declared here, where the composition knows — never inferred from the
        // runtime (ADR-0010): an operator who mounts a folder deliberately can unset this in their
        // own compose and owns the consequence. The sentence travels verbatim to the capability
        // read, the save refusal and the Run refusal.
        server.WithEnvironment(
            "Habitat__LocalFolderUnavailableReason",
            "the orchestrator runs in a container here, and a folder on this machine is not "
                + "visible to it — local folders need the dev loop, where the server is a process "
                + "on this machine"
        );

        // Runs execute in pods here (#246): the Server's own image carries no agent CLI on purpose
        // — fattening it was rejected at grill — so each Run gets a container from the worker image
        // instead. Named here, and honestly incomplete by default: without the docker socket (the
        // operator's explicit grant, root-equivalent, made in their own compose override) a Run
        // fails naming exactly that. A named failure beats a silent in-process fallback that would
        // erase the isolation the operator asked for. selfhost/README.md carries the grant.
        // Since #257 the default is the published worker image — the operator pulls it rather than
        // building it, and overriding the name in their own compose still works. The tag is spelled
        // plain here, not as the compose placeholder: this method also declares the `aspire run`
        // rehearsal, where nothing interpolates `${...}` — an operator pinning a SHA overrides the
        // whole variable in their own compose, which wins over this default either way.
        server.WithEnvironment(
            "Dispatch__PodImage",
            "ghcr.io/asantamariaplainconcepts/ai-orchestrator/dispatch-worker:latest"
        );
        // `docker compose` prefixes networks with the project name, which defaults to the
        // directory: selfhost/. An operator running with -p overrides this too.
        server.WithEnvironment("Dispatch__PodNetwork", "selfhost_aspire");
    }
}
