using Aspire.Hosting.Docker.Resources.ComposeNodes;

namespace AiOrchestrator.AppHost;

static class AppHostCompose
{
    // Where CI publishes every image (#257): GHCR, tagged by commit SHA plus a moving `latest`.
    // The compose carries the literal `${AIO_IMAGE_TAG:-latest}` — compose's own inline default —
    // so the quickstart needs no new .env variable and an operator can still pin a SHA. Spelled
    // once, because three services writing the registry path by hand is how one of them drifts.
    public static string PublishedImage(string name) =>
        $"ghcr.io/asantamariaplainconcepts/ai-orchestrator/{name}:${{AIO_IMAGE_TAG:-latest}}";

    public static void ConfigurePostgres(Service service)
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

    public static void ConfigureMigrations(Service service)
    {
        service.Image = PublishedImage("migrations");
        WaitForHealthyPostgres(service);
    }

    public static void ConfigureServer(Service service)
    {
        service.Image = PublishedImage("server");
        service.Ports = ["${SERVER_PORT}:${SERVER_PORT}"];
        WaitForHealthyPostgres(service);
    }

    // Waiting on a HEALTHY postgres, spelled once: it is each dependent's requirement, and two
    // dependents writing the condition by hand is how one of them forgets the condition exists.
    static void WaitForHealthyPostgres(Service service)
    {
        if (service.DependsOn.TryGetValue("postgres", out var dependency))
        {
            dependency.Condition = "service_healthy";
        }
    }
}
