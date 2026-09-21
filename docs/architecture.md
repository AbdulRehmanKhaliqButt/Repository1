# Architecture

## Goal

The system should help an engineer answer:

> What changed, what is failing, what evidence supports the hypothesis, and what should we do next?

It must remain useful even when the LLM is unavailable, and every hypothesis must be traceable back to concrete evidence.

## Current vertical slice

```text
Browser dashboard
      |
      v
ASP.NET Core API
      |
      v
IncidentInvestigator
      |
      +--> Deployment source
      +--> Git commit source
      +--> Metrics source
      +--> Log source
      +--> Trace source
      |
      v
Evidence correlation + ranked hypotheses
```

Each evidence category has a deterministic demo fallback. GitHub, Kubernetes, Loki, Tempo, and Prometheus also have real HTTP adapters, so the same investigator can run against production-shaped telemetry without changing the domain model.

## Implemented provider behavior

- **GitHub commits** can come from the GitHub REST API.
- **Kubernetes deployments** inspect Deployments, ReplicaSets, and Pods around the incident window and normalize rollout image/version, generation, replica health, and failed Pods.
- **Loki logs** use `/loki/api/v1/query_range` over a configurable incident window.
- **Tempo traces** use `/api/search` with a configurable TraceQL template and normalize span attributes such as `db.system`.
- **Prometheus metrics** use `/api/v1/query_range`, summarize series, and preserve metric labels as evidence attributes.
- Every real provider can be enabled independently; disabled providers fall back to deterministic demo evidence.
- Each evidence provider is isolated: a failed provider is converted into `SourceError` evidence instead of failing the full investigation.
- A specific deterministic root-cause claim requires convergence from at least three matching evidence signals. With weaker evidence, the engine returns a partial-correlation result and avoids automated remediation.
- **LLM reasoning** is optional and provider-abstracted for OpenAI-compatible and Anthropic APIs. Model output is parsed as structured JSON, every summary/hypothesis/action citation is checked against real evidence IDs, and any failure falls back to deterministic reasoning.
- LLM telemetry records provider/model, token usage, latency, configurable estimated cost, and fallback reason without returning credentials.
- **PostgreSQL persistence** stores complete investigations, evidence, hypotheses, actions, reasoning telemetry, and per-source execution telemetry. History can be filtered by service/time and prior runs can be reopened or compared.

## Target architecture

```text
Slack / Teams / Web UI
          |
          v
    Incident API
          |
          v
 Workflow Orchestrator
          |
     Evidence Bus
   /   /   |   \   \
Git  K8s Logs Traces Metrics
Hub      Loki Tempo Prometheus
   \      |      /
    Normalized evidence
          |
          v
  Correlation + scoring
          |
     +----+----+
     |         |
Deterministic  LLM reasoner
rules          (structured output)
     |         |
     +----+----+
          |
          v
 Evidence-backed hypotheses
          |
          v
 Jira / Slack / remediation workflow
```

## Design principles

1. **Evidence first** — the model never receives undocumented facts.
2. **Provider abstraction** — GitHub, Grafana, Kubernetes, and cloud sources implement the same evidence-source contract.
3. **Deterministic baseline** — the platform remains testable without an LLM.
4. **Human approval for remediation** — investigation can be automated; production changes require explicit approval.
5. **Observability of the investigator itself** — reasoning mode, token cost, latency, and fallback reasons are surfaced now; broader OpenTelemetry instrumentation and confidence calibration can extend this.
6. **Tenant boundaries** — credentials and evidence must be isolated per customer in a real deployment.

## Next engineering slices

1. Build the React incident timeline and evidence workspace.
2. Add evaluation cases for known incidents and measure top-1/top-3 root-cause accuracy.
