namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

/// <summary>
/// Discriminated union of failure kinds returned across the API surface.
/// Every handler returns <c>Result&lt;T, ApiError&gt;</c>; the boundary
/// switch on these cases is exhaustive at compile time.
/// </summary>
public abstract record ApiError
{
    public sealed record NotFound(string Resource, string Id)              : ApiError;
    public sealed record Validation(string Field, string Message)          : ApiError;
    public sealed record Conflict(string Resource, string Reason)          : ApiError;
    public sealed record Upstream(string ExceptionType, string Message)    : ApiError;
    public sealed record Unexpected(string Message)                        : ApiError;
}
