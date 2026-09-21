using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class OpenAiIncidentLlmProvider : IIncidentLlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmReasonerOptions _options;

    public OpenAiIncidentLlmProvider(
        HttpClient httpClient,
        IOptions<LlmReasonerOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var baseUrl = _options.OpenAiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? _options.OpenAiBaseUrl
            : _options.OpenAiBaseUrl + "/";

        _httpClient.BaseAddress ??= new Uri(baseUrl, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public string Name => "openai";

    public async Task<LlmGenerationResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Clamp(_options.TimeoutSeconds, 1, 180)));

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsJsonAsync(
            "v1/chat/completions",
            payload,
            timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(timeoutCts.Token));
        stopwatch.Stop();

        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI returned no JSON content.");
        }

        var inputTokens = TryReadInt(document.RootElement, "usage", "prompt_tokens");
        var outputTokens = TryReadInt(document.RootElement, "usage", "completion_tokens");

        return new LlmGenerationResult(
            content,
            Name,
            _options.Model,
            inputTokens,
            outputTokens,
            stopwatch.ElapsedMilliseconds,
            EstimateCost(inputTokens, outputTokens));
    }

    private decimal EstimateCost(int inputTokens, int outputTokens) =>
        ((decimal)inputTokens / 1_000_000m * _options.InputCostPerMillionTokens) +
        ((decimal)outputTokens / 1_000_000m * _options.OutputCostPerMillionTokens);

    private static int TryReadInt(
        JsonElement root,
        string parent,
        string property)
    {
        return root.TryGetProperty(parent, out var parentValue) &&
               parentValue.TryGetProperty(property, out var value) &&
               value.TryGetInt32(out var parsed)
            ? parsed
            : 0;
    }
}
