import { untrack } from "solid-js";

/** Track one source while running an effect callback without tracking its reads. */
export function onSource<T>(
  source: () => T,
  callback: (value: T, previous: T | undefined) => void,
  options?: { defer?: boolean },
): () => void {
  let initialized = false;
  let previous: T | undefined;
  return () => {
    const value = source();
    if (initialized || !options?.defer) untrack(() => callback(value, previous));
    previous = value;
    initialized = true;
  };
}
