import { Show, type JSX } from "solid-js";
import type { UseQueryResult } from "@tanstack/solid-query";
import { MIcon } from "./icons";

/* ── QueryGuard ──────────────────────────────────────────────────────────────
 *  Wraps a TanStack Query result and renders the appropriate state:
 *    • loading  → skeleton fallback
 *    • error    → inline error banner with retry
 *    • empty    → optional empty-state slot
 *    • success  → children (with typed data)
 *
 *  Prevents blank screens, layout shifts, and duplicated state handling.
 * ────────────────────────────────────────────────────────────────────────── */

interface QueryGuardProps<T> {
  /** The TanStack query result to guard. */
  query: UseQueryResult<T>;
  /** Rendered while the query is loading (first fetch). */
  loadingFallback: JSX.Element;
  /** If true, shows the empty fallback even when data arrived but is "empty". */
  isEmpty?: boolean;
  /** Rendered when data is present but isEmpty is true. */
  emptyFallback?: JSX.Element;
  /** Render function called with the resolved data. */
  children: (data: T) => JSX.Element;
}

export function QueryGuard<T>(props: QueryGuardProps<T>) {
  return (
    <>
      {/* Loading — first fetch only (no cached data yet) */}
      <Show when={props.query.isLoading}>
        {props.loadingFallback}
      </Show>

      {/* Error — show banner with retry */}
      <Show when={props.query.isError}>
        <QueryErrorBanner
          message={props.query.error?.message ?? "Something went wrong."}
          onRetry={() => props.query.refetch()}
        />
      </Show>

      {/* Empty state */}
      <Show when={!props.query.isLoading && !props.query.isError && props.isEmpty}>
        {props.emptyFallback}
      </Show>

      {/* Success — render children with data */}
      <Show when={!props.query.isLoading && !props.query.isError && !props.isEmpty && props.query.data !== undefined}>
        {props.children(props.query.data as T)}
      </Show>
    </>
  );
}

/* ── QueryErrorBanner ────────────────────────────────────────────────────── */

interface QueryErrorBannerProps {
  message: string;
  onRetry?: () => void;
}

export function QueryErrorBanner(props: QueryErrorBannerProps) {
  return (
    <div class="flex items-center gap-3 px-4 py-3 rounded-xl bg-error/10 border border-error/20 text-[13px] animate-fade-in">
      <MIcon name="error" class="text-[18px] text-error shrink-0" />
      <span class="text-error flex-1">{props.message}</span>
      <Show when={props.onRetry}>
        <button
          onClick={() => props.onRetry?.()}
          class="flex items-center gap-1 px-3 py-1 rounded-lg text-[12px] font-medium bg-error/10 text-error hover:bg-error/20 transition-colors cursor-pointer border-0"
        >
          <MIcon name="refresh" class="text-[14px]" />
          Retry
        </button>
      </Show>
    </div>
  );
}
