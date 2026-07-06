import { For } from "solid-js";

/** Skeleton matching the ProjectPage layout (header + API keys + environments + params table). */
export function ProjectPageSkeleton() {
  return (
    <div class="space-y-6 animate-fade-in">
      {/* Header skeleton */}
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="space-y-2">
          <div class="skeleton h-5 w-48 rounded" />
          <div class="skeleton h-3.5 w-72 rounded" />
        </div>
        <div class="flex gap-2">
          <div class="skeleton h-9 w-32 rounded-lg" />
          <div class="skeleton h-9 w-28 rounded-lg" />
          <div class="skeleton h-9 w-32 rounded-lg" />
        </div>
      </div>

      {/* API keys skeleton */}
      <div class="bg-surface-container-low border border-outline-variant/15 rounded-2xl p-5 space-y-3">
        <div class="skeleton h-3 w-20 rounded" />
        <div class="flex gap-4">
          <div class="skeleton h-9 flex-1 rounded-lg" />
          <div class="skeleton h-9 flex-1 rounded-lg" />
        </div>
      </div>

      {/* Environments skeleton */}
      <div class="flex gap-2">
        <div class="skeleton h-8 w-24 rounded-lg" />
        <div class="skeleton h-8 w-24 rounded-lg" />
        <div class="skeleton h-8 w-24 rounded-lg" />
      </div>

      {/* Params table skeleton */}
      <div class="space-y-3">
        <div class="skeleton h-3 w-24 rounded" />
        <div class="bg-surface-container-low rounded-xl border border-outline-variant/15 overflow-hidden">
          <table class="w-full text-left border-collapse">
            <thead>
              <tr class="border-b border-outline-variant/15 bg-surface-container-lowest/50">
                <th class="py-3 px-6"><div class="skeleton h-2.5 w-16 rounded" /></th>
                <th class="py-3 px-6"><div class="skeleton h-2.5 w-12 rounded" /></th>
                <th class="py-3 px-6"><div class="skeleton h-2.5 w-10 rounded" /></th>
                <th class="py-3 px-6"><div class="skeleton h-2.5 w-10 rounded" /></th>
                <th class="py-3 px-6" />
              </tr>
            </thead>
            <tbody class="divide-y divide-outline-variant/10">
              <For each={[1, 2, 3, 4]}>
                {() => (
                <tr>
                  <td class="py-4 px-6"><div class="skeleton h-4 w-40 rounded" /></td>
                  <td class="py-4 px-6"><div class="skeleton h-4 w-32 rounded" /></td>
                  <td class="py-4 px-6"><div class="skeleton h-5 w-14 rounded-full" /></td>
                  <td class="py-4 px-6"><div class="skeleton h-5 w-14 rounded-full" /></td>
                  <td class="py-4 px-6" />
                </tr>
                )}
              </For>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
