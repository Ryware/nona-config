import { Show } from "solid-js";
import type { Project } from "../../../types";

interface ProjectsStatsProps {
  isSuccess: boolean;
  projects: Project[];
  filteredCount: number;
}

export function ProjectsStats(props: ProjectsStatsProps) {
  const lastUpdated = () => {
    if (props.projects.length === 0) return null;
    return new Date(
      Math.max(...props.projects.map((p) => new Date(p.updatedAt).getTime()))
    ).toLocaleDateString("en-US", { month: "short", day: "numeric" });
  };

  return (
    <Show when={props.isSuccess && props.projects.length > 0}>
      <div class="flex items-center gap-1.5 text-[12.5px]">
        <span class="font-medium text-on-surface">{props.projects.length}</span>
        <span class="text-on-surface-variant">
          {props.projects.length === 1 ? "project" : "projects"}
        </span>
        <Show when={lastUpdated()}>
          <span class="text-outline/30 mx-1.5">·</span>
          <span class="text-on-surface-variant">updated</span>
          <span class="font-medium text-on-surface ml-1">{lastUpdated()}</span>
        </Show>
        <Show when={props.filteredCount !== props.projects.length}>
          <span class="text-outline/30 mx-1.5">·</span>
          <span class="font-medium text-on-surface">{props.filteredCount}</span>
          <span class="text-on-surface-variant ml-1">shown</span>
        </Show>
      </div>
    </Show>
  );
}
