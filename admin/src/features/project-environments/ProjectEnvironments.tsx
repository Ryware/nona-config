import { For, Show, createSignal } from "solid-js";
import { Button } from "../../shared/ui/button";
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
    <section
      id="environments"
      data-testid="project-environments-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5 scroll-mt-20"
    >
      <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <p class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase">
            <MIcon name="dns" class="text-[15px]" />
            Environments
          </p>
          <p class="text-on-surface-variant mt-1 text-xs">
            Choose the active environment for this project and manage the available environments.
          </p>
        </div>

        <Show when={props.canManage}>
          <button
            data-testid="project-add-environment-button"
            type="button"
            onClick={() => props.setShowEnvForm(!props.showEnvForm)}
            class="bg-primary text-on-primary inline-flex h-10 cursor-pointer items-center gap-1.5 self-start rounded-lg border-0 px-4 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] md:self-auto"
          >
            <MIcon name="add" class="text-[17px]" />
            Add Environment
          </button>
        </Show>
      </div>

      <Show when={props.canManage && props.showEnvForm}>
        <form
          onSubmit={handleSubmit}
          class="bg-surface-container-low border-outline-variant/15 animate-fade-in rounded-2xl border p-6 shadow-sm"
        >
          <h3 class="font-headline text-on-surface mb-6 text-xs font-bold tracking-wider uppercase">
            New Environment
          </h3>
          <div class="group">
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
          <div class="mt-6 flex justify-end gap-3">
            <Button
              data-testid="environment-create-submit-button"
              type="submit"
              disabled={props.createEnvPending}
            >
              <MIcon name="add" class="text-[16px]" />
              {props.createEnvPending ? "Creating..." : "Create"}
            </Button>
            <Button
              data-testid="environment-create-cancel-button"
              type="button"
              variant="outline"
              onClick={() => props.setShowEnvForm(false)}
            >
              <MIcon name="close" class="text-[16px]" />
              Cancel
            </Button>
          </div>
        </form>
      </Show>

      <Show
        when={props.environments.length > 0}
        fallback={
          <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
            No environments yet.
          </div>
        }
      >
        <div class="space-y-2">
          <For each={props.environments}>
            {env => (
              <div
                class={`bg-surface-container grid gap-3 rounded-xl px-4 py-3 md:grid-cols-[minmax(180px,1fr)_auto] md:items-center ${
                  props.activeEnvName === env.name ? "ring-1 ring-primary/20" : ""
                }`}
              >
                <button
                  data-testid={`environment-tab-${env.name}`}
                  type="button"
                  onClick={() => props.setActiveEnvName(env.name)}
                  class="flex min-w-0 items-start gap-3 rounded-lg border-0 bg-transparent p-0 text-left"
                >
                  <div class="min-w-0">
                    <div class="flex flex-wrap items-center gap-2">
                      <span class="text-on-surface truncate font-mono text-[13px] font-bold">
                        {env.name}
                      </span>
                      <Show when={props.activeEnvName === env.name}>
                        <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[11px] font-bold">
                          Active
                        </span>
                      </Show>
                    </div>
                    <p class="text-on-surface-variant mt-1 text-[12px]">
                      Active release: {env.activeReleaseVersion ?? "none"}
                    </p>
                  </div>
                </button>

                <div class="flex items-center justify-end gap-2">
                  <Show when={props.activeEnvName !== env.name}>
                    <button
                      type="button"
                      onClick={() => props.setActiveEnvName(env.name)}
                      class="bg-surface-container-high text-on-surface hover:bg-surface-bright inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-3 text-[12px] font-semibold"
                    >
                      <MIcon name="check_circle" class="text-[15px]" />
                      Set Active
                    </button>
                  </Show>
                  <Show when={props.canManage}>
                    <button
                      data-testid={`environment-delete-${env.name}`}
                      type="button"
                      onClick={() => props.onDeleteEnv(env.name)}
                      class="bg-error-container/10 text-error hover:bg-error-container/20 inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border-0 px-3 text-[12px] font-semibold"
                      title={`Delete environment ${env.name}`}
                    >
                      <MIcon name="delete" class="text-[15px]" />
                      Delete
                    </button>
                  </Show>
                </div>
              </div>
            )}
          </For>
        </div>
      </Show>
    </section>
  );
}
