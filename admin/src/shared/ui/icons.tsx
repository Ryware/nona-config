import { type JSX } from "solid-js";

// Material Symbols helper
export function MIcon(props: {
  name: string;
  class?: string;
  filled?: boolean;
}): JSX.Element {
  return (
    <span
      aria-hidden="true"
      class={`material-symbols-outlined ${props.class ?? ""}`}
      style={
        props.filled
          ? "font-variation-settings: 'FILL' 1, 'wght' 400, 'GRAD' 0, 'opsz' 24;"
          : undefined
      }
    >
      {props.name}
    </span>
  );
}

// Legacy icon wrappers (Material Symbols)
export const IconGrid = (): JSX.Element => <MIcon name="grid_view" />;
export const IconFolder = (): JSX.Element => <MIcon name="folder_open" />;
export const IconUsers = (): JSX.Element => <MIcon name="group" />;
export const IconSliders = (): JSX.Element => <MIcon name="tune" />;
export const IconLogout = (): JSX.Element => <MIcon name="logout" />;
export const IconSearch = (): JSX.Element => <MIcon name="search" />;
export const IconBell = (): JSX.Element => <MIcon name="notifications" />;
export const IconUserCircle = (): JSX.Element => (
  <MIcon name="account_circle" />
);
export const IconSettings = (): JSX.Element => <MIcon name="settings" />;
export const IconHelpCircle = (): JSX.Element => <MIcon name="help_outline" />;
export const IconChevronRight = (): JSX.Element => (
  <MIcon name="chevron_right" />
);
