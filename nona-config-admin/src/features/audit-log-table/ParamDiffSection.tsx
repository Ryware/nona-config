import { Show } from "solid-js";
import type { AuditEntry } from "../../pages/audit-logs/types";
import { isCreateAction, isDeleteAction, isUpdateAction } from "./audit-kind";
import { DiffField } from "./DiffField";

interface ParamDiffSectionProps {
  entry: AuditEntry;
}

/**
 * Renders the parameter-level diff block (value / name / description changes)
 * for Created, Updated, and Deleted Parameter audit actions.
 */
export function ParamDiffSection(props: ParamDiffSectionProps) {
  return (
    <div class="mt-2 space-y-1">
      <Show when={isUpdateAction(props.entry.action)}>
        <UpdateDiff entry={props.entry} />
      </Show>

      <Show when={isCreateAction(props.entry.action) && props.entry.newValue}>
        <DiffField
          label="value"
          newValue={props.entry.newValue}
          newColor="text-success"
          mono
          maxLength={36}
        />
      </Show>

      <Show when={isDeleteAction(props.entry.action) && props.entry.oldValue}>
        <DiffField
          label="value"
          oldValue={props.entry.oldValue}
          mono
          maxLength={36}
        />
      </Show>
    </div>
  );
}

/** Inline diff for update actions — shows changed value, name, and description. */
function UpdateDiff(props: { entry: AuditEntry }) {
  const valueChanged = () => props.entry.oldValue !== props.entry.newValue;
  const nameChanged = () =>
    props.entry.oldDisplayName !== undefined &&
    props.entry.oldDisplayName !== props.entry.displayName;
  const descChanged = () =>
    props.entry.oldDescription !== undefined &&
    props.entry.oldDescription !== props.entry.description;
  const hasAnyDiff = () => valueChanged() || nameChanged() || descChanged();

  return (
    <>
      <Show when={valueChanged()}>
        <DiffField
          label="value"
          oldValue={props.entry.oldValue}
          newValue={props.entry.newValue}
          mono
        />
      </Show>

      <Show when={nameChanged()}>
        <DiffField
          label="name"
          oldValue={props.entry.oldDisplayName}
          newValue={props.entry.displayName}
        />
      </Show>

      <Show when={descChanged()}>
        <DiffField
          label="desc"
          oldValue={props.entry.oldDescription}
          newValue={props.entry.description}
          italic
          maxLength={22}
        />
      </Show>

      <Show when={!hasAnyDiff()}>
        <span class="text-[10.5px] text-outline/40 italic">settings updated</span>
      </Show>
    </>
  );
}
