import { type JSX } from "solid-js";
import { useTheme } from "../hooks/useTheme";
import { cn } from "../lib/utils";

type ThemeToggleProps = JSX.ButtonHTMLAttributes<HTMLButtonElement>;

export function ThemeToggle(props: ThemeToggleProps): JSX.Element {
  const { theme, toggleTheme } = useTheme();
  const isLight = () => theme() === "light";
  const label = () => `Switch to ${isLight() ? "dark" : "light"} theme`;

  return (
    <button
      type="button"
      title={label()}
      aria-label={label()}
      {...props}
      onClick={() => toggleTheme()}
      class={cn(
        "inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-outline-variant/20 bg-surface-container-low text-outline transition-all hover:border-outline-variant/40 hover:text-on-surface focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
        props.class,
      )}
    >
      <span class="material-symbols-outlined text-[17px]">
        {isLight() ? "dark_mode" : "light_mode"}
      </span>
    </button>
  );
}
