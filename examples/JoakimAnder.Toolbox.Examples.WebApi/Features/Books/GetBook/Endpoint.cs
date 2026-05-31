using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;

using HttpResults = global::Microsoft.AspNetCore.Http.Results;

public static class GetBookEndpoint
{
    public static RouteGroupBuilder MapGetBook(this RouteGroupBuilder books)
    {
        books.MapGet("/{id:int}", async (int id, GetBookHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.Match<IResult>(
                onSuccess: response => HttpResults.Ok(response),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("GetBook")
        .WithSummary("Get a single book by ID.");

        return books;
    }
}
