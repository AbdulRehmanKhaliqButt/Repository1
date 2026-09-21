using System.Globalization;
using System.Text.Json;
using IncidentAgent.Core;
using Microsoft.Extensions.Options;

namespace IncidentAgent.Infrastructure;

public sealed class PrometheusMetricEvidenceSource : IIncidentEvidenceSource
{
    private readonly HttpClient _httpClient;
    private readonly PrometheusEvidenceOptions _options;

    public PrometheusMetricEvidenceSource(
        HttpClient httpClient,
        IOptions<PrometheusEvidenceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        ObservabilityHttpConfiguration.Configure(
            _httpClient,
            _options.BaseUrl,
            _options.BearerToken);
    }

    public string Name => "prometheus";

    public async Task<IReadOnlyCollection<IncidentEvidence>> CollectAsync(
        IncidentRequest incident,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var service = incident.ServiceName ?? "unknown";
        var window = Math.Clamp(_options.WindowMinutes, 1, 240);
        var start = incident.StartedAtUtc.AddMinutes(-window);
        var end = incident.StartedAtUtc.AddMinutes(window);
        var query = _options.QueryTemplate.Replace(
            "{service}",
            ObservabilityHttpConfiguration.EscapeQuotedValue(service),
            StringComparison.Ordinal);

        var uri =
            "api/v1/query_range" +
            $"?query={Uri.EscapeDataString(query)}" +
            $"&start={Uri.EscapeDataString(start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&end={Uri.EscapeDataString(end.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))}" +
            $"&step={Math.Clamp(_options.StepSeconds, 1, 3600)}s";

        var json = await HttpRequestExecutor.GetStringAsync(
            _httpClient,
            uri,
            _options.TimeoutSeconds,
            _options.RetryCount,
            cancellationToken);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("status", out var status) ||
            !string.Equals(status.GetString(), "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Prometheus returned a non-success response.");
        }

        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("result", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<IncidentEvidence>();
        }

        var evidence = new List<IncidentEvidence>();

        foreach (var series in results.EnumerateArray())
        {
            if (!series.TryGetProperty("values", out var values) ||
                values.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var samples = ReadSamples(values);
            if (samples.Count == 0)
            {
                continue;
            }

            var maxSample = samples.MaxBy(sample => sample.Value);
            var latestSample = samples.MaxBy(sample => sample.Timestamp);
            var labels = ReadLabels(series);
            var attributes = new Dictionary<string, string>(labels, StringComparer.OrdinalIgnoreCase)
            {
                ["metric.name"] = _options.MetricName,
                ["metric.unit"] = _options.Unit,
                ["observed.max"] = maxSample.Value.ToString("0.###", CultureInfo.InvariantCulture),
                ["observed.latest"] = latestSample.Value.ToString("0.###", CultureInfo.InvariantCulture)
            };

            if (string.Equals(_options.Unit, "ms", StringComparison.OrdinalIgnoreCase))
            {
                attributes["observed.ms"] = maxSample.Value.ToString("0.###", CultureInfo.InvariantCulture);
            }

            evidence.Add(new IncidentEvidence(
                $"prometheus-{evidence.Count}-{latestSample.Timestamp.ToUnixTimeSeconds()}",
                EvidenceType.Metric,
                Name,
                latestSample.Timestamp,
                service,
                $"{_options.MetricName} peaked at {maxSample.Value:0.##} {_options.Unit}",
                $"Prometheus observed {samples.Count} samples; latest={latestSample.Value:0.##} {_options.Unit}; max={maxSample.Value:0.##} {_options.Unit}.",
                attributes));
        }

        return evidence;
    }

    private static List<MetricSample> ReadSamples(JsonElement values)
    {
        var samples = new List<MetricSample>();

        foreach (var item in values.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 2)
            {
                continue;
            }

            if (!item[0].TryGetDouble(out var epochSeconds))
            {
                continue;
            }

            if (!double.TryParse(
                    item[1].ToString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var value) ||
                double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                continue;
            }

            var milliseconds = checked((long)(epochSeconds * 1000d));
            samples.Add(new MetricSample(
                DateTimeOffset.FromUnixTimeMilliseconds(milliseconds),
                value));
        }

        return samples;
    }

    private static Dictionary<string, string> ReadLabels(JsonElement series)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!series.TryGetProperty("metric", out var metric) ||
            metric.ValueKind != JsonValueKind.Object)
        {
            return labels;
        }

        foreach (var label in metric.EnumerateObject())
        {
            labels[label.Name] = label.Value.ToString();
        }

        return labels;
    }

    private sealed record MetricSample(DateTimeOffset Timestamp, double Value);
}
