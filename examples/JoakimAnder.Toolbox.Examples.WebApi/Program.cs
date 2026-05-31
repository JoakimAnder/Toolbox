var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "JoakimAnder.Toolbox.Examples.WebApi");
app.Run();
