using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;

using HttpResults = global::Microsoft.AspNetCore.Http.Results;

public static class GetAuthorEndpoint
{
    public static RouteGroupBuilder MapGetAuthor(this RouteGroupBuilder authors)
    {
        authors.MapGet("/{id:int}", async (int id, GetAuthorHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.Match<IResult>(
                onSuccess: response => HttpResults.Ok(response),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("GetAuthor")
        .WithSummary("Get a single author by ID.");

        return authors;
    }
}
