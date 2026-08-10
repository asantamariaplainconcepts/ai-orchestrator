using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AiOrchestrator.ServiceDefaults;

public static class ServiceDefaults
{
    public const string HealthEndpoint = "/api/health";
    public const string AliveEndpoint = "/api/alive";

    /// <summary>
    /// OpenTelemetry (logs, metrics, traces), service discovery, and resilient HTTP defaults for
    /// every service. Exporters are environment-driven: OTLP locally, Azure Monitor in the cloud
    /// when APPLICATIONINSIGHTS_CONNECTION_STRING is present (DEC-023).
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        builder
            .Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    static void ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder
            .Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
            )
            .WithTracing(tracing =>
                tracing
                    .AddSource(Diagnostics.ActivitySourceName)
                    .AddAspNetCoreInstrumentation(options =>
                        // Health probes fire constantly and say nothing about behaviour.
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpoint)
                            && !context.Request.Path.StartsWithSegments(AliveEndpoint)
                    )
                    .AddHttpClientInstrumentation()
            );

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        if (
            !string.IsNullOrWhiteSpace(
                builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
            )
        )
        {
            builder.Services.AddOpenTelemetry().UseAzureMonitor();
        }
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpoint);
        app.MapHealthChecks(
            AliveEndpoint,
            new() { Predicate = registration => registration.Tags.Contains("live") }
        );
        return app;
    }
}
