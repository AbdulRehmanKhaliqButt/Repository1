using IncidentAgent.Core;
using IncidentAgent.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IncidentAgent.Tests;

public sealed class ReasonerRegistrationTests
{
    [Fact]
    public void DisabledLlm_RegistersDeterministicReasoner()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{LlmReasonerOptions.SectionName}:Enabled"] = "false"
        });

        Assert.IsType<DeterministicIncidentReasoner>(
            provider.GetRequiredService<IIncidentReasoner>());
    }

    [Fact]
    public void OpenAiLlm_RegistersValidatedReasonerAndOpenAiProvider()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{LlmReasonerOptions.SectionName}:Enabled"] = "true",
            [$"{LlmReasonerOptions.SectionName}:Provider"] = "openai"
        });

        Assert.IsType<ValidatedLlmIncidentReasoner>(
            provider.GetRequiredService<IIncidentReasoner>());
        Assert.IsType<OpenAiIncidentLlmProvider>(
            provider.GetRequiredService<IIncidentLlmProvider>());
    }

    [Fact]
    public void AnthropicLlm_RegistersValidatedReasonerAndAnthropicProvider()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [$"{LlmReasonerOptions.SectionName}:Enabled"] = "true",
            [$"{LlmReasonerOptions.SectionName}:Provider"] = "anthropic"
        });

        Assert.IsType<ValidatedLlmIncidentReasoner>(
            provider.GetRequiredService<IIncidentReasoner>());
        Assert.IsType<AnthropicIncidentLlmProvider>(
            provider.GetRequiredService<IIncidentLlmProvider>());
    }

    private static ServiceProvider BuildProvider(
        IDictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddIncidentInvestigation(configuration);
        return services.BuildServiceProvider();
    }
}
