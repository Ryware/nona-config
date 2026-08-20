import { makePersisted } from "@solid-primitives/storage";
import type { JSX } from "solid-js";
import { createSignal, Show } from "solid-js";
import { cn } from "../../shared/lib/utils";
import { MIcon } from "../../shared/ui/icons";
import { Input } from "../../shared/ui/input";
import type { ConfigEntry } from "../../types";
import {
  ProjectParamsTable,
  type ParameterViewDensity,
  type ProjectParamsTableProps
} from "./ProjectParamsTable";

interface ProjectParamsTabProps {
  activeEnvName: string;
  configEntries: ConfigEntry[];
  filteredConfig: ConfigEntry[];
  isLoading: boolean;
  paramSearch: string;
  onParamSearch: (q: string) => void;
  onToggleBulkImport: () => void;
  onAddParameter: (opener: HTMLElement) => void;
  bulkImportPanel?: JSX.Element;
  canManage: boolean;
  isReadOnly?: boolean;
  viewingReleaseVersion?: string;
  table: ProjectParamsTableProps;
}

export const PARAMETER_DENSITY_STORAGE_KEY = "nona_parameter_density";

function parseParameterDensity(value: string): ParameterViewDensity {
  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed === "comfortable" || parsed === "compact" ? parsed : "compact";
  } catch {
    return "compact";
  }
}

export function ProjectParamsTab(props: ProjectParamsTabProps) {
  const [density, setDensity] = makePersisted(
    // eslint-disable-next-line solid/reactivity -- makePersisted intentionally wraps the signal.
    createSignal<ParameterViewDensity>("compact"),
    {
      deserialize: parseParameterDensity,
      name: PARAMETER_DENSITY_STORAGE_KEY
    }
  );
  const isCompact = () => density() === "compact";

  return (
    <section
      id="parameters"
      data-testid="project-parameters-section"
      data-density={density()}
      class={cn(
        "bg-surface-container-low border-outline-variant/15 border scroll-mt-20",
        isCompact() ? "space-y-2.5 rounded-xl p-3 sm:p-4" : "space-y-4 rounded-2xl p-5"
      )}
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

          <Show when={props.activeEnvName}>
            <div
              role="group"
              aria-label="Parameter spacing"
              class="border-outline-variant/20 bg-surface-container-high flex shrink-0 items-center rounded-lg border p-1"
            >
              <button
                data-testid="parameters-density-comfortable"
                type="button"
                aria-label="Comfortable spacing"
                aria-pressed={density() === "comfortable"}
                title="Comfortable spacing"
                onClick={() => setDensity("comfortable")}
                class={cn(
                  "inline-flex h-9 w-9 cursor-pointer items-center justify-center rounded-md border-0 transition-colors md:h-8 md:w-8",
                  density() === "comfortable"
                    ? "bg-surface-container-lowest text-primary shadow-sm"
                    : "text-outline hover:bg-surface-bright hover:text-on-surface bg-transparent"
                )}
              >
                <MIcon name="density_medium" class="text-[17px]" />
              </button>
              <button
                data-testid="parameters-density-compact"
                type="button"
                aria-label="Compact spacing"
                aria-pressed={density() === "compact"}
                title="Compact spacing"
                onClick={() => setDensity("compact")}
                class={cn(
                  "inline-flex h-9 w-9 cursor-pointer items-center justify-center rounded-md border-0 transition-colors md:h-8 md:w-8",
                  density() === "compact"
                    ? "bg-surface-container-lowest text-primary shadow-sm"
                    : "text-outline hover:bg-surface-bright hover:text-on-surface bg-transparent"
                )}
              >
                <MIcon name="density_small" class="text-[17px]" />
              </button>
            </div>
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
                onClick={event => props.onAddParameter(event.currentTarget)}
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

      <Show when={!props.activeEnvName}>
        <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
          Select an active environment from the header to view its parameters.
        </div>
      </Show>

      <Show when={props.activeEnvName && (props.isLoading || props.filteredConfig.length > 0)}>
        <ProjectParamsTable
          {...props.table}
          density={density()}
          search={props.paramSearch}
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
