namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

public sealed record BookListItem(int Id, string Isbn, string Title, int AuthorId);
