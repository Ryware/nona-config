/** Skeleton matching the UserPage edit form layout (identity + role + scope sections). */
export function UserFormSkeleton() {
  return (
    <div class="space-y-8 animate-fade-in">
      {/* Identity section */}
      <div class="bg-surface-container-low border border-outline-variant/15 rounded-2xl p-6 space-y-5">
        <div class="skeleton h-4 w-32 rounded" />
        <div class="space-y-4">
          <div class="space-y-2">
            <div class="skeleton h-3 w-16 rounded" />
            <div class="skeleton h-10 w-full rounded-lg" />
          </div>
          <div class="space-y-2">
            <div class="skeleton h-3 w-20 rounded" />
            <div class="skeleton h-10 w-full rounded-lg" />
          </div>
        </div>
      </div>

      {/* Role section */}
      <div class="bg-surface-container-low border border-outline-variant/15 rounded-2xl p-6 space-y-5">
        <div class="skeleton h-4 w-40 rounded" />
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="skeleton h-20 rounded-xl" />
          <div class="skeleton h-20 rounded-xl" />
        </div>
      </div>

      {/* Project scope section */}
      <div class="bg-surface-container-low border border-outline-variant/15 rounded-2xl p-6 space-y-5">
        <div class="skeleton h-4 w-28 rounded" />
        <div class="space-y-2">
          <div class="skeleton h-12 w-full rounded-lg" />
          <div class="skeleton h-12 w-full rounded-lg" />
          <div class="skeleton h-12 w-full rounded-lg" />
        </div>
      </div>

      {/* Footer actions */}
      <div class="flex items-center justify-between pt-4 pb-8">
        <div class="skeleton h-9 w-20 rounded-lg" />
        <div class="skeleton h-9 w-32 rounded-lg" />
      </div>
    </div>
  );
}
