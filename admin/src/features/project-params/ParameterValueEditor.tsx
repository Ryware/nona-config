import { Show } from "solid-js";
import { cn } from "../../shared/lib/utils";
import type { ConfigEntry } from "../../types";

interface ParameterValueEditorProps {
  entry: ConfigEntry;
  value: string;
  onChange?: (value: string) => void;
  readOnly?: boolean;
  invalid?: boolean;
  describedBy?: string;
  compact?: boolean;
  testId?: string;
}

export function ParameterValueEditor(props: ParameterValueEditorProps) {
  const inputClass = () => cn(
    "border-outline-variant/25 bg-surface-container-lowest text-on-surface focus:border-primary min-w-0 w-full rounded-lg border font-mono outline-none transition-colors",
    props.compact ? "h-8 px-2 text-[12px]" : "h-10 px-3 text-[13px]",
    props.invalid && "border-error focus:border-error"
  );

  return (
    <Show
      when={!props.readOnly}
      fallback={
        <span
          data-testid={`parameter-value-${props.entry.key}`}
          class="text-on-surface-variant block min-w-0 truncate font-mono text-[13px]"
          title={props.value}
        >
          {props.value}
          <Show when={props.entry.contentType === "number" && props.entry.unit}>
            <span class="text-outline ml-1 font-sans">{props.entry.unit}</span>
          </Show>
        </span>
      }
    >
      <Show
        when={props.entry.contentType === "boolean"}
        fallback={
          <label class="flex min-w-0 items-center gap-2">
            <input
              data-testid={props.testId ?? `parameter-value-input-${props.entry.key}`}
              type="text"
              inputmode={props.entry.contentType === "number" ? "decimal" : undefined}
              value={props.value}
              onInput={event => props.onChange?.(event.currentTarget.value)}
              aria-label={`Value for ${props.entry.key}`}
              aria-invalid={props.invalid}
              aria-describedby={props.describedBy}
              class={inputClass()}
            />
            <Show when={props.entry.contentType === "number" && props.entry.unit}>
              <span class="text-outline shrink-0 text-[12px]" aria-label={`Unit ${props.entry.unit}`}>
                {props.entry.unit}
              </span>
            </Show>
          </label>
        }
      >
        <button
          data-testid={props.testId ?? `parameter-value-input-${props.entry.key}`}
          type="button"
          role="switch"
          aria-checked={props.value === "true"}
          aria-label={`Value for ${props.entry.key}`}
          aria-describedby={props.describedBy}
          onClick={() => props.onChange?.(props.value === "true" ? "false" : "true")}
          class={cn(
            "relative inline-flex cursor-pointer items-center rounded-full border-0 p-0 transition-colors",
            props.compact ? "h-6 w-10" : "h-7 w-12",
            props.value === "true" ? "bg-primary" : "bg-outline-variant/40"
          )}
        >
          <span
            aria-hidden="true"
            class={cn(
              "bg-on-primary block rounded-full shadow-sm transition-transform",
              props.compact ? "h-4 w-4" : "h-5 w-5",
              props.value === "true"
                ? props.compact ? "translate-x-5" : "translate-x-6"
                : "translate-x-1"
            )}
          />
          <span class="sr-only">{props.value === "true" ? "True" : "False"}</span>
        </button>
      </Show>
    </Show>
  );
}
