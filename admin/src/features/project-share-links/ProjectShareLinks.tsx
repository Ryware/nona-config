import { For, Show } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import type { ParameterShareLink } from "../../types";

interface ProjectShareLinksProps {
  environmentName: string;
  shareLinks: ParameterShareLink[];
  isLoading: boolean;
  revokingId: number | null;
  canManage: boolean;
  onCopy: (value: string) => void;
  onRevoke: (link: ParameterShareLink) => void;
  buildShareUrl: (token: string) => string;
}

export function ProjectShareLinks(props: ProjectShareLinksProps) {
  return (
    <section
      id="shared-links"
      data-testid="project-shared-links-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5 scroll-mt-20"
    >
      <div>
        <p
          data-testid="project-shared-links-heading"
          class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
        >
          <MIcon name="link" class="text-[15px]" />
          Shared Links
        </p>
        <p class="text-on-surface-variant mt-1 text-xs">
          All parameter share links for the active environment: {props.environmentName}.
        </p>
      </div>

      <Show
        when={!props.isLoading}
        fallback={<div class="skeleton h-20 w-full rounded-xl" />}
      >
        <Show
          when={props.shareLinks.length > 0}
          fallback={
            <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
              No share links yet.
            </div>
          }
        >
          <div class="space-y-2">
            <For each={props.shareLinks}>
              {link => {
                const status = () => linkStatus(link);

                return (
                  <div class="bg-surface-container grid gap-3 rounded-xl px-4 py-3 md:grid-cols-[minmax(220px,1fr)_auto] md:items-center">
                    <div class="min-w-0">
                      <div class="flex flex-wrap items-center gap-2">
                        <span class="text-on-surface truncate font-mono text-[13px] font-bold">
                          {link.key}
                        </span>
                        <span
                          class={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${
                            link.canEdit
                              ? "bg-secondary/10 text-secondary"
                              : "bg-primary/10 text-primary"
                          }`}
                        >
                          {link.canEdit ? "Edit" : "View"}
                        </span>
                        <span
                          class={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${
                            status() === "active"
                              ? "bg-success/10 text-success"
                              : "bg-error/10 text-error"
                          }`}
                        >
                          {status()}
                        </span>
                      </div>
                      <p class="text-on-surface-variant mt-1 text-[12px]">
                        Expires {formatDate(link.expiresAt)}
                      </p>
                      <p class="text-outline mt-0.5 text-[11px]">
                        Created by {link.createdBy}
                      </p>
                    </div>

                    <div class="flex items-center justify-end gap-2">
                      <Button
                        type="button"
                        variant="secondary"
                        size="icon"
                        disabled={!link.token}
                        onClick={() => props.onCopy(props.buildShareUrl(link.token))}
                        aria-label="Copy share link"
                        title={link.token ? "Copy share link" : "Copy unavailable"}
                        data-testid={`project-shared-link-copy-${link.id}`}
                      >
                        <MIcon name="content_copy" class="text-[18px]" />
                      </Button>
                      <Show when={props.canManage}>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          disabled={status() !== "active" || props.revokingId === link.id}
                          onClick={() => props.onRevoke(link)}
                          data-testid={`project-shared-link-revoke-${link.id}`}
                        >
                          <MIcon name="link_off" class="text-[16px]" />
                          {props.revokingId === link.id ? "Revoking..." : "Revoke"}
                        </Button>
                      </Show>
                    </div>
                  </div>
                );
              }}
            </For>
          </div>
        </Show>
      </Show>
    </section>
  );
}

function formatDate(value: string): string {
  return new Date(value).toLocaleString([], {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function linkStatus(link: ParameterShareLink): "active" | "expired" | "revoked" {
  if (link.revokedAt) return "revoked";
  return new Date(link.expiresAt).getTime() <= Date.now() ? "expired" : "active";
}
