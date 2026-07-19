import { createSignal, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";

interface AuditLogsHeaderProps {
  onExport: (format: "csv" | "json") => void;
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
      <div class="relative">
        <button
          data-testid="audit-export-button"
          onClick={() => setShowExportMenu(v => !v)}
          class="bg-primary text-on-primary flex shrink-0 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98]"
        >
          <MIcon name="download" class="text-[18px]" />
          Export Logs
          <MIcon name="arrow_drop_down" class="text-[16px]" />
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
  );
}
