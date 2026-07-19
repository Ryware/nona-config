import { makePersisted } from "@solid-primitives/storage";
import { createSignal } from "solid-js";
import type { Environment } from "./types";

type ActiveEnvironmentMap = Record<string, string>;

// eslint-disable-next-line solid/reactivity -- persisted signal intentionally lives at module scope.
const [activeEnvironmentByProjectSignal, setActiveEnvironmentByProjectSignal] =
  makePersisted(createSignal<ActiveEnvironmentMap>({}), {
    name: "active_environment_by_project",
  });

function normalizeProjectSlug(projectSlug: string) {
  return projectSlug.trim();
}

export function getActiveEnvironmentName(projectSlug: string) {
  const normalizedSlug = normalizeProjectSlug(projectSlug);
  if (!normalizedSlug) {
    return "";
  }

  return activeEnvironmentByProjectSignal()[normalizedSlug] ?? "";
}

export function setActiveEnvironmentName(projectSlug: string, environmentName: string) {
  const normalizedSlug = normalizeProjectSlug(projectSlug);
  if (!normalizedSlug) {
    return;
  }

  const normalizedEnvironmentName = environmentName.trim();

  setActiveEnvironmentByProjectSignal(current => {
    if (!normalizedEnvironmentName) {
      const { [normalizedSlug]: _removed, ...rest } = current;
      return rest;
    }

    return {
      ...current,
      [normalizedSlug]: normalizedEnvironmentName,
    };
  });
}

export function clearActiveEnvironmentName(projectSlug: string) {
  setActiveEnvironmentName(projectSlug, "");
}

export function syncActiveEnvironment(projectSlug: string, environments: Environment[]) {
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
}
