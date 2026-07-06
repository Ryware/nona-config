import { type JSX, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import type { AuditEntry } from "../types";
import { formatTimestamp, timeAgo } from "../utils";

interface AuditLogDrawerProps {
  entry: AuditEntry | null;
  onClose: () => void;
}

type ActionKind = "create" | "update" | "delete" | "system";

function getKind(action: string): ActionKind {
  if (action.includes("Created") || action.includes("Invited")) return "create";
  if (action.includes("Updated") || action.includes("changed")) return "update";
  if (action.includes("Deleted") || action.includes("Deleted Key")) return "delete";
  return "system";
}

const KIND = {
  create: { icon: "add_circle", iconColor: "text-success", label: "Created" },
  update: {
    icon: "edit",
    iconColor: "text-primary-container",
    label: "Updated"
  },
  delete: { icon: "delete", iconColor: "text-error", label: "Deleted" },
  system: { icon: "settings", iconColor: "text-outline", label: "System" }
};

function MetaRow(props: { label: string; children: JSX.Element; mono?: boolean }) {
  return (
    <div class="border-outline-variant/8 flex items-start justify-between gap-4 border-b py-2.5 last:border-0">
      <span class="text-outline/70 shrink-0 text-[12px]">{props.label}</span>
      <span
        class={`text-on-surface text-right text-[12.5px] font-medium ${props.mono ? "font-mono" : ""}`}
      >
        {props.children}
      </span>
    </div>
  );
}

export function AuditLogDrawer(props: AuditLogDrawerProps) {
  return (
    <Show when={props.entry}>
      {(() => {
        const entry = props.entry!;
        const kind = getKind(entry.action);
        const ks = KIND[kind];
        const isUpdate = entry.actionCode === "UPDATE_ENTRY";
        const isCreate = entry.actionCode === "CREATE_ENTRY";
        const isDelete = entry.actionCode === "DELETE_ENTRY";
        const isParamAction = isUpdate || isCreate || isDelete;

        const valueChanged = isUpdate && entry.oldValue !== entry.newValue;
        const nameChanged =
          isUpdate &&
          entry.oldDisplayName !== undefined &&
          entry.oldDisplayName !== entry.displayName;
        const descChanged =
          isUpdate &&
          entry.oldDescription !== undefined &&
          entry.oldDescription !== entry.description;

        return (
          <>
            {/* Backdrop */}
            <div
              onClick={() => props.onClose()}
              class="fixed inset-0 z-200 mb-0 bg-black/70 backdrop-blur-sm transition-opacity"
            />

            {/* Drawer */}
            <div
              data-testid="audit-drawer"
              class="bg-surface-container-lowest border-outline-variant/15 fixed top-0 right-0 z-201 flex h-screen w-full max-w-115 flex-col border-l"
            >
              {/* Header */}
              <div class="border-outline-variant/15 flex shrink-0 items-start justify-between gap-3 border-b px-5 pt-5 pb-4">
                <div class="flex items-center gap-3">
                  <div class="bg-surface-container-high flex h-8 w-8 shrink-0 items-center justify-center rounded-lg">
                    <MIcon name={ks.icon} class={`text-[16px] ${ks.iconColor}`} />
                  </div>
                  <div>
                    <h3
                      data-testid="audit-drawer-heading"
                      class="font-headline text-on-surface text-[14px] leading-tight font-semibold"
                    >
                      {entry.action}
                    </h3>
                    <p class="text-outline mt-0.5 font-mono text-[11px]">
                      {entry.sysId} · {timeAgo(entry.time)}
                    </p>
                  </div>
                </div>
                <button
                  onClick={() => props.onClose()}
                  data-testid="audit-drawer-close-button"
                  class="text-outline hover:text-on-surface hover:bg-surface-container-high flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-lg border-0 bg-transparent transition-colors"
                  aria-label="Close details"
                >
                  <MIcon name="close" class="text-[18px]" />
                </button>
              </div>

              {/* Scrollable body */}
              <div class="flex-1 overflow-y-auto">
                {/* Parameter identity */}
                <div class="px-5 pt-4 pb-4">
                  <div class="flex items-start justify-between gap-3">
                    <div class="min-w-0 flex-1">
                      <code
                        class={`font-mono text-[14px] font-semibold ${isDelete ? "text-error/70 line-through" : "text-on-surface"}`}
                      >
                        {entry.target}
                      </code>
                      <Show when={entry.displayName && entry.displayName !== entry.target}>
                        <div class="text-outline mt-0.5 text-[12px]">{entry.displayName}</div>
                      </Show>
                    </div>
                    <div class="flex shrink-0 flex-col items-end gap-1">
                      <span
                        class={`rounded-md px-2 py-0.5 text-[10px] font-semibold ${entry.envStyle}`}
                      >
                        {entry.env === "Global Scope" ? "System" : entry.env}
                      </span>
                      <Show when={entry.project}>
                        <span class="text-outline/50 font-mono text-[10px]">{entry.project}</span>
                      </Show>
                    </div>
                  </div>
                  <Show when={entry.description}>
                    <p class="text-outline/70 mt-2 text-[12px] leading-relaxed italic">
                      {entry.description}
                    </p>
                  </Show>
                </div>

                {/* Changes section */}
                <Show when={isParamAction}>
                  <div class="border-outline-variant/10 mx-5 border-t" />
                  <div class="space-y-4 px-5 pt-4 pb-5">
                    <span
                      data-testid="audit-drawer-changes-heading"
                      class="text-on-surface-variant block text-[12px] font-semibold"
                    >
                      Changes
                    </span>

                    {/* UPDATE diffs */}
                    <Show when={isUpdate}>
                      <div class="space-y-3">
                        <Show when={valueChanged}>
                          <div class="space-y-1.5">
                            <span class="text-outline/60 block text-[11px]">Value</span>
                            <div class="bg-surface-container-low border-outline-variant/10 rounded-lg border px-3 py-2.5">
                              <span class="text-outline/40 mb-1 block text-[10px]">Before</span>
                              <code class="text-error/70 font-mono text-[12px] break-all">
                                {entry.oldValue || "(empty)"}
                              </code>
                            </div>
                            <div class="bg-surface-container-low border-outline-variant/10 rounded-lg border px-3 py-2.5">
                              <span class="text-outline/40 mb-1 block text-[10px]">After</span>
                              <code class="text-on-surface font-mono text-[12px] break-all">
                                {entry.newValue || "(empty)"}
                              </code>
                            </div>
                          </div>
                        </Show>

                        <Show when={nameChanged}>
                          <div class="space-y-1.5">
                            <span class="text-outline/60 block text-[11px]">Name</span>
                            <div class="flex items-baseline gap-2 text-[12.5px]">
                              <span class="text-error/70 line-through">
                                {entry.oldDisplayName || "—"}
                              </span>
                              <span class="text-outline/30">→</span>
                              <span class="text-on-surface">{entry.displayName || "—"}</span>
                            </div>
                          </div>
                        </Show>

                        <Show when={descChanged}>
                          <div class="space-y-1.5">
                            <span class="text-outline/60 block text-[11px]">Description</span>
                            <div class="flex items-baseline gap-2 text-[12px]">
                              <span class="text-error/70 italic line-through">
                                {entry.oldDescription || "—"}
                              </span>
                              <span class="text-outline/30">→</span>
                              <span class="text-on-surface italic">{entry.description || "—"}</span>
                            </div>
                          </div>
                        </Show>

                        <Show when={!valueChanged && !nameChanged && !descChanged}>
                          <p class="text-outline/50 text-[12px] italic">
                            Detailed diff not available for this record.
                          </p>
                        </Show>
                      </div>
                    </Show>

                    {/* CREATE: initial value */}
                    <Show when={isCreate}>
                      <div class="space-y-1.5">
                        <span class="text-outline/60 block text-[11px]">Initial value</span>
                        <div class="bg-surface-container-low border-outline-variant/10 rounded-lg border px-3 py-2.5">
                          <code class="text-success font-mono text-[12px] break-all">
                            {entry.newValue || "(empty)"}
                          </code>
                        </div>
                      </div>
                    </Show>

                    {/* DELETE: last known value */}
                    <Show when={isDelete}>
                      <div class="space-y-1.5">
                        <span class="text-outline/60 block text-[11px]">Deleted value</span>
                        <div class="bg-surface-container-low border-outline-variant/10 rounded-lg border px-3 py-2.5">
                          <code class="text-error/70 font-mono text-[12px] break-all line-through">
                            {entry.oldValue || "(empty)"}
                          </code>
                        </div>
                      </div>
                    </Show>
                  </div>
                </Show>

                {/* Technical metadata */}
                <div class="border-outline-variant/10 mx-5 border-t" />
                <div class="px-5 pt-4 pb-6">
                  <span
                    data-testid="audit-drawer-metadata-heading"
                    class="text-on-surface-variant mb-3 block text-[12px] font-semibold"
                  >
                    Technical metadata
                  </span>
                  <div class="divide-outline-variant/8 divide-y">
                    <MetaRow label="Actor">{entry.actor}</MetaRow>
                    <MetaRow label="Timestamp">{formatTimestamp(entry.time)}</MetaRow>
                    <MetaRow label="System ID" mono>
                      {entry.sysId}
                    </MetaRow>
                    <Show when={entry.ipAddress}>
                      <MetaRow label="IP Address" mono>
                        {entry.ipAddress}
                      </MetaRow>
                    </Show>
                    <Show when={entry.contentType}>
                      <MetaRow label="Content Type">
                        <span class="text-on-surface-variant bg-surface-container-high rounded-md px-2 py-0.5 font-mono text-[11px] font-medium">
                          {entry.contentType}
                        </span>
                      </MetaRow>
                    </Show>
                    <Show when={entry.scope}>
                      <MetaRow label="Access Scope">
                        <span class="text-on-surface-variant bg-surface-container-high rounded-md px-2 py-0.5 font-mono text-[11px] font-medium">
                          {entry.scope}
                        </span>
                      </MetaRow>
                    </Show>
                  </div>
                </div>
              </div>

              {/* Footer */}
              <div class="border-outline-variant/15 shrink-0 border-t px-5 py-4">
                <button
                  type="button"
                  onClick={props.onClose}
                  class="text-on-surface-variant hover:text-on-surface bg-surface-container-high hover:bg-surface-container-highest w-full cursor-pointer rounded-lg border-0 py-2.5 text-[13px] font-medium transition-colors"
                >
                  Close
                </button>
              </div>
            </div>
          </>
        );
      })()}
    </Show>
  );
}
