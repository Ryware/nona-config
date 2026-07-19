import { makePersisted } from "@solid-primitives/storage";
import { createSignal } from "solid-js";
import type { Project } from "./types";

// eslint-disable-next-line solid/reactivity -- persisted signal intentionally lives at module scope.
const [activeProjectSlugSignal, setActiveProjectSlugSignal] = makePersisted(createSignal(""), {
  name: "active_project_slug",
});

export function getActiveProjectSlug() {
  return activeProjectSlugSignal();
}

export function setActiveProjectSlug(slug: string) {
  setActiveProjectSlugSignal(slug.trim());
}

export function clearActiveProjectSlug() {
  setActiveProjectSlugSignal("");
}

export function getActiveProjectHref() {
  const slug = getActiveProjectSlug();
  return slug ? `/projects/${slug}` : "/projects";
}

export function syncActiveProject(projects: Project[]) {
  if (projects.length === 0) {
    clearActiveProjectSlug();
    return undefined;
  }

  const currentSlug = getActiveProjectSlug();
  const matchingProject = projects.find(project => project.urlSlug === currentSlug) ?? projects[0];

  if (matchingProject.urlSlug !== currentSlug) {
    setActiveProjectSlug(matchingProject.urlSlug);
  }

  return matchingProject;
}
