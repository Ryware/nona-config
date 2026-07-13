import { For, Show, createSignal } from "solid-js";
import { MIcon } from "../../shared/ui/icons";
import type { Environment } from "../../types";
import { FormField } from "../../widgets/auth-shell/FormField";

interface ProjectEnvironmentsProps {
  environments: Environment[];
  activeEnvName: string;
  setActiveEnvName: (v: string) => void;
  onCreateEnv: (envName: string) => void;
  onDeleteEnv: (envName: string) => void;
  showEnvForm: boolean;
  setShowEnvForm: (v: boolean) => void;
  createEnvPending: boolean;
  canManage: boolean;
}

export function ProjectEnvironments(props: ProjectEnvironmentsProps) {
  const [envName, setEnvName] = createSignal("");
  const [createError, setCreateError] = createSignal("");

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    const trimmed = envName().trim();
    if (!trimmed) {
      setCreateError("Environment name is required.");
      return;
    }
    if (props.environments.some(env => env.name === trimmed)) {
      setCreateError("Environment name already exists.");
      return;
    }
    setCreateError("");
    props.onCreateEnv(trimmed);
    setEnvName("");
  };

  return (
    <div class="space-y-4">
      {/* Environments Tabs */}
      <div class="space-y-3">
        <p class="text-on-surface-variant text-[12px] font-semibold">Environments</p>
        <Show
          when={props.environments.length > 0}
          fallback={<span class="text-on-surface-variant block text-sm">No environments yet</span>}
        >
          <div class="flex flex-wrap gap-2.5">
            <For each={props.environments}>
              {env => (
                <button
                  data-testid={`environment-tab-${env.name}`}
                  type="button"
                  class={`group focus-visible:ring-primary/40 flex cursor-pointer items-center gap-2 rounded-xl border px-4 py-2 font-mono text-[12px] transition-all duration-300 outline-none focus-visible:ring-2 ${
                    props.activeEnvName === env.name
                      ? "border-primary/20 bg-primary/10 text-primary font-bold shadow-[0_0_15px_rgba(99,102,241,0.05)]"
                      : "border-outline-variant/10 bg-surface-container-low text-on-surface-variant hover:bg-surface-container-high/60 hover:text-on-surface"
                  }`}
                  title={env.name}
                  onClick={() =>
                    props.setActiveEnvName(props.activeEnvName === env.name ? "" : env.name)
                  }
                >
                  <span class="max-w-50 truncate">{env.name}</span>
                  <Show when={props.canManage}>
                    <span
                      data-testid={`environment-delete-${env.name}`}
                      role="button"
                      tabindex="0"
                      onClick={e => {
                        e.stopPropagation();
                        props.onDeleteEnv(env.name);
                      }}
                      aria-label={`Delete environment ${env.name}`}
                      class="text-outline hover:text-error ml-1 flex cursor-pointer items-center justify-center border-0 bg-transparent p-0 opacity-40 transition-opacity group-hover:opacity-100 focus:opacity-100"
                    >
                      <MIcon name="close" class="text-[14px]" />
                    </span>
                  </Show>
                </button>
              )}
            </For>
          </div>
        </Show>
      </div>

      {/* Create env form */}
      <Show when={props.canManage && props.showEnvForm}>
        <form
          onSubmit={handleSubmit}
          class="bg-surface-container-low border-outline-variant/15 animate-fade-in flex flex-col items-end gap-4 rounded-2xl border p-6 shadow-sm sm:flex-row"
        >
          <div class="group w-full flex-1">
            <FormField
              id="env-name"
              label="Environment Name *"
              type="text"
              placeholder="production"
              value={envName()}
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) => {
                setEnvName(e.currentTarget.value);
                if (createError()) setCreateError("");
              }}
              required
              leftIcon="dns"
              testId="environment-name-input"
            />
            <Show when={createError()}>
              <p class="text-error mt-2 text-[11px] font-bold">{createError()}</p>
            </Show>
          </div>
          <div class="flex w-full gap-2 sm:w-auto">
            <button
              data-testid="environment-create-submit-button"
              type="submit"
              disabled={props.createEnvPending}
              class="bg-primary text-on-primary flex-1 cursor-pointer rounded-lg border-0 px-4 py-2.5 text-[13px] font-semibold transition-all hover:brightness-105 disabled:opacity-50 sm:flex-none"
            >
              {props.createEnvPending ? "Creating…" : "Create"}
            </button>
            <button
              data-testid="environment-create-cancel-button"
              type="button"
              onClick={() => props.setShowEnvForm(false)}
              class="text-on-surface-variant bg-surface-container-high hover:bg-surface-bright flex-1 cursor-pointer rounded-lg border-0 px-4 py-2.5 text-[13px] font-semibold transition-all sm:flex-none"
            >
              Cancel
            </button>
          </div>
        </form>
      </Show>
    </div>
  );
}
