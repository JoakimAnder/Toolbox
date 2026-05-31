namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;

public sealed record Review(int Id, int BookId, string Reviewer, int Rating, string Body);
