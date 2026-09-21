import {
  FormEvent,
  useEffect,
  useMemo,
  useState
} from "react";
import {
  investigate,
  loadHistory,
  loadInvestigation
} from "./api";
import type {
  EvidenceType,
  IncidentInvestigation,
  IncidentRequest,
  InvestigationHistoryItem
} from "./types";

type FormState = {
  title: string;
  description: string;
  serviceName: string;
  startedAtLocal: string;
};

function toLocalInputValue(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset)
    .toISOString()
    .slice(0, 16);
}

const initialForm: FormState = {
  title: "Checkout failures",
  description:
    "Customers report checkout requests timing out shortly after a production deployment.",
  serviceName: "payment-service",
  startedAtLocal: toLocalInputValue(
    new Date(Date.now() - 10 * 60_000)
  )
};

function formatDate(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: "medium",
    timeStyle: "medium"
  }).format(new Date(value));
}

function percentage(value: number): string {
  return `${Math.round(value * 100)}%`;
}

function evidenceDomId(id: string): string {
  return `evidence-${encodeURIComponent(id)}`;
}

export default function App() {
  const [form, setForm] = useState<FormState>(initialForm);
  const [current, setCurrent] =
    useState<IncidentInvestigation | null>(null);
  const [history, setHistory] =
    useState<InvestigationHistoryItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [error, setError] = useState("");
  const [historyError, setHistoryError] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [sourceFilter, setSourceFilter] = useState("all");
  const [highlightedEvidence, setHighlightedEvidence] =
    useState<string | null>(null);

  async function refreshHistory(serviceName = "") {
    setHistoryLoading(true);
    setHistoryError("");

    try {
      const page = await loadHistory(serviceName);
      setHistory(page.items);
    } catch (requestError) {
      setHistoryError(
        requestError instanceof Error
          ? requestError.message
          : "Unable to load investigation history."
      );
    } finally {
      setHistoryLoading(false);
    }
  }

  useEffect(() => {
    void refreshHistory();
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError("");

    const request: IncidentRequest = {
      title: form.title.trim(),
      description: form.description.trim(),
      serviceName: form.serviceName.trim() || null,
      startedAtUtc: new Date(form.startedAtLocal).toISOString()
    };

    try {
      const result = await investigate(request);
      setCurrent(result);
      setTypeFilter("all");
      setSourceFilter("all");
      setHighlightedEvidence(null);
      await refreshHistory(form.serviceName);
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Investigation failed."
      );
    } finally {
      setLoading(false);
    }
  }

  async function reopen(id: string) {
    setLoading(true);
    setError("");

    try {
      const stored = await loadInvestigation(id);
      setCurrent(stored.investigation);
      setForm({
        title: stored.request.title,
        description: stored.request.description,
        serviceName: stored.request.serviceName ?? "",
        startedAtLocal: toLocalInputValue(
          new Date(stored.request.startedAtUtc)
        )
      });
      setTypeFilter("all");
      setSourceFilter("all");
    } catch (requestError) {
      setError(
        requestError instanceof Error
          ? requestError.message
          : "Unable to reopen investigation."
      );
    } finally {
      setLoading(false);
    }
  }

  function showEvidence(id: string) {
    setTypeFilter("all");
    setSourceFilter("all");
    setHighlightedEvidence(id);

    window.setTimeout(() => {
      document
        .getElementById(evidenceDomId(id))
        ?.scrollIntoView({
          behavior: "smooth",
          block: "center"
        });
    }, 30);

    window.setTimeout(() => {
      setHighlightedEvidence(null);
    }, 2200);
  }

  const evidenceTypes = useMemo(
    () =>
      Array.from(
        new Set(current?.evidence.map((item) => item.type) ?? [])
      ).sort(),
    [current]
  );

  const evidenceSources = useMemo(
    () =>
      Array.from(
        new Set(current?.evidence.map((item) => item.source) ?? [])
      ).sort(),
    [current]
  );

  const filteredEvidence = useMemo(() => {
    if (!current) return [];

    return [...current.evidence]
      .filter(
        (item) =>
          (typeFilter === "all" || item.type === typeFilter) &&
          (sourceFilter === "all" ||
            item.source === sourceFilter)
      )
      .sort(
        (left, right) =>
          new Date(left.timestampUtc).getTime() -
          new Date(right.timestampUtc).getTime()
      );
  }, [current, typeFilter, sourceFilter]);

  const sourceFailures =
    current?.sourceExecutions.filter(
      (item) => item.status !== "success"
    ) ?? [];

  return (
    <div className="app-shell">
      <aside className="history-panel">
        <div className="brand">
          <div className="brand-mark">IA</div>
          <div>
            <strong>Incident Agent</strong>
            <span>Evidence-grounded RCA</span>
          </div>
        </div>

        <div className="history-heading">
          <div>
            <span className="eyebrow">Previous runs</span>
            <h2>Investigation history</h2>
          </div>
          <button
            className="icon-button"
            onClick={() => void refreshHistory()}
            disabled={historyLoading}
            title="Refresh history"
          >
            ↻
          </button>
        </div>

        {historyError && (
          <div className="compact-error">{historyError}</div>
        )}

        <div className="history-list">
          {historyLoading && history.length === 0 && (
            <div className="history-empty">Loading history…</div>
          )}

          {!historyLoading && history.length === 0 && (
            <div className="history-empty">
              No stored investigations yet.
            </div>
          )}

          {history.map((item) => (
            <button
              key={item.investigationId}
              className={
                current?.investigationId === item.investigationId
                  ? "history-card active"
                  : "history-card"
              }
              onClick={() => void reopen(item.investigationId)}
            >
              <div className="history-card-top">
                <strong>{item.title}</strong>
                {item.primaryConfidence != null && (
                  <span className="confidence-small">
                    {percentage(item.primaryConfidence)}
                  </span>
                )}
              </div>
              <span>{item.serviceName || "Unknown service"}</span>
              <small>
                {formatDate(item.generatedAtUtc)} ·{" "}
                {item.evidenceCount} evidence
              </small>
            </button>
          ))}
        </div>
      </aside>

      <main className="workspace">
        <header className="workspace-header">
          <div>
            <span className="eyebrow">Production diagnostics</span>
            <h1>Incident Investigation Workspace</h1>
            <p>
              Correlate code, deployments, logs, traces and
              metrics. Every hypothesis stays linked to its
              supporting evidence.
            </p>
          </div>
          <div className="live-badge">
            <span className="live-dot" />
            Evidence first
          </div>
        </header>

        <section className="panel intake-panel">
          <div className="section-title">
            <div>
              <span className="eyebrow">Incident intake</span>
              <h2>Start an investigation</h2>
            </div>
          </div>

          <form onSubmit={submit} className="incident-form">
            <label>
              <span>Title</span>
              <input
                value={form.title}
                onChange={(event) =>
                  setForm({
                    ...form,
                    title: event.target.value
                  })
                }
                required
              />
            </label>

            <label>
              <span>Service</span>
              <input
                value={form.serviceName}
                onChange={(event) =>
                  setForm({
                    ...form,
                    serviceName: event.target.value
                  })
                }
                placeholder="payment-service"
              />
            </label>

            <label className="full-field">
              <span>Description</span>
              <textarea
                value={form.description}
                onChange={(event) =>
                  setForm({
                    ...form,
                    description: event.target.value
                  })
                }
                rows={3}
                required
              />
            </label>

            <label>
              <span>Incident started</span>
              <input
                type="datetime-local"
                value={form.startedAtLocal}
                onChange={(event) =>
                  setForm({
                    ...form,
                    startedAtLocal: event.target.value
                  })
                }
                required
              />
            </label>

            <div className="submit-cell">
              <button
                className="primary-button"
                type="submit"
                disabled={loading}
              >
                {loading ? "Investigating…" : "Run investigation"}
              </button>
            </div>
          </form>

          {error && <div className="error-banner">{error}</div>}
        </section>

        {!current && (
          <section className="empty-workspace">
            <div className="empty-icon">⌁</div>
            <h2>No investigation selected</h2>
            <p>
              Run an incident above or reopen a previous run from
              the history panel.
            </p>
          </section>
        )}

        {current && (
          <>
            <section className="summary-grid">
              <article className="panel summary-card">
                <span className="eyebrow">Investigation summary</span>
                <h2>{current.summary}</h2>
                <div className="summary-meta">
                  <span>
                    ID <code>{current.investigationId.slice(0, 10)}</code>
                  </span>
                  <span>{formatDate(current.generatedAtUtc)}</span>
                  <span>{current.evidence.length} evidence items</span>
                </div>
              </article>

              <article className="panel telemetry-card">
                <span className="eyebrow">Reasoning</span>
                <div className="telemetry-main">
                  <strong>
                    {current.reasoningTelemetry?.mode ?? "unknown"}
                  </strong>
                  <span>
                    {current.reasoningTelemetry?.provider ?? "—"} /{" "}
                    {current.reasoningTelemetry?.model ?? "—"}
                  </span>
                </div>
                <div className="telemetry-stats">
                  <span>
                    <strong>
                      {current.reasoningTelemetry?.latencyMs ?? 0} ms
                    </strong>
                    latency
                  </span>
                  <span>
                    <strong>
                      {(current.reasoningTelemetry?.inputTokens ?? 0) +
                        (current.reasoningTelemetry?.outputTokens ?? 0)}
                    </strong>
                    tokens
                  </span>
                  <span>
                    <strong>
                      $
                      {(
                        current.reasoningTelemetry
                          ?.estimatedCostUsd ?? 0
                      ).toFixed(5)}
                    </strong>
                    est. cost
                  </span>
                </div>
                {current.reasoningTelemetry?.fallbackReason && (
                  <div className="fallback-note">
                    Fallback:{" "}
                    {current.reasoningTelemetry.fallbackReason}
                  </div>
                )}
              </article>
            </section>

            <section className="panel">
              <div className="section-title">
                <div>
                  <span className="eyebrow">Evidence health</span>
                  <h2>Source execution</h2>
                </div>
                {sourceFailures.length > 0 && (
                  <span className="warning-badge">
                    {sourceFailures.length} source failure
                    {sourceFailures.length === 1 ? "" : "s"}
                  </span>
                )}
              </div>

              <div className="source-grid">
                {current.sourceExecutions.map((source) => (
                  <div
                    className={
                      source.status === "success"
                        ? "source-card success"
                        : "source-card failure"
                    }
                    key={source.source}
                  >
                    <div>
                      <span className="status-dot" />
                      <strong>{source.source}</strong>
                    </div>
                    <span>
                      {source.durationMs} ms · {source.evidenceCount} items
                    </span>
                    {source.errorType && (
                      <small>{source.errorType}</small>
                    )}
                  </div>
                ))}
              </div>
            </section>

            <section className="hypothesis-grid">
              <div className="panel">
                <div className="section-title">
                  <div>
                    <span className="eyebrow">Root cause analysis</span>
                    <h2>Ranked hypotheses</h2>
                  </div>
                </div>

                <div className="hypothesis-list">
                  {current.hypotheses.map((hypothesis) => (
                    <article
                      className="hypothesis"
                      key={hypothesis.rank}
                    >
                      <div className="hypothesis-rank">
                        #{hypothesis.rank}
                      </div>
                      <div className="hypothesis-body">
                        <div className="hypothesis-heading">
                          <h3>{hypothesis.title}</h3>
                          <span className="confidence">
                            {percentage(hypothesis.confidence)}
                          </span>
                        </div>
                        <p>{hypothesis.explanation}</p>
                        <div className="citation-row">
                          {hypothesis.evidenceIds.map((id) => (
                            <button
                              key={id}
                              onClick={() => showEvidence(id)}
                            >
                              ↳ {id}
                            </button>
                          ))}
                        </div>
                      </div>
                    </article>
                  ))}
                </div>
              </div>

              <div className="panel action-panel">
                <div className="section-title">
                  <div>
                    <span className="eyebrow">Next steps</span>
                    <h2>Recommended actions</h2>
                  </div>
                </div>
                <ol className="action-list">
                  {current.recommendedActions.map((action) => (
                    <li key={action}>{action}</li>
                  ))}
                </ol>
              </div>
            </section>

            <section className="panel evidence-panel">
              <div className="section-title evidence-title">
                <div>
                  <span className="eyebrow">Chronology</span>
                  <h2>Evidence timeline</h2>
                </div>

                <div className="filters">
                  <label>
                    <span>Type</span>
                    <select
                      value={typeFilter}
                      onChange={(event) =>
                        setTypeFilter(event.target.value)
                      }
                    >
                      <option value="all">All types</option>
                      {evidenceTypes.map((type) => (
                        <option value={type} key={type}>
                          {type}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label>
                    <span>Source</span>
                    <select
                      value={sourceFilter}
                      onChange={(event) =>
                        setSourceFilter(event.target.value)
                      }
                    >
                      <option value="all">All sources</option>
                      {evidenceSources.map((source) => (
                        <option value={source} key={source}>
                          {source}
                        </option>
                      ))}
                    </select>
                  </label>
                </div>
              </div>

              <div className="timeline">
                {filteredEvidence.map((item) => (
                  <article
                    id={evidenceDomId(item.id)}
                    key={item.id}
                    className={
                      highlightedEvidence === item.id
                        ? "timeline-item highlighted"
                        : "timeline-item"
                    }
                  >
                    <div
                      className={`timeline-marker type-${item.type.toLowerCase()}`}
                    />
                    <div className="timeline-time">
                      {formatDate(item.timestampUtc)}
                    </div>
                    <div className="timeline-content">
                      <div className="evidence-heading">
                        <div>
                          <span
                            className={`type-badge type-${item.type.toLowerCase()}`}
                          >
                            {item.type}
                          </span>
                          <code>{item.source}</code>
                        </div>
                        <code className="evidence-id">{item.id}</code>
                      </div>
                      <h3>{item.summary}</h3>
                      <p>{item.details}</p>

                      {Object.keys(item.attributes).length > 0 && (
                        <details>
                          <summary>
                            {Object.keys(item.attributes).length} attributes
                          </summary>
                          <dl className="attributes">
                            {Object.entries(item.attributes).map(
                              ([key, value]) => (
                                <div key={key}>
                                  <dt>{key}</dt>
                                  <dd>{value}</dd>
                                </div>
                              )
                            )}
                          </dl>
                        </details>
                      )}
                    </div>
                  </article>
                ))}

                {filteredEvidence.length === 0 && (
                  <div className="timeline-empty">
                    No evidence matches the selected filters.
                  </div>
                )}
              </div>
            </section>
          </>
        )}
      </main>
    </div>
  );
}
