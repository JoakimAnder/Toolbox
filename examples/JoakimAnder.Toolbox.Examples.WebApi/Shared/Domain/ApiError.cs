namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

/// <summary>
/// Discriminated union of failure kinds returned across the API surface.
/// Every handler returns <c>Result&lt;T, ApiError&gt;</c>; the boundary
/// switch on these cases is exhaustive at compile time.
/// </summary>
public abstract record ApiError
{
    private ApiError() { }

    /// <summary>The requested resource was not found.</summary>
    /// <param name="Resource">Domain type name (e.g., "Book").</param>
    /// <param name="Id">The identifier that was looked up, as a string.</param>
    public sealed record NotFound(string Resource, string Id) : ApiError;

    /// <summary>The request payload failed validation.</summary>
    /// <param name="Field">The offending field name (e.g., "Isbn").</param>
    /// <param name="Message">Human-readable explanation of why validation failed.</param>
    public sealed record Validation(string Field, string Message) : ApiError;

    /// <summary>The request would violate a uniqueness or state constraint.</summary>
    /// <param name="Resource">Domain type name (e.g., "Book").</param>
    /// <param name="Reason">Human-readable description of the conflict.</param>
    public sealed record Conflict(string Resource, string Reason) : ApiError;

    /// <summary>An upstream dependency threw an exception that was caught and mapped.</summary>
    /// <param name="ExceptionType">The original exception type name (e.g., <c>nameof(HttpRequestException)</c>).</param>
    /// <param name="Message">The exception's message at the time of catch.</param>
    public sealed record Upstream(string ExceptionType, string Message) : ApiError;

    /// <summary>A catch-all for failures that don't fit any other case.</summary>
    /// <param name="Message">Human-readable description of what went wrong.</param>
    public sealed record Unexpected(string Message) : ApiError;
}
