import { Title } from "@solidjs/meta";
import { useQuery } from "@tanstack/solid-query";
import { createEffect, createMemo, createSignal, onCleanup, Show } from "solid-js";
import { auditLogService } from "../../entities/audit-log/api/audit-log.service";
import { auditLogKeys } from "../../entities/audit-log/queries/keys";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import type { AuditLog } from "../../types";

import { AuditLogsFilters } from "./components/AuditLogsFilters";
import { AuditLogsHeader } from "./components/AuditLogsHeader";
import { AuditLogsTable } from "./components/AuditLogsTable";
import { serializeAuditLogs, type AuditExportFormat } from "./export";
import type { AuditEntry } from "./types";
import { ACTION_STYLE, actorStyle, ENV_STYLE } from "./utils";

const PAGE_SIZE = 25;
const SEARCH_DEBOUNCE_MS = 300;

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
    actionKind: log.actionKind,
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

function environmentLabel(environment: string): string {
  return environment.charAt(0).toUpperCase() + environment.slice(1);
}

export default function AuditLogsPage() {
  const [search, setSearch] = createSignal("");
  const [debouncedSearch, setDebouncedSearch] = createSignal("");
  const [filterAction, setFilterAction] = createSignal("all");
  const [filterEnv, setFilterEnv] = createSignal("all");
  const [dateFrom, setDateFrom] = createSignal("");
  const [dateTo, setDateTo] = createSignal("");
  const [page, setPage] = createSignal(0);

  createEffect(() => {
    const value = search().trim();
    const timeout = window.setTimeout(() => setDebouncedSearch(value), SEARCH_DEBOUNCE_MS);
    onCleanup(() => window.clearTimeout(timeout));
  });

  const auditParameters = () => ({
    page: page() + 1,
    pageSize: PAGE_SIZE,
    search: debouncedSearch() || undefined,
    action: filterAction() === "all" ? undefined : filterAction(),
    environment: filterEnv() === "all" ? undefined : filterEnv(),
    dateFrom: dateFrom() || undefined,
    dateTo: dateTo() || undefined
  });

  const auditQuery = useQuery(() => {
    const parameters = auditParameters();
    return {
      queryKey: auditLogKeys.list(parameters),
      queryFn: () => auditLogService.getPage(parameters),
      placeholderData: previous => previous
    };
  });

  const pageEntries = createMemo<AuditEntry[]>(() =>
    (auditQuery.data?.items ?? []).map(mapAuditLog)
  );
  const totalCount = () => auditQuery.data?.totalCount ?? 0;
  const totalPages = () => Math.max(1, auditQuery.data?.totalPages ?? 0);
  const uniqueActions = createMemo(() =>
    (auditQuery.data?.actions ?? []).map(action => ({
      value: action,
      label: resolveActionLabel(action)
    }))
  );
  const uniqueEnvs = createMemo(() =>
    (auditQuery.data?.environments ?? []).map(environment => ({
      value: environment,
      label: environmentLabel(environment)
    }))
  );
  const isLoading = () => auditQuery.isLoading || auditQuery.isFetching;

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

  const exportLogs = (format: AuditExportFormat) => {
    const { content, mimeType } = serializeAuditLogs(pageEntries(), format);
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `audit-logs-${new Date().toISOString().slice(0, 10)}.${format}`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <>
      <Title>Audit Logs | Nona Config Admin</Title>
      <div class="animate-page-enter space-y-6">
        <section
          data-testid="audit-logs-section"
          class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5"
        >
          <AuditLogsHeader
            onExport={exportLogs}
            search={search()}
            setSearch={v => {
              setSearch(v);
              setPage(0);
            }}
          />

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
          hideSearch
        />

        <AuditLogsTable
          isLoading={isLoading()}
          entries={pageEntries()}
          totalCount={totalCount()}
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
