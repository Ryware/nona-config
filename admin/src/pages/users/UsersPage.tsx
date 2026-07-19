import { writeClipboard } from "@solid-primitives/clipboard";
import { Title } from "@solidjs/meta";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { createMemo, createSignal, Show } from "solid-js";
import {
  canManageUsers,
  canManageUsersFor,
  isCurrentUser
} from "../../entities/auth/model/permissions";
import { authStore } from "../../entities/auth/model/store";
import { projectService } from "../../entities/project/api/project.service";
import { projectKeys } from "../../entities/project/queries/keys";
import type { UpdateUserRequest } from "../../entities/user/api/user.service";
import { userService } from "../../entities/user/api/user.service";
import { userKeys } from "../../entities/user/queries/keys";
import { MSG } from "../../shared/lib/messages";
import { ConfirmDialog } from "../../shared/ui/confirm-dialog";
import { MIcon } from "../../shared/ui/icons";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import { useToast } from "../../shared/ui/toast";
import type { CreateUserResponse, User } from "../../types";

import { UserForm, type UserFormValue } from "./components/UserForm";
import { UserInviteDialog } from "./components/UserInviteDialog";
import { UsersFilters } from "./components/UsersFilters";
import { UsersStats } from "./components/UsersStats";
import { UsersTable } from "./components/UsersTable";

