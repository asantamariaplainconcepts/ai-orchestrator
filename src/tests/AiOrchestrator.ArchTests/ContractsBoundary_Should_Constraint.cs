using Shouldly;

namespace AiOrchestrator.ArchTests;

/// <summary>
/// Both directions of the Contracts rule (module-integration spec), asserted with the real
/// <c>Backlog.Contracts</c> in place: the sibling-Contracts reference is allowed, everything a
/// Contracts assembly must not do is forbidden, and the boundary check itself is proven
/// non-vacuous.
/// </summary>
public class ContractsBoundary_Should_Constraint
{
    [Fact]
    public void AModule_Should_BeAllowedToReferenceItsSiblingContractsAssembly()
    {
        // The allowed direction. Without this, a green boundary suite could mean "nobody
        // references Contracts at all" — the check would be passing vacuously.
        var contractsNames = ModuleAssemblies
            .Contracts.Select(assembly => assembly.GetName().Name)
            .ToHashSet();

        contractsNames.ShouldNotBeEmpty();

        var referencing = ModuleAssemblies
            .Implementations.Where(module =>
                module
                    .GetReferencedAssemblies()
                    .Any(reference => contractsNames.Contains(reference.Name))
            )
            .Select(module => module.GetName().Name)
            .ToList();

        referencing.ShouldContain("AiOrchestrator.Modules.Backlog");
    }

    [Fact]
    public void ContractsAssemblies_Should_NotReferenceAnyModuleImplementation()
    {
        // Contracts are the leaf: an implementation reference here would let infrastructure
        // ride into every consumer through the one assembly designed to carry none.
        var implementationNames = ModuleAssemblies
            .Implementations.Select(assembly => assembly.GetName().Name)
            .ToHashSet();

        foreach (var contracts in ModuleAssemblies.Contracts)
        {
            contracts
                .GetReferencedAssemblies()
                .Where(reference => implementationNames.Contains(reference.Name))
                .ShouldBeEmpty();
        }
    }

    [Fact]
    public void ContractsAssemblies_Should_ContainNoImplementationTypes()
    {
        // Events, enums, and read interfaces only. A record is recognised by its
        // compiler-generated Clone method — the honest reflection-level marker.
        foreach (var contracts in ModuleAssemblies.Contracts)
        {
            var offenders = contracts
                .GetTypes()
                .Where(type =>
                    type is { IsInterface: false, IsEnum: false, IsNested: false }
                    && type.GetMethod("<Clone>$") is null
                )
                .Select(type => type.FullName)
                .ToList();

            offenders.ShouldBeEmpty();
        }
    }

    [Fact]
    public void ModuleAssemblies_Should_NotReferenceMessagingInfrastructure()
    {
        // Design D1: modules and their Contracts see the BuildingBlocks seam, never CAP. The
        // functional consequence (no transitive CAP either) is checked at build time via
        // `dotnet list package --include-transitive`; this pins the direct-reference level.
        foreach (
            var assembly in ModuleAssemblies.Implementations.Concat(ModuleAssemblies.Contracts)
        )
        {
            assembly
                .GetReferencedAssemblies()
                .Where(reference =>
                    reference.Name!.StartsWith("DotNetCore.CAP", StringComparison.Ordinal)
                    || reference.Name.StartsWith("Savorboard", StringComparison.Ordinal)
                )
                .ShouldBeEmpty();
        }
    }
}
