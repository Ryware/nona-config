import { For, Show, splitProps } from "solid-js";

import { MIcon } from "../../shared/ui/icons";
import { ReleaseRow, type ReleaseRowActions } from "./ReleaseRow";
import type { MinorGroup } from "./release-grouping";

interface MinorReleaseGroupProps extends ReleaseRowActions {
  minorGroup: MinorGroup;
}

export function MinorReleaseGroup(props: MinorReleaseGroupProps) {
  const [local, actions] = splitProps(props, ["minorGroup"]);

  return (
    <Show
      when={local.minorGroup.releases.length > 1}
      fallback={<ReleaseRow release={local.minorGroup.releases[0]} {...actions} />}
    >
      <div data-testid={`release-minor-group-${local.minorGroup.key}`}>
        <div class="flex select-none items-center gap-1.5 rounded-md px-1.5 py-1">
          <MIcon name="chevron_right" class="text-outline text-[15px]" />
          <span class="text-on-surface font-mono text-[13px] font-bold">
            {local.minorGroup.label}
          </span>
          <span class="text-outline text-[11.5px]">
            {local.minorGroup.releases.length}{" "}
            {local.minorGroup.releases.length === 1 ? "release" : "releases"}
          </span>
          <Show when={local.minorGroup.hasActive}>
            <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[12px] font-bold">
              Active
            </span>
          </Show>
        </div>

        <div class="border-outline-variant/20 ml-2.5 space-y-2 border-l py-1 pl-3">
          <For each={local.minorGroup.releases}>
            {release => <ReleaseRow release={release} {...actions} />}
          </For>
        </div>
      </div>
    </Show>
  );
}
