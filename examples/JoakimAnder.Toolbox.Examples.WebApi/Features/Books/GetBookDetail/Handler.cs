using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;
using JoakimAnder.Toolbox.Threading;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;

[Scoped]
public sealed class GetBookDetailHandler(
    BookRepository books,
    AuthorRepository authors,
    ReviewRepository reviews)
{
    public async Task<Result<BookDetailResponse, ApiError>> HandleAsync(int id, CancellationToken ct)
    {
        // 1) Book is the root — without it, every other lookup is useless.
        var book = await books.GetAsync(id, ct);
        if (book is null)
        {
            return new ApiError.NotFound("Book", id.ToString(CultureInfo.InvariantCulture));
        }

        // 2) Author + reviews fan out in parallel.
        //    Thrown exceptions become ApiError.Upstream via Result.TryAsync.
        //    OperationCanceledException is rethrown unchanged (per the spec's catch policy).
        var depsResult = await Result.TryAsync(
            async c =>
            {
                var (author, list) = await new FanOut()
                    .Add(t => authors.GetAsync(book.AuthorId, t))
                    .Add(t => reviews.GetByBookAsync(book.Id, t))
                    .WhenAll(c);
                return (Author: author, Reviews: list);
            },
            ex => (ApiError)new ApiError.Upstream(ex.GetType().Name, ex.Message),
            ct);

        // 3) Canonical consumer-read pattern.
        if (!depsResult.TryGetValue(out var deps, out var fetchError))
        {
            return fetchError;
        }

        // 4) Orphaned book (AuthorId points to a missing author).
        if (deps.Author is null)
        {
            return new ApiError.NotFound("Author", book.AuthorId.ToString(CultureInfo.InvariantCulture));
        }

        // 5) Compose the wire response.
        return new BookDetailResponse(
            book.Id, book.Isbn, book.Title,
            new AuthorSummary(deps.Author.Id, deps.Author.Name),
            deps.Reviews.Select(r => new ReviewSummary(r.Reviewer, r.Rating, r.Body)).ToList());
    }
}
