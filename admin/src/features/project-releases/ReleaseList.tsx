import { For, Show, createMemo, splitProps } from "solid-js";

import type { ConfigRelease } from "../../types";
import { MajorReleaseGroup } from "./MajorReleaseGroup";
import { ReleaseRow, type ReleaseRowActions } from "./ReleaseRow";
import { groupReleases } from "./release-grouping";

interface ReleaseListProps extends ReleaseRowActions {
  releases: ConfigRelease[];
  isLoading: boolean;
}

export function ReleaseList(props: ReleaseListProps) {
  const [local, actions] = splitProps(props, ["releases", "isLoading"]);
  const groupedReleases = createMemo(() => groupReleases(local.releases));

  return (
    <Show when={!local.isLoading} fallback={<div class="skeleton h-20 w-full rounded-xl" />}>
      <Show
        when={local.releases.length > 0}
        fallback={
          <div class="bg-surface-container rounded-xl px-4 py-5 text-center text-xs text-on-surface-variant">
            No releases yet.
          </div>
        }
      >
        <Show
          when={local.releases.length > 1}
          fallback={
            <For each={local.releases}>
              {release => <ReleaseRow release={release} {...actions} />}
            </For>
          }
        >
          <div class="space-y-1">
            <For each={groupedReleases()}>
              {majorGroup => <MajorReleaseGroup majorGroup={majorGroup} {...actions} />}
            </For>
          </div>
        </Show>
      </Show>
    </Show>
  );
}
