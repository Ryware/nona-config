import { createMemo, createSignal, For, Show } from "solid-js";
import type { ApiKey, CreateApiKeyRequest } from "../../../types";
import { useClipboard } from "../../../shared/hooks/useClipboard";
import { Button } from "../../../shared/ui/button";
import { Label } from "../../../shared/ui/label";
import { Select } from "../../../shared/ui/select";
import { MIcon } from "../../../shared/ui/icons";
import { FormField } from "../../../widgets/auth-shell/FormField";

interface ProjectApiKeysProps {
  apiKeys: ApiKey[];
  isLoading: boolean;
  isCreating: boolean;
  deletingId: string | null;
  canManage: boolean;
  showCreateForm: boolean;
  setShowCreateForm: (value: boolean) => void;
  onCreate: (data: CreateApiKeyRequest) => void;
  onDelete: (apiKeyId: string) => void;
  onCopied: (message: string) => void;
}

const SCOPE_OPTIONS = [
  { value: "client", label: "Client" },
  { value: "server", label: "Server" },
  { value: "all", label: "All" },
];

function ScopeBadge(props: { scope: ApiKey["scope"] }) {
  const className = () =>
    ({
      client: "bg-primary/10 text-primary",
      server: "bg-tertiary/15 text-tertiary",
      all: "bg-success/10 text-success",
    })[props.scope];

  return (
    <span class={`rounded-full px-2 py-0.5 text-[10px] font-bold uppercase ${className()}`}>
      {props.scope}
    </span>
  );
}

