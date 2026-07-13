import { Meta, Title } from "@solidjs/meta";
import { useParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { createEffect, createMemo, createSignal, Show } from "solid-js";
import { sharedParameterService } from "../../entities/project/api/config-entry.service";
import { ApiRequestError } from "../../shared/api/client";
import { Button } from "../../shared/ui/button";
import { Input } from "../../shared/ui/input";
import { MIcon } from "../../shared/ui/icons";
import { Select } from "../../shared/ui/select";
import { useToast } from "../../shared/ui/toast";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import type { SharedParameter } from "../../types";

function formatExpiry(value: string): string {
  return new Date(value).toLocaleString([], {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function isValidValue(parameter: SharedParameter | undefined, value: string): boolean {
  if (!parameter) return false;

  if (parameter.contentType === "text") {
    return true;
  }

  try {
    const parsed = JSON.parse(value);
    if (parameter.contentType === "json") return true;
    if (parameter.contentType === "number") return typeof parsed === "number" && Number.isFinite(parsed);
    return typeof parsed === "boolean";
  } catch {
    return false;
  }
}

function formatJson(value: string): string {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function errorMessage(caught: unknown): string {
  if (caught instanceof ApiRequestError && caught.message) {
    return caught.message;
  }

  if (caught instanceof Error && caught.message) {
    return caught.message;
  }

  return "This shared link is not available.";
}

export default function SharedParameterPage() {
  const params = useParams<{ token: string }>();
  const queryClient = useQueryClient();
  const { addToast } = useToast();
  const [editValue, setEditValue] = createSignal("");
  const [saveError, setSaveError] = createSignal<string | null>(null);

  const query = useQuery(() => ({
    queryKey: ["shared-parameter", params.token],
    queryFn: () => sharedParameterService.get(params.token),
    retry: false
  }));

  createEffect(() => {
    if (query.status === "success" && query.data) {
      setEditValue(query.data.value);
      setSaveError(null);
    }
  });

  const parameter = createMemo(() => (query.status === "success" ? query.data : undefined));
  const isInvalid = createMemo(() => !isValidValue(parameter(), editValue()));
  const isDirty = createMemo(() => parameter()?.value !== editValue());

  const updateMutation = useMutation(() => ({
    mutationFn: () => sharedParameterService.update(params.token, { value: editValue() }),
    onSuccess: updated => {
      queryClient.setQueryData(["shared-parameter", params.token], updated);
      setEditValue(updated.value);
      setSaveError(null);
      addToast("Parameter saved", "success");
    },
    onError: caught => {
      const message = errorMessage(caught);
      setSaveError(message);
      addToast(message, "error");
    }
  }));

  return (
    <>
      <Title>Shared Parameter | Nona</Title>
      <Meta name="robots" content="noindex,nofollow" />
      <main class="bg-surface text-on-surface flex min-h-screen items-center justify-center px-4 py-8">
        <div class="w-full max-w-xl">
          <Show when={query.isLoading}>
            <div class="bg-surface-container-low border-outline-variant/15 rounded-2xl border p-6 shadow-xl">
              <div class="skeleton mb-4 h-5 w-48 rounded" />
              <div class="skeleton mb-6 h-4 w-32 rounded" />
              <div class="skeleton h-11 w-full rounded-xl" />
            </div>
          </Show>

          <Show when={query.isError}>
            <div
              data-testid="shared-parameter-error"
              class="bg-surface-container-low border-outline-variant/15 rounded-2xl border p-8 text-center shadow-xl"
            >
              <MIcon name="link_off" class="text-error mx-auto mb-3 text-[32px]" />
              <h1 class="font-headline text-on-surface text-lg font-bold">
                Shared link unavailable
              </h1>
              <p class="text-on-surface-variant mt-2 text-sm leading-relaxed">
                {errorMessage(query.error)}
              </p>
            </div>
          </Show>

          <Show when={parameter()}>
            {current => (
              <section
                data-testid="shared-parameter-page"
                class="bg-surface-container-low border-outline-variant/15 rounded-2xl border p-6 shadow-xl"
              >
                <div class="border-outline-variant/15 mb-5 border-b pb-4">
                  <p class="text-outline mb-1 text-[11px] font-bold tracking-[0.08em] uppercase">
                    Shared parameter
                  </p>
                  <h1
                    data-testid="shared-parameter-key"
                    class="font-headline text-on-surface break-all text-xl font-bold"
                  >
                    {current().key}
                  </h1>
                  <div class="mt-3 flex flex-wrap gap-2">
                    <span
                      data-testid="shared-parameter-environment"
                      class="bg-primary/10 text-primary rounded-full px-2.5 py-1 font-mono text-[11px] font-semibold"
                    >
                      {current().environment}
                    </span>
                    <span class="bg-secondary/10 text-secondary rounded-full px-2.5 py-1 text-[11px] font-semibold">
                      {current().canEdit ? "Editable" : "View only"}
                    </span>
                  </div>
                </div>

                <div class="space-y-4">
                  <div class="space-y-2">
                    <label class="text-on-surface-variant block text-[11px] font-medium tracking-[0.05em]">
                      Current value
                    </label>
                    <Show
                      when={current().canEdit}
                      fallback={
                        <Show
                          when={current().contentType === "json"}
                          fallback={
                            <Input
                              value={current().value}
                              readOnly
                              data-testid="shared-parameter-value"
                              class="font-mono"
                            />
                          }
                        >
                          <pre
                            data-testid="shared-parameter-value"
                            class="bg-surface-container-lowest border-outline-variant/20 text-on-surface max-h-72 overflow-auto rounded-xl border p-3 font-mono text-[12px]"
                          >
                            {formatJson(current().value)}
                          </pre>
                        </Show>
                      }
                    >
                      <Show when={current().contentType === "boolean"}>
                        <Select
                          value={editValue()}
                          onChange={setEditValue}
                          options={[
                            { value: "true", label: "True / Active" },
                            { value: "false", label: "False / Inactive" }
                          ]}
                        />
                      </Show>
                      <Show when={current().contentType === "number"}>
                        <Input
                          type="number"
                          value={editValue()}
                          onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                            setEditValue(e.currentTarget.value)
                          }
                          data-testid="shared-parameter-value-input"
                          class="font-mono"
                        />
                      </Show>
                      <Show when={current().contentType === "json"}>
                        <VisualJsonEditor
                          id="shared-parameter-value"
                          value={editValue()}
                          onChange={setEditValue}
                        />
                      </Show>
                      <Show when={current().contentType === "text"}>
                        <Input
                          type="text"
                          value={editValue()}
                          onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                            setEditValue(e.currentTarget.value)
                          }
                          data-testid="shared-parameter-value-input"
                          class="font-mono"
                        />
                      </Show>
                    </Show>
                  </div>

                  <Show when={saveError()}>
                    {message => <p class="text-error text-[12px]">{message()}</p>}
                  </Show>

                  <div class="flex flex-wrap items-center justify-between gap-3 pt-2">
                    <p class="text-outline text-[11px]">
                      Expires {formatExpiry(current().expiresAt)}
                    </p>
                    <Show when={current().canEdit}>
                      <Button
                        type="button"
                        onClick={() => updateMutation.mutate()}
                        disabled={updateMutation.isPending || isInvalid() || !isDirty()}
                        data-testid="shared-parameter-save-button"
                      >
                        <MIcon name="save" class="text-[18px]" />
                        {updateMutation.isPending ? "Saving..." : "Save"}
                      </Button>
                    </Show>
                  </div>
                </div>
              </section>
            )}
          </Show>
        </div>
      </main>
    </>
  );
}
