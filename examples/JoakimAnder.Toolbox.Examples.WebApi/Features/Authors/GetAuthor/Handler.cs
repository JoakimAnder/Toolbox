using System.Globalization;
using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Domain;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Repositories;
using JoakimAnder.Toolbox.Results;

namespace JoakimAnder.Toolbox.Examples.WebApi.Features.Authors.GetAuthor;

[Scoped]
public sealed class GetAuthorHandler(AuthorRepository authors)
{
    public async Task<Result<AuthorResponse, ApiError>> HandleAsync(int id, CancellationToken ct)
    {
        var author = await authors.GetAsync(id, ct);
        if (author is null)
        {
            return new ApiError.NotFound("Author", id.ToString(CultureInfo.InvariantCulture));
        }

        return new AuthorResponse(author.Id, author.Name);
    }
}
