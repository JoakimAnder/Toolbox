using System.Collections.Immutable;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

internal static class Parser
{
    public static EquatableArray<ServiceRegistration> Parse(GeneratorAttributeSyntaxContext context, Lifetime lifetime)
    {
        var implementation = (INamedTypeSymbol)context.TargetSymbol;
        var implName = implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var builder = ImmutableArray.CreateBuilder<ServiceRegistration>(context.Attributes.Length);
        foreach (var attribute in context.Attributes)
        {
            string? serviceType = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol service)
            {
                serviceType = service.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            }

            builder.Add(new ServiceRegistration(
                implName,
                serviceType,
                lifetime,
                NamedString(attribute, "Group"),
                NamedString(attribute, "Key")));
        }

        return new EquatableArray<ServiceRegistration>(builder.ToImmutable());
    }

    private static string? NamedString(AttributeData attribute, string name)
    {
        foreach (var arg in attribute.NamedArguments)
            if (arg.Key == name && arg.Value.Value is string s && s.Length > 0)
                return s;
        return null;
    }
}
