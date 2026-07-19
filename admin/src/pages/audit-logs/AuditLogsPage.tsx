import { Title } from "@solidjs/meta";
import { useQuery } from "@tanstack/solid-query";
import { createMemo, createSignal, Show } from "solid-js";
import { auditLogService } from "../../entities/audit-log/api/audit-log.service";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import type { AuditLog } from "../../types";

import { AuditLogsFilters } from "./components/AuditLogsFilters";
import { AuditLogsHeader } from "./components/AuditLogsHeader";
import { AuditLogsTable } from "./components/AuditLogsTable";
import type { AuditEntry } from "./types";
import { ACTION_STYLE, actorStyle, ENV_STYLE } from "./utils";

const PAGE_SIZE = 25;

/** Maps a backend action string to a display label present in ACTION_STYLE. */
function resolveActionLabel(action: string): string {
  if (ACTION_STYLE[action]) return action;
  const normalized = action.toLowerCase().replace(/[\s-]+/g, "_");
  const MAP: Record<string, string> = {
    create_project: "Created Project",
    created_project: "Created Project",
    update_project: "Updated Project",
    updated_project: "Updated Project",
    invite_user: "Invited User",
    invited_user: "Invited User",
    delete_key: "Deleted Key",
    deleted_key: "Deleted Key",
    auto_scaling: "Auto-Scaling",
    create_config_entry: "Created Parameter",
    created_config_entry: "Created Parameter",
    create_entry: "Created Parameter",
    created_entry: "Created Parameter",
    update_config_entry: "Updated Parameter",
    updated_config_entry: "Updated Parameter",
    update_entry: "Updated Parameter",
    updated_entry: "Updated Parameter",
    delete_config_entry: "Deleted Parameter",
    deleted_config_entry: "Deleted Parameter",
    delete_entry: "Deleted Parameter",
    deleted_entry: "Deleted Parameter"
  };
  return MAP[normalized] || action;
}

function mapAuditLog(log: AuditLog): AuditEntry {
  const action = resolveActionLabel(log.action);
  const actorName = log.actorIsSystem ? "System" : log.actor;
  const s = log.actorIsSystem
    ? { bg: "bg-surface-bright", text: "text-outline" }
    : actorStyle(actorName);
  const envRaw = log.environment ?? "Global Scope";
  const env = envRaw.charAt(0).toUpperCase() + envRaw.slice(1);
  return {
    id: String(log.id),
    time: new Date(log.createdAt),
    actor: actorName,
    actorIconColor: s.bg,
    actorTextColor: s.text,
    actorIsSystem: log.actorIsSystem,
    action,
    actionStyle: ACTION_STYLE[action] ?? ACTION_STYLE["Updated Parameter"],
    target: log.target,
    targetMono: false,
    targetDeleted: false,
    env,
    envStyle: ENV_STYLE[env] ?? ENV_STYLE["Global Scope"],
    sysId: String(log.id).replace(/-/g, "").slice(0, 8).toUpperCase(),
    project: log.project ?? undefined
  };
}

