import { Show } from "solid-js";
import type { AuditEntry } from "../../pages/audit-logs/types";
import { formatTimestamp, timeAgo } from "../../pages/audit-logs/utils";
import { MIcon } from "../../shared/ui/icons";
import {
  getActionDescription,
  getActionKind,
  isDeleteAction,
  isParamAction,
  KIND_STYLES
} from "./audit-kind";
import { ParamDiffSection } from "./ParamDiffSection";

interface AuditLogRowProps {
  entry: AuditEntry;
  onSelect: (entry: AuditEntry) => void;
}

export function AuditLogRow(props: AuditLogRowProps) {
  const kind = () => getActionKind(props.entry.action);
  const ks = () => KIND_STYLES[kind()];
  const desc = () => getActionDescription(props.entry.action);

  return (
    <tr
      data-testid={`audit-row-${props.entry.id}`}
      onClick={() => props.onSelect(props.entry)}
      class="group hover:bg-surface-container-high/20 cursor-pointer transition-colors"
    >
      {/* Activity */}
      <td class="py-4 pr-4 pl-5 align-top">
        <div class="flex min-w-0 items-start gap-3">
          <div class="bg-surface-container-high mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg">
            <MIcon name={ks().icon} class={`text-[14px] ${ks().iconColor}`} />
          </div>
          <div class="min-w-0 flex-1">
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-on-surface shrink-0 text-[13px] font-semibold">
                {props.entry.actor}
              </span>
              <span
                class={`shrink-0 rounded-md px-1.5 py-0.5 text-[10px] font-semibold ${ks().badge}`}
              >
                {ks().label}
              </span>
              <code
                data-testid={`audit-target-${props.entry.id}`}
                class={`bg-surface-container-high border-outline-variant/20 max-w-45 truncate rounded border px-1.5 py-0.5 font-mono text-[11px] ${
                  isDeleteAction(props.entry.action)
                    ? "text-outline/60 line-through"
                    : "text-on-surface-variant"
                }`}
                title={props.entry.target}
              >
                {props.entry.target}
              </code>
            </div>

            <Show when={props.entry.displayName && props.entry.displayName !== props.entry.target}>
              <div class="mt-0.5 flex items-center gap-1.5">
                <span class="text-outline truncate text-[11.5px]">{props.entry.displayName}</span>
                <Show when={props.entry.description}>
                  <span class="text-outline/30 shrink-0">·</span>
                  <span class="text-outline/50 max-w-55 truncate text-[11px] italic">
                    {props.entry.description}
                  </span>
                </Show>
              </div>
            </Show>

            <Show when={isParamAction(props.entry.action)}>
              <ParamDiffSection entry={props.entry} />
            </Show>

            <Show when={desc()}>
              {d => <p class={`mt-0.5 text-[11.5px] ${d().colorClass}`}>{d().text}</p>}
            </Show>
          </div>
        </div>
      </td>

      {/* Context */}
      <td class="w-32 px-4 py-4 align-top">
        <div class="flex flex-col items-center gap-1.5">
          <span class={`rounded-md px-2 py-0.5 text-[10px] font-semibold ${props.entry.envStyle}`}>
            {props.entry.env === "Global Scope" ? "System" : props.entry.env}
          </span>
          <Show when={props.entry.project}>
            <span
              class="text-outline/50 max-w-24 truncate text-center font-mono text-[10px]"
              title={props.entry.project}
            >
              {props.entry.project}
            </span>
          </Show>
        </div>
      </td>

      {/* When */}
      <td class="w-44 py-4 pr-5 pl-4 text-right align-top">
        <div class="text-on-surface-variant group-hover:text-on-surface text-[12.5px] font-medium transition-colors">
          {timeAgo(props.entry.time)}
        </div>
        <div
          class="text-outline mt-0.5 truncate font-mono text-[10.5px]"
          title={`Audit Record System ID: ${props.entry.sysId}`}
        >
          {formatTimestamp(props.entry.time)}
        </div>
        <div class="text-outline/40 mt-0.5 font-mono text-[9.5px] tracking-wide">
          {props.entry.sysId}
        </div>
      </td>
    </tr>
  );
}
