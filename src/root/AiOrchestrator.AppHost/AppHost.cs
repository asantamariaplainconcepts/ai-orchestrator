using AiOrchestrator.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("compose");

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume("aio-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .PublishAsDockerComposeService((_, service) => AppHostCompose.ConfigurePostgres(service));

var database = postgres.AddDatabase("aiorchestratordb");

var migrations = builder
    .AddProject<Projects.AiOrchestrator_MigrationService>("migrations")
    .WithReference(database)
    .WaitFor(database)
    .PublishAsDockerComposeService((_, service) => AppHostCompose.ConfigureMigrations(service));

var server = builder
    .AddProject<Projects.AiOrchestrator_Server>("server")
    .WithReference(database)
    .WaitFor(database)
    .WaitForCompletion(migrations)
    .WithExternalHttpEndpoints()
    .PublishAsDockerComposeService((_, service) => AppHostCompose.ConfigureServer(service));

if (builder.ExecutionContext.IsRunMode)
{
    var frontend = builder.AddViteApp("frontend", "../../frontend").WithPnpm();

    server.WithReference(frontend);

    var habitat = builder.Configuration["Parameters:habitat"] ?? "local";

    switch (habitat)
    {
        case "local":
            AppHostHabitats.DeclareDevLoop(server);
            break;
        case "server":
            AppHostHabitats.DeclareServerShape(server);
            break;
        default:
            throw new InvalidOperationException(
                $"Parameters:habitat is '{habitat}', which is not a habitat. Valid values: "
                    + "'local' (the dev loop — seeder, local secret store, Local locus) and "
                    + "'server' (the compose shape — pods, no seeder, Local locus declared out)."
            );
    }
}
else
{
    AppHostHabitats.DeclareServerShape(server);
}

await builder.Build().RunAsync();