export function ProjectApiKeys(props: ProjectApiKeysProps) {
  const [name, setName] = createSignal("");
  const [scope, setScope] = createSignal<ApiKey["scope"]>("client");
  const [revealed, setRevealed] = createSignal<Record<string, boolean>>({});
  const { copy } = useClipboard();

  const canCreate = createMemo(() => name().trim().length > 0 && !props.isCreating);

  const handleCreate = () => {
    if (!canCreate()) return;

    props.onCreate({
      name: name().trim(),
      scope: scope(),
      environment: null,
    });
    setName("");
    setScope("client");
  };

  const handleCopy = async (value: string) => {
    if (await copy(value)) {
      props.onCopied("Copied to clipboard");
    }
  };

  const toggleReveal = (id: string) => {
    setRevealed(current => ({ ...current, [id]: !current[id] }));
  };

  return (
    <section
      id="api-keys"
      data-testid="project-api-keys-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5 scroll-mt-20"
    >
      <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <p
            data-testid="project-api-keys-heading"
            class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
          >
            <MIcon name="key" class="text-[15px]" />
            API Keys
          </p>
          <p class="text-on-surface-variant mt-1 text-xs">
            Keys belong to this project and can be limited by access type or environment.
          </p>
        </div>

        <Show when={props.canManage}>
          <button
            data-testid="project-add-api-key-button"
            type="button"
            onClick={() => props.setShowCreateForm(!props.showCreateForm)}
            aria-label="Add API Key"
            title="Add API Key"
            class="bg-primary text-on-primary inline-flex h-10 w-10 cursor-pointer items-center justify-center gap-1.5 self-end rounded-lg border-0 px-0 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] md:h-10 md:w-auto md:px-4 md:self-auto"
          >
            <MIcon name="add" class="text-[16px]" />
            <span class="hidden md:inline">Add API Key</span>
          </button>
        </Show>
      </div>

      <Show when={props.canManage && props.showCreateForm}>
        <div class="bg-surface-container-low border-outline-variant/15 animate-fade-in rounded-2xl border p-6 shadow-sm">
          <h3 class="font-headline text-on-surface mb-6 text-xs font-bold tracking-wider uppercase">
            New API Key
          </h3>
          <div class="grid gap-6 md:grid-cols-2">
            <FormField
              id="api-key-name"
              label="Key Name *"
              type="text"
              testId="api-key-name-input"
              value={name()}
              onInput={(event: InputEvent & { currentTarget: HTMLInputElement }) =>
                setName(event.currentTarget.value)
              }
              placeholder="Key name"
              leftIcon="badge"
              required
            />
            <div>
              <Label>Scope</Label>
              <Select
                value={scope()}
                onChange={value => setScope(value as ApiKey["scope"])}
                options={SCOPE_OPTIONS}
              />
            </div>
          </div>
          <div class="mt-6 flex justify-end gap-3">
            <Button
              data-testid="api-key-create-button"
              type="button"
              disabled={!canCreate()}
              onClick={handleCreate}
            >
              <MIcon name="add" class="text-[16px]" />
              Create
            </Button>
            <Button
              data-testid="api-key-create-cancel-button"
              type="button"
              variant="outline"
              onClick={() => props.setShowCreateForm(false)}
            >
              <MIcon name="close" class="text-[16px]" />
              Cancel
            </Button>
          </div>
        </div>
      </Show>

      <Show
        when={!props.isLoading}
        fallback={<div class="skeleton h-11 w-full rounded-xl" />}
      >
        <Show
          when={props.apiKeys.length > 0}
          fallback={
            <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
              No API keys yet.
            </div>
          }
        >
          <div class="space-y-2">
            <For each={props.apiKeys}>
              {apiKey => (
                <div class="bg-surface-container grid gap-3 rounded-xl px-4 py-3 md:grid-cols-[minmax(160px,0.8fr)_minmax(180px,1.2fr)_auto] md:items-center">
                  <div class="min-w-0">
                    <div class="flex items-center gap-2">
                      <span class="text-on-surface truncate text-sm font-semibold">
                        {apiKey.name}
                      </span>
                      <ScopeBadge scope={apiKey.scope} />
                    </div>
                    <p class="text-outline mt-1 truncate text-[11px]">
                      {apiKey.environment ?? "All environments"}
                    </p>
                  </div>

                  <code
                    data-testid={`api-key-value-${apiKey.id}`}
                    class="text-on-surface bg-surface-container-lowest rounded-lg px-3 py-2 font-mono text-[12px]"
                  >
                    {revealed()[apiKey.id] ? apiKey.key : "•".repeat(32)}
                  </code>

                  <div class="flex items-center justify-end gap-1">
                    <button
                      data-testid={`api-key-toggle-${apiKey.id}`}
                      type="button"
                      onClick={() => toggleReveal(apiKey.id)}
                      class="text-outline hover:text-on-surface hover:bg-surface-bright cursor-pointer rounded-lg border-0 p-1.5 transition-all"
                      title={revealed()[apiKey.id] ? "Hide" : "Show"}
                    >
                      <MIcon
                        name={revealed()[apiKey.id] ? "visibility_off" : "visibility"}
                        class="text-[16px]"
                      />
                    </button>
                    <button
                      type="button"
                      onClick={() => handleCopy(apiKey.key)}
                      class="text-outline hover:text-on-surface hover:bg-surface-bright cursor-pointer rounded-lg border-0 p-1.5 transition-all"
                      title="Copy"
                    >
                      <MIcon name="content_copy" class="text-[16px]" />
                    </button>
                    <Show when={props.canManage}>
                      <button
                        data-testid={`api-key-delete-${apiKey.id}`}
                        type="button"
                        onClick={() => props.onDelete(apiKey.id)}
                        disabled={props.deletingId === apiKey.id}
                        class="text-outline hover:text-error hover:bg-error/10 cursor-pointer rounded-lg border-0 p-1.5 transition-all disabled:opacity-40"
                        title="Delete API key"
                      >
                        <MIcon name="delete" class="text-[16px]" />
                      </button>
                    </Show>
                  </div>
                </div>
              )}
            </For>
          </div>
        </Show>
      </Show>
    </section>
  );
}
