using System.Net;

namespace IncidentAgent.Infrastructure;

internal static class HttpRequestExecutor
{
    public static async Task<string> GetStringAsync(
        HttpClient httpClient,
        string requestUri,
        int timeoutSeconds,
        int retryCount,
        CancellationToken cancellationToken)
    {
        var attempts = Math.Clamp(retryCount, 0, 5) + 1;
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 1, 120));

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                using var response = await httpClient.GetAsync(requestUri, timeoutCts.Token);

                if (IsTransient(response.StatusCode) && attempt < attempts)
                {
                    await DelayAsync(attempt, cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < attempts)
            {
                await DelayAsync(attempt, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < attempts)
            {
                await DelayAsync(attempt, cancellationToken);
            }
        }

        throw new HttpRequestException("Evidence provider request failed after retries.");
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static Task DelayAsync(int attempt, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
}
