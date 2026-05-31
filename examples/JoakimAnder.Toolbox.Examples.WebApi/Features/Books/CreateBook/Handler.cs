using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

[Scoped]
public sealed class CreateBookHandler(BookRepository books, AuthorRepository authors)
{
    public async Task<Result<BookResponse, ApiError>> HandleAsync(
        CreateBookRequest req, CancellationToken ct)
    {
        // 1) Pure validation — two steps chained with Bind, short-circuits on first failure.
        var validated = ValidateIsbn(req).Bind(_ => ValidateTitle(req));
        if (!validated.TryGetValue(out _, out var validationError))
        {
            return validationError;
        }

        // 2) Referenced author must exist.
        var author = await authors.GetAsync(req.AuthorId, ct);
        if (author is null)
        {
            return new ApiError.NotFound("Author", req.AuthorId.ToString(CultureInfo.InvariantCulture));
        }

        // 3) Persist. AddAsync returns null on duplicate ISBN.
        var stored = await books.AddAsync(
            new Book(Id: 0, req.Isbn, req.Title, req.AuthorId), ct);
        if (stored is null)
        {
            return new ApiError.Conflict("Book", $"ISBN {req.Isbn} is already in use.");
        }

        return new BookResponse(stored.Id, stored.Isbn, stored.Title, stored.AuthorId);
    }

    private static Result<CreateBookRequest, ApiError> ValidateIsbn(CreateBookRequest req) =>
        string.IsNullOrWhiteSpace(req.Isbn)
            ? new ApiError.Validation(nameof(req.Isbn), "ISBN is required.")
            : req;

    private static Result<CreateBookRequest, ApiError> ValidateTitle(CreateBookRequest req) =>
        string.IsNullOrWhiteSpace(req.Title)
            ? new ApiError.Validation(nameof(req.Title), "Title is required.")
            : req;
}
