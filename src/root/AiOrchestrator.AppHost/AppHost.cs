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
IResourceBuilder<Aspire.Hosting.Azure.AzureQueueStorageResource>? queues = null;
if (builder.ExecutionContext.IsRunMode)
{
    queues = builder.AddAzureStorage("storage").RunAsEmulator().AddQueues("queues");
}

const string AzuriteComposeConnection =
    "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;"
    + "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
    + "QueueEndpoint=http://storage:10001/devstoreaccount1;";

if (builder.ExecutionContext.IsPublishMode)
{
    builder
        .AddContainer("storage", "mcr.microsoft.com/azure-storage/azurite")
        .WithArgs("azurite-queue", "--queueHost", "0.0.0.0")
        .WithEndpoint(targetPort: 10001, name: "queue");
}

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

// The dispatch worker. Since #18 it composes the modules, so it needs the database as much as
// the queue — without it the process throws at startup, which nobody saw because the resource
// used to require an explicit start and nobody pressed it.
//
// It drains the queue and exits by design (#16), so Aspire restarts it and a queued Run is
// picked up within seconds. That is NOT KEDA: KEDA scales on queue length and can scale to
// zero; this restarts unconditionally and burns a little idle CPU. What the AppHost proves is
// the queue contract and the agent loop — never the scale rule.
var dispatch = builder
    .AddProject<Projects.AiOrchestrator_DispatchWorker>("dispatch")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations);

if (queues is not null)
{
    dispatch.WithReference(queues).WaitFor(queues);
}
else
{
    dispatch
        .WithEnvironment("ConnectionStrings__queues", AzuriteComposeConnection)
        // No KEDA in compose: the worker is a long-lived drainer on a timer, the same divergence
        // the local loop documents — WHAT starts a pass differs, the pass is identical.
        .WithEnvironment("Dispatch__LocalPollSeconds", "5");
}

if (builder.ExecutionContext.IsRunMode)
{
    // A timer starts each drain pass locally, because nothing else will. Deployed, this is
    // unset and the job drains once and exits — the pass itself is identical either way.
    dispatch.WithEnvironment("Dispatch__LocalPollSeconds", "5");
}

// The server's endpoints come from its launchSettings "http" profile — without that profile the
// resource has no named endpoint and nothing can resolve it. ASPNETCORE_ENVIRONMENT is left out
// of that profile on purpose, so the AppHost and the E2E fixture stay in charge of it.

var server = builder
    .AddProject<Projects.AiOrchestrator_Server>("server")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints();

if (queues is not null)
{
    server.WithReference(queues).WaitFor(queues);
}
else
{
    server.WithEnvironment("ConnectionStrings__queues", AzuriteComposeConnection);
}

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
