using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

[Scoped]
public sealed class ListBooksHandler(BookRepository books)
{
    public async Task<Result<IReadOnlyList<BookListItem>, ApiError>> HandleAsync(CancellationToken ct)
    {
        var all = await books.ListAsync(ct);
        IReadOnlyList<BookListItem> items =
            all.Select(b => new BookListItem(b.Id, b.Isbn, b.Title, b.AuthorId)).ToList();

        // Explicit Success factory: the LINQ chain returns List<BookListItem>, and the
        // implicit conversion can't cross List<T> -> IReadOnlyList<T> -> Result<...> in
        // one step. Other handlers return a concrete record directly and use the implicit
        // T -> Result<T, TError> conversion instead.
        return Result<IReadOnlyList<BookListItem>, ApiError>.Success(items);
    }
}
