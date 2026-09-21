import type {
  IncidentInvestigation,
  IncidentRequest,
  InvestigationHistoryPage,
  StoredInvestigation
} from "./types";

async function requestJson<T>(
  input: RequestInfo | URL,
  init?: RequestInit
): Promise<T> {
  const response = await fetch(input, init);

  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const body = (await response.json()) as { error?: string };
      if (body.error) message = body.error;
    } catch {
      // Preserve the status-based fallback message.
    }

    throw new Error(message);
  }

  return (await response.json()) as T;
}

export function investigate(
  request: IncidentRequest
): Promise<IncidentInvestigation> {
  return requestJson<IncidentInvestigation>("/api/incidents/investigate", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
}

export function loadHistory(
  serviceName = ""
): Promise<InvestigationHistoryPage> {
  const params = new URLSearchParams({
    page: "1",
    pageSize: "30"
  });

  if (serviceName.trim()) {
    params.set("serviceName", serviceName.trim());
  }

  return requestJson<InvestigationHistoryPage>(
    `/api/investigations?${params.toString()}`
  );
}

export function loadInvestigation(
  id: string
): Promise<StoredInvestigation> {
  return requestJson<StoredInvestigation>(
    `/api/investigations/${encodeURIComponent(id)}`
  );
}
