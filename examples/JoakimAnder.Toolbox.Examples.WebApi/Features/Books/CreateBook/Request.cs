namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;

public sealed record CreateBookRequest(string Isbn, string Title, int AuthorId);
