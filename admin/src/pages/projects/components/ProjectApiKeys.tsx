import { createMemo, createSignal, For, Show } from "solid-js";
import type { ApiKey, CreateApiKeyRequest, Environment } from "../../../types";
import { useClipboard } from "../../../shared/hooks/useClipboard";
import { Button } from "../../../shared/ui/button";
import { Input } from "../../../shared/ui/input";
import { Select } from "../../../shared/ui/select";
import { MIcon } from "../../../shared/ui/icons";

interface ProjectApiKeysProps {
  apiKeys: ApiKey[];
  environments: Environment[];
  isLoading: boolean;
  isCreating: boolean;
  deletingId: string | null;
  canManage: boolean;
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
  const [environment, setEnvironment] = createSignal("");
  const [revealed, setRevealed] = createSignal<Record<string, boolean>>({});
  const { copy } = useClipboard();

  const environmentOptions = createMemo(() => [
    { value: "", label: "All environments" },
    ...props.environments.map(env => ({ value: env.name, label: env.name })),
  ]);

  const canCreate = createMemo(() => name().trim().length > 0 && !props.isCreating);

  const handleCreate = () => {
    if (!canCreate()) return;

    props.onCreate({
      name: name().trim(),
      scope: scope(),
      environment: environment() || null,
    });
    setName("");
    setScope("client");
    setEnvironment("");
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
      data-testid="project-api-keys-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5"
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
      </div>

      <Show when={props.canManage}>
        <div class="grid gap-2 md:grid-cols-[minmax(180px,1fr)_150px_190px_auto]">
          <Input
            data-testid="api-key-name-input"
            value={name()}
            onInput={event => setName(event.currentTarget.value)}
            placeholder="Key name"
            leftIcon="badge"
          />
          <Select
            value={scope()}
            onChange={value => setScope(value as ApiKey["scope"])}
            options={SCOPE_OPTIONS}
          />
          <Select
            value={environment()}
            onChange={setEnvironment}
            options={environmentOptions()}
          />
          <Button
            data-testid="api-key-create-button"
            type="button"
            size="sm"
            class="h-11"
            disabled={!canCreate()}
            onClick={handleCreate}
          >
            <MIcon name="add" class="text-[16px]" />
            Create
          </Button>
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
