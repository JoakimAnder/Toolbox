using System.Collections.Immutable;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Diagnostics;
using JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection;

internal static class Parser
{
    public static EquatableArray<RegistrationResult> Parse(GeneratorAttributeSyntaxContext context, Lifetime lifetime)
    {
        var implementation = (INamedTypeSymbol)context.TargetSymbol;
        var implName = implementation.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var builder = ImmutableArray.CreateBuilder<RegistrationResult>(context.Attributes.Length);
        foreach (var attribute in context.Attributes)
        {
            var location = LocationInfo.From(attribute.ApplicationSyntaxReference?.GetSyntax());

            if (implementation.IsAbstract || implementation.IsStatic)
            {
                builder.Add(RegistrationResult.Error(new DiagnosticInfo(
                    DiagnosticDescriptors.NotConstructible, location,
                    new EquatableArray<string>(ImmutableArray.Create(implName)))));
                continue;
            }

            INamedTypeSymbol? serviceSymbol = null;
            if (attribute.ConstructorArguments.Length > 0 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol service)
            {
                serviceSymbol = service;
            }

            if (serviceSymbol is not null && !IsAssignable(implementation, serviceSymbol))
            {
                builder.Add(RegistrationResult.Error(new DiagnosticInfo(
                    DiagnosticDescriptors.NotAssignable, location,
                    new EquatableArray<string>(ImmutableArray.Create(
                        implName, serviceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))));
                continue;
            }

            var group = NamedString(attribute, "Group");
            if (group is not null && !SyntaxFacts.IsValidIdentifier(group))
            {
                builder.Add(RegistrationResult.Error(new DiagnosticInfo(
                    DiagnosticDescriptors.InvalidGroup, location,
                    new EquatableArray<string>(ImmutableArray.Create(group, implName)))));
                continue;
            }

            builder.Add(RegistrationResult.Ok(new ServiceRegistration(
                implName,
                serviceSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                lifetime,
                group,
                NamedString(attribute, "Key"))));
        }

        return new EquatableArray<RegistrationResult>(builder.ToImmutable());
    }

    private static bool IsAssignable(INamedTypeSymbol implementation, INamedTypeSymbol service)
    {
        if (SymbolEqualityComparer.Default.Equals(implementation, service)) return true;
        foreach (var iface in implementation.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(iface, service)) return true;
        for (var baseType = implementation.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (SymbolEqualityComparer.Default.Equals(baseType, service)) return true;
        return false;
    }

    private static string? NamedString(AttributeData attribute, string name)
    {
        foreach (var arg in attribute.NamedArguments)
            if (arg.Key == name && arg.Value.Value is string s && s.Length > 0)
                return s;
        return null;
    }
}
