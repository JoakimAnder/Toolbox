namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;

// Intentionally a distinct type from GetBook's BookResponse — list and detail
// shapes may diverge as the example grows (e.g., the list view might drop
// fields that only make sense in a detail view).
public sealed record BookListItem(int Id, string Isbn, string Title, int AuthorId);
