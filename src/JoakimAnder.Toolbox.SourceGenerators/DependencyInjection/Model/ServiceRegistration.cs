namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// All members are scalar so record-struct value equality is correct — required for
// incremental caching. No ISymbol / SyntaxNode / Location is allowed here.
internal readonly record struct ServiceRegistration(
    string ImplementationType,  // global::-qualified fully-qualified name
    string? ServiceType,        // global::-qualified FQN; null => register as self
    Lifetime Lifetime,
    string? Group,              // null/empty => default AddAttributedServices
    string? Key);               // null => non-keyed
