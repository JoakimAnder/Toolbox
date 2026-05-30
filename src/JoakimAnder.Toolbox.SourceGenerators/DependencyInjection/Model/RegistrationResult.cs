namespace JoakimAnder.Toolbox.SourceGenerators.DependencyInjection.Model;

// Exactly one of the two is non-null. Both members are equatable => cache-safe.
internal readonly record struct RegistrationResult(ServiceRegistration? Registration, DiagnosticInfo? Diagnostic)
{
    public static RegistrationResult Ok(ServiceRegistration registration) => new(registration, null);
    public static RegistrationResult Error(DiagnosticInfo diagnostic) => new(null, diagnostic);
}
