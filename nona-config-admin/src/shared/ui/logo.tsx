import { type JSX, splitProps } from "solid-js";
import { cn } from "../lib/utils";


type DivProps = JSX.HTMLAttributes<HTMLDivElement>;

export function Logo(props: DivProps) {
  const [local, others] = splitProps(props, ["class"]);
  return (
    <div class={cn("w-20 h-20 mb-6", local.class)} {...others}>
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 64 64"
        class="w-full h-full"
      >
        <rect width="64" height="64" rx="14" fill="#070A13" />
        <path
          d="M16 46 V18 L34 46 V18"
          fill="none"
          stroke="#60A5FA"
          stroke-width="6"
          stroke-linecap="round"
          stroke-linejoin="round"
        />
        <circle
          cx="36"
          cy="34"
          r="7"
          fill="#34D399"
          stroke="#60A5FA"
          stroke-width="4"
        />
      </svg>
    </div>
  );
}
