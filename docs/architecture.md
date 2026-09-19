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

The current providers are deterministic demo adapters. They make the repository runnable without credentials and provide a stable scenario for automated tests.

## Implemented provider behavior

- **GitHub commits** can now come from the real GitHub REST API when `Evidence:GitHub:Enabled=true`.
- The demo Git commit provider remains the default so the project is runnable without credentials.
- Each evidence provider is isolated: a failed provider is converted into `SourceError` evidence instead of failing the full investigation.
- A specific root-cause claim requires convergence from at least three matching evidence signals. With weaker evidence, the engine returns a partial-correlation result and avoids automated remediation.

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
5. **Observability of the investigator itself** — future phases will emit OpenTelemetry traces, token cost, latency, source failures, and confidence calibration.
6. **Tenant boundaries** — credentials and evidence must be isolated per customer in a real deployment.

## Next engineering slices

1. Replace demo Git/deployment providers with GitHub + Kubernetes adapters.
2. Add real OpenTelemetry/Tempo and Loki providers.
3. Add Prometheus query adapter and configurable incident windows.
4. Add structured LLM reasoner that cites evidence IDs.
5. Persist investigations in PostgreSQL.
6. Add evaluation cases for known incidents and measure top-1/top-3 root-cause accuracy.
