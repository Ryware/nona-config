import { makePersisted } from "@solid-primitives/storage";
import { createSignal } from "solid-js";
import type { Project } from "./types";

function createActiveProjectSlug() {
  const [activeProjectSlug, setActiveProjectSlug] = makePersisted(
    // eslint-disable-next-line solid/reactivity -- persisted signal intentionally lives at module scope.
    createSignal(""),
    { name: "active_project_slug" }
  );

  const getActiveProjectSlug = () => activeProjectSlug();

  const setActiveProjectSlugValue = (slug: string) => setActiveProjectSlug(slug.trim());

  const clearActiveProjectSlug = () => setActiveProjectSlug("");

  const getActiveProjectHref = () => {
    const slug = getActiveProjectSlug();
    return slug ? `/projects/${slug}` : "/projects";
  };

  const syncActiveProject = (projects: Project[]) => {
    if (projects.length === 0) {
      clearActiveProjectSlug();
      return undefined;
    }

    const currentSlug = getActiveProjectSlug();
    const matchingProject =
      projects.find(project => project.urlSlug === currentSlug) ?? projects[0];

    if (matchingProject.urlSlug !== currentSlug) {
      setActiveProjectSlugValue(matchingProject.urlSlug);
    }

    return matchingProject;
  };

  return {
    getActiveProjectSlug,
    setActiveProjectSlug: setActiveProjectSlugValue,
    clearActiveProjectSlug,
    getActiveProjectHref,
    syncActiveProject
  };
}

export const {
  getActiveProjectSlug,
  setActiveProjectSlug,
  clearActiveProjectSlug,
  getActiveProjectHref,
  syncActiveProject
} = createActiveProjectSlug();
