using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Server;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.Agents;
using AiOrchestrator.ServiceDefaults.Dispatch;
using AiOrchestrator.ServiceDefaults.Identity;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using AiOrchestrator.ServiceDefaults.Secrets;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.AddServiceDefaults();

var modules = ModuleRegistration.Discover();

builder.Services.AddModules(modules, builder.Configuration);
builder.Services.AddVsaCqsArchitecture(modules.Assemblies());

builder.AddSecretResolution();
builder.AddIdentity();
builder.AddPersistedKeyRing();
builder.AddIntegrationEvents();
builder.AddRunDispatch();
builder.AddRunDispatchConsumer();
builder.AddAgentRuntime();
builder.AddConversationRuntime();
builder.AddCodeWorkspace();
builder.AddLocalCodeWorkspace();
builder.AddLocalCheckoutReaper();
builder.AddFeatureState();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHostedService<LocalLoopSeeder>();

var app = builder.Build();

IdentityComposition.WarnIfUnauthenticated(
    app.Services,
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Identity")
);

app.UseExceptionHandler();
app.UseSignIn();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapModules(modules);

var frontendDevServer = app.Configuration["services:frontend:http:0"];

if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(frontendDevServer))
{
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
    app.MapFallbackToFile("index.html");
}

await app.RunAsync();

public partial class Program;
