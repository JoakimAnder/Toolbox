using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// One line registers every [Singleton]/[Scoped] in the project — repos and handlers.
builder.Services.AddAttributedServices();

// .NET 10 built-in OpenAPI; reader can import /openapi/v1.json into Postman/Bruno.
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

app
    .MapBookEndpoints()
    .MapAuthorEndpoints()
    .MapReviewEndpoints();

app.Run();
