using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Diagnostics;

internal static class DiagnosticDescriptors
{
    private const string Category = "JoakimAnder.Toolbox.DependencyInjection";

    public static readonly DiagnosticDescriptor NotAssignable = new(
        "TBX1001", "Service type not assignable from implementation",
        "'{0}' does not implement or inherit the service type '{1}'", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotConstructible = new(
        "TBX1002", "Marked type cannot be used as an implementation",
        "'{0}' is abstract or static and cannot be registered as a service implementation", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidGroup = new(
        "TBX1003", "Group name is not a valid identifier",
        "Group '{0}' on '{1}' is not a valid C# identifier and cannot be used in a method name", Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingServiceCollection = new(
        "TBX1004", "IServiceCollection is not referenced",
        "Attributed services were found but Microsoft.Extensions.DependencyInjection is not referenced; no registration method was generated", Category,
        DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
