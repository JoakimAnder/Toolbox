using System.Linq;
using Microsoft.CodeAnalysis;

namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

internal readonly record struct DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), MessageArgs.AsImmutableArray().Cast<object>().ToArray());
}
