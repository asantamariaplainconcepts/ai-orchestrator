using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AiOrchestrator.ArchitectureAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mod003PublicDomainEntityAnalyzer : DiagnosticAnalyzer
{
    const string DomainNamespace = "AiOrchestrator.BuildingBlocks.Domain";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [DiagnosticDescriptors.Mod003PublicDomainEntity];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.DeclaredAccessibility != Accessibility.Public)
        {
            return;
        }

        if (
            !type.ContainingAssembly.Name.StartsWith(
                SymbolExtensions.ModuleAssemblyPrefix,
                System.StringComparison.Ordinal
            ) || type.ContainingAssembly.IsContractsAssembly()
        )
        {
            return;
        }

        if (
            type.InheritsFrom(DomainNamespace, "Aggregate")
            || type.InheritsFrom(DomainNamespace, "BaseEntity")
        )
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.Mod003PublicDomainEntity,
                    type.Locations[0],
                    type.Name
                )
            );
        }
    }
}
