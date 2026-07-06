import { Show } from "solid-js";
import { truncate } from "./audit-kind";

interface DiffFieldProps {
  label: string;
  oldValue?: string;
  newValue?: string;
  mono?: boolean;
  italic?: boolean;
  maxLength?: number;
  /** Tailwind color class for the new value. Defaults to "text-on-surface". */
  newColor?: string;
}

/**
 * Renders a single diff field row: label + optional old value → new value.
 * Used for value / name / description diffs in audit log entries.
 */
export function DiffField(props: DiffFieldProps) {
  const max = () => props.maxLength ?? 28;

  const oldClasses = () => {
    let cls = "text-[10.5px] text-error/70 line-through";
    if (props.mono) cls += " font-mono";
    if (props.italic) cls += " italic";
    return cls;
  };

  const newClasses = () => {
    const color = props.newColor ?? "text-on-surface";
    let cls = `text-[10.5px] ${color}`;
    if (props.mono) cls += " font-mono";
    if (props.italic) cls += " italic";
    return cls;
  };

  return (
    <div class="flex items-baseline gap-2">
      <span class="text-[9.5px] font-medium text-outline/40 w-10 shrink-0 uppercase tracking-wide">
        {props.label}
      </span>
      <div class="flex items-baseline gap-1.5 flex-wrap">
        <Show when={props.oldValue !== undefined && props.oldValue !== ""}>
          <span class={oldClasses()} title={props.oldValue}>
            {truncate(props.oldValue, max())}
          </span>
        </Show>
        <Show when={props.oldValue !== undefined && props.newValue !== undefined}>
          <span class="text-outline/30 text-[10px]">→</span>
        </Show>
        <Show when={props.newValue !== undefined}>
          <span class={newClasses()} title={props.newValue}>
            {truncate(props.newValue, max())}
          </span>
        </Show>
      </div>
    </div>
  );
}
