namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;

public sealed record BookDetailResponse(
    int Id,
    string Isbn,
    string Title,
    AuthorSummary Author,
    IReadOnlyList<ReviewSummary> Reviews);

public sealed record AuthorSummary(int Id, string Name);

public sealed record ReviewSummary(string Reviewer, int Rating, string Body);
