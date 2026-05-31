namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

public sealed record CreateReviewRequest(string Reviewer, int Rating, string Body);
