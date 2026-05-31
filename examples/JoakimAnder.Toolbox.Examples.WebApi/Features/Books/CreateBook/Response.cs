namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

public sealed record BookResponse(int Id, string Isbn, string Title, int AuthorId);
