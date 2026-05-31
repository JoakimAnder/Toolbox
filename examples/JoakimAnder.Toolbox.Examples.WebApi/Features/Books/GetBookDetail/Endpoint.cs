using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;

using HttpResults = global::Microsoft.AspNetCore.Http.Results;

public static class GetBookDetailEndpoint
{
    public static RouteGroupBuilder MapGetBookDetail(this RouteGroupBuilder books)
    {
        books.MapGet("/{id:int}/detail", async (int id, GetBookDetailHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.Match<IResult>(
                onSuccess: response => HttpResults.Ok(response),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("GetBookDetail")
        .WithSummary("Get a book plus its author and reviews (FanOut composition).");

        return books;
    }
}
