using System.Net;
using Microsoft.Extensions.Logging;

namespace SearchLab.Ingest;

public sealed class ThrottlingRetryHandler(TimeSpan delay, int maxAttempts, ILogger<ThrottlingRetryHandler> logger) : DelegatingHandler
{
    private readonly ILogger<ThrottlingRetryHandler> _logger = logger;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            bool isLastAttempt = attempt == maxAttempts;
            string reason;

            try
            {
                await Task.Delay(delay, cancellationToken);

                HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

                if (response.StatusCode != HttpStatusCode.TooManyRequests || isLastAttempt)
                {
                    return response;
                }

                response.Dispose();
                reason = "429 Too Many Requests";
            }
            catch (HttpRequestException ex) when (!isLastAttempt)
            {
                reason = ex.Message;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && !isLastAttempt)
            {
                reason = "timed out";
            }

            TimeSpan backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));

            IngestLog.RetryingRequest(_logger, request.RequestUri, reason, attempt, maxAttempts, backoff.TotalSeconds);

            await Task.Delay(backoff, cancellationToken);
        }
    }
}
