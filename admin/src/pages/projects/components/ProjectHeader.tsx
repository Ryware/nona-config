import { Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import type { Project } from "../../../types";

interface ProjectHeaderProps {
  project: Project | undefined;
  showConfigForm: boolean;
  showBulkImport: boolean;
  showEnvForm: boolean;
  canManageProject: boolean;
  onToggleEnvForm: () => void;
  onToggleBulkImport: () => void;
  onToggleConfigForm: () => void;
}

export function ProjectHeader(props: ProjectHeaderProps) {
  return (
    <Show
      when={props.project}
      fallback={
        <div class="flex items-center justify-between gap-4">
          <div>
            <h2 class="font-headline text-on-surface text-[17px] font-bold tracking-tight">
              Projects
            </h2>
          </div>
        </div>
      }
    >
      <div class="flex flex-col justify-between gap-4 md:flex-row md:items-center">
        <div>
          <h2
            data-testid="project-detail-heading"
            class="font-headline text-on-surface text-[17px] font-bold tracking-tight"
          >
            {props.project!.name}
          </h2>
          <p class="text-on-surface-variant mt-1 text-[12.5px]">
            {props.project!.description || "No description provided."}
          </p>
        </div>
        <Show when={props.canManageProject}>
          <div class="flex flex-wrap gap-2">
            <button
              data-testid="project-add-environment-button"
              onClick={() => props.onToggleEnvForm()}
              class="bg-surface-container-high text-on-surface-variant hover:bg-surface-bright hover:text-on-surface flex cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all active:scale-[0.98]"
            >
              <MIcon name="add" class="text-[17px]" />
              Add Environment
            </button>
            <button
              data-testid="project-bulk-import-button"
              onClick={() => props.onToggleBulkImport()}
              class="bg-surface-container-high text-on-surface-variant hover:bg-surface-bright hover:text-on-surface flex cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all active:scale-[0.98]"
            >
              <MIcon name="publish" class="text-[17px]" />
              Bulk Import
            </button>
            <button
              data-testid="project-add-parameter-button"
              onClick={() => props.onToggleConfigForm()}
              class="bg-primary text-on-primary flex cursor-pointer items-center gap-1.5 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98]"
            >
              <MIcon name="add" class="text-[17px]" />
              Add Parameter
            </button>
          </div>
        </Show>
      </div>
    </Show>
  );
}
