import { createSignal, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import { Input } from "../../../shared/ui/input";

interface AuditLogsHeaderProps {
  onExport: (format: "csv" | "json") => void;
  search: string;
  setSearch: (value: string) => void;
}

export function AuditLogsHeader(props: AuditLogsHeaderProps) {
  const [showExportMenu, setShowExportMenu] = createSignal(false);

  return (
    <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
      <div>
        <h2
          data-testid="audit-logs-heading"
          class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
        >
          <MIcon name="history" class="text-[15px]" />
          Audit Logs
        </h2>
        <p class="text-on-surface-variant mt-1 text-xs">
          All administrative actions across your organization.
        </p>
      </div>
      <div class="flex w-full min-w-0 items-center justify-end gap-2 md:w-auto md:flex-row md:flex-wrap md:items-center md:justify-end">
        <Input
          data-testid="audit-search-input"
          type="text"
          placeholder="Filter audit trail..."
          value={props.search}
          onInput={e => props.setSearch(e.currentTarget.value)}
          class="h-10 min-w-0 flex-1 md:w-64"
          leftIcon="search"
          wrapperStyle="min-w-0 flex-1 md:w-auto md:flex-none"
        />
        <div class="relative">
          <button
            data-testid="audit-export-button"
            onClick={() => setShowExportMenu(v => !v)}
            aria-label="Export Logs"
            title="Export Logs"
            class="bg-primary text-on-primary inline-flex h-10 w-10 shrink-0 cursor-pointer items-center justify-center gap-1.5 rounded-lg border-0 px-0 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] md:h-10 md:w-auto md:px-4"
          >
            <MIcon name="download" class="text-[18px]" />
            <span class="hidden md:inline">Export Logs</span>
          </button>
          <Show when={showExportMenu()}>
            <div class="bg-surface-container-low border-outline-variant/20 animate-fade-in absolute top-full right-0 z-50 mt-1.5 min-w-40 overflow-hidden rounded-lg border shadow-xl">
              <button
                onClick={() => {
                  props.onExport("csv");
                  setShowExportMenu(false);
                }}
                class="text-on-surface hover:bg-surface-container-high flex w-full cursor-pointer items-center gap-2.5 border-0 bg-transparent px-4 py-2.5 text-[13px] transition-colors"
              >
                <MIcon name="table_view" class="text-outline text-[16px]" />
                Export CSV
              </button>
              <button
                onClick={() => {
                  props.onExport("json");
                  setShowExportMenu(false);
                }}
                class="text-on-surface hover:bg-surface-container-high flex w-full cursor-pointer items-center gap-2.5 border-0 bg-transparent px-4 py-2.5 text-[13px] transition-colors"
              >
                <MIcon name="data_object" class="text-outline text-[16px]" />
                Export JSON
              </button>
            </div>
          </Show>
        </div>
      </div>
    </div>
  );
}
