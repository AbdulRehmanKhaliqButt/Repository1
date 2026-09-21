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

The project keeps deterministic reasoning as the baseline while allowing optional OpenAI-compatible or Anthropic reasoning over normalized evidence. GitHub, Kubernetes, Loki, Tempo, and Prometheus can all supply real evidence without changing the domain model.

## Tech

- .NET 10 / ASP.NET Core
- C#
- xUnit
- Docker
- GitHub Actions
- OpenTelemetry-ready evidence model
- React 19 + TypeScript investigation workspace

## Run

```bash
docker compose up --build
```

Open http://localhost:8080 and click **Run investigation**.

For local UI development, run the API and Vite separately:

```bash
dotnet run --project src/IncidentAgent.Api/IncidentAgent.Api.csproj --urls http://localhost:5000
```

Then in another terminal:

```bash
cd src/IncidentAgent.Web
npm install
npm run dev
```

Open http://localhost:5173. Vite proxies `/api` and `/health` to the ASP.NET API. The Docker image performs the React production build automatically and serves it from ASP.NET on port 8080.

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

## Use real Kubernetes deployment evidence

Enable the Kubernetes provider to correlate rollout state with commits, logs, traces, and metrics:

```bash
KUBERNETES_EVIDENCE_ENABLED=true
KUBERNETES_BASE_URL=https://kubernetes.default.svc/
KUBERNETES_NAMESPACE=production
KUBERNETES_BEARER_TOKEN=
KUBERNETES_TOKEN_FILE=/var/run/secrets/kubernetes.io/serviceaccount/token
```

In-cluster deployments can use the mounted service-account token file. For local development, point `KUBERNETES_BASE_URL` at your API server and provide a bearer token from your kubeconfig/credential flow. The adapter inspects Deployments, ReplicaSets, and Pods, then emits normalized rollout evidence including image/version, generation, desired/updated/available replicas, failed Pods, and rollout health.

## Optional LLM reasoning

The deterministic reasoner is always available. Enable the LLM layer only when you want model-assisted synthesis:

```bash
LLM_REASONING_ENABLED=true
LLM_PROVIDER=openai
LLM_MODEL=gpt-5
LLM_API_KEY=your-key
```

Set `LLM_PROVIDER=anthropic` and an Anthropic model name to use the Anthropic Messages API instead. Base URLs are configurable for gateways or compatible endpoints.

The LLM receives a serialized evidence envelope and is explicitly instructed to treat logs, commits, traces, and all other evidence as untrusted data. Every summary, hypothesis, and recommended action must cite existing evidence IDs. Unknown or missing citations, malformed JSON, provider errors, or timeouts cause an automatic fallback to deterministic reasoning.

The API response includes `reasoningTelemetry` with mode, provider, model, input/output tokens, latency, configurable estimated cost, and fallback reason when applicable. API keys and raw authorization headers are never included.

## PostgreSQL investigation history

Docker Compose includes PostgreSQL and enables persistence by default. Each run stores the request, normalized evidence, hypotheses, actions, reasoning telemetry, and per-source status/duration.

Useful APIs:

```http
GET /api/investigations?serviceName=payment-service&page=1&pageSize=20
GET /api/investigations/{investigationId}
GET /api/investigations/compare?leftId={olderId}&rightId={newerId}
```

For direct development outside Docker, enable persistence and supply `Persistence:ConnectionString`. The application applies EF Core migrations on startup when persistence is enabled.

## Investigation workspace

The React/TypeScript workspace is the primary UI. It includes:

- incident intake with service and start time
- persisted investigation history and reopen flow
- evidence-source health, duration and failure indicators
- reasoning mode/model/token/cost telemetry
- ranked root-cause hypotheses with confidence
- clickable evidence citations that jump to the supporting timeline event
- chronological evidence timeline
- evidence filtering by source and type
- expandable evidence attributes
- recommended diagnostic/remediation actions
- responsive loading and error states

## Evaluation benchmark

The repository includes a repeatable known-incident benchmark under `benchmarks/known-incidents.json`. It runs through the same `IIncidentReasoner` abstraction used by the application and reports:

- Top-1 root-cause accuracy
- Top-3 root-cause accuracy
- evidence citation precision and recall
- unsupported-citation rate
- reasoning latency
- estimated model cost

Run it locally with:

```bash
dotnet run --project src/IncidentAgent.Evaluation/IncidentAgent.Evaluation.csproj -- \
  --dataset benchmarks/known-incidents.json \
  --output artifacts/evaluation-results.json \
  --fail-on-regression
```

CI uses regression gates of 80% Top-1 accuracy, 90% Top-3 accuracy, 95% citation precision, 80% citation recall, and no more than 5% unsupported citations. Thresholds can be overridden from the command line for experiments.

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

- Slack/Teams incident intake
- Jira incident creation
