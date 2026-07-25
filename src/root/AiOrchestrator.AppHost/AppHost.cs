var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL — one database, one schema per module.
var database = builder.AddPostgres("postgres").WithDataVolume().AddDatabase("aiorchestratordb");

// Azurite stands in for Azure Storage Queues, the Run dispatch substrate KEDA scales on
// (DEC-013). It is here from day 0 so the queue contract is exercised locally, never mocked.
var queues = builder.AddAzureStorage("storage").RunAsEmulator().AddQueues("queues");

var frontend = builder.AddViteApp("frontend", "../../frontend").WithPnpm();

builder
    .AddProject<Projects.AiOrchestrator_Server>("server")
    .WithReference(database)
    .WaitFor(database)
    .WithReference(queues)
    .WaitFor(queues)
    // Same-origin in dev: the host proxies unmatched paths to the Vite dev server.
    .WithReference(frontend)
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync();
