var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL — one database, one schema per module.
var database = builder.AddPostgres("postgres").WithDataVolume().AddDatabase("aiorchestratordb");

// Azurite stands in for Azure Storage Queues, the Run dispatch substrate KEDA scales on
// (DEC-013). It is here from day 0 so the queue contract is exercised locally, never mocked.
var queues = builder.AddAzureStorage("storage").RunAsEmulator().AddQueues("queues");

var frontend = builder.AddViteApp("frontend", "../../frontend").WithPnpm();

// Migrations are a bootstrap step in the composition graph, not a side effect of the Server
// starting: this resource runs once against a healthy database and exits, and the Server waits
// for its completion. That ordering is what makes a fresh clone — or a deleted data volume —
// boot with an up-to-date schema, deterministically, in dev and E2E alike. In production the
// same executable runs as a deploy job (#8); the Server never migrates anywhere.
var migrations = builder
    .AddProject<Projects.AiOrchestrator_MigrationService>("migrations")
    .WithReference(database)
    .WaitFor(database);

// The server's endpoints come from its launchSettings "http" profile — without that profile the
// resource has no named endpoint and nothing can resolve it. ASPNETCORE_ENVIRONMENT is left out
// of that profile on purpose, so the AppHost and the E2E fixture stay in charge of it.
// The dispatch worker runs locally as an ordinary resource: KEDA has no local equivalent, so
// what the AppHost proves is the queue contract, not the scaler. Explicit here rather than
// implied, because a green local run must not be mistaken for a working scale rule.
builder
    .AddProject<Projects.AiOrchestrator_DispatchWorker>("dispatch")
    .WithReference(queues)
    .WaitFor(queues)
    .WithExplicitStart();

var server = builder
    .AddProject<Projects.AiOrchestrator_Server>("server")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(queues)
    .WaitFor(queues)
    .WaitForCompletion(migrations)
    // Same-origin in dev: the host proxies unmatched paths to the Vite dev server.
    .WithReference(frontend)
    .WithExternalHttpEndpoints();

// The AppHost owning the environment is the other half of that launchSettings decision — and the
// half that was missing: with neither party setting it, the Server silently ran as Production
// under `aspire run`, which skipped the dev conveniences and proxied nothing to Vite. Run mode
// means development; the E2E fixture's own WithEnvironment lands later and therefore wins.
if (builder.ExecutionContext.IsRunMode)
{
    server.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");
}

await builder.Build().RunAsync();
