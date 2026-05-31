using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

using HttpResults = global::Microsoft.AspNetCore.Http.Results;

public static class ListBooksEndpoint
{
    public static RouteGroupBuilder MapListBooks(this RouteGroupBuilder books)
    {
        books.MapGet("/", async (ListBooksHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(ct);
            return result.Match<IResult>(
                onSuccess: items => HttpResults.Ok(items),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("ListBooks")
        .WithSummary("List all books.");

        return books;
    }
}
