import { useNavigate, useParams } from "@solidjs/router";
import { useQuery } from "@tanstack/solid-query";
import { Show, createMemo, createSignal } from "solid-js";

import { configReleaseService } from "../../../entities/project/api/config-release.service";
import { projectKeys } from "../../../entities/project/queries/keys";
import { ProjectReleases } from "../../../features/project-releases/ProjectReleases";
import { ReleaseVersionDialog } from "../../../features/project-releases/ReleaseVersionDialog";
import { useEscapeKey } from "../../../shared/hooks/useEscapeKey";
import { ConfirmDialog } from "../../../shared/ui/confirm-dialog";
import { ProjectSectionLayout } from "../components/ProjectSectionLayout";
import { useProjectContext } from "../hooks/useProjectContext";
import { useReleaseActions } from "../hooks/useReleaseActions";

export default function ReleasesSection() {
  const params = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const [confirmDeleteRelease, setConfirmDeleteRelease] = createSignal<string | null>(null);
  const [confirmActivateRelease, setConfirmActivateRelease] = createSignal<string | null>(null);
  const [confirmClearActiveRelease, setConfirmClearActiveRelease] = createSignal(false);
  const [createVersionOpen, setCreateVersionOpen] = createSignal(false);

  const {
    projectsQuery,
    project,
    projectId,
    activeEnvName,
    activeEnvironment,
    canManageProject
  } = useProjectContext();

  const releasesQuery = useQuery(() => ({
    queryKey: projectKeys.configReleases(params.slug, activeEnvName()),
    queryFn: () => configReleaseService.getAll(projectId(), activeEnvName()),
    enabled: !!project() && !!activeEnvName()
  }));

  const releases = createMemo(() =>
    releasesQuery.status === "success" ? (releasesQuery.data ?? []) : []
  );

  const {
    setActiveReleaseMutation,
    deleteReleaseMutation,
    releaseVersions,
    nextPatchVersion,
    deletingReleaseVersion
  } = useReleaseActions({
    projectId,
    activeEnvName,
    releases,
    onDeleteSuccess: () => setConfirmDeleteRelease(null)
  });

  useEscapeKey(() => {
    setCreateVersionOpen(false);
    setConfirmDeleteRelease(null);
    setConfirmActivateRelease(null);
    setConfirmClearActiveRelease(false);
  });

  return (
    <>
      <ProjectSectionLayout
        section="releases"
        project={project()}
        projectLoading={projectsQuery.isLoading}
      >
        <Show
          when={activeEnvName()}
          fallback={
            <div class="border-outline-variant/15 bg-surface-container-low rounded-2xl border px-5 py-6 text-sm text-on-surface-variant">
              Select an active environment from the header to manage releases.
            </div>
          }
        >
          <ProjectReleases
            environmentName={activeEnvName()}
            activeReleaseVersion={activeEnvironment()?.activeReleaseVersion}
            releases={releases()}
            isLoading={releasesQuery.isLoading}
            isActivating={setActiveReleaseMutation.isPending}
            amendingVersion={null}
            deletingVersion={deletingReleaseVersion()}
            canManage={canManageProject()}
            onCreateVersion={() => setCreateVersionOpen(true)}
            onView={version =>
              navigate(`/projects/${params.slug}?viewRelease=${encodeURIComponent(version)}`)
            }
            onAmend={version =>
              navigate(
                `/projects/${params.slug}?release=${encodeURIComponent(nextPatchVersion(version))}&amend=${encodeURIComponent(version)}`
              )
            }
            onActivate={setConfirmActivateRelease}
            onClearActive={() => setConfirmClearActiveRelease(true)}
            onDelete={setConfirmDeleteRelease}
          />
        </Show>
      </ProjectSectionLayout>

      <ConfirmDialog
        open={confirmActivateRelease() !== null}
        title="Activate Release?"
        message={
          <>
            Make release{" "}
            <span class="text-primary font-mono font-bold">{confirmActivateRelease()}</span> the
            active version for the{" "}
            <span class="text-on-surface font-medium">{activeEnvName()}</span> environment?
          </>
        }
        confirmLabel="Activate Release"
        isLoading={setActiveReleaseMutation.isPending}
        onConfirm={() => {
          const environmentName = activeEnvName();
          const version = confirmActivateRelease();
          if (environmentName && version) {
            setActiveReleaseMutation.mutate({ environmentName, version });
            setConfirmActivateRelease(null);
          }
        }}
        onCancel={() => setConfirmActivateRelease(null)}
        testId="release-activate-dialog"
        confirmTestId="release-activate-confirm-button"
        cancelTestId="release-activate-cancel-button"
      />

      <ConfirmDialog
        open={confirmClearActiveRelease()}
        title="Clear Active Release?"
        message={
          <>
            Remove the active release from the{" "}
            <span class="text-on-surface font-medium">{activeEnvName()}</span> environment? Clients
            will no longer resolve to a pinned release until another one is activated.
          </>
        }
        confirmLabel="Clear Active Release"
        isLoading={setActiveReleaseMutation.isPending}
        onConfirm={() => {
          const environmentName = activeEnvName();
          if (environmentName) {
            setActiveReleaseMutation.mutate({ environmentName, version: null });
            setConfirmClearActiveRelease(false);
          }
        }}
        onCancel={() => setConfirmClearActiveRelease(false)}
        testId="release-clear-active-dialog"
        confirmTestId="release-clear-active-confirm-button"
        cancelTestId="release-clear-active-cancel-button"
      />

      <ConfirmDialog
        open={confirmDeleteRelease() !== null}
        title="Delete Release?"
        message={
          <>
            Permanently delete release{" "}
            <span class="text-primary font-mono font-bold">{confirmDeleteRelease()}</span> from the{" "}
            <span class="text-on-surface font-medium">{activeEnvName()}</span> environment? The
            working configuration will not be changed.
          </>
        }
        confirmLabel="Delete Release"
        variant="danger"
        isLoading={deleteReleaseMutation.isPending}
        onConfirm={() => {
          const environmentName = activeEnvName();
          const version = confirmDeleteRelease();
          if (environmentName && version) {
            deleteReleaseMutation.mutate({ environmentName, version });
          }
        }}
        onCancel={() => setConfirmDeleteRelease(null)}
        testId="release-delete-dialog"
        confirmTestId="release-delete-confirm-button"
        cancelTestId="release-delete-cancel-button"
      />

      <ReleaseVersionDialog
        open={createVersionOpen()}
        title="Create a version"
        description="Choose a major and minor version. The release will start at patch .0 after you review its parameters."
        initialVersion=""
        existingVersions={releaseVersions()}
        confirmLabel="Continue to parameters"
        placeholder="1.2"
        validationMessage="Use major.minor."
        versionFormat="majorMinor"
        normalizeVersion={version => `${version}.0`}
        onConfirm={version => {
          setCreateVersionOpen(false);
          navigate(`/projects/${params.slug}?release=${encodeURIComponent(version)}`);
        }}
        onCancel={() => setCreateVersionOpen(false)}
      />
    </>
  );
}
