using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.Server;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.ServiceDefaults.Identity;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using AiOrchestrator.ServiceDefaults.Secrets;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Unconditional, not Development-only: the default leaves scope validation off outside
// Development, which let a scoped-service-from-root bug ship silently and surface only as an
// intermittent E2E 500. Startup cost is negligible; the guardrail must not depend on which
// environment happens to be running.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.AddServiceDefaults();

var modules = ModuleRegistration.Discover();
builder.Services.AddModules(modules, builder.Configuration);
builder.Services.AddVsaCqsArchitecture(modules.Assemblies());

// Secret-store wiring lives here because IModule.Add receives only IServiceCollection and
// IConfiguration — a module structurally cannot call an IHostApplicationBuilder extension, which
// is what Aspire's client integrations are (design D3). Key Vault when a vault URI is
// configured, configuration otherwise; the swap #7 promised, costing one line and no call site.
builder.AddSecretResolution();

// Who the caller is, per habitat (#119): the machine's owner locally, nobody yet on the web.
// Composed here for the same reason secrets are — a module receives only IServiceCollection and
// IConfiguration, and this reads the environment and the addresses the host will bind.
builder.AddIdentity();

// Where the Data Protection key ring lives (#180). Before authentication needs it: the OIDC state
// is encrypted with this ring, and an in-memory ring cannot survive the scale-to-zero and the new
// revision per deploy that sit between a challenge and its callback.
builder.AddPersistedKeyRing();

// Integration events: modules publish and subscribe through the BuildingBlocks seam; CAP and
// its outbox live behind it. Composed here because a module structurally cannot (design D1).
builder.AddIntegrationEvents();

// The producer side of dispatch: matching (Runs module) enqueues through IRunDispatcher.
builder.AddRunDispatch();

// The runtime seam's implementation: the Server never invokes it, but the Runs module's
// executor depends on the seam and DI validation rightly demands the dependency exist.
builder.AddAgentRuntime();
builder.AddCodeWorkspace();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Local convenience only: it refuses unless the AppHost's run composition asked for it, so a
// deployed host has no way to invoke it (local-agent-loop D3).
builder.Services.AddHostedService<LocalLoopSeeder>();

var app = builder.Build();

// The Server never migrates — in any environment. Migrations are the AppHost's `migrations`
// resource (AiOrchestrator.MigrationService), which this process waits on via WaitForCompletion.
// The previous in-process version was gated on `!IsProduction()`, and under `aspire run` the
// environment silently defaulted to Production: fresh database, no schema, 500s that read as an
// application bug. A gate that guesses from the environment name is the defect; owning the step
// elsewhere removes the gate rather than tuning it.

// Said once, at startup, when nobody is authenticated (#119, design D3): a temporary state
// with no voice is how it becomes permanent.
IdentityComposition.WarnIfUnauthenticated(
    app.Services,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Identity")
);

app.UseExceptionHandler();

// The provider mode (#12): middleware, the surface split and the auth endpoints, extracted to
// SignInPipeline because a host file that inlines its auth is a host file nobody can scan.
app.UseSignIn();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapModules(modules);

// The SPA is served same-origin either way, so the frontend never needs CORS or an absolute API
// base URL. In dev the Vite dev server is proxied (its URL arrives via Aspire service discovery);
// in every other environment the static build in wwwroot is served with an index.html fallback.
var frontendDevServer = app.Configuration["services:frontend:http:0"];

if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(frontendDevServer))
{
    // Guarded by "did routing select an endpoint", because UseSpa is TERMINAL middleware: routing
    // *selects* the endpoint up front, but endpoints *execute* at the end of the pipeline, and an
    // unguarded UseSpa sits between the two and never calls next — so it swallowed every request,
    // /api included, and Vite answered 200 index.html for all of them. It hid from day one behind
    // the environment bug: dev mode was accidentally running as Production, where this branch
    // never executed. The guard restores what the old comment merely asserted: mapped endpoints
    // win, and only unclaimed paths reach the dev server.
    app.UseWhen(
        context => context.GetEndpoint() is null,
        spaPipeline =>
            spaPipeline.UseSpa(spa => spa.UseProxyToSpaDevelopmentServer(frontendDevServer))
    );
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();

    // The shell is served to anyone, deliberately (#182): it is a public bundle, and requiring a
    // session for it is what forced the cookie down to Lax. With the shell anonymous, the only
    // cross-site-initiated navigation is the callback's return — which needs no cookie — so the
    // session cookie can be Strict. The data behind /api still answers 401, and the SPA offers
    // sign-in when it sees one.
    app.MapFallbackToFile("index.html");
}

await app.RunAsync();

/// <summary>Entry point marker so functional tests can drive this host via WebApplicationFactory.</summary>
public partial class Program;
