import { For, Show, splitProps } from "solid-js";

import { MIcon } from "../../shared/ui/icons";
import { MinorReleaseGroup } from "./MinorReleaseGroup";
import { ReleaseRow, type ReleaseRowActions } from "./ReleaseRow";
import { countReleases, type MajorGroup } from "./release-grouping";

interface MajorReleaseGroupProps extends ReleaseRowActions {
  majorGroup: MajorGroup;
}

export function MajorReleaseGroup(props: MajorReleaseGroupProps) {
  const [local, actions] = splitProps(props, ["majorGroup"]);

  return (
    <Show
      when={countReleases(local.majorGroup) > 1}
      fallback={
        <ReleaseRow release={local.majorGroup.minorGroups[0].releases[0]} {...actions} />
      }
    >
      <div data-testid={`release-major-group-${local.majorGroup.key}`}>
        <div class="flex select-none items-center gap-1.5 rounded-md px-1.5 py-1.5">
          <MIcon name="chevron_right" class="text-outline text-[16px]" />
          <span class="text-on-surface font-mono text-[13px] font-bold">
            {local.majorGroup.label}
          </span>
          <span class="text-outline text-[11px]">
            {countReleases(local.majorGroup)}{" "}
            {countReleases(local.majorGroup) === 1 ? "release" : "releases"}
          </span>
          <Show when={local.majorGroup.hasActive}>
            <span class="bg-primary/10 text-primary rounded-md px-2 py-0.5 text-[11px] font-bold">
              Active
            </span>
          </Show>
        </div>

        <div class="border-outline-variant/20 ml-2.5 space-y-1 border-l pl-3">
          <For each={local.majorGroup.minorGroups}>
            {minorGroup => <MinorReleaseGroup minorGroup={minorGroup} {...actions} />}
          </For>
        </div>
      </div>
    </Show>
  );
}
