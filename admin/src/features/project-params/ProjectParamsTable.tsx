import { createMediaQuery } from "@solid-primitives/media";
import { For, Show, createEffect, createMemo, createSignal, on } from "solid-js";
import type { ConfigEntry } from "../../types";
import { cn } from "../../shared/lib/utils";
import { MIcon } from "../../shared/ui/icons";
import { getConfigEntryValueError } from "../project-param-edit/config-entry-value";
import { ParameterValueEditor } from "./ParameterValueEditor";
import { buildParameterTree, parameterName, type ParameterTreeNode } from "./parameter-tree";

export type ParameterViewDensity = "comfortable" | "compact";

export interface ProjectParamsTableProps {
  isLoading: boolean;
  projectId: string;
  activeEnvName: string;
  filteredConfig: ConfigEntry[];
  onSelectEntry: (entry: ConfigEntry, opener?: HTMLElement) => void;
  onDeleteEntry: (key: string) => void;
  onUpdateValue?: (entry: ConfigEntry, value: string) => Promise<void> | void;
  updatingKey?: string | null;
  canManage: boolean;
  search: string;
  isReadOnly?: boolean;
  density?: ParameterViewDensity;
}

const collapseStorageKey = (projectId: string, environmentName: string) =>
  `nona_parameter_tree:${projectId}:${environmentName}`;

function readCollapsed(projectId: string, environmentName: string) {
  try {
    const stored = localStorage.getItem(collapseStorageKey(projectId, environmentName));
    const parsed = stored ? JSON.parse(stored) as unknown : [];
    return new Set(Array.isArray(parsed) ? parsed.filter(value => typeof value === "string") : []);
  } catch {
    return new Set<string>();
  }
}

