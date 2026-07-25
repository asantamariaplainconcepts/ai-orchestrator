using Microsoft.CodeAnalysis;

namespace AiOrchestrator.ArchitectureAnalyzers;

public static class DiagnosticDescriptors
{
    const string ModuleBoundaryCategory = "ModuleBoundary";

    public static readonly DiagnosticDescriptor Mod001PublicCqsRequest = new(
        id: "MOD001",
        title: new LocalizableResourceString(
            nameof(Resources.MOD001Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.MOD001MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        category: ModuleBoundaryCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.MOD001Description),
            Resources.ResourceManager,
            typeof(Resources)
        )
    );

    public static readonly DiagnosticDescriptor Mod002CrossModuleReference = new(
        id: "MOD002",
        title: new LocalizableResourceString(
            nameof(Resources.MOD002Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.MOD002MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        category: ModuleBoundaryCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.MOD002Description),
            Resources.ResourceManager,
            typeof(Resources)
        )
    );

    public static readonly DiagnosticDescriptor Mod003PublicDomainEntity = new(
        id: "MOD003",
        title: new LocalizableResourceString(
            nameof(Resources.MOD003Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.MOD003MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        category: ModuleBoundaryCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.MOD003Description),
            Resources.ResourceManager,
            typeof(Resources)
        )
    );

    public static readonly DiagnosticDescriptor Mod004ControllerInModule = new(
        id: "MOD004",
        title: new LocalizableResourceString(
            nameof(Resources.MOD004Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.MOD004MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        category: ModuleBoundaryCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.MOD004Description),
            Resources.ResourceManager,
            typeof(Resources)
        )
    );

    public static readonly DiagnosticDescriptor Mod005EntityLeak = new(
        id: "MOD005",
        title: new LocalizableResourceString(
            nameof(Resources.MOD005Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.MOD005MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        category: ModuleBoundaryCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.MOD005Description),
            Resources.ResourceManager,
            typeof(Resources)
        )
    );

    public static readonly DiagnosticDescriptor Cqs001PublicOrUnsealedHandler = new(
        id: "CQS001",
        title: new LocalizableResourceString(
            nameof(Resources.CQS001Title),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        messageFormat: new LocalizableResourceString(
            nameof(Resources.CQS001MessageFormat),
            Resources.ResourceManager,
            typeof(Resources)
        ),
        category: ModuleBoundaryCategory,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: new LocalizableResourceString(
            nameof(Resources.CQS001Description),
            Resources.ResourceManager,
            typeof(Resources)
        )
    );
}
