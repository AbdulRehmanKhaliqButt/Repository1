export type EvidenceType =
  | "Log"
  | "Trace"
  | "Metric"
  | "Deployment"
  | "Commit"
  | "SourceError";

export interface IncidentRequest {
  title: string;
  description: string;
  serviceName: string | null;
  startedAtUtc: string;
}

export interface IncidentEvidence {
  id: string;
  type: EvidenceType;
  source: string;
  timestampUtc: string;
  service: string;
  summary: string;
  details: string;
  attributes: Record<string, string>;
}

export interface RootCauseHypothesis {
  rank: number;
  title: string;
  explanation: string;
  confidence: number;
  evidenceIds: string[];
}

export interface ReasoningTelemetry {
  mode: string;
  provider: string;
  model: string;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  estimatedCostUsd: number;
  fallbackReason?: string | null;
}

export interface SourceExecutionTelemetry {
  source: string;
  status: string;
  durationMs: number;
  evidenceCount: number;
  errorType?: string | null;
}

export interface IncidentInvestigation {
  investigationId: string;
  generatedAtUtc: string;
  summary: string;
  evidence: IncidentEvidence[];
  hypotheses: RootCauseHypothesis[];
  recommendedActions: string[];
  reasoningTelemetry?: ReasoningTelemetry | null;
  sourceExecutions: SourceExecutionTelemetry[];
}

export interface StoredInvestigation {
  request: IncidentRequest;
  investigation: IncidentInvestigation;
}

export interface InvestigationHistoryItem {
  investigationId: string;
  generatedAtUtc: string;
  title: string;
  serviceName?: string | null;
  startedAtUtc: string;
  summary: string;
  reasoningMode: string;
  primaryConfidence?: number | null;
  evidenceCount: number;
}

export interface InvestigationHistoryPage {
  items: InvestigationHistoryItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}
