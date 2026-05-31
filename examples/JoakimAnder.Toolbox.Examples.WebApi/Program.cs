using JoakimAnder.Toolbox.DependencyInjection;
using JoakimAnder.Toolbox.Examples.WebApi.Shared.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// One line registers every [Singleton]/[Scoped] in the project — repos and handlers.
builder.Services.AddAttributedServices();

var app = builder.Build();

app
    .MapBookEndpoints()
    .MapAuthorEndpoints()
    .MapReviewEndpoints();

app.Run();
