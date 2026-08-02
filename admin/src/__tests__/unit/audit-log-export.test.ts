import { describe, expect, it } from "vitest";
import { serializeAuditLogs } from "../../pages/audit-logs/export";
import type { AuditEntry } from "../../pages/audit-logs/types";

const entry: AuditEntry = {
  id: "12",
  time: new Date("2026-07-29T12:00:00Z"),
  actor: "audit.user@example.test",
  actorIconColor: "bg-primary",
  actorTextColor: "text-primary",
  actorIsSystem: false,
  actionKind: "create",
  action: 'Published "Config" Release',
  actionStyle: "",
  target: "1.3.1",
  targetMono: false,
  targetDeleted: false,
  env: "Production",
  envStyle: "",
  sysId: "12",
  project: "sample-project"
};

describe("serializeAuditLogs", () => {
  it("places ActionKind before Action in CSV exports", () => {
    const result = serializeAuditLogs([entry], "csv");

    expect(result.mimeType).toBe("text/csv");
    expect(result.content).toContain(
      "Time,Actor,ActionKind,Action,Target,Environment,SysID,Project"
    );
    expect(result.content).toContain(
      '"audit.user@example.test","create","Published ""Config"" Release","1.3.1"'
    );
  });

  it("places actionKind before action in JSON exports", () => {
    const result = serializeAuditLogs([entry], "json");
    const parsed = JSON.parse(result.content);

    expect(parsed[0].actionKind).toBe("create");
    expect(parsed[0].action).toBe('Published "Config" Release');
    expect(result.content.indexOf('"actionKind"')).toBeLessThan(result.content.indexOf('"action"'));
  });
});
