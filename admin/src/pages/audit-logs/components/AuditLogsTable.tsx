import { For, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import { AuditLogRow } from "../../../features/audit-log-table/AuditLogRow";
import { AuditLogTableSkeleton } from "./AuditLogTableSkeleton";
import type { AuditEntry } from "../types";

interface AuditLogsTableProps {
  isLoading: boolean;
  filteredEntries: AuditEntry[];
  pageEntries: AuditEntry[];
  page: number;
  totalPages: number;
  pageSize: number;
  onChangePage: (page: number) => void;
}

export function AuditLogsTable(props: AuditLogsTableProps) {
  return (
    <div class="space-y-4">
      <div class="border border-outline-variant/15 rounded-xl overflow-hidden bg-surface-container-low">
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-outline-variant/15 bg-surface-container-lowest/50">
                <th class="px-6 py-3 text-[11px] font-medium text-outline uppercase tracking-[0.05em]">Activity</th>
                <th class="px-6 py-3 text-[11px] font-medium text-outline uppercase tracking-[0.05em] text-center w-32">Context</th>
                <th class="px-6 py-3 text-[11px] font-medium text-outline uppercase tracking-[0.05em] text-right w-44">When</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-outline-variant/10" data-testid="audit-log-list">
              <Show when={props.isLoading}>
                <AuditLogTableSkeleton rows={6} />
              </Show>

              <Show when={!props.isLoading && props.filteredEntries.length === 0}>
                <tr>
                  <td colspan="3" class="py-16 text-center">
                    <MIcon name="search_off" class="text-[40px] text-outline/30 block mx-auto mb-3" />
                    <p class="text-on-surface-variant text-[13px]">No activity recorded yet.</p>
                    <p class="text-outline text-[12px] mt-1">Try adjusting your filters.</p>
                  </td>
                </tr>
              </Show>

              <Show when={!props.isLoading}>
                <For each={props.pageEntries}>
                  {(entry) => <AuditLogRow entry={entry} />}
                </For>
              </Show>
            </tbody>
          </table>
        </div>
      </div>

      <Show when={props.filteredEntries.length > 0}>
        <div class="flex items-center justify-between">
          <p class="text-[12px] text-outline">
            <span class="font-medium text-on-surface-variant">
              {props.filteredEntries.length === 0
                ? 0
                : props.page * props.pageSize + 1}
              –
              {Math.min((props.page + 1) * props.pageSize, props.filteredEntries.length)}
            </span>
            {" "}of {props.filteredEntries.length}
          </p>
          <div class="flex items-center gap-1.5">
            <button
              onClick={() => props.onChangePage(props.page - 1)}
              disabled={props.page === 0}
              aria-label="Previous Page"
              class="w-8 h-8 flex items-center justify-center rounded-lg border border-outline-variant/20 text-outline hover:text-on-surface hover:border-outline-variant/40 hover:bg-surface-container-high/30 disabled:opacity-30 transition-all cursor-pointer bg-transparent"
            >
              <MIcon name="chevron_left" class="text-sm" />
            </button>
            <For each={Array.from({ length: Math.min(props.totalPages, 5) }, (_, i) => i)}>
              {(i) => (
                <button
                  onClick={() => props.onChangePage(i)}
                  class={`h-8 min-w-8 px-2.5 flex items-center justify-center rounded-lg text-[12px] font-medium border transition-all cursor-pointer ${
                    props.page === i
                      ? "bg-surface-container-high text-on-surface border-outline-variant/30"
                      : "border-transparent text-outline hover:text-on-surface hover:bg-surface-container-high/30"
                  }`}
                >
                  {i + 1}
                </button>
              )}
            </For>
            <Show when={props.totalPages > 5}>
              <span class="text-outline mx-0.5 text-[12px]">…</span>
              <button
                onClick={() => props.onChangePage(props.totalPages - 1)}
                class={`h-8 min-w-8 px-2.5 flex items-center justify-center rounded-lg text-[12px] font-medium border transition-all cursor-pointer ${
                  props.page === props.totalPages - 1
                    ? "bg-surface-container-high text-on-surface border-outline-variant/30"
                    : "border-transparent text-outline hover:text-on-surface hover:bg-surface-container-high/30"
                }`}
              >
                {props.totalPages}
              </button>
            </Show>
            <button
              onClick={() => props.onChangePage(props.page + 1)}
              disabled={props.page >= props.totalPages - 1}
              aria-label="Next Page"
              class="w-8 h-8 flex items-center justify-center rounded-lg border border-outline-variant/20 text-outline hover:text-on-surface hover:border-outline-variant/40 hover:bg-surface-container-high/30 disabled:opacity-30 transition-all cursor-pointer bg-transparent"
            >
              <MIcon name="chevron_right" class="text-sm" />
            </button>
          </div>
        </div>
      </Show>
    </div>
  );
}
