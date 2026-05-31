using System.Diagnostics;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

using HttpResults = global::Microsoft.AspNetCore.Http.Results;

/// <summary>
/// Cross-cutting projection from <see cref="ApiError"/> to <see cref="IResult"/>.
/// Every endpoint's failure branch calls <see cref="ToHttpResult"/>.
/// The switch is exhaustive — adding a new <c>ApiError</c> case fails the
/// build until this method is updated.
/// </summary>
internal static class ApiErrorMapping
{
    public static IResult ToHttpResult(ApiError err) => err switch
    {
        ApiError.NotFound nf   => HttpResults.NotFound(new { type = "NotFound",   nf.Resource, nf.Id }),
        ApiError.Validation v  => HttpResults.BadRequest(new { type = "Validation", v.Field, v.Message }),
        ApiError.Conflict c    => HttpResults.Conflict(new { type = "Conflict",   c.Resource, c.Reason }),
        ApiError.Upstream u    => HttpResults.Json(new { type = "Upstream", u.ExceptionType, u.Message }, statusCode: 502),
        // Unexpected uses RFC 7807 ProblemDetails (the convention for 5xx server errors),
        // intentionally differing in body shape from the domain-failure arms above.
        ApiError.Unexpected ux => HttpResults.Problem(ux.Message, statusCode: 500),
        _ => throw new UnreachableException($"Unhandled {nameof(ApiError)} case: {err.GetType().Name}"),
    };
}
