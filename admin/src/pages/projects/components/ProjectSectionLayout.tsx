import { Title } from "@solidjs/meta";
import { Show, type JSX } from "solid-js";

import type { ProjectPageSection } from "../../../shared/lib/project-navigation";
import type { Project } from "../../../types";
import { ProjectPageSkeleton } from "./ProjectPageSkeleton";

interface ProjectSectionLayoutProps {
  section: ProjectPageSection;
  projectLoading: boolean;
  project?: Project;
  children: JSX.Element;
}

const sectionTitle = (section: ProjectPageSection) =>
  ({
    environments: " Environments",
    parameters: "",
    sharedLinks: " Shared Links",
    apiKeys: " API Keys",
    releases: " Releases"
  })[section];

export function ProjectSectionLayout(props: ProjectSectionLayoutProps) {
  return (
    <>
      <Title>
        {props.project
          ? `${props.project.name}${sectionTitle(props.section)} | Nona Config Admin`
          : "Project | Nona Config Admin"}
      </Title>
      <div class="space-y-6">
        <Show when={!props.projectLoading} fallback={<ProjectPageSkeleton />}>
          <Show
            when={props.project}
            fallback={
              <div class="flex items-center justify-between gap-4">
                <div>
                  <h2 class="font-headline text-on-surface text-[17px] font-bold tracking-tight">
                    Projects
                  </h2>
                </div>
              </div>
            }
          >
            {props.children}
          </Show>
        </Show>
      </div>
    </>
  );
}
