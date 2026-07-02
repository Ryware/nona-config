import { createShortcut } from "@solid-primitives/keyboard";

/**
 * Calls `callback` whenever the Escape key is pressed.
 * Automatically registers/deregisters the listener with the component lifecycle.
 */
export function useEscapeKey(callback: () => void) {
  createShortcut(["Escape"], callback, { preventDefault: false, requireReset: true });
}
