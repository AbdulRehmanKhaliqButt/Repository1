namespace IncidentAgent.Infrastructure;

public sealed class LlmReasonerOptions
{
    public const string SectionName = "Reasoning:Llm";

    public bool Enabled { get; init; }

    public string Provider { get; init; } = "openai";

    public string Model { get; init; } = "gpt-5";

    public string ApiKey { get; init; } = string.Empty;

    public string OpenAiBaseUrl { get; init; } = "https://api.openai.com/";

    public string AnthropicBaseUrl { get; init; } = "https://api.anthropic.com/";

    public int MaxTokens { get; init; } = 1600;

    public int TimeoutSeconds { get; init; } = 30;

    public decimal InputCostPerMillionTokens { get; init; }

    public decimal OutputCostPerMillionTokens { get; init; }
}
