using SearchLab.Api.Endpoints;
using SearchLab.Core.Search;
using SearchLab.Search.Lucene;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Relative to this project's folder, not the repo root.
string indexPath = Path.GetFullPath(
    builder.Configuration["Lucene:IndexPath"]
        ?? throw new InvalidOperationException("Lucene:IndexPath is not configured."),
    builder.Environment.ContentRootPath);

builder.Services.AddSingleton<ISearchBackend>(services =>
    new LuceneSearchBackend(indexPath, services.GetRequiredService<ILogger<LuceneSearchBackend>>()));

WebApplication app = builder.Build();

app.UseStatusCodePages();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health")
   .ExcludeFromDescription();

app.MapSearchEndpoints();

await app.RunAsync();

// For WebApplicationFactory in the integration tests.
public partial class Program;
