/**
 * WARNING: Storing parameter history in localStorage is a client-side workaround (technical debt).
 * It grows unbounded, doesn't sync across devices/browsers, and is prone to quota limits.
 * TODO: Replace with backend API calls once the server-side audit logs / revision history is ready.
 */
import { getCurrentUserEmail } from "../../../entities/auth/lib/jwt";

export interface ParamRevision {
  timestamp: string;
  project: string;
  environment: string;
  key: string;
  value: string;
  actor: string;
  displayName?: string;
  description?: string;
}

export function addParamRevision(
  project: string,
  environment: string,
  key: string,
  value: string,
  displayName?: string,
  description?: string,
) {
  try {
    const raw = localStorage.getItem("nonaconfig_param_history") || "[]";
    const history: ParamRevision[] = JSON.parse(raw);
    const newRevision: ParamRevision = {
      timestamp: new Date().toISOString(),
      project,
      environment,
      key,
      value,
      actor: getCurrentUserEmail(),
      displayName,
      description,
    };
    history.push(newRevision);
    localStorage.setItem("nonaconfig_param_history", JSON.stringify(history));
  } catch (e) {
    console.error("Failed to write to param history", e);
  }
}

export function addAuditLog(
  _project: string,
  _environment: string,
  _key: string,
  _actionCode: string,
  _oldValue?: string,
  _newValue?: string,
  _contentType?: string,
  _scope?: string,
  _displayName?: string,
  _description?: string,
  _oldDisplayName?: string,
  _oldDescription?: string,
) {
  // Audit logging is handled by the backend — no localStorage write needed
}

export function getParamHistory(
  proj: string,
  env: string,
  key: string,
): ParamRevision[] {
  try {
    const raw = localStorage.getItem("nonaconfig_param_history") || "[]";
    const history: ParamRevision[] = JSON.parse(raw);
    const filtered = history.filter(
      (h) => h.project === proj && h.environment === env && h.key === key,
    );
    return filtered.sort(
      (a, b) =>
        new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime(),
    );
  } catch {
    return [];
  }
}
