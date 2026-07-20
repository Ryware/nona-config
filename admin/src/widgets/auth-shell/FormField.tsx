import { type Component, createSignal, Show } from "solid-js";
import type { JSX } from "solid-js/h/jsx-runtime";
import { Input } from "../../shared/ui/input";

interface FormFieldProps {
  id: string;
  label: string;
  type?: string;
  placeholder: string;
  value: string;
  onInput: (e: InputEvent & { currentTarget: HTMLInputElement }) => void;
  onKeyDown?:
    | JSX.EventHandlerUnion<
        HTMLInputElement,
        KeyboardEvent,
        JSX.EventHandler<HTMLInputElement, KeyboardEvent>
      >
    | undefined;
  required?: boolean;
  autofocus?: boolean;
  leftIcon?: string;
  autocomplete?: string;
  maxLength?: number;
  testId?: string;
  inputRef?: (element: HTMLInputElement) => void;
}

export const FormField: Component<FormFieldProps> = props => {
  const [showPassword, setShowPassword] = createSignal(false);

  return (
    <div class="group flex flex-col gap-1.5">
      <label
        for={props.id}
        class="text-on-surface-variant group-focus-within:text-primary block text-[11px] font-medium tracking-[0.05em] transition-colors"
      >
        {props.label}
      </label>
      <div class="relative flex items-center">
        <Input
          ref={props.inputRef}
          id={props.id}
          type={
            props.type === "password"
              ? showPassword()
                ? "text"
                : "password"
              : props.type || "text"
          }
          placeholder={props.placeholder}
          value={props.value}
          onInput={props.onInput}
          required={props.required}
          autofocus={props.autofocus}
          onkeydown={props.onKeyDown}
          autocomplete={props.autocomplete}
          maxLength={props.maxLength}
          data-testid={props.testId}
          leftIcon={props.leftIcon}
          class={`py-3.5${props.type === "password" ? "pr-11" : ""}`}
        />
        <Show when={props.type === "password"}>
          <button
            type="button"
            onClick={() => setShowPassword(!showPassword())}
            class="text-outline hover:text-on-surface absolute right-3.5 flex cursor-pointer items-center justify-center p-1 transition-colors select-none focus:outline-none"
            aria-label={showPassword() ? "Hide password" : "Show password"}
          >
            <span class="material-symbols-outlined text-[20px]">
              {showPassword() ? "visibility_off" : "visibility"}
            </span>
          </button>
        </Show>
      </div>
    </div>
  );
};
