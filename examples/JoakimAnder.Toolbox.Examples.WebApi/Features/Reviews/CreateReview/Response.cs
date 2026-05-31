namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

public sealed record ReviewResponse(int Id, int BookId, string Reviewer, int Rating, string Body);
