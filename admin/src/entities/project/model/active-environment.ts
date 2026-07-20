import { makePersisted } from "@solid-primitives/storage";
import { createSignal } from "solid-js";
import type { Environment } from "./types";

type ActiveEnvironmentMap = Record<string, string>;

function createActiveEnvironment() {
  const [activeEnvironmentByProject, setActiveEnvironmentByProject] = makePersisted(
    // eslint-disable-next-line solid/reactivity -- persisted signal intentionally lives at module scope.
    createSignal<ActiveEnvironmentMap>({}),
    { name: "active_environment_by_project" }
  );

  const normalizeProjectSlug = (projectSlug: string) => projectSlug.trim();

  const getActiveEnvironmentName = (projectSlug: string) => {
    const normalizedSlug = normalizeProjectSlug(projectSlug);
    if (!normalizedSlug) {
      return "";
    }

    return activeEnvironmentByProject()[normalizedSlug] ?? "";
  };

  const setActiveEnvironmentName = (projectSlug: string, environmentName: string) => {
    const normalizedSlug = normalizeProjectSlug(projectSlug);
    if (!normalizedSlug) {
      return;
    }

    const normalizedEnvironmentName = environmentName.trim();

    setActiveEnvironmentByProject(current => {
      if (!normalizedEnvironmentName) {
        const { [normalizedSlug]: _removed, ...rest } = current;
        return rest;
      }

      return {
        ...current,
        [normalizedSlug]: normalizedEnvironmentName
      };
    });
  };

  const clearActiveEnvironmentName = (projectSlug: string) =>
    setActiveEnvironmentName(projectSlug, "");

  const syncActiveEnvironment = (projectSlug: string, environments: Environment[]) => {
    const normalizedSlug = normalizeProjectSlug(projectSlug);
    if (!normalizedSlug) {
      return undefined;
    }

    if (environments.length === 0) {
      clearActiveEnvironmentName(normalizedSlug);
      return undefined;
    }

    const currentEnvironmentName = getActiveEnvironmentName(normalizedSlug);
    const matchingEnvironment =
      environments.find(environment => environment.name === currentEnvironmentName) ??
      environments[0];

    if (matchingEnvironment.name !== currentEnvironmentName) {
      setActiveEnvironmentName(normalizedSlug, matchingEnvironment.name);
    }

    return matchingEnvironment;
  };

  return {
    getActiveEnvironmentName,
    setActiveEnvironmentName,
    clearActiveEnvironmentName,
    syncActiveEnvironment
  };
}

export const {
  getActiveEnvironmentName,
  setActiveEnvironmentName,
  clearActiveEnvironmentName,
  syncActiveEnvironment
} = createActiveEnvironment();
