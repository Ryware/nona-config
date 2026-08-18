import { writeClipboard } from "@solid-primitives/clipboard";
import { useParams } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { Show, createSignal } from "solid-js";

import { configEntryService } from "../../../entities/project/api/config-entry.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import { MSG } from "../../../shared/lib/messages";
import { AccessDenied } from "../../../shared/ui/AccessDenied";
import { useToast } from "../../../shared/ui/toast";
import type { ParameterShareLink } from "../../../types";
import { ProjectShareLinks } from "../../../features/project-share-links/ProjectShareLinks";
import { ProjectSectionLayout } from "../components/ProjectSectionLayout";
import { useProjectContext } from "../hooks/useProjectContext";

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

export default function SharedLinksSection() {
  const params = useParams<{ slug: string }>();
  const queryClient = useQueryClient();
  const { addToast } = useToast();
  const [revokingShareLinkId, setRevokingShareLinkId] = createSignal<number | null>(null);

  const { projectsQuery, project, projectId, activeEnvName, canManageProject } =
    useProjectContext();

  const projectShareLinksQuery = useQuery(() => ({
    queryKey: projectKeys.environmentShareLinks(params.slug, activeEnvName()),
    queryFn: async () => {
      const entries = await configEntryService.getAll(projectId(), activeEnvName());
      const shareLinksByEntry = await Promise.all(
        entries.map(entry =>
          configEntryService.listShareLinks(projectId(), activeEnvName(), entry.key)
        )
      );

      return shareLinksByEntry
        .flat()
        .sort(
          (left, right) =>
            new Date(right.createdAt).getTime() - new Date(left.createdAt).getTime()
        );
    },
    enabled: !!project() && !!activeEnvName() && canManageProject()
  }));

  const copyShareUrl = async (value: string) => {
    try {
      await writeClipboard(value);
      addToast(MSG.COPIED, "success");
    } catch {
      addToast(MSG.COPY_FAILED, "error");
    }
  };

  const buildShareUrl = (token: string) =>
    `${window.location.origin}/share/${encodeURIComponent(token)}`;

  const revokeProjectShareLinkMutation = useMutation(() => ({
    mutationFn: (link: ParameterShareLink) => {
      setRevokingShareLinkId(link.id);
      return configEntryService.revokeShareLink(
        projectId(),
        link.environment,
        link.key,
        link.id
      );
    },
    onSuccess: (_, link) => {
      queryClient.invalidateQueries({
        queryKey: projectKeys.environmentShareLinks(params.slug, activeEnvName())
      });
      queryClient.invalidateQueries({
        queryKey: projectKeys.configEntryShareLinks(params.slug, link.environment, link.key)
      });
      addToast(MSG.SHARE_LINK_REVOKED, "success");
    },
    onError: error => addToast(errorMessage(error, MSG.SHARE_LINK_REVOKE_FAILED), "error"),
    onSettled: () => setRevokingShareLinkId(null)
  }));

  return (
    <ProjectSectionLayout
      section="sharedLinks"
      project={project()}
      projectLoading={projectsQuery.isLoading}
    >
      <Show when={canManageProject()} fallback={<AccessDenied />}>
        <Show
          when={activeEnvName()}
          fallback={
            <div class="border-outline-variant/15 bg-surface-container-low rounded-2xl border px-5 py-6 text-sm text-on-surface-variant">
              Select an active environment from the header to view shared links.
            </div>
          }
        >
          <ProjectShareLinks
            environmentName={activeEnvName()}
            shareLinks={
              projectShareLinksQuery.status === "success" ? (projectShareLinksQuery.data ?? []) : []
            }
            isLoading={projectShareLinksQuery.isLoading}
            revokingId={revokingShareLinkId()}
            canManage={canManageProject()}
            onCopy={copyShareUrl}
            onRevoke={link => revokeProjectShareLinkMutation.mutate(link)}
            buildShareUrl={buildShareUrl}
          />
        </Show>
      </Show>
    </ProjectSectionLayout>
  );
}
