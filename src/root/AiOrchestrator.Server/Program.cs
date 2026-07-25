using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.ServiceDefaults;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var modules = ModuleRegistration.Discover();
builder.Services.AddModules(modules, builder.Configuration);
builder.Services.AddVsaCqsArchitecture(modules.Assemblies());

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

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
// Reserved prefixes are mapped above and win, because routing runs before both branches.
var frontendDevServer = app.Configuration["services:frontend:http:0"];

if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(frontendDevServer))
{
    app.UseSpa(spa => spa.UseProxyToSpaDevelopmentServer(frontendDevServer));
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html");
}

await app.RunAsync();

/// <summary>Entry point marker so functional tests can drive this host via WebApplicationFactory.</summary>
public partial class Program;
