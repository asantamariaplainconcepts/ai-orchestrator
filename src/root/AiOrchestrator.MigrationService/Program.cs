using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Secrets;
using AiOrchestrator.ServiceDefaults;
using AiOrchestrator.ServiceDefaults.IntegrationEvents;
using AiOrchestrator.ServiceDefaults.Secrets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// The migration step as its own process, not a side effect of the Server starting. The AppHost
// runs it before the Server (WaitForCompletion), so a fresh clone — or a deleted data volume —
// boots with an up-to-date schema regardless of which environment name the Server happens to
// carry. The old in-process migration was gated on `!IsProduction()`, and under `aspire run`
// the Server's environment silently defaulted to Production: fresh database, no schema, 500s.
// A dedicated resource has no gate to guess wrong.
//
// The same executable is the deliberate production step: #8 runs it as a deploy job before
// rollout, which is what keeps "schema changes are a deploy step, never an app-start side
// effect" true in every environment rather than only in the comment that claimed it.
var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Same secret composition as the Server: the connection string is a vault secret in Azure and
// plain configuration locally, and the migrator must reach it the same way the app does.
builder.AddSecretResolution();
builder.AddIntegrationEvents();

var modules = ModuleRegistration.Discover();
builder.Services.AddModules(modules, builder.Configuration);

// Build without running: module registrations (DbContexts, options) become resolvable, but
// hosted services — the Backlog poller among them — never start, because migrating must not
// also mean polling.
using var host = builder.Build();

await host.Services.MigrateModules(modules);

// CAP's outbox schema is a migration concern like every other (design D5). CAP offers no switch
// to disable its startup initializer, so apps still run an idempotent CREATE IF NOT EXISTS —
// this step is what makes that a structural no-op rather than an app changing schema.
await host
    .Services.GetRequiredService<DotNetCore.CAP.Persistence.IStorageInitializer>()
    .InitializeAsync(CancellationToken.None);

// Reaching here means every module migrated; the process exits 0 and dependents may start.
// Any failure above throws, the process exits non-zero, and the Server deliberately never
// comes up — a missing schema surfacing at the first query would be strictly worse.
