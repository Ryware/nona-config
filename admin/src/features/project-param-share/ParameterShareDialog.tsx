import { createEffect, createSignal, For, Show } from "solid-js";
import { Button } from "../../shared/ui/button";
import { Input } from "../../shared/ui/input";
import { MIcon } from "../../shared/ui/icons";
import { Select } from "../../shared/ui/select";
import type {
  ConfigEntry,
  CreateParameterShareLinkRequest,
  ParameterShareLink
} from "../../types";

type ExpirationOption = NonNullable<CreateParameterShareLinkRequest["expiration"]>;

interface ParameterShareDialogProps {
  entry: ConfigEntry | null;
  shareLinks: ParameterShareLink[];
  generatedUrl: string | null;
  isLoading: boolean;
  isCreating: boolean;
  revokingId: number | null;
  onClose: () => void;
  onCreate: (data: CreateParameterShareLinkRequest) => void;
  onRevoke: (id: number) => void;
  onCopy: (value: string) => void;
}

const EXPIRATION_OPTIONS: { value: ExpirationOption; label: string }[] = [
  { value: "1h", label: "1 hour" },
  { value: "1d", label: "1 day" },
  { value: "3d", label: "3 days" },
  { value: "30d", label: "30 days" },
  { value: "12m", label: "12 months" }
];

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

export function ParameterShareDialog(props: ParameterShareDialogProps) {
  const [expiration, setExpiration] = createSignal<ExpirationOption>("1h");
  const [permission, setPermission] = createSignal<"edit" | "view">("edit");

  createEffect(() => {
    if (props.entry) {
      setExpiration("1h");
      setPermission("edit");
    }
  });

  const handleCreate = () => {
    props.onCreate({
      expiration: expiration(),
      canEdit: permission() === "edit"
    });
  };

  return (
    <Show when={props.entry}>
      {entry => (
        <>
          <div
            onClick={() => props.onClose()}
            class="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm"
          />
          <div
            data-testid="parameter-share-dialog"
            class="bg-surface-container-low border-outline-variant/20 fixed top-1/2 left-1/2 z-50 flex max-h-[88vh] w-[min(560px,calc(100vw-32px))] -translate-x-1/2 -translate-y-1/2 flex-col rounded-2xl border p-6 shadow-2xl"
          >
            <div class="border-outline-variant/15 mb-5 flex items-start justify-between gap-4 border-b pb-4">
              <div class="min-w-0">
                <h3 class="font-headline text-on-surface text-base font-bold">
                  Share Parameter
                </h3>
                <p class="text-outline mt-1 truncate font-mono text-[11px]">
                  {entry().environment} / {entry().key}
                </p>
              </div>
              <button
                onClick={() => props.onClose()}
                class="text-outline hover:text-on-surface hover:bg-surface-container-high/60 flex cursor-pointer items-center justify-center rounded-lg border-0 bg-transparent p-1.5 transition-colors"
                aria-label="Close share dialog"
              >
                <MIcon name="close" />
              </button>
            </div>

            <div class="flex-1 space-y-5 overflow-y-auto pr-1">
              <div class="grid gap-3 sm:grid-cols-2">
                <div class="space-y-2">
                  <label class="text-on-surface-variant block text-[11px] font-medium tracking-[0.05em]">
                    Expiration
                  </label>
                  <Select
                    value={expiration()}
                    onChange={value => setExpiration(value as ExpirationOption)}
                    options={EXPIRATION_OPTIONS}
                  />
                </div>
                <div class="space-y-2">
                  <label class="text-on-surface-variant block text-[11px] font-medium tracking-[0.05em]">
                    Permission
                  </label>
                  <Select
                    value={permission()}
                    onChange={value => setPermission(value as "edit" | "view")}
                    options={[
                      { value: "edit", label: "Can edit" },
                      { value: "view", label: "View only" }
                    ]}
                  />
                </div>
              </div>

              <Button
                type="button"
                onClick={handleCreate}
                disabled={props.isCreating}
                class="w-full"
                data-testid="parameter-share-create-button"
              >
                <MIcon name="add_link" class="text-[18px]" />
                {props.isCreating ? "Generating..." : "Generate Link"}
              </Button>

              <Show when={props.generatedUrl}>
                {url => (
                  <div class="border-primary/20 bg-primary/5 rounded-xl border p-3">
                    <label class="text-primary mb-2 block text-[11px] font-semibold tracking-[0.05em] uppercase">
                      Generated link
                    </label>
                    <div class="flex gap-2">
                      <Input
                        value={url()}
                        readonly
                        data-testid="parameter-share-generated-url"
                        class="font-mono text-[12px]"
                      />
                      <Button
                        type="button"
                        size="icon"
                        variant="secondary"
                        onClick={() => props.onCopy(url())}
                        aria-label="Copy share link"
                        title="Copy share link"
                      >
                        <MIcon name="content_copy" class="text-[18px]" />
                      </Button>
                    </div>
                  </div>
                )}
              </Show>

              <div>
                <p class="text-outline mb-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                  Existing links
                </p>
                <Show
                  when={!props.isLoading}
                  fallback={<div class="skeleton h-16 rounded-xl" />}
                >
                  <Show
                    when={props.shareLinks.length > 0}
                    fallback={
                      <div class="text-on-surface-variant border-outline-variant/15 rounded-xl border p-4 text-center text-[13px]">
                        No share links have been generated for this parameter.
                      </div>
                    }
                  >
                    <div class="divide-outline-variant/10 rounded-xl border border-outline-variant/15">
                      <For each={props.shareLinks}>
                        {link => {
                          const status = () => linkStatus(link);
                          return (
                            <div class="flex items-center justify-between gap-3 p-3">
                              <div class="min-w-0">
                                <div class="flex flex-wrap items-center gap-2">
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
                              <Button
                                type="button"
                                variant="outline"
                                size="sm"
                                disabled={status() !== "active" || props.revokingId === link.id}
                                onClick={() => props.onRevoke(link.id)}
                                data-testid={`parameter-share-revoke-${link.id}`}
                              >
                                <MIcon name="link_off" class="text-[16px]" />
                                {props.revokingId === link.id ? "Revoking..." : "Revoke"}
                              </Button>
                            </div>
                          );
                        }}
                      </For>
                    </div>
                  </Show>
                </Show>
              </div>
            </div>
          </div>
        </>
      )}
    </Show>
  );
}
