import { type JSX, splitProps } from "solid-js";
import { cn } from "../lib/utils";

export type LabelProps = JSX.LabelHTMLAttributes<HTMLLabelElement>;

export function Label(props: LabelProps) {
  const [local, others] = splitProps(props, ["class"]);
  return (
    <label
      class={cn(
        "block text-[11px] font-medium text-on-surface-variant tracking-[0.05em] mb-1.5",
        local.class,
      )}
      {...others}
    />
  );
}
