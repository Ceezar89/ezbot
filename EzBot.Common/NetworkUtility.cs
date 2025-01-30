using Polly;
using Polly.Retry;

namespace EzBot.Common;
public static class NetworkUtility
{
    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy
        .Handle<HttpRequestException>()
        .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                // TODO: Log the retry attempt
                Console.WriteLine($"Retrying... Attempt {retryAttempt}");
            });

    public static async Task<HttpResponseMessage> MakeRequestAsync(HttpClient httpClient, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await RetryPolicy.ExecuteAsync(() => httpClient.SendAsync(request, cancellationToken));
    }
}