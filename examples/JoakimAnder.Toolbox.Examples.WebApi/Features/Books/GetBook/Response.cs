namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;

public sealed record BookResponse(int Id, string Isbn, string Title, int AuthorId);
