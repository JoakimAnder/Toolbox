using System.Globalization;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;
using HttpResults = global::Microsoft.AspNetCore.Http.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

public static class CreateBookEndpoint
{
    public static RouteGroupBuilder MapCreateBook(this RouteGroupBuilder books)
    {
        books.MapPost("/", async (CreateBookRequest req, CreateBookHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(req, ct);
            return result.Match<IResult>(
                onSuccess: created =>
                    HttpResults.Created($"/books/{created.Id.ToString(CultureInfo.InvariantCulture)}", created),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("CreateBook")
        .WithSummary("Create a book.");

        return books;
    }
}
