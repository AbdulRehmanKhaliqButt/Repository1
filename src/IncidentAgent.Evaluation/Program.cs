using System.Text.Json;
using System.Text.Json.Serialization;
using IncidentAgent.Core;
using IncidentAgent.Evaluation;

var arguments = Arguments.Parse(args);
var serializerOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};
serializerOptions.Converters.Add(new JsonStringEnumConverter());

var datasetJson = await File.ReadAllTextAsync(arguments.DatasetPath);
var dataset = JsonSerializer.Deserialize<EvaluationDataset>(
    datasetJson,
    serializerOptions)
    ?? throw new InvalidOperationException("Unable to deserialize evaluation dataset.");

IIncidentReasoner reasoner = new DeterministicIncidentReasoner();
var runner = new EvaluationRunner(reasoner, arguments.Thresholds);
var summary = await runner.RunAsync(dataset);

Console.WriteLine("Incident Investigation Evaluation");
Console.WriteLine($"Cases: {summary.CaseCount}");
Console.WriteLine($"Top-1 accuracy: {summary.Top1Accuracy:P1}");
Console.WriteLine($"Top-3 accuracy: {summary.Top3Accuracy:P1}");
Console.WriteLine($"Citation precision: {summary.CitationPrecision:P1}");
Console.WriteLine($"Citation recall: {summary.CitationRecall:P1}");
Console.WriteLine($"Unsupported-claim rate: {summary.UnsupportedClaimRate:P1}");
Console.WriteLine($"Average latency: {summary.AverageLatencyMs:0.0} ms");
Console.WriteLine($"Estimated cost: ${summary.TotalEstimatedCostUsd:0.000000}");
Console.WriteLine($"Result: {(summary.Passed ? "PASS" : "FAIL")}");

foreach (var item in summary.Cases)
{
    Console.WriteLine(
        $"- {item.Id}: top1={item.Top1Match}, top3={item.Top3Match}, " +
        $"precision={item.CitationPrecision:P0}, recall={item.CitationRecall:P0}, " +
        $"unsupported={item.UnsupportedClaimRate:P0}, latency={item.LatencyMs}ms");
}

if (!string.IsNullOrWhiteSpace(arguments.OutputPath))
{
    var outputDirectory = Path.GetDirectoryName(arguments.OutputPath);
    if (!string.IsNullOrWhiteSpace(outputDirectory))
    {
        Directory.CreateDirectory(outputDirectory);
    }

    await File.WriteAllTextAsync(
        arguments.OutputPath,
        JsonSerializer.Serialize(summary, serializerOptions));
}

if (arguments.FailOnRegression && !summary.Passed)
{
    Environment.ExitCode = 2;
}

internal sealed record Arguments(
    string DatasetPath,
    string? OutputPath,
    bool FailOnRegression,
    EvaluationThresholds Thresholds)
{
    public static Arguments Parse(string[] args)
    {
        var dataset = "benchmarks/known-incidents.json";
        string? output = null;
        var failOnRegression = false;
        var minTop1 = 0.80;
        var minTop3 = 0.90;
        var minPrecision = 0.95;
        var minRecall = 0.80;
        var maxUnsupported = 0.05;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--dataset":
                    dataset = RequireValue(args, ref index);
                    break;
                case "--output":
                    output = RequireValue(args, ref index);
                    break;
                case "--fail-on-regression":
                    failOnRegression = true;
                    break;
                case "--min-top1":
                    minTop1 = ParseRate(RequireValue(args, ref index));
                    break;
                case "--min-top3":
                    minTop3 = ParseRate(RequireValue(args, ref index));
                    break;
                case "--min-citation-precision":
                    minPrecision = ParseRate(RequireValue(args, ref index));
                    break;
                case "--min-citation-recall":
                    minRecall = ParseRate(RequireValue(args, ref index));
                    break;
                case "--max-unsupported":
                    maxUnsupported = ParseRate(RequireValue(args, ref index));
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        return new Arguments(
            dataset,
            output,
            failOnRegression,
            new EvaluationThresholds(
                minTop1,
                minTop3,
                minPrecision,
                minRecall,
                maxUnsupported));
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {args[index]}.");
        }

        index++;
        return args[index];
    }

    private static double ParseRate(string value)
    {
        if (!double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed is < 0 or > 1)
        {
            throw new ArgumentException($"Expected a rate between 0 and 1, received '{value}'.");
        }

        return parsed;
    }
}
