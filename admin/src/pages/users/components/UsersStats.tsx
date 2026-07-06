import { Show } from "solid-js";

interface UsersStatsProps {
  totalMembers: number;
  editorsAdminsCount: number;
  viewersCount: number;
}

export function UsersStats(props: UsersStatsProps) {
  return (
    <div class="flex items-center gap-1.5 text-[12.5px]">
      <span class="font-medium text-on-surface">{props.totalMembers}</span>
      <span class="text-on-surface-variant">
        {props.totalMembers === 1 ? "member" : "members"}
      </span>
      <Show when={props.editorsAdminsCount > 0}>
        <span class="text-outline/30 mx-1.5">·</span>
        <span class="font-medium text-on-surface">{props.editorsAdminsCount}</span>
        <span class="text-on-surface-variant ml-1">with edit access</span>
      </Show>
      <Show when={props.viewersCount > 0}>
        <span class="text-outline/30 mx-1.5">·</span>
        <span class="font-medium text-on-surface">{props.viewersCount}</span>
        <span class="text-on-surface-variant ml-1">read-only</span>
      </Show>
    </div>
  );
}
