import { For, type JSX } from "solid-js";

/* ── Skeleton Primitives ─────────────────────────────────────────────────────
 *  Composable building blocks that mirror the `.skeleton` shimmer from index.css.
 *  All elements are purely visual — no aria-live needed (the parent page
 *  handles accessible loading announcements via sr-only text).
 * ────────────────────────────────────────────────────────────────────────── */

interface SkeletonProps {
  class?: string;
}

/** Generic shimmer rectangle. Pass sizing via `class`. */
export function Skeleton(props: SkeletonProps) {
  return <div class={`skeleton ${props.class ?? ""}`} />;
}

/* ── Skeleton Line ───────────────────────────────────────────────────────── */

interface SkeletonLineProps {
  width?: string;
  height?: string;
}

/** Single text-line placeholder. */
export function SkeletonLine(props: SkeletonLineProps) {
  return (
    <div
      class="skeleton rounded"
      style={{ width: props.width ?? "100%", height: props.height ?? "0.75rem" }}
    />
  );
}

/* ── Skeleton Table ──────────────────────────────────────────────────────── */

interface SkeletonTableProps {
  /** Number of placeholder rows (default 5). */
  rows?: number;
  /** Column definitions as arrays of [width] strings. */
  columns: string[];
  /** Optional column header widths for the <thead> row. */
  headerWidths?: string[];
}

/** Full skeleton table matching the standard admin table layout. */
export function SkeletonTable(props: SkeletonTableProps) {
  const rowCount = () => props.rows ?? 5;

  return (
    <div class="bg-surface-container-low rounded-xl border border-outline-variant/15 overflow-hidden">
      <div class="overflow-x-auto">
        <table class="w-full text-left border-collapse">
          <thead>
            <tr class="border-b border-outline-variant/15 bg-surface-container-lowest/50">
              <For each={props.columns}>
                {(_, i) => (
                  <th class="py-3 px-6">
                    <div
                      class="skeleton h-2.5 rounded"
                      style={{ width: props.headerWidths?.[i()] ?? "3rem" }}
                    />
                  </th>
                )}
              </For>
            </tr>
          </thead>
          <tbody class="divide-y divide-outline-variant/10">
            <For each={Array.from({ length: rowCount() })}>
              {() => (
                <tr>
                  <For each={props.columns}>
                    {(w) => (
                      <td class="py-4 px-6">
                        <div class="skeleton h-4 rounded" style={{ width: w }} />
                      </td>
                    )}
                  </For>
                </tr>
              )}
            </For>
          </tbody>
        </table>
      </div>
    </div>
  );
}

/* ── Skeleton Card Grid ──────────────────────────────────────────────────── */

interface SkeletonCardGridProps {
  /** Number of cards to show (default 4). */
  count?: number;
  /** Custom card content. If omitted, renders a default card placeholder. */
  children?: (index: number) => JSX.Element;
}

/** Grid of skeleton cards matching the project card layout. */
export function SkeletonCardGrid(props: SkeletonCardGridProps) {
  const count = () => props.count ?? 4;

  return (
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <For each={Array.from({ length: count() }, (_, i) => i)}>
        {(i) =>
          props.children ? (
            props.children(i)
          ) : (
            <div class="bg-surface-container-low rounded-2xl p-5 border border-outline-variant/15 space-y-3">
              <div class="skeleton h-3 w-24 rounded" />
              <div class="skeleton h-4 w-2/3 rounded-lg" />
              <div class="skeleton h-3 w-full rounded" />
              <div class="skeleton h-3 w-4/5 rounded" />
              <div class="flex justify-between pt-2 border-t border-outline-variant/10">
                <div class="skeleton h-3 w-16 rounded" />
                <div class="skeleton h-5 w-5 rounded-md" />
              </div>
            </div>
          )
        }
      </For>
    </div>
  );
}

/* ── Skeleton Stat Card ──────────────────────────────────────────────────── */

/** Matches the DashboardPage StatCard layout exactly. */
export function SkeletonStatCard() {
  return (
    <div class="bg-surface-container-low border border-outline-variant/15 rounded-2xl p-6 flex flex-col gap-4 shadow-sm">
      <div class="flex items-center justify-between">
        <div class="skeleton h-3 w-20 rounded" />
        <div class="skeleton h-5 w-5 rounded" />
      </div>
      <div class="skeleton h-9 w-16 rounded-lg" />
    </div>
  );
}

/* ── Full-Page Route Loader ──────────────────────────────────────────────── */

/** Shown by Suspense while a lazy-loaded route chunk is downloading. */
export function RouteLoader() {
  return (
    <div class="min-h-screen bg-background flex items-center justify-center">
      <div class="flex flex-col items-center gap-4 animate-fade-in">
        <div class="relative w-10 h-10">
          <div class="absolute inset-0 rounded-full border-2 border-outline-variant/20" />
          <div class="absolute inset-0 rounded-full border-2 border-primary border-t-transparent animate-spin" />
        </div>
        <span class="sr-only">Loading…</span>
      </div>
    </div>
  );
}

/* ── Page Content Loader ─────────────────────────────────────────────────── */

/** Inline loader for within-page async content areas (lighter weight). */
export function ContentLoader(props: { message?: string }) {
  return (
    <div class="flex items-center justify-center py-12">
      <div class="flex flex-col items-center gap-3 animate-fade-in">
        <div class="relative w-8 h-8">
          <div class="absolute inset-0 rounded-full border-2 border-outline-variant/20" />
          <div class="absolute inset-0 rounded-full border-2 border-primary border-t-transparent animate-spin" />
        </div>
        {props.message && (
          <p class="text-[12px] text-outline">{props.message}</p>
        )}
      </div>
    </div>
  );
}
