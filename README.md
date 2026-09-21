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

## Use real GitHub commit evidence

The app runs with demo evidence by default. To use a real repository for commit evidence, copy `.env.example` to `.env` and set:

```bash
GITHUB_EVIDENCE_ENABLED=true
GITHUB_EVIDENCE_OWNER=your-org
GITHUB_EVIDENCE_REPOSITORY=payment-service
GITHUB_TOKEN=your-token
```

Then run:

```bash
docker compose --env-file .env up --build
```

The token is required for private repositories and should never be committed. The adapter queries commits around the incident window and normalizes them into the same evidence model used by logs, traces, metrics, and deployments.

## Use real observability evidence

Loki, Tempo, and Prometheus are individually configurable. Demo providers remain the default so the app still runs without external infrastructure.

Example `.env` values:

```bash
LOKI_EVIDENCE_ENABLED=true
LOKI_BASE_URL=http://localhost:3100/

TEMPO_EVIDENCE_ENABLED=true
TEMPO_BASE_URL=http://localhost:3200/

PROMETHEUS_EVIDENCE_ENABLED=true
PROMETHEUS_BASE_URL=http://localhost:9090/
```

Run with:

```bash
docker compose --env-file .env up --build
```

The default queries assume a `service_name` label in Loki/Prometheus and `resource.service.name` in Tempo. Override `Evidence:Loki:QueryTemplate`, `Evidence:Tempo:TraceQlTemplate`, or `Evidence:Prometheus:QueryTemplate` for your telemetry conventions.

Each provider has independent `TimeoutSeconds` and `RetryCount` settings. Transient timeouts, HTTP 429s, and 5xx responses are retried; a provider that still fails is isolated as `SourceError` evidence rather than failing the complete investigation.

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
- Kubernetes deployment-event adapter
- LLM reasoning adapter with structured output and evidence citations
- Incident timeline UI
- Slack/Teams incident intake
- Jira incident creation
- Evaluation suite for root-cause accuracy and hallucination resistance
