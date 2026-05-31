using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

[Scoped]
public sealed class CreateReviewHandler(BookRepository books, ReviewRepository reviews)
{
    public async Task<Result<ReviewResponse, ApiError>> HandleAsync(
        int bookId, CreateReviewRequest req, CancellationToken ct)
    {
        // 1) Pure validation — three sync steps chained with Bind; short-circuits on first failure.
        var validated = ValidateReviewer(req)
            .Bind(_ => ValidateRating(req))
            .Bind(_ => ValidateBody(req));

        if (!validated.TryGetValue(out _, out var validationError))
        {
            return validationError;
        }

        // 2) Book existence check.
        var book = await books.GetAsync(bookId, ct);
        if (book is null)
        {
            return new ApiError.NotFound("Book", bookId.ToString(CultureInfo.InvariantCulture));
        }

        // 3) Persist; AddAsync returns null on (BookId, Reviewer) duplicate.
        var stored = await reviews.AddAsync(
            new Review(Id: 0, BookId: bookId, req.Reviewer, req.Rating, req.Body), ct);
        if (stored is null)
        {
            return new ApiError.Conflict(
                "Review", $"{req.Reviewer} has already reviewed book {bookId.ToString(CultureInfo.InvariantCulture)}.");
        }

        return new ReviewResponse(stored.Id, stored.BookId, stored.Reviewer, stored.Rating, stored.Body);
    }

    private static Result<CreateReviewRequest, ApiError> ValidateReviewer(CreateReviewRequest req) =>
        string.IsNullOrWhiteSpace(req.Reviewer)
            ? new ApiError.Validation(nameof(req.Reviewer), "Reviewer is required.")
            : req;

    private static Result<CreateReviewRequest, ApiError> ValidateRating(CreateReviewRequest req) =>
        req.Rating is < 1 or > 5
            ? new ApiError.Validation(nameof(req.Rating), "Rating must be between 1 and 5.")
            : req;

    private static Result<CreateReviewRequest, ApiError> ValidateBody(CreateReviewRequest req) =>
        string.IsNullOrWhiteSpace(req.Body)
            ? new ApiError.Validation(nameof(req.Body), "Body is required.")
            : req;
}
