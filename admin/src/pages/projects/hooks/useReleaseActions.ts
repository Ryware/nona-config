import { useNavigate, useParams } from "@solidjs/router";
import { useMutation, useQueryClient } from "@tanstack/solid-query";
import { createMemo, createSignal, type Accessor } from "solid-js";

import { configReleaseService } from "../../../entities/project/api/config-release.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import { MSG } from "../../../shared/lib/messages";
import { useToast } from "../../../shared/ui/toast";
import type { ConfigRelease, PublishConfigReleaseRequest } from "../../../types";

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

interface UseReleaseActionsOptions {
  projectId: Accessor<string>;
  activeEnvName: Accessor<string>;
  releases: Accessor<ConfigRelease[]>;
  onDeleteSuccess?: () => void;
}

export function useReleaseActions(props: UseReleaseActionsOptions) {
  const params = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { addToast } = useToast();
  const [deletingReleaseVersion, setDeletingReleaseVersion] = createSignal<string | null>(null);

  const publishReleaseMutation = useMutation(() => ({
    mutationFn: ({
      environmentName,
      request
    }: {
      environmentName: string;
      request: PublishConfigReleaseRequest;
    }) => configReleaseService.publish(props.projectId(), environmentName, request),
    onSuccess: (_, { environmentName }) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, environmentName)
      });
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.RELEASE_PUBLISHED, "success");
      navigate(`/projects/${params.slug}/releases`);
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_PUBLISH_FAILED), "error")
  }));

  const releaseVersions = createMemo(() => props.releases().map(release => release.version));

  const nextPatchVersion = (source: string) => {
    const [major, minor] = source.split(".");
    let maxPatch = 0;
    for (const version of releaseVersions()) {
      const [vMajor, vMinor, vPatch] = version.split(".");
      if (vMajor === major && vMinor === minor) {
        const patch = Number(vPatch);
        if (Number.isFinite(patch) && patch > maxPatch) maxPatch = patch;
      }
    }
    return `${major}.${minor}.${maxPatch + 1}`;
  };

  const setActiveReleaseMutation = useMutation(() => ({
    mutationFn: (version: string | null) =>
      version
        ? configReleaseService.setActive(props.projectId(), props.activeEnvName(), { version })
        : configReleaseService.clearActive(props.projectId(), props.activeEnvName()),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, props.activeEnvName())
      });
      queryClient.invalidateQueries({ queryKey: projectKeys.environments(params.slug) });
      addToast(MSG.RELEASE_ACTIVATED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_ACTIVATE_FAILED), "error")
  }));

  const deleteReleaseMutation = useMutation(() => ({
    mutationFn: (version: string) => {
      setDeletingReleaseVersion(version);
      return configReleaseService.delete(props.projectId(), props.activeEnvName(), version);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.configReleases(params.slug, props.activeEnvName())
      });
      props.onDeleteSuccess?.();
      addToast(MSG.RELEASE_DELETED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.RELEASE_DELETE_FAILED), "error"),
    onSettled: () => setDeletingReleaseVersion(null)
  }));

  return {
    publishReleaseMutation,
    setActiveReleaseMutation,
    deleteReleaseMutation,
    releaseVersions,
    nextPatchVersion,
    deletingReleaseVersion
  };
}
