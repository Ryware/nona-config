import type { AuditEntry } from "./types";

export type AuditExportFormat = "csv" | "json";

export function serializeAuditLogs(
  entries: readonly AuditEntry[],
  format: AuditExportFormat
): { content: string; mimeType: string } {
  if (format === "json") {
    return {
      content: JSON.stringify(
        entries.map(entry => ({
          id: entry.id,
          time: entry.time.toISOString(),
          actor: entry.actor,
          actionKind: entry.actionKind,
          action: entry.action,
          target: entry.target,
          environment: entry.env,
          sysId: entry.sysId,
          project: entry.project
        })),
        null,
        2
      ),
      mimeType: "application/json"
    };
  }

  const header = "Time,Actor,ActionKind,Action,Target,Environment,SysID,Project";
  const rows = entries.map(entry =>
    [
      entry.time.toISOString(),
      entry.actor,
      entry.actionKind,
      entry.action,
      entry.target,
      entry.env,
      entry.sysId,
      entry.project ?? ""
    ]
      .map(toCsvCell)
      .join(",")
  );

  return {
    content: [header, ...rows].join("\n"),
    mimeType: "text/csv"
  };
}

function toCsvCell(value: string): string {
  return `"${value.replaceAll('"', '""')}"`;
}
