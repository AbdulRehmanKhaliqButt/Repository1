using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class AnthropicIncidentLlmProvider : IIncidentLlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly LlmReasonerOptions _options;

    public AnthropicIncidentLlmProvider(
        HttpClient httpClient,
        IOptions<LlmReasonerOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var baseUrl = _options.AnthropicBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? _options.AnthropicBaseUrl
            : _options.AnthropicBaseUrl + "/";

        _httpClient.BaseAddress ??= new Uri(baseUrl, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Remove("x-api-key");
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        }

        _httpClient.DefaultRequestHeaders.Remove("anthropic-version");
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public string Name => "anthropic";

    public async Task<LlmGenerationResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = _options.Model,
            max_tokens = Math.Clamp(_options.MaxTokens, 256, 8192),
            system = systemPrompt,
            messages = new object[]
            {
                new { role = "user", content = userPrompt }
            }
        };

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Clamp(_options.TimeoutSeconds, 1, 180)));

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient.PostAsJsonAsync(
            "v1/messages",
            payload,
            timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(timeoutCts.Token));
        stopwatch.Stop();

        var content = document.RootElement
            .GetProperty("content")
            .EnumerateArray()
            .Where(block =>
                block.TryGetProperty("type", out var type) &&
                type.GetString() == "text")
            .Select(block =>
                block.TryGetProperty("text", out var text)
                    ? text.GetString()
                    : null)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Anthropic returned no JSON content.");
        }

        var inputTokens = TryReadInt(document.RootElement, "usage", "input_tokens");
        var outputTokens = TryReadInt(document.RootElement, "usage", "output_tokens");

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
