using System.Reflection;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AiOrchestrator.ArchTests;

/// <summary>
/// #13 / BR-009 — operations name permissions, roles are bundles over them, and forgetting to declare
/// fails closed. Composition-level, with the real <c>AddVsaCqsArchitecture</c> and this assembly's own
/// handlers, so the decorator under test is the one the product composes.
/// <para>
/// The reflection sweeps at the bottom police the indirection itself, which is what the string-keyed
/// shape costs: a declaration nobody granted, or a permission nobody declares, would be a silent
/// refusal rather than a compile error. They turn both into a red build.
/// </para>
/// </summary>
public class ProjectRoles_Should_Constraint
{
    /// <summary>Granted to Member in the test pipeline below; the other is Admin-only.</summary>
    const string Observe = "test.observe";
    const string Configure = "test.configure";

    static IServiceProvider Pipeline(ProjectRole? role) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<IProjectPermissions>(new FixedRole(role))
            .AddPermissionGrants(ProjectRole.Member, Observe)
            .AddVsaCqsArchitecture(Assembly.GetExecutingAssembly())
            .BuildServiceProvider();

    static async Task<TResponse> Send<TResponse>(ProjectRole? role, ICommand<TResponse> command)
    {
        await using var scope = Pipeline(role).CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    [Fact]
    public async Task AnUndeclaredOperation_Should_BeRefusedEvenFromAnAdmin()
    {
        // The default-deny that design D1 exists for: a use case added without thinking is locked,
        // not open. Admin is the caller here on purpose — if the omission were read as "no
        // requirement", this would pass and the test would prove nothing.
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            Send(ProjectRole.Admin, new Undeclared())
        );
    }

