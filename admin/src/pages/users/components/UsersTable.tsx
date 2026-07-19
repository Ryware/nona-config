import { For, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import type { Project, User } from "../../../types";
import { UserForm, type UserFormValue } from "./UserForm";

interface UsersTableProps {
  isLoading: boolean;
  totalUsersCount: number;
  filteredUsers: User[];
  currentUserEmail?: string;
  canManageUsers: boolean;
  onEdit: (user: User) => void;
  onDelete: (user: User) => void;
  onInvite?: () => void;
  // Inline edit
  editingUserId: string | null;
  editingUser: User | null;
  isEditLoading: boolean;
  isSaving: boolean;
  projects: Project[];
  onCancelEdit: () => void;
  onSubmitEdit: (value: UserFormValue) => void;
}

function roleMeta(role: string) {
  if (role === "admin")
    return {
      label: "Admin",
      class: "bg-primary/10 text-primary border border-primary/20",
      icon: "admin_panel_settings"
    };
  if (role === "editor")
    return {
      label: "Editor",
      class: "bg-secondary/15 text-secondary border border-secondary/20",
      icon: "edit"
    };
  return {
    label: "Viewer",
    class: "bg-surface-container-high text-outline border border-outline-variant/15",
    icon: "visibility"
  };
}

export function UsersTable(props: UsersTableProps) {
  return (
    <div class="bg-surface-container-low border-outline-variant/15 overflow-hidden rounded-xl border">
      <div class="overflow-x-auto">
        <table class="w-full border-collapse text-left">
          <thead>
            <tr class="border-outline-variant/15 bg-surface-container-lowest/50 border-b">
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                Member
              </th>
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                System Role
              </th>
              <th class="text-outline px-6 py-3 text-[11px] font-medium tracking-[0.05em] uppercase">
                Project Scope
              </th>
              <th class="text-outline w-24 px-6 py-3 text-right text-[11px] font-medium tracking-[0.05em] uppercase">
                Actions
              </th>
            </tr>
          </thead>
          <tbody class="divide-outline-variant/10 divide-y">
            <Show when={props.isLoading}>
              <For each={[1, 2, 3, 4, 5]}>
                {() => (
                  <tr class="border-outline-variant/10 border-b">
                    <td class="px-6 py-4">
                      <div class="flex items-center gap-3">
                        <div class="skeleton h-9 w-9 rounded-full" />
                        <div class="space-y-1.5">
                          <div class="skeleton h-3.5 w-28 rounded" />
                          <div class="skeleton h-3 w-36 rounded" />
                        </div>
                      </div>
                    </td>
                    <td class="px-6 py-4">
                      <div class="skeleton h-5 w-16 rounded-full" />
                    </td>
                    <td class="px-6 py-4">
                      <div class="skeleton h-4 w-24 rounded" />
                    </td>
                    <td class="px-6 py-4" />
                  </tr>
                )}
              </For>
            </Show>

            <Show
              when={
                !props.isLoading &&
                props.filteredUsers.length === 0 &&
                props.totalUsersCount === 0
              }
            >
              <tr>
                <td colspan="4" class="py-16 text-center">
                  <div class="bg-primary/5 mb-4 inline-flex h-14 w-14 items-center justify-center rounded-2xl">
                    <MIcon name="group_add" class="text-primary/60 text-3xl" />
                  </div>
                  <p class="text-on-surface font-headline mb-1 text-[14px] font-bold">
                    No team members yet
                  </p>
                  <p class="text-on-surface-variant mb-4 text-[13px]">
                    Invite your first team member to get started.
                  </p>
                  <Show when={props.onInvite}>
                    <button
                      type="button"
                      onClick={() => props.onInvite?.()}
                      class="bg-primary text-on-primary inline-flex cursor-pointer items-center gap-2 rounded-lg border-0 px-4 py-2 text-[13px] font-semibold transition-all hover:brightness-105 active:scale-[0.98]"
                    >
                      <MIcon name="person_add" class="text-[17px]" />
                      Invite Member
                    </button>
                  </Show>
                </td>
              </tr>
            </Show>

            <Show
              when={!props.isLoading && props.filteredUsers.length === 0 && props.totalUsersCount > 0}
            >
              <tr>
                <td colspan="4" class="text-on-surface-variant py-10 text-center text-sm">
                  No members match your search
                </td>
              </tr>
            </Show>

            <Show when={!props.isLoading}>
              <For each={props.filteredUsers}>
                {(user: User) => {
                  const initials = user.name
                    ? user.name.slice(0, 2).toUpperCase()
                    : user.email.slice(0, 2).toUpperCase();
                  const rm = roleMeta(user.role);
                  const isCurrentUser =
                    props.currentUserEmail?.toLowerCase() === user.email.toLowerCase();
                  const canEditUser = props.canManageUsers || isCurrentUser;
                  const isExpanded = () => props.editingUserId === user.id;

                  return (
                    <>
                      <tr
                        data-testid={`team-row-${user.id}`}
                        onClick={() => {
                          if (canEditUser) props.onEdit(user);
                        }}
                        class={`group transition-colors ${
                          canEditUser
                            ? "cursor-pointer"
                            : "cursor-default"
                        } ${
                          isExpanded()
                            ? "bg-surface-container-high/40"
                            : canEditUser
                              ? "hover:bg-surface-container-high/40"
                              : ""
                        }`}
                      >
                        <td class="px-6 py-4">
                          <div class="flex items-center gap-3">
                            <Show when={canEditUser}>
                              <div
                                class={`bg-surface-container-high text-outline flex h-7 w-7 shrink-0 items-center justify-center rounded-lg transition-transform ${
                                  isExpanded() ? "rotate-180" : ""
                                }`}
                              >
                                <MIcon name="expand_more" class="text-[16px]" />
                              </div>
                            </Show>
                            <div class="font-headline bg-primary/10 text-primary border-primary/20 flex h-9 w-9 shrink-0 items-center justify-center rounded-full border text-xs font-bold">
                              {initials}
                            </div>
                            <div>
                              <div class="font-headline text-on-surface text-sm font-semibold">
                                {user.name || "Pending Invite"}
                              </div>
                              <div class="text-outline mt-0.5 font-mono text-[11.5px]">
                                {user.email}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td class="px-6 py-4">
                          <span
                            class={`inline-flex items-center rounded-md px-2 py-0.5 text-[11px] font-medium ${rm.class}`}
                          >
                            {rm.label}
                          </span>
                        </td>
                        <td class="px-6 py-4">
                          <div class="text-on-surface-variant flex items-center gap-1.5 text-sm">
                            <span class="material-symbols-outlined text-outline text-[16px]">
                              {user.role === "admin" ? "public" : "lock_person"}
                            </span>
                            <span>{user.role === "admin" ? "Global Access" : "Scoped Access"}</span>
                          </div>
                        </td>
                        <td class="px-6 py-4 text-right" onClick={e => e.stopPropagation()}>
                          <Show when={props.canManageUsers}>
                            <button
                              data-testid={`team-remove-${user.id}`}
                              type="button"
                              disabled={isCurrentUser}
                              onClick={() => {
                                if (!isCurrentUser) props.onDelete(user);
                              }}
                              class={
                                isCurrentUser
                                  ? "text-outline/35 cursor-not-allowed rounded-lg border-0 bg-transparent p-1.5 opacity-60"
                                  : "text-outline hover:text-error hover:bg-error/10 cursor-pointer rounded-lg border-0 bg-transparent p-1.5 opacity-40 transition-opacity group-hover:opacity-100 focus:opacity-100"
                              }
                              title={
                                isCurrentUser
                                  ? "You cannot remove your own account"
                                  : `Remove ${user.name || user.email}`
                              }
                              aria-label={
                                isCurrentUser
                                  ? "You cannot remove your own account"
                                  : `Remove ${user.name || user.email}`
                              }
                            >
                              <MIcon name="delete_outline" class="text-[18px]" />
                            </button>
                          </Show>
                        </td>
                      </tr>

                      <Show when={isExpanded()}>
                        <tr data-testid={`team-edit-row-${user.id}`}>
                          <td colspan="4" class="bg-surface-container-lowest/30 px-6 py-5">
                            <Show
                              when={!props.isEditLoading && props.editingUser}
                              fallback={<div class="skeleton h-48 w-full rounded-xl" />}
                            >
                              <UserForm
                                mode="edit"
                                initial={{
                                  name: props.editingUser!.name,
                                  email: props.editingUser!.email,
                                  role: props.editingUser!.role,
                                  isAdmin: props.editingUser!.isAdmin,
                                  projects: (props.editingUser!.projects ?? []).map(
                                    p => p.projectName
                                  )
                                }}
                                projects={props.projects}
                                allowManagement={props.canManageUsers}
                                isPending={props.isSaving}
                                onCancel={props.onCancelEdit}
                                onSubmit={props.onSubmitEdit}
                              />
                            </Show>
                          </td>
                        </tr>
                      </Show>
                    </>
                  );
                }}
              </For>
            </Show>
          </tbody>
        </table>
      </div>
    </div>
  );
}
