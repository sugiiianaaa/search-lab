using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SearchLab.IntegrationTests;

public sealed class MissingIndexApiFactory : WebApplicationFactory<Program>
{
    public string MissingIndexPath { get; } =
        Path.Combine(Path.GetTempPath(), "searchlab-missing-index-" + Guid.NewGuid().ToString("n"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("Lucene:IndexPath", MissingIndexPath);
}

public sealed class SearchApiTests(MissingIndexApiFactory factory) : IClassFixture<MissingIndexApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_Always_ReturnsOk()
    {
        using HttpResponseMessage response = await _client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_SkipBeyondMax_Returns400WithStableCode()
    {
        using HttpResponseMessage response = await _client.GetAsync(
            new Uri($"/v1/search?q=rust&skip={int.MaxValue}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("search.skip_too_large", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Search_IndexMissing_Returns503WithoutServerPath()
    {
        using HttpResponseMessage response = await _client.GetAsync(new Uri("/v1/search?q=rust", UriKind.Relative));
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("search.index_unavailable", ProblemCode(body));
        Assert.DoesNotContain(factory.MissingIndexPath, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_BlankQuery_Returns400WithStableCode()
    {
        using HttpResponseMessage response = await _client.GetAsync(new Uri("/v1/search?q=%20", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("search.query_empty", await ProblemCodeAsync(response));
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
        => ProblemCode(await response.Content.ReadAsStringAsync());

    private static string? ProblemCode(string body)
        => JsonDocument.Parse(body).RootElement.GetProperty("code").GetString();
}
