import { type JSX, Show, splitProps } from "solid-js";
import { cn } from "../lib/utils";


export interface InputProps extends JSX.InputHTMLAttributes<HTMLInputElement> {
  leftIcon?: string;
  wrapperStyle?: string;
}

const BASE =
  "flex w-full h-11 pr-3.5 rounded-xl text-[13px] text-on-surface " +
  "bg-surface-container-lowest border border-outline-variant/20 " +
  "placeholder:text-outline/60 transition-all duration-200 " +
  "focus:outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 " +
  "hover:border-outline-variant/30 disabled:cursor-not-allowed disabled:opacity-40";

export function Input(props: InputProps) {
  const [local, others] = splitProps(props, ["class", "leftIcon", "wrapperStyle"]);

  return (
    <Show
      when={local.leftIcon}
      fallback={<input class={cn(BASE, "pl-3.5", local.class)} {...others} />}
    >
      <div class={cn("relative flex items-center w-full", local.wrapperStyle)}>
        <span
          class="material-symbols-outlined absolute left-3 text-outline/60 text-[18px] pointer-events-none select-none"
          aria-hidden="true"
        >
          {local.leftIcon}
        </span>
        <input class={cn(BASE, "pl-10", local.class)} {...others} />
      </div>
    </Show>
  );
}
