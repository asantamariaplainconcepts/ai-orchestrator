using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AiOrchestrator.ArchitectureAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mod005EntityLeakAnalyzer : DiagnosticAnalyzer
{
    const string DomainNamespace = "AiOrchestrator.BuildingBlocks.Domain";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [DiagnosticDescriptors.Mod005EntityLeak];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
        context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
    }

    static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (
            !IsEffectivelyPublic(method)
            || method.MethodKind is not (MethodKind.Ordinary or MethodKind.Constructor)
            || !IsModuleType(method.ContainingAssembly)
        )
        {
            return;
        }

        var leakedType = ContainsDomainEntity(method.ReturnType)
            ? method.ReturnType
            : method.Parameters.Select(p => p.Type).FirstOrDefault(t => ContainsDomainEntity(t));

        if (leakedType is not null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.Mod005EntityLeak,
                    method.Locations[0],
                    method.Name,
                    leakedType.Name
                )
            );
        }
    }

    static void AnalyzeProperty(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;

        if (!IsEffectivelyPublic(property) || !IsModuleType(property.ContainingAssembly))
        {
            return;
        }

        if (ContainsDomainEntity(property.Type))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.Mod005EntityLeak,
                    property.Locations[0],
                    property.Name,
                    property.Type.Name
                )
            );
        }
    }

    static bool IsModuleType(IAssemblySymbol assembly) =>
        assembly.Name.StartsWith(
            SymbolExtensions.ModuleAssemblyPrefix,
            System.StringComparison.Ordinal
        );

    /// <summary>A member is only part of the public surface if it, and every enclosing type, is public.</summary>
    static bool IsEffectivelyPublic(ISymbol member)
    {
        if (member.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        for (
            var containingType = member.ContainingType;
            containingType is not null;
            containingType = containingType.ContainingType
        )
        {
            if (containingType.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    static bool ContainsDomainEntity(ITypeSymbol type, int depth = 0)
    {
        if (depth > 2)
        {
            return false;
        }

        if (
            type.InheritsFrom(DomainNamespace, "Aggregate")
            || type.InheritsFrom(DomainNamespace, "BaseEntity")
        )
        {
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named)
        {
            return named.TypeArguments.Any(arg => ContainsDomainEntity(arg, depth + 1));
        }

        return false;
    }
}
