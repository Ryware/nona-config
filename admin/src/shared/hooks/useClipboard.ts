import { writeClipboard } from "@solid-primitives/clipboard";
import { createTimer } from "@solid-primitives/timer";
import { createSignal } from "solid-js";

/**
 * Returns a `copy(text)` function and a reactive `copied()` signal
 * that stays `true` for `resetMs` milliseconds after a successful copy.
 *
 * Usage:
 *   const { copy, copied } = useClipboard();
 *   <button onClick={() => copy(value)}>{copied() ? "Copied!" : "Copy"}</button>
 */
export function useClipboard(resetMs = 1500) {
  const [copied, setCopied] = createSignal<string | null>(null);

  createTimer(() => setCopied(null), () => (copied() ? resetMs : false), setTimeout);

  const copy = async (text: string) => {
    try {
      await writeClipboard(text);
      setCopied(text);
      return true;
    } catch {
      // clipboard access denied — silently ignore (caller should handle UI)
      return false;
    }
  };

  return { copy, copied };
}
