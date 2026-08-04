var builder = DistributedApplication.CreateBuilder(args);

// The self-host output (#99, DEC-049): `aspire publish` emits docker-compose from this same
// composition, so run mode and the distributable can never describe two different systems.
// Publish-only — in run mode this adds nothing.
//
// Every compose fact lives on the resource it describes (#252): the environment itself carries
// no file surgery, because a global patch block is where a removed service's name keeps living
// after the service is gone — the dead `dispatch` entries this change deleted were exactly that.
builder.AddDockerComposeEnvironment("compose");

// PostgreSQL — one database, one schema per module.
// The volume is named so the generated compose is byte-stable across machines — the default
// name embeds a path hash, which would make the drift check (#99) fail wherever the checkout
// path differs.
var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("aio-postgres-data")
    // Two things `aspire run` does that raw compose does not, discovered by booting the output
    // (#99): Aspire creates the AddDatabase database itself, and it health-gates startup.
    // Declared here, on the container they describe — without them, migrations raced postgres
    // and then failed against a database nothing had created.
    .PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Environment["POSTGRES_DB"] = "aiorchestratordb";
            service.Healthcheck = new Aspire.Hosting.Docker.Resources.ServiceNodes.Healthcheck
            {
                Test = ["CMD-SHELL", "pg_isready -U postgres -d aiorchestratordb"],
                Interval = "2s",
                Timeout = "5s",
                Retries = 15,
                StartPeriod = "5s",
            };
        }
    );
var database = postgres.AddDatabase("aiorchestratordb");

// Azurite stands in for Azure Storage Queues, the Run dispatch substrate KEDA scales on
// (DEC-013). It is here from day 0 so the queue contract is exercised locally, never mocked.
//
// Two shapes for one stand-in: run mode uses the Aspire emulator resource; publish mode adds
// Azurite as a plain container, because AddAzureStorage in publish emits Azure provisioning —
// and the compose output must contact zero Azure (#99 AC4). The connection string below is
// Azurite's PUBLISHED well-known dev credential, the same constant every Azurite quickstart
// carries — a documented emulator constant, not a secret (BR-010 untouched).
// Neither shape composes one any more (#225, DEC-054): with no queue connection string the
// Server's AddRunDispatch composes the outbox pair and consumes in its own process.

// The Vite dev server exists only in run mode: published/deployed, the SPA is a build artifact
// served same-origin by the Server from wwwroot, so the compose output must not carry it (#99).
var frontend = builder.ExecutionContext.IsRunMode
    ? builder.AddViteApp("frontend", "../../frontend").WithPnpm()
    : null;

// Migrations are a bootstrap step in the composition graph, not a side effect of the Server
// starting: this resource runs once against a healthy database and exits, and the Server waits
// for its completion. That ordering is what makes a fresh clone — or a deleted data volume —
// boot with an up-to-date schema, deterministically, in dev and E2E alike. In production the
// same executable runs as a deploy job (#8); the Server never migrates anywhere.
var migrations = builder
    .AddProject<Projects.AiOrchestrator_MigrationService>("migrations")
    .WithReference(database)
    .WaitFor(database)
    .PublishAsDockerComposeService(
        (_, service) =>
        {
            // The distribution story is clone + `docker compose up`, images published by CI
            // (#257, DEC-049's published-images lane): the operator pulls, builds nothing —
            // no Dockerfile exists to build. The tag is overridable in the operator's .env;
            // the inline default keeps the quickstart at zero new variables.
            service.Image = PublishedImage("migrations");

            // Waiting for a HEALTHY postgres is this dependent's requirement (#99): plain
            // depends_on starts it against a database still initialising.
            WaitForHealthyPostgres(service);
        }
    );

// No dispatch worker here. It exists to drain a queue, and this habitat has none: the Server
// consumes the outbox in its own process, which is the container this change removes. The
// project still builds and still deploys — the Azure template composes it from its own file.

