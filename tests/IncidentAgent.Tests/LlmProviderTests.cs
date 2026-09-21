using System.Net;
using System.Text;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Tests;

public sealed class LlmProviderTests
{
    [Fact]
    public async Task OpenAiProvider_MapsJsonContentUsageAndCost()
    {
        var handler = new CaptureHandler("""
        {
          "choices": [
            {
              "message": {
                "content": "{\"summary\":{\"text\":\"ok\",\"evidenceIds\":[\"e1\"]},\"hypotheses\":[],\"actions\":[]}"
              }
            }
          ],
          "usage": {
            "prompt_tokens": 100,
            "completion_tokens": 50
          }
        }
        """);

        var provider = new OpenAiIncidentLlmProvider(
            new HttpClient(handler),
            Options.Create(new LlmReasonerOptions
            {
                ApiKey = "openai-secret",
                Model = "test-openai",
                OpenAiBaseUrl = "https://openai.example/",
                InputCostPerMillionTokens = 2m,
                OutputCostPerMillionTokens = 8m
            }));

        var result = await provider.GenerateAsync("system", "user");

        Assert.Equal("openai", result.Provider);
        Assert.Equal("test-openai", result.Model);
        Assert.Equal(100, result.InputTokens);
        Assert.Equal(50, result.OutputTokens);
        Assert.Equal(0.0006m, result.EstimatedCostUsd);
        Assert.Equal(
            "Bearer openai-secret",
            handler.Request!.Headers.Authorization?.ToString());
        Assert.Equal(
            "/v1/chat/completions",
            handler.Request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("openai-secret", result.Content);
    }

    [Fact]
    public async Task AnthropicProvider_MapsJsonContentUsageAndCost()
    {
        var handler = new CaptureHandler("""
        {
          "content": [
            {
              "type": "text",
              "text": "{\"summary\":{\"text\":\"ok\",\"evidenceIds\":[\"e1\"]},\"hypotheses\":[],\"actions\":[]}"
            }
          ],
          "usage": {
            "input_tokens": 200,
            "output_tokens": 40
          }
        }
        """);

        var provider = new AnthropicIncidentLlmProvider(
            new HttpClient(handler),
            Options.Create(new LlmReasonerOptions
            {
                ApiKey = "anthropic-secret",
                Model = "test-claude",
                AnthropicBaseUrl = "https://anthropic.example/",
                InputCostPerMillionTokens = 3m,
                OutputCostPerMillionTokens = 15m
            }));

        var result = await provider.GenerateAsync("system", "user");

        Assert.Equal("anthropic", result.Provider);
        Assert.Equal(200, result.InputTokens);
        Assert.Equal(40, result.OutputTokens);
        Assert.Equal(0.0012m, result.EstimatedCostUsd);
        Assert.True(handler.Request!.Headers.TryGetValues(
            "x-api-key",
            out var apiKeyValues));
        Assert.Equal("anthropic-secret", Assert.Single(apiKeyValues));
        Assert.Equal("/v1/messages", handler.Request.RequestUri!.AbsolutePath);
        Assert.DoesNotContain("anthropic-secret", result.Content);
    }

    private sealed class CaptureHandler(string responseJson) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = Clone(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json")
            });
        }

        private static HttpRequestMessage Clone(HttpRequestMessage source)
        {
            var clone = new HttpRequestMessage(source.Method, source.RequestUri);
            foreach (var header in source.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
