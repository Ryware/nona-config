import type { JSX } from "solid-js";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import { Show } from "solid-js";
import type {
  ConfigEntry,
  ConfigEntryVersion,
  CreateConfigEntryRequest,
  Environment
} from "../../types";
import { ProjectParamCreateForm } from "../project-param-edit/ProjectParamCreateForm";
import { ProjectParamsTable } from "./ProjectParamsTable";

interface ProjectParamsTabProps {
  activeEnvName: string;
  environments: Environment[];
  configEntries: ConfigEntry[];
  filteredConfig: ConfigEntry[];
  editingEntry: ConfigEntry | null;
  isLoading: boolean;
  projectId: string;
  paramSearch: string;
  onParamSearch: (q: string) => void;
  onToggleBulkImport: () => void;
  onToggleConfigForm: () => void;
  showConfigForm: boolean;
  bulkImportPanel?: JSX.Element;
  onCancelCreate: () => void;
  onSubmitCreate: (data: {
    key: string;
    value: string;
    contentType: CreateConfigEntryRequest["contentType"];
    scope: CreateConfigEntryRequest["scope"];
    description: string;
  }) => void;
  isCreatePending: boolean;
  onSelectEntry: (entry: ConfigEntry) => void;
  onShareEntry: (entry: ConfigEntry) => void;
  onDeleteEntry: (key: string) => void;
  canManage: boolean;
  copiedKey: string | null;
  onCopyValue: (key: string, value: string) => void;
  getParamMeta: (
    proj: string,
    env: string,
    key: string
  ) => { displayName: string; description: string };
  initialDescription: string;
  onCloseEntry: () => void;
  onSaveSettings: (data: { value: string; description: string }) => void;
  isSaving: boolean;
  historyVersions: ConfigEntryVersion[];
  isHistoryLoading: boolean;
  isRollingBack: boolean;
  onRollbackVersion: (version: ConfigEntryVersion) => void;
  isReadOnly?: boolean;
  viewingReleaseVersion?: string;
}

export function ProjectParamsTab(props: ProjectParamsTabProps) {
  return (
    <section
      id="parameters"
      data-testid="project-parameters-section"
      class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5 scroll-mt-20"
    >
      <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <p
            data-testid="project-parameters-heading"
            class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
          >
            <MIcon name="tune" class="text-[15px]" />
            Parameters
          </p>
          <p class="text-on-surface-variant mt-1 text-xs">
            <Show
              when={props.isReadOnly && props.viewingReleaseVersion}
              fallback={
                <>
                  Manage configuration parameters for the active environment
                  {props.activeEnvName ? `: ${props.activeEnvName}.` : "."}
                </>
              }
            >
              View the parameters captured in release {props.viewingReleaseVersion}
              {props.activeEnvName ? ` for ${props.activeEnvName}.` : "."}
            </Show>
          </p>
        </div>

        <div class="flex w-full min-w-0 items-center justify-end gap-2 md:w-auto md:flex-row md:flex-wrap md:items-center md:justify-end">
          <Show when={props.activeEnvName && props.configEntries.length > 0}>
            <Input
              data-testid="parameters-search-input"
              type="text"
              placeholder="Search parameters..."
              value={props.paramSearch}
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                props.onParamSearch(e.currentTarget.value)
              }
              class="h-10 min-w-0 flex-1 md:w-72"
              leftIcon="search"
              wrapperStyle="min-w-0 flex-1 md:w-auto md:flex-none"
            />
          </Show>

          <Show when={!props.isReadOnly && props.canManage && props.activeEnvName}>
            <div class="flex flex-wrap justify-end gap-2">
              <button
                data-testid="project-bulk-import-button"
                type="button"
                onClick={() => props.onToggleBulkImport()}
                aria-label="Bulk Import"
                title="Bulk Import"
                class="bg-surface-container-high text-on-surface-variant hover:bg-surface-bright hover:text-on-surface inline-flex h-10 w-10 cursor-pointer items-center justify-center rounded-lg border-0 px-0 text-[13px] font-semibold transition-all active:scale-[0.98] md:w-auto md:gap-1.5 md:px-4"
              >
                <MIcon name="publish" class="text-[17px]" />
                <span class="hidden md:inline">Bulk Import</span>
              </button>
              <button
                data-testid="project-add-parameter-button"
                type="button"
                onClick={() => props.onToggleConfigForm()}
                aria-label="Add Parameter"
                title="Add Parameter"
                class="bg-primary text-on-primary inline-flex h-10 w-10 cursor-pointer items-center justify-center self-end rounded-lg border-0 px-0 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98] md:w-auto md:gap-1.5 md:px-4 md:self-auto"
              >
                <MIcon name="add" class="text-[16px]" />
                <span class="hidden md:inline">Add Parameter</span>
              </button>
            </div>
          </Show>
        </div>
      </div>

      {props.bulkImportPanel}

      <Show when={!props.isReadOnly && props.canManage && props.activeEnvName && props.showConfigForm}>
        <ProjectParamCreateForm
          onCancel={props.onCancelCreate}
          onSubmit={props.onSubmitCreate}
          isPending={props.isCreatePending}
          existingEntries={props.configEntries}
        />
      </Show>

      <Show when={!props.activeEnvName}>
        <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
          Select an active environment from the header to view its parameters.
        </div>
      </Show>

      <Show when={props.activeEnvName && (props.isLoading || props.filteredConfig.length > 0)}>
        <ProjectParamsTable
          isLoading={props.isLoading}
          projectId={props.projectId}
          activeEnvName={props.activeEnvName}
          filteredConfig={props.filteredConfig}
          editingEntry={props.editingEntry}
          onSelectEntry={props.onSelectEntry}
          onShareEntry={props.onShareEntry}
          onDeleteEntry={props.onDeleteEntry}
          canManage={props.canManage}
          copiedKey={props.copiedKey}
          onCopyValue={props.onCopyValue}
          getParamMeta={props.getParamMeta}
          initialDescription={props.initialDescription}
          onCloseEntry={props.onCloseEntry}
          onSaveSettings={props.onSaveSettings}
          isSaving={props.isSaving}
          historyVersions={props.historyVersions}
          isHistoryLoading={props.isHistoryLoading}
          isRollingBack={props.isRollingBack}
          onRollbackVersion={props.onRollbackVersion}
          search={props.paramSearch}
          isReadOnly={props.isReadOnly}
          releaseVersion={props.viewingReleaseVersion}
        />
      </Show>

      <Show when={props.activeEnvName && !props.isLoading && props.filteredConfig.length === 0}>
        <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
          <Show
            when={props.isReadOnly && props.viewingReleaseVersion}
            fallback={<>No parameters yet for this environment</>}
          >
            No parameters were captured in release {props.viewingReleaseVersion}
          </Show>
        </div>
      </Show>
    </section>
  );
}
