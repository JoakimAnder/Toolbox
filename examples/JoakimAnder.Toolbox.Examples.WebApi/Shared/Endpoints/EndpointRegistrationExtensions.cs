using JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.CreateBook;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBook;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.GetBookDetail;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Books.ListBooks;
using JoakimAnder.Toolbox.Examples.WebApi.Features.Reviews.CreateReview;

namespace JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

/// <summary>
/// Composes per-feature MapXxx() extensions into per-area route-group calls.
/// Each slice owns its endpoint; this file owns the URL structure.
/// </summary>
public static class EndpointRegistrationExtensions
{
    public static IEndpointRouteBuilder MapBookEndpoints(this IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/books");
        books
            .MapListBooks()
            .MapGetBook()
            .MapGetBookDetail()
            .MapCreateBook();
        return app;
    }

    public static IEndpointRouteBuilder MapAuthorEndpoints(this IEndpointRouteBuilder app)
    {
        var authors = app.MapGroup("/authors");
        authors.MapGetAuthor();
        return app;
    }

    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var reviews = app.MapGroup("/books/{bookId:int}/reviews");
        reviews.MapCreateReview();
        return app;
    }
}
