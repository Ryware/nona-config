import { createSignal, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import { FormField } from "../../../widgets/auth-shell/FormField";
import type { Project } from "../../../types";

interface ProjectCreateFormProps {
  onCancel: () => void;
  onSubmit: (data: { name: string; description?: string }) => void;
  isPending: boolean;
  projects: Project[];
}

const PROJECT_NAME_PATTERN = /^[a-zA-Z0-9-]+$/;

export function ProjectCreateForm(props: ProjectCreateFormProps) {
  const [name, setName] = createSignal("");
  const [description, setDescription] = createSignal("");
  const [createError, setCreateError] = createSignal("");

  const handleSubmit = (e: Event) => {
    e.preventDefault();

    const trimmedName = name().trim();
    if (!trimmedName) {
      setCreateError("Project name is required.");
      return;
    }

    if (props.projects.some(project => project.name === trimmedName)) {
      setCreateError("Project name already exists.");
      return;
    }

    if (!PROJECT_NAME_PATTERN.test(trimmedName)) {
      setCreateError("Use only letters, numbers, and hyphens.");
      return;
    }

    setCreateError("");
    props.onSubmit({ name: trimmedName, description: description().trim() || undefined });
  };

  return (
    <div
      data-testid="project-create-form"
      class="bg-surface-container-low border-outline-variant/15 animate-fade-in rounded-2xl border p-6 shadow-sm"
    >
      <h3 class="font-headline text-on-surface mb-6 text-xs font-bold tracking-wider uppercase">
        New Project
      </h3>
      <form onSubmit={handleSubmit} class="grid gap-6 md:grid-cols-2">
        <div>
          <FormField
            id="project-name"
            label="Project Name *"
            type="text"
            placeholder="my-project"
            value={name()}
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) => {
              setName(e.currentTarget.value);
              if (createError()) setCreateError("");
            }}
            required
            leftIcon="folder"
            testId="project-name-input"
          />
          <p class="text-outline mt-2 text-[10px] font-medium tracking-wider uppercase">
            Use letters, numbers, and hyphens only.
          </p>
          <Show when={createError()}>
            <p class="text-error mt-2 text-[11px] font-bold">{createError()}</p>
          </Show>
        </div>
        <FormField
          id="project-description"
          label="Description"
          type="text"
          placeholder="Optional description"
          value={description()}
          onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
            setDescription(e.currentTarget.value)
          }
          leftIcon="notes"
          testId="project-description-input"
        />
        <div class="flex gap-3 md:col-span-2">
          <button
            data-testid="project-create-submit-button"
            type="submit"
            disabled={props.isPending}
            class="bg-primary text-on-primary flex cursor-pointer items-center gap-2 rounded-lg border-0 px-6 py-2.5 text-xs font-bold tracking-wider uppercase shadow-md transition-all hover:brightness-105 active:scale-[0.98] disabled:opacity-50"
          >
            <MIcon name="add" class="text-[16px]" />
            {props.isPending ? "Creating…" : "Create Project"}
          </button>
          <button
            data-testid="project-create-cancel-button"
            type="button"
            onClick={() => props.onCancel()}
            class="text-on-surface-variant bg-surface-container-high hover:bg-surface-bright cursor-pointer rounded-lg border-0 px-6 py-2.5 text-xs font-bold tracking-wider uppercase transition-all"
          >
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
