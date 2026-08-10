import { Show } from "solid-js";

interface UsersStatsProps {
  totalUsers: number;
  adminCount: number;
  memberCount: number;
}

export function UsersStats(props: UsersStatsProps) {
  return (
    <div class="flex items-center gap-1.5 text-[12.5px]">
      <span class="font-medium text-on-surface">{props.totalUsers}</span>
      <span class="text-on-surface-variant">
        {props.totalUsers === 1 ? "user" : "users"}
      </span>
      <Show when={props.adminCount > 0}>
        <span class="text-outline/30 mx-1.5">·</span>
        <span class="font-medium text-on-surface">{props.adminCount}</span>
        <span class="text-on-surface-variant ml-1">
          {props.adminCount === 1 ? "admin" : "admins"}
        </span>
      </Show>
      <Show when={props.memberCount > 0}>
        <span class="text-outline/30 mx-1.5">·</span>
        <span class="font-medium text-on-surface">{props.memberCount}</span>
        <span class="text-on-surface-variant ml-1">
          {props.memberCount === 1 ? "member" : "members"}
        </span>
      </Show>
    </div>
  );
}
