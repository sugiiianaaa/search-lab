using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace SearchLab.UnitTests.Support;

internal sealed class StubHttpHandler(HttpStatusCode httpStatusCode, string body) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);

        return Task.FromResult(new HttpResponseMessage(httpStatusCode) { Content = new StringContent(body) });
    }
}
