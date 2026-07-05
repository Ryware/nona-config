import { Title } from "@solidjs/meta";
import { useNavigate } from "@solidjs/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/solid-query";
import { createMemo, createSignal, Show } from "solid-js";
import {
  canManageUsers,
  canManageUsersFor,
  isCurrentUser
} from "../../entities/auth/model/permissions";
import { authStore } from "../../entities/auth/model/store";
import { userService } from "../../entities/user/api/user.service";
import { userKeys } from "../../entities/user/queries/keys";
import { MSG } from "../../shared/lib/messages";
import { ConfirmDialog } from "../../shared/ui/confirm-dialog";
import { QueryErrorBanner } from "../../shared/ui/QueryGuard";
import { useToast } from "../../shared/ui/toast";
import type { User } from "../../types";

import { UsersFilters } from "./components/UsersFilters";
import { UsersStats } from "./components/UsersStats";
import { UsersTable } from "./components/UsersTable";

export default function UsersPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { addToast } = useToast();

  const [deleteTarget, setDeleteTarget] = createSignal<User | null>(null);
  const [search, setSearch] = createSignal("");
  const [roleFilter, setRoleFilter] = createSignal("all");
  const sessionAllowsUserManagement = canManageUsers();

  const usersQuery = useQuery(() => ({
    queryKey: userKeys.list(),
    queryFn: () => userService.getAll()
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

  const users = () => (usersQuery.status === "success" ? (usersQuery.data ?? []) : []);
  const currentUserEmail = () => authStore.getSession()?.email ?? "";
  const currentUser = createMemo(() =>
    users().find(user => user.email.toLowerCase() === currentUserEmail().toLowerCase())
  );
  const allowUserManagement = createMemo(
    () =>
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

  return (
    <>
      <Title>Team Management | Nona Config Admin</Title>

      <div class="space-y-6">
        {/* Page header */}
        <div class="flex items-start justify-between gap-4 sm:items-center">
          <div class="space-y-1.5">
            <h2
              data-testid="team-heading"
              class="font-headline text-on-surface text-[17px] font-bold tracking-tight"
            >
              Team
            </h2>
            <UsersStats
              totalMembers={users().length}
              editorsAdminsCount={editorsCount() + adminCount()}
              viewersCount={viewersCount()}
            />
          </div>
          <Show when={allowUserManagement()}>
            <button
              data-testid="team-invite-button"
              onClick={() => navigate("/user")}
              class="bg-primary text-on-primary flex shrink-0 cursor-pointer items-center gap-2 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98]"
            >
              <span class="material-symbols-outlined text-[17px]">person_add</span>
              Invite Member
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

        {/* Member Table Section */}
        <UsersTable
          isLoading={usersQuery.isLoading}
          totalUsersCount={users().length}
          filteredUsers={filteredUsers()}
          currentUserEmail={currentUserEmail()}
          canManageUsers={allowUserManagement()}
          onEdit={user => {
            if (allowUserManagement() || isCurrentUser(user.email)) {
              navigate("/user", { state: { userId: user.id } });
            }
          }}
          onDelete={user => setDeleteTarget(user)}
          onInvite={allowUserManagement() ? () => navigate("/user") : undefined}
        />
      </div>

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
