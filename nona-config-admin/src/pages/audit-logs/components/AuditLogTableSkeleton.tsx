import { For } from "solid-js";

/** Skeleton rows matching the three-column AuditLogsTable layout exactly. */
export function AuditLogTableSkeleton(props: { rows?: number }) {
  const count = () => props.rows ?? 6;

  return (
    <For each={Array.from({ length: count() })}>
      {() => (
        <tr class="border-b border-outline-variant/10 last:border-b-0">
          {/* Activity cell */}
          <td class="py-4 pl-5 pr-4">
            <div class="flex items-start gap-3">
              <div class="skeleton w-7 h-7 rounded-lg shrink-0" />
              <div class="space-y-2 flex-1">
                <div class="flex items-center gap-2">
                  <div class="skeleton h-3.5 w-20 rounded" />
                  <div class="skeleton h-4 w-14 rounded-md" />
                  <div class="skeleton h-4 w-28 rounded" />
                </div>
                <div class="skeleton h-3 w-48 rounded" />
              </div>
            </div>
          </td>
          {/* Context cell */}
          <td class="py-4 px-4 w-32">
            <div class="flex flex-col items-center gap-1.5">
              <div class="skeleton h-5 w-16 rounded-md" />
              <div class="skeleton h-3 w-12 rounded" />
            </div>
          </td>
          {/* When cell */}
          <td class="py-4 pl-4 pr-5 w-44">
            <div class="flex flex-col items-end gap-1">
              <div class="skeleton h-3.5 w-14 rounded" />
              <div class="skeleton h-3 w-24 rounded" />
              <div class="skeleton h-2.5 w-16 rounded" />
            </div>
          </td>
        </tr>
      )}
    </For>
  );
}
