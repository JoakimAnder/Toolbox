using System.Globalization;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;
using JoakimAnder.Toolbox.Results;
using HttpResults = global::Microsoft.AspNetCore.Http.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

public static class CreateReviewEndpoint
{
    public static RouteGroupBuilder MapCreateReview(this RouteGroupBuilder reviews)
    {
        reviews.MapPost("/", async (int bookId, CreateReviewRequest req, CreateReviewHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(bookId, req, ct);
            return result.Match<IResult>(
                onSuccess: created =>
                    HttpResults.Created(
                        $"/books/{bookId.ToString(CultureInfo.InvariantCulture)}/reviews/{created.Id.ToString(CultureInfo.InvariantCulture)}",
                        created),
                onFailure: ApiErrorMapping.ToHttpResult);
        })
        .WithName("CreateReview")
        .WithSummary("Create a review for a book.");

        return reviews;
    }
}