export default function AuditLogsPage() {
  const [search, setSearch] = createSignal("");
  const [filterAction, setFilterAction] = createSignal("all");
  const [filterEnv, setFilterEnv] = createSignal("all");
  const [dateFrom, setDateFrom] = createSignal("");
  const [dateTo, setDateTo] = createSignal("");
  const [page, setPage] = createSignal(0);

  const auditQuery = useQuery(() => ({
    queryKey: ["audit-logs"],
    queryFn: () => auditLogService.getAll()
  }));

  const allEntries = createMemo<AuditEntry[]>(() => {
    const data = auditQuery.status === "success" ? (auditQuery.data ?? []) : [];
    return data.map(mapAuditLog).sort((a, b) => b.time.getTime() - a.time.getTime());
  });

  const filtered = createMemo(() => {
    let entries = allEntries();
    if (filterAction() !== "all") entries = entries.filter(e => e.action === filterAction());
    if (filterEnv() !== "all") entries = entries.filter(e => e.env === filterEnv());

    if (dateFrom()) {
      const from = new Date(dateFrom()).getTime();
      if (!isNaN(from)) entries = entries.filter(e => e.time.getTime() >= from);
    }
    if (dateTo()) {
      const to = new Date(dateTo()).getTime() + 86_400_000;
      if (!isNaN(to)) entries = entries.filter(e => e.time.getTime() <= to);
    }
    if (search().trim()) {
      const q = search().toLowerCase();
      entries = entries.filter(
        e =>
          e.actor.toLowerCase().includes(q) ||
          e.target.toLowerCase().includes(q) ||
          (e.project ?? "").toLowerCase().includes(q)
      );
    }
    return entries;
  });

  const totalPages = createMemo(() => Math.max(1, Math.ceil(filtered().length / PAGE_SIZE)));
  const pageEntries = createMemo(() =>
    filtered().slice(page() * PAGE_SIZE, (page() + 1) * PAGE_SIZE)
  );
  const uniqueActions = createMemo(() => [...new Set(allEntries().map(e => e.action))]);
  const uniqueEnvs = createMemo(() => [...new Set(allEntries().map(e => e.env))]);
  const isLoading = () => auditQuery.isLoading;

  const changePage = (n: number) => {
    if (n >= 0 && n < totalPages()) setPage(n);
  };

  const clearFilters = () => {
    setFilterAction("all");
    setFilterEnv("all");
    setSearch("");
    setDateFrom("");
    setDateTo("");
    setPage(0);
  };

  const exportLogs = (format: "csv" | "json") => {
    const entries = filtered();
    if (format === "json") {
      const json = JSON.stringify(
        entries.map(e => ({
          id: e.id,
          time: e.time.toISOString(),
          actor: e.actor,
          action: e.action,
          target: e.target,
          environment: e.env,
          sysId: e.sysId,
          project: e.project
        })),
        null,
        2
      );
      const blob = new Blob([json], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.json`;
      a.click();
      URL.revokeObjectURL(url);
    } else {
      const header = "Time,Actor,Action,Target,Environment,SysID,Project\n";
      const rows = entries.map(
        e =>
          `"${e.time.toISOString()}","${e.actor}","${e.action}","${e.target}","${e.env}","${e.sysId}","${e.project ?? ""}"`
      );
      const blob = new Blob([header + rows.join("\n")], { type: "text/csv" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    }
  };

  return (
    <>
      <Title>Audit Logs | Nona Config Admin</Title>
      <div class="animate-page-enter space-y-6">
        <section
          data-testid="audit-logs-section"
          class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5"
        >
          <AuditLogsHeader onExport={exportLogs} />

          <Show when={auditQuery.isError}>
            <QueryErrorBanner
              message="Failed to load audit logs."
              onRetry={() => auditQuery.refetch()}
            />
          </Show>

          <AuditLogsFilters
          search={search()}
          setSearch={v => {
            setSearch(v);
            setPage(0);
          }}
          filterAction={filterAction()}
          setFilterAction={v => {
            setFilterAction(v);
            setPage(0);
          }}
          filterEnv={filterEnv()}
          setFilterEnv={v => {
            setFilterEnv(v);
            setPage(0);
          }}
          dateFrom={dateFrom()}
          setDateFrom={v => {
            setDateFrom(v);
            setPage(0);
          }}
          dateTo={dateTo()}
          setDateTo={v => {
            setDateTo(v);
            setPage(0);
          }}
          uniqueActions={uniqueActions()}
          uniqueEnvs={uniqueEnvs()}
          clearAllFilters={clearFilters}
        />

        <AuditLogsTable
          isLoading={isLoading()}
          filteredEntries={filtered()}
          pageEntries={pageEntries()}
          page={page()}
          totalPages={totalPages()}
          pageSize={PAGE_SIZE}
          onChangePage={changePage}
        />
        </section>
      </div>
    </>
  );
}
