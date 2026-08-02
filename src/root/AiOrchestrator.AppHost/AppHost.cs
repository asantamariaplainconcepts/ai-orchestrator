var builder = DistributedApplication.CreateBuilder(args);

// The self-host output (#99, DEC-049): `aspire publish` emits docker-compose from this same
// composition, so run mode and the distributable can never describe two different systems.
// Publish-only — in run mode this adds nothing.
//
// ConfigureComposeFile turns the publisher's image placeholders into build contexts: the
// distribution story is clone + `docker compose up --build`, no registry anywhere (owner
// decision on #99). The Dockerfiles are multi-stage with the SDK inside, so a Docker-only
// machine builds everything.
builder
    .AddDockerComposeEnvironment("compose")
    .ConfigureComposeFile(file =>
    {
        var builds = new Dictionary<string, string>
        {
            ["server"] = "src/root/AiOrchestrator.Server/Dockerfile",
            ["migrations"] = "src/root/AiOrchestrator.MigrationService/Dockerfile",
            ["dispatch"] = "src/root/AiOrchestrator.DispatchWorker/Dockerfile",
        };

        foreach (var service in file.Services)
        {
            if (builds.TryGetValue(service.Key, out var dockerfile))
            {
                service.Value.Image = null;
                service.Value.Build = new Aspire.Hosting.Docker.Resources.ServiceNodes.Build
                {
                    // Relative to selfhost/, where the generated file is committed.
                    Context = "..",
                    Dockerfile = dockerfile,
                };
            }
        }

        // A fixed host mapping: the quickstart says "open localhost:$SERVER_PORT", which a
        // random host port would turn into a scavenger hunt.
        if (file.Services.TryGetValue("server", out var web))
        {
            web.Ports = ["${SERVER_PORT}:${SERVER_PORT}"];
        }

        // Two things `aspire run` does that raw compose does not, discovered by booting the
        // output (#99): Aspire creates the AddDatabase database itself, and it health-gates
        // startup. Without these, migrations raced postgres and then failed against a database
        // that nothing had created.
        if (file.Services.TryGetValue("postgres", out var db))
        {
            db.Environment["POSTGRES_DB"] = "aiorchestratordb";
            db.Healthcheck = new Aspire.Hosting.Docker.Resources.ServiceNodes.Healthcheck
            {
                Test = ["CMD-SHELL", "pg_isready -U postgres -d aiorchestratordb"],
                Interval = "2s",
                Timeout = "5s",
                Retries = 15,
                StartPeriod = "5s",
            };
        }

        foreach (var name in new[] { "migrations", "server", "dispatch" })
        {
            if (
                file.Services.TryGetValue(name, out var dependent)
                && dependent.DependsOn.TryGetValue("postgres", out var dependency)
            )
            {
                dependency.Condition = "service_healthy";
            }
        }
    });

// PostgreSQL — one database, one schema per module.
// The volume is named so the generated compose is byte-stable across machines — the default
// name embeds a path hash, which would make the drift check (#99) fail wherever the checkout
// path differs.
var database = builder
    .AddPostgres("postgres")
    .WithDataVolume("aio-postgres-data")
    .AddDatabase("aiorchestratordb");

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
    .WaitFor(database);

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
    .WithExternalHttpEndpoints();

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

    // The demo seeder runs only here (local-agent-loop design D3). No deployed template sets
    // this, and the seeder refuses without it — a property rather than a promise.
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
    var values = Path.Combine(secrets, "values");
    var keys = Path.Combine(secrets, "keys");

    // Both processes, not just the one with the form: the worker resolves the same credential
    // when it executes a Run, and a store only the Server can read would make a pasted token
    // work in the portal and fail at the first dispatch.
    // One process now (#225): the Server holds the form and executes the Run, so the store it
    // writes is the store it reads. This configured the worker too, for a reason that no longer
    // exists — there is no second process to disagree with.
    server.WithEnvironment("Secrets__LocalStorePath", values);
    server.WithEnvironment("Secrets__LocalKeyRingPath", keys);
}
else
{
    // The self-host compose is also a machine somebody owns (#119): the operator who ran
    // `docker compose up` is the owner, and asking them to configure an identity would be the
    // second command DEC-049 promises they will not need. Azure gets neither branch — Terraform
    // composes that deployment and never sets this.
    server.WithEnvironment("Identity__Mode", "LocalOwner");
}

await builder.Build().RunAsync();