export default function UsersPage() {
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const [deleteTarget, setDeleteTarget] = createSignal<User | null>(null);
  const [search, setSearch] = createSignal("");
  const [roleFilter, setRoleFilter] = createSignal("all");
  const [showInvite, setShowInvite] = createSignal(false);
  const [editingUserId, setEditingUserId] = createSignal<string | null>(null);
  const [createdInvite, setCreatedInvite] = createSignal<CreateUserResponse | null>(null);
  const [copyFeedback, setCopyFeedback] = createSignal("");
  const sessionAllowsUserManagement = canManageUsers();

  const usersQuery = useQuery(() => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll()
  }));

  const projectsQuery = useQuery(() => ({
    queryKey: projectKeys.list(),
    queryFn: () => projectService.getAll()
  }));

  const editUserQuery = useQuery(() => ({
    queryKey: userKeys.detail(String(editingUserId())),
    queryFn: () => userService.getById(editingUserId()!),
    enabled: !!editingUserId()
  }));

  const users = () => (usersQuery.status === "success" ? (usersQuery.data ?? []) : []);
  const projects = () => (projectsQuery.status === "success" ? (projectsQuery.data ?? []) : []);
  const currentUserEmail = () => authStore.getSession()?.email ?? "";
  const currentUser = createMemo(() =>
    users().find(user => user.email.toLowerCase() === currentUserEmail().toLowerCase())
  );
  const allowUserManagement = createMemo(() =>
    usersQuery.status === "success"
      ? canManageUsersFor(currentUser())
      : sessionAllowsUserManagement
  );

  const filteredUsers = createMemo(() => {
    const q = search().toLowerCase().trim();
    const role = roleFilter();
    return users().filter((u: User) => {
      const matchesRole = role === "all" || u.role === role;
      const matchesSearch =
        !q || u.email.toLowerCase().includes(q) || (u.name ?? "").toLowerCase().includes(q);
      return matchesRole && matchesSearch;
    });
  });

  const adminCount = () => users().filter((u: User) => u.role === "admin").length;
  const editorsCount = () => users().filter((u: User) => u.role === "editor").length;
  const viewersCount = () => users().filter((u: User) => u.role === "viewer").length;

  const createMutation = useMutation(() => ({
    mutationFn: async (value: UserFormValue) => {
      const response = await userService.create({
        name: value.name,
        email: value.email,
        role: value.role
      });
      const newUserId = String(response.user.id);
      const projectsToAdd = value.role === "viewer" ? value.selectedProjects : [];
      if (projectsToAdd.length > 0) {
        await Promise.allSettled(
          projectsToAdd.map(projectName => userService.addProject(newUserId, projectName, "viewer"))
        );
      }
      return response;
    },
    onSuccess: (response: CreateUserResponse) => {
      queryClient.invalidateQueries({ queryKey: userKeys.list() });
      setShowInvite(false);
      setCreatedInvite(response);
      setCopyFeedback("");
      addToast(MSG.INVITE_GENERATED, "success");
    },
    onError: (error: Error) => addToast(error.message || "Failed to invite member", "error")
  }));

  const updateMutation = useMutation(() => ({
    mutationFn: async (payload: { user: User; value: UserFormValue }) => {
      const { user, value } = payload;
      if (!allowUserManagement()) {
        await userService.update(user.id, { name: value.name });
        return;
      }
      const updates: UpdateUserRequest = { name: value.name, role: value.role };
      await userService.update(user.id, updates);

      const canScope = value.role === "viewer" && user.isAdmin !== true;
      if (canScope) {
        const original = new Set((user.projects ?? []).map(p => p.projectName));
        const current = new Set(value.selectedProjects);
        const toAdd = [...current].filter(s => !original.has(s));
        const toRemove = [...original].filter(s => !current.has(s));
        await Promise.allSettled([
          ...toAdd.map(projectName => userService.addProject(user.id, projectName, "viewer")),
          ...toRemove.map(projectName => userService.removeProject(user.id, projectName))
        ]);
      }
    },
    onSuccess: () => {
      const id = editingUserId();
      queryClient.invalidateQueries({ queryKey: userKeys.list() });
      if (id) queryClient.invalidateQueries({ queryKey: userKeys.detail(id) });
      addToast(MSG.MEMBER_UPDATED, "success");
      setEditingUserId(null);
    },
    onError: (error: Error) => addToast(error.message || "Failed to update member", "error")
  }));

  const deleteMutation = useMutation(() => ({
    mutationFn: (id: string) => userService.delete(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: userKeys.list() });
      setDeleteTarget(null);
      addToast(MSG.MEMBER_REMOVED, "success");
    },
    onError: () => addToast(MSG.MEMBER_REMOVE_FAILED, "error")
  }));

  const toggleInvite = () => {
    setEditingUserId(null);
    setShowInvite(v => !v);
  };

  const openEdit = (user: User) => {
    setShowInvite(false);
    setEditingUserId(prev => (prev === user.id ? null : user.id));
  };

  const invitationUrl = () => {
    const invite = createdInvite();
    if (!invite) return "";
    return new URL(`/invite/${invite.invitationToken}`, window.location.origin).toString();
  };

  const copyInvitationUrl = async () => {
    try {
      await writeClipboard(invitationUrl());
      setCopyFeedback("Invitation link copied");
      addToast(MSG.INVITE_COPIED, "success");
    } catch {
      setCopyFeedback("Copy failed. You can still copy the URL manually.");
      addToast(MSG.INVITE_COPY_FAILED, "error");
    }
  };

  return (
    <>
      <Title>Team Management | Nona Config Admin</Title>

      <div class="space-y-6">
        <section class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-2xl border p-5">
          {/* Section header */}
          <div class="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
            <div>
              <h2
                data-testid="team-heading"
                class="text-outline font-headline flex items-center gap-1.5 text-[10px] font-bold tracking-widest uppercase"
              >
                <MIcon name="group" class="text-[15px]" />
                Team
              </h2>
              <div class="mt-1">
                <UsersStats
                  totalMembers={users().length}
                  editorsAdminsCount={editorsCount() + adminCount()}
                  viewersCount={viewersCount()}
                />
              </div>
            </div>
            <Show when={allowUserManagement()}>
              <button
                data-testid="team-invite-button"
                onClick={toggleInvite}
                class="bg-primary text-on-primary flex shrink-0 cursor-pointer items-center gap-2 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98]"
              >
                <MIcon name={showInvite() ? "close" : "person_add"} class="text-[17px]" />
                {showInvite() ? "Cancel" : "Invite Member"}
              </button>
            </Show>
          </div>

          {/* Error banner */}
          <Show when={usersQuery.isError}>
            <QueryErrorBanner
              message="Failed to load team members."
              onRetry={() => usersQuery.refetch()}
            />
          </Show>

          {/* Search + filter bar */}
          <UsersFilters
            search={search()}
            roleFilter={roleFilter()}
            onSearchChange={setSearch}
            onRoleFilterChange={setRoleFilter}
          />

          {/* Inline invite form */}
          <Show when={allowUserManagement() && showInvite()}>
            <UserForm
              mode="create"
              projects={projects()}
              allowManagement={allowUserManagement()}
              isPending={createMutation.isPending}
              onCancel={() => setShowInvite(false)}
              onSubmit={value => createMutation.mutate(value)}
            />
          </Show>

          {/* Member table (with inline edit) */}
          <UsersTable
            isLoading={usersQuery.isLoading}
            totalUsersCount={users().length}
            filteredUsers={filteredUsers()}
            currentUserEmail={currentUserEmail()}
            canManageUsers={allowUserManagement()}
            onEdit={user => {
              if (allowUserManagement() || isCurrentUser(user.email)) openEdit(user);
            }}
            onDelete={user => setDeleteTarget(user)}
            onInvite={allowUserManagement() ? () => setShowInvite(true) : undefined}
            editingUserId={editingUserId()}
            editingUser={editUserQuery.data ?? null}
            isEditLoading={editUserQuery.isLoading}
            isSaving={updateMutation.isPending}
            projects={projects()}
            onCancelEdit={() => setEditingUserId(null)}
            onSubmitEdit={value => {
              const user = editUserQuery.data;
              if (user) updateMutation.mutate({ user, value });
            }}
          />
        </section>
      </div>

      {/* Invitation link dialog */}
      <UserInviteDialog
        open={createdInvite() !== null}
        email={createdInvite()?.user.email ?? ""}
        invitationUrl={invitationUrl()}
        copyFeedback={copyFeedback()}
        onCopy={copyInvitationUrl}
        onClose={() => setCreatedInvite(null)}
      />

      {/* Delete confirmation */}
      <ConfirmDialog
        open={deleteTarget() !== null}
        title="Revoke Access"
        variant="danger"
        message={
          <>
            <p class="mb-1">
              Remove <span class="text-on-surface font-semibold">{deleteTarget()?.email}</span> from
              this instance?
            </p>
            <p class="text-outline font-sans text-[11px]">
              All active sessions and credentials will be terminated immediately.
            </p>
          </>
        }
        confirmLabel="Remove Member"
        cancelLabel="Cancel"
        isLoading={deleteMutation.isPending}
        onConfirm={() => deleteMutation.mutate(deleteTarget()!.id)}
        onCancel={() => setDeleteTarget(null)}
        testId="remove-member-dialog"
        confirmTestId="remove-member-confirm-button"
        cancelTestId="remove-member-cancel-button"
      />
    </>
  );
}