function ParameterRow(props: {
  entry: ConfigEntry;
  table: ProjectParamsTableProps;
  depth: number;
  compact: boolean;
}) {
  const [draft, setDraft] = createSignal("");
  const [submitError, setSubmitError] = createSignal("");
  const [isSubmitting, setIsSubmitting] = createSignal(false);
  const errorId = () => `parameter-value-error-${encodeURIComponent(props.entry.key)}`;
  const valueError = createMemo(() => getConfigEntryValueError(props.entry.contentType, draft()));
  const isDirty = () => draft() !== props.entry.value;

  createEffect(on(
    () => [props.entry.key, props.entry.value] as const,
    ([, value]) => {
      setDraft(value);
      setSubmitError("");
    }
  ));

  const update = async () => {
    if (!props.table.onUpdateValue || !isDirty() || valueError()) return;
    setIsSubmitting(true);
    setSubmitError("");
    try {
      await props.table.onUpdateValue(props.entry, draft());
    } catch (caught) {
      setSubmitError(caught instanceof Error ? caught.message : "The value could not be updated.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const open = (opener: HTMLElement) => props.table.onSelectEntry(props.entry, opener);
  const disabled = () =>
    isSubmitting()
    || props.table.updatingKey === props.entry.key
    || !isDirty()
    || !!valueError();

  return (
    <div
      data-testid={`parameter-row-${props.entry.key}`}
      onClick={event => {
        if ((event.target as HTMLElement).closest("button,input,textarea,select,a")) return;
        open(event.currentTarget);
      }}
      class={cn(
        "border-outline-variant/10 bg-surface-container grid items-center border-b last:border-b-0",
        props.compact
          ? "min-h-12 gap-2 px-3 py-1.5 md:grid-cols-[minmax(11rem,1.1fr)_minmax(12rem,1.5fr)_8rem_auto]"
          : "min-h-16 gap-3 px-4 py-3 md:grid-cols-[minmax(12rem,1.1fr)_minmax(14rem,1.5fr)_9rem_auto]"
      )}
      style={{ "--parameter-depth": props.depth }}
    >
      <button
        type="button"
        onClick={event => open(event.currentTarget)}
        class="min-w-0 cursor-pointer border-0 bg-transparent text-left"
        style={{ "padding-left": `${Math.min(props.depth, 4) * (props.compact ? 14 : 18)}px` }}
        aria-label={`Open details for ${props.entry.key}`}
      >
        <span
          data-testid={`parameter-display-${props.entry.key}`}
          class="text-on-surface block truncate text-[13px] font-bold"
        >
          {parameterName(props.entry.key)}
        </span>
        <span
          data-testid={`parameter-key-${props.entry.key}`}
          class="text-outline block truncate font-mono text-[10px]"
          title={props.entry.key}
        >
          {props.entry.key}
        </span>
      </button>

      <div class="min-w-0" onClick={event => event.stopPropagation()}>
        <ParameterValueEditor
          entry={props.entry}
          value={draft()}
          onChange={value => {
            setDraft(value);
            setSubmitError("");
          }}
          readOnly={props.table.isReadOnly || !props.table.canManage}
          invalid={!!valueError() || !!submitError()}
          describedBy={valueError() || submitError() ? errorId() : undefined}
          compact={props.compact}
        />
        <Show when={valueError() || submitError()}>
          <p
            id={errorId()}
            role="alert"
            aria-live="polite"
            class="text-error mt-1 text-[10px] leading-tight"
          >
            {valueError() || submitError()}
          </p>
        </Show>
      </div>

      <div class="text-outline flex items-center gap-2 text-[10px] uppercase md:flex-col md:items-start md:gap-0.5">
        <span><span class="sr-only">Datatype </span>{props.entry.contentType}</span>
        <span><span class="sr-only">Scope </span>{props.entry.scope}</span>
      </div>

      <div class="flex items-center justify-end gap-1.5">
        <Show when={!props.table.isReadOnly && props.table.canManage}>
          <button
            data-testid={`parameter-update-${props.entry.key}`}
            type="button"
            disabled={disabled()}
            onClick={() => void update()}
            class={cn(
              "inline-flex h-8 cursor-pointer items-center justify-center rounded-lg border-0 px-3 text-[11px] font-bold transition-colors",
              disabled()
                ? "bg-surface-container-high text-outline cursor-not-allowed opacity-60"
                : "bg-primary text-on-primary hover:brightness-105"
            )}
          >
            {isSubmitting() || props.table.updatingKey === props.entry.key ? "Updating…" : "Update"}
          </button>
        </Show>
        <button
          data-testid={`parameter-edit-${props.entry.key}`}
          type="button"
          onClick={event => {
            event.stopPropagation();
            open(event.currentTarget);
          }}
          aria-label={`${props.table.isReadOnly ? "View" : "Edit"} parameter ${props.entry.key}`}
          title={`${props.table.isReadOnly ? "View" : "Edit"} parameter ${props.entry.key}`}
          class="text-outline hover:bg-primary/10 hover:text-primary inline-flex h-8 cursor-pointer items-center gap-1 rounded-lg border-0 bg-transparent px-2 text-[11px] font-semibold"
        >
          <MIcon name="edit" class="text-[16px]" />
          <span class="hidden lg:inline">{props.table.isReadOnly ? "View" : "Edit"}</span>
        </button>
        <Show when={!props.table.isReadOnly && props.table.canManage}>
          <button
            data-testid={`parameter-delete-${props.entry.key}`}
            type="button"
            onClick={() => props.table.onDeleteEntry(props.entry.key)}
            aria-label={`Delete parameter ${props.entry.key}`}
            title={`Delete parameter ${props.entry.key}`}
            class="text-outline hover:bg-error/10 hover:text-error inline-flex h-8 cursor-pointer items-center gap-1 rounded-lg border-0 bg-transparent px-2 text-[11px] font-semibold"
          >
            <MIcon name="delete_outline" class="text-[16px]" />
            <span class="hidden xl:inline">Delete</span>
          </button>
        </Show>
      </div>
    </div>
  );
}

function TreeBranch(props: {
  node: ParameterTreeNode;
  table: ProjectParamsTableProps;
  compact: boolean;
  collapsed: Set<string>;
  toggle: (id: string) => void;
  searching: boolean;
}) {
  const isGroup = () => props.node.children.length > 0;
  const isCollapsed = () => !props.searching && props.collapsed.has(props.node.id);

  return (
    <>
      <Show
        when={isGroup()}
        fallback={
          <Show when={props.node.entry}>
            {entry => (
              <ParameterRow
                entry={entry()}
                table={props.table}
                depth={props.node.legacy ? 0 : props.node.depth}
                compact={props.compact}
              />
            )}
          </Show>
        }
      >
        <button
          data-testid={`parameter-group-${props.node.id}`}
          type="button"
          aria-expanded={!isCollapsed()}
          onClick={() => props.toggle(props.node.id)}
          class={cn(
            "border-outline-variant/10 bg-surface-container-lowest text-on-surface flex w-full cursor-pointer items-center gap-2 border-b px-3 text-left font-semibold",
            props.compact ? "h-9 text-[12px]" : "h-11 text-[13px]"
          )}
          style={{ "padding-left": `${props.node.depth * (props.compact ? 14 : 18) + 12}px` }}
        >
          <MIcon name={isCollapsed() ? "chevron_right" : "expand_more"} class="text-outline text-[17px]" />
          <span>{props.node.label}</span>
          <span class="text-outline text-[10px] font-normal">{props.node.count}</span>
        </button>
        <Show when={!isCollapsed()}>
          <Show when={props.node.entry}>
            {entry => (
              <ParameterRow
                entry={entry()}
                table={props.table}
                depth={props.node.depth + 1}
                compact={props.compact}
              />
            )}
          </Show>
          <For each={props.node.children}>
            {child => (
              <TreeBranch
                node={child}
                table={props.table}
                compact={props.compact}
                collapsed={props.collapsed}
                toggle={props.toggle}
                searching={props.searching}
              />
            )}
          </For>
        </Show>
      </Show>
    </>
  );
}

export function ProjectParamsTable(props: ProjectParamsTableProps) {
  const isMobile = createMediaQuery("(max-width: 767px)");
  const isCompact = () => props.density === "compact";
  const tree = createMemo(() => buildParameterTree(props.filteredConfig));
  const [collapsed, setCollapsed] = createSignal<Set<string>>(new Set());

  createEffect(on(
    () => [props.projectId, props.activeEnvName] as const,
    ([projectId, environmentName]) => setCollapsed(readCollapsed(projectId, environmentName))
  ));

  const toggle = (id: string) => {
    const next = new Set(collapsed());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    setCollapsed(next);
    try {
      localStorage.setItem(
        collapseStorageKey(props.projectId, props.activeEnvName),
        JSON.stringify([...next])
      );
    } catch {
      // Storage is optional; the tree remains usable when it is unavailable.
    }
  };

  return (
    <div
      data-testid="parameter-table"
      data-density={props.density ?? "compact"}
      class="border-outline-variant/15 bg-surface-container overflow-hidden rounded-xl border"
    >
      <Show when={!isMobile()}>
        <div
          aria-hidden="true"
          class={cn(
            "border-outline-variant/15 bg-surface-container-lowest text-outline grid border-b px-4 text-[9px] font-bold tracking-widest uppercase",
            isCompact()
              ? "h-8 grid-cols-[minmax(11rem,1.1fr)_minmax(12rem,1.5fr)_8rem_auto] items-center gap-2"
              : "h-10 grid-cols-[minmax(12rem,1.1fr)_minmax(14rem,1.5fr)_9rem_auto] items-center gap-3"
          )}
        >
          <span>Parameter</span>
          <span>Value</span>
          <span>Details</span>
          <span class="text-right">Actions</span>
        </div>
      </Show>

      <Show when={props.isLoading}>
        <For each={[1, 2, 3]}>
          {() => <div class={cn("skeleton border-outline-variant/10 border-b", isCompact() ? "h-12" : "h-16")} />}
        </For>
      </Show>

      <Show when={!props.isLoading}>
        <For each={tree()}>
          {node => (
            <TreeBranch
              node={node}
              table={props}
              compact={isCompact()}
              collapsed={collapsed()}
              toggle={toggle}
              searching={props.search.trim().length > 0}
            />
          )}
        </For>
      </Show>

      <Show when={!props.isLoading && props.search && props.filteredConfig.length === 0}>
        <div class="text-on-surface-variant px-4 py-8 text-center text-sm">
          No parameters match “{props.search}”.
        </div>
      </Show>

    </div>
  );
}