    [Fact]
    public async Task AMember_Should_BeRefusedAPermissionTheirBundleDoesNotHold()
    {
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            Send(ProjectRole.Member, new NeedsConfigure(Guid.NewGuid()))
        );
    }

    [Fact]
    public async Task AMember_Should_ReachAPermissionTheirBundleHolds()
    {
        (await Send(ProjectRole.Member, new NeedsObserve(Guid.NewGuid()))).ShouldBe("handled");
    }

    [Fact]
    public async Task AnAdmin_Should_HoldEveryPermissionWithoutBeingGrantedOne()
    {
        // DEC-034's "Admin = all", made a rule rather than a list. The grant table above never
        // mentions Configure, and Admin still reaches it — which is the property that stops a
        // permission added later from being refused to the bundle defined as holding it.
        (await Send(ProjectRole.Admin, new NeedsConfigure(Guid.NewGuid()))).ShouldBe("handled");
    }

    [Fact]
    public async Task SomebodyHoldingNoRole_Should_BeRefusedTheGentlestPermission()
    {
        // Null is "no row", which is also the answer for a project that does not exist — the two
        // must be indistinguishable or a refusal becomes a way to enumerate projects.
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            Send(null, new NeedsObserve(Guid.NewGuid()))
        );
    }

    [Fact]
    public async Task APermissionDeclaredWithNoProject_Should_FailLoudlyRatherThanRefuse()
    {
        // A wiring mistake, not a permission decision. Refusing would hide it behind a 403 that reads
        // like an ordinary refusal to whoever hits it.
        var mistake = await Should.ThrowAsync<InvalidOperationException>(() =>
            Send(ProjectRole.Admin, new ScopelessButScoped())
        );

        mistake.Message.ShouldContain(nameof(IScopedToProject));
    }

    [Fact]
    public async Task AQuery_Should_BeCheckedToo()
    {
        // The query chain is decorated separately, and its position matters more than the command
        // one: authorization sits OUTSIDE caching, so a response cached for somebody allowed cannot
        // be handed to somebody who is not.
        await using var refused = Pipeline(null).CreateAsyncScope();
        await Should.ThrowAsync<PermissionDeniedException>(() =>
            refused
                .ServiceProvider.GetRequiredService<ISender>()
                .Send(new ObserveQuery(Guid.NewGuid()))
        );

        await using var allowed = Pipeline(ProjectRole.Member).CreateAsyncScope();
        (
            await allowed
                .ServiceProvider.GetRequiredService<ISender>()
                .Send(new ObserveQuery(Guid.NewGuid()))
        ).ShouldBe("handled");
    }

    [Fact]
    public void EveryRequestInTheProduct_Should_DeclareWhatItRequires()
    {
        var undeclared = Requests()
            .Where(request => request.GetCustomAttribute<RequiresAttribute>() is null)
            .Select(request => request.FullName!)
            .ToList();

        // The pipeline would refuse these at runtime, which is the safe failure — but "every button
        // in this feature is broken" is a worse way to find out than a red test.
        undeclared.ShouldBeEmpty();
    }

    [Fact]
    public void EveryRequestNamingAPermission_Should_NameItsProjectToo()
    {
        var unpaired = Requests()
            .Where(request =>
                request.GetCustomAttribute<RequiresAttribute>()?.Permission is not null
                && !typeof(IScopedToProject).IsAssignableFrom(request)
            )
            .Select(request => request.FullName!)
            .ToList();

        unpaired.ShouldBeEmpty();
    }

    [Fact]
    public void EveryDeclaredPermission_Should_BeOneOfTheModulesConstants()
    {
        var known = KnownPermissions();

        var invented = Requests()
            .Select(request => request.GetCustomAttribute<RequiresAttribute>()?.Permission)
            .Where(permission => permission is not null && !known.Contains(permission))
            .Distinct()
            .ToList();

        // What the string-keyed shape costs, made cheap: a typo'd permission is held by nobody, so it
        // would be refused for Member, allowed for Admin, and silent. This is the compile error the
        // strings gave up.
        invented.ShouldBeEmpty();
    }

    [Fact]
    public void EveryPermissionConstant_Should_BeDeclaredBySomething()
    {
        var declared = Requests()
            .Select(request => request.GetCustomAttribute<RequiresAttribute>()?.Permission)
            .Where(permission => permission is not null)
            .ToHashSet();

        var unused = KnownPermissions().Where(known => !declared.Contains(known)).ToList();

        // The other direction, and the one that rots quietly: a permission no operation requires
        // still looks like a rule somebody must honour, and a grant table listing it reads as a
        // decision that was made. Neither is true.
        unused.ShouldBeEmpty();
    }

    /// <summary>Every command and query the product actually dispatches.</summary>
    static IEnumerable<Type> Requests() =>
        ModuleAssemblies
            .Implementations.SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false }
                && type.GetInterfaces()
                    .Any(contract =>
                        contract.IsGenericType
                        && (
                            contract.GetGenericTypeDefinition() == typeof(ICommand<>)
                            || contract.GetGenericTypeDefinition() == typeof(IQuery<>)
                        )
                    )
            );

    /// <summary>
    /// The permission vocabulary, read from the <c>*Permissions</c> classes rather than from a list
    /// here — a list here would be the second place a new permission has to be added, which is the
    /// drift these tests exist to catch.
    /// </summary>
    static HashSet<string> KnownPermissions() =>
        ModuleAssemblies
            .Implementations.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name.EndsWith("Permissions", StringComparison.Ordinal))
            .SelectMany(type =>
                type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            )
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    sealed class FixedRole(ProjectRole? role) : IProjectPermissions
    {
        public Task<ProjectRole?> RoleOn(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(role);

        public Task<IReadOnlySet<Guid>?> VisibleProjects(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>?>(null);
    }

    // Deliberately carries no [Requires]: this type IS the test.
    internal sealed record Undeclared : ICommand<string>;

    [Requires(Observe)]
    internal sealed record NeedsObserve(Guid ProjectId) : ICommand<string>, IScopedToProject;

    [Requires(Configure)]
    internal sealed record NeedsConfigure(Guid ProjectId) : ICommand<string>, IScopedToProject;

    // The pairing mistake: names a permission, names no project.
    [Requires(Configure)]
    internal sealed record ScopelessButScoped : ICommand<string>;

    // A query as well as commands: AddVsaCqsArchitecture decorates both open handler interfaces and
    // Scrutor refuses to decorate one with no registrations, so an assembly of commands alone cannot
    // compose the real pipeline at all. Found by composing it.
    [Requires(Observe)]
    internal sealed record ObserveQuery(Guid ProjectId) : IQuery<string>, IScopedToProject;

    internal sealed class UndeclaredHandler : IAppCommandHandler<Undeclared, string>
    {
        public Task<string> Handle(Undeclared command, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }

    internal sealed class NeedsObserveHandler : IAppCommandHandler<NeedsObserve, string>
    {
        public Task<string> Handle(NeedsObserve command, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }

    internal sealed class NeedsConfigureHandler : IAppCommandHandler<NeedsConfigure, string>
    {
        public Task<string> Handle(NeedsConfigure command, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }

    internal sealed class ScopelessHandler : IAppCommandHandler<ScopelessButScoped, string>
    {
        public Task<string> Handle(
            ScopelessButScoped command,
            CancellationToken cancellationToken
        ) => Task.FromResult("handled");
    }

    internal sealed class ObserveQueryHandler : IAppQueryHandler<ObserveQuery, string>
    {
        public Task<string> Handle(ObserveQuery query, CancellationToken cancellationToken) =>
            Task.FromResult("handled");
    }
}
