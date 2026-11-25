import { omit } from "solid-js";

/** Keep the selected props reactive while passing the rest through to an element. */
export function splitProps<T extends object, K extends keyof T>(
  props: T,
  keys: readonly K[],
): [Pick<T, K>, Omit<T, K>] {
  const selected = {} as Pick<T, K>;
  for (const key of keys) {
    Object.defineProperty(selected, key, {
      enumerable: true,
      get: () => props[key],
    });
  }
  const remaining = omit(props, ...keys) as unknown as Omit<T, K>;
  return [selected, remaining];
}
