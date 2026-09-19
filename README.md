# Incident Investigation Agent

An FDE-style portfolio project that investigates production incidents by correlating evidence across logs, distributed traces, metrics, deployments, and Git commits.

## What the first vertical slice does

A demo checkout incident is investigated end to end:

1. A payment-service deployment occurs shortly before the incident.
2. A related Git commit changes database pool behavior.
3. Prometheus-style latency evidence shows a p95 spike.
4. Loki-style logs show database timeouts.
5. Tempo-style traces show a slow PostgreSQL span.
6. The investigation engine ranks an evidence-backed root-cause hypothesis and proposes remediation actions.

The project intentionally starts with deterministic reasoning and clean provider interfaces. Real OpenAI/Anthropic, Grafana/Loki/Tempo, GitHub, Kubernetes, and OpenTelemetry adapters can be added without rewriting the domain model.

## Tech

- .NET 10 / ASP.NET Core
- C#
- xUnit
- Docker
- GitHub Actions
- OpenTelemetry-ready evidence model
- Static dashboard for the first end-to-end demo

## Run

```bash
docker compose up --build
```

Open http://localhost:8080 and click **Run investigation**.

Or run the API directly:

```bash
dotnet run --project src/IncidentAgent.Api/IncidentAgent.Api.csproj
```

## API

```http
POST /api/incidents/investigate
Content-Type: application/json

{
  "title": "Checkout failures",
  "description": "Customers report checkout requests timing out.",
  "serviceName": "payment-service",
  "startedAtUtc": "2026-09-19T16:00:00Z"
}
```

The response contains correlated evidence, ranked hypotheses, confidence, and recommended actions.

## Architecture

See [docs/architecture.md](docs/architecture.md).

## Roadmap

- Real GitHub deployment/commit adapter
- OpenTelemetry/Tempo trace adapter
- Loki/OpenSearch log adapter
- Prometheus metrics adapter
- Kubernetes deployment-event adapter
- LLM reasoning adapter with structured output and evidence citations
- Incident timeline UI
- Slack/Teams incident intake
- Jira incident creation
- Evaluation suite for root-cause accuracy and hallucination resistance