// The server's endpoints come from its launchSettings "http" profile — without that profile the
// resource has no named endpoint and nothing can resolve it. ASPNETCORE_ENVIRONMENT is left out
// of that profile on purpose, so the AppHost and the E2E fixture stay in charge of it.

var server = builder
    .AddProject<Projects.AiOrchestrator_Server>("server")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints()
    .PublishAsDockerComposeService(
        (_, service) =>
        {
            service.Image = PublishedImage("server");

            // A fixed host mapping: the quickstart says "open localhost:$SERVER_PORT", which a
            // random host port would turn into a scavenger hunt (#99).
            service.Ports = ["${SERVER_PORT}:${SERVER_PORT}"];

            WaitForHealthyPostgres(service);
        }
    );

// Deliberately no queue connection string on the Server: its absence is the configuration that
// composes the outbox substrate and its in-process consumer (ADR-0010 — asked, never inferred).

if (frontend is not null)
{
    // Same-origin in dev: the host proxies unmatched paths to the Vite dev server.
    server.WithReference(frontend);
}

// The AppHost owning the environment is the other half of that launchSettings decision — and the
// half that was missing: with neither party setting it, the Server silently ran as Production
// under `aspire run`, which skipped the dev conveniences and proxied nothing to Vite. Run mode
// means development; the E2E fixture's own WithEnvironment lands later and therefore wins.
if (builder.ExecutionContext.IsRunMode)
{
    server.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

    // Which declaration set run mode applies (#250): `local` is the dev loop, `server`
    // rehearses the operator's shape without `docker compose up`. Read from configuration
    // (`dotnet user-secrets set Parameters:habitat server`) rather than AddParameter on
    // purpose (design D1): a parameter *resource* would materialise in publish output, and
    // this value must never reach the artifact — publishing always emits the server shape.
    var habitat = builder.Configuration["Parameters:habitat"] ?? "local";

    switch (habitat)
    {
        case "local":
            DeclareDevLoop(server);
            break;
        case "server":
            DeclareServerShape(server);
            break;
        default:
            // The queue/outbox rule (DEC-054): ambiguity refuses where a person is watching,
            // never defaults silently — a rehearsal of the wrong shape teaches wrong lessons.
            throw new InvalidOperationException(
                $"Parameters:habitat is '{habitat}', which is not a habitat. Valid values: "
                    + "'local' (the dev loop — seeder, local secret store, Local locus) and "
                    + "'server' (the compose shape — pods, no seeder, Local locus declared out)."
            );
    }
}
else
{
    // Publishing IS the server shape — no parameter, no branch: the artifact cannot carry a
    // habitat choice, only declarations (ADR-0010).
    DeclareServerShape(server);
}

await builder.Build().RunAsync();
return;

// Where CI publishes every image (#257): GHCR, tagged by commit SHA plus a moving `latest`.
// The compose carries the literal `${AIO_IMAGE_TAG:-latest}` — compose's own inline default —
// so the quickstart needs no new .env variable and an operator can still pin a SHA. Spelled
// once, because three services writing the registry path by hand is how one of them drifts.
static string PublishedImage(string name) =>
    $"ghcr.io/asantamariaplainconcepts/ai-orchestrator/{name}:${{AIO_IMAGE_TAG:-latest}}";

// Waiting on a HEALTHY postgres, spelled once: it is each dependent's requirement, and two
// dependents writing the condition by hand is how one of them forgets the condition exists.
static void WaitForHealthyPostgres(Aspire.Hosting.Docker.Resources.ComposeNodes.Service service)
{
    if (service.DependsOn.TryGetValue("postgres", out var dependency))
    {
        dependency.Condition = "service_healthy";
    }
}

// The dev loop: a machine one person owns, worked on from the keyboard. Everything here exists
// to make the first `aspire run` clickable and the local loop exercisable.
static void DeclareDevLoop(IResourceBuilder<ProjectResource> server)
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
static void DeclareServerShape(IResourceBuilder<ProjectResource> server)
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
