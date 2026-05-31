using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;

[Scoped]
public sealed class GetBookHandler(BookRepository books)
{
    public async Task<Result<BookResponse, ApiError>> HandleAsync(int id, CancellationToken ct)
    {
        var book = await books.GetAsync(id, ct);
        if (book is null)
        {
            return new ApiError.NotFound("Book", id.ToString(CultureInfo.InvariantCulture));
        }

        return new BookResponse(book.Id, book.Isbn, book.Title, book.AuthorId);
    }
}
