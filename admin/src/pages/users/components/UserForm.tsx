import { createMemo, createSignal, Show } from "solid-js";
import { Button } from "../../../shared/ui/button";
import { MIcon } from "../../../shared/ui/icons";
import type { Project } from "../../../types";
import { UserIdentityForm } from "./UserIdentityForm";
import { UserProjectScope } from "./UserProjectScope";
import { UserRoleSelector } from "./UserRoleSelector";

export interface UserFormValue {
  name: string;
  email: string;
  role: "admin" | "editor" | "viewer";
  selectedProjects: string[];
}

interface UserFormProps {
  mode: "create" | "edit";
  initial?: {
    name: string;
    email: string;
    role: string;
    projects?: string[];
  };
  projects: Project[];
  /** Whether the current user may manage roles/scope (admins). */
  allowManagement: boolean;
  isPending: boolean;
  onCancel: () => void;
  onSubmit: (value: UserFormValue) => void;
}

export function UserForm(props: UserFormProps) {
  const isEdit = () => props.mode === "edit";

  const [name, setName] = createSignal(props.initial?.name ?? "");
  const [email, setEmail] = createSignal(props.initial?.email ?? "");
  const [role, setRole] = createSignal<"admin" | "editor" | "viewer">(
    props.initial?.role === "admin"
      ? "admin"
      : props.initial?.role === "viewer"
        ? "viewer"
        : "editor"
  );
  const [selectedProjects, setSelectedProjects] = createSignal<Set<string>>(
    new Set(props.initial?.projects ?? [])
  );
  const assignableRole = () => (role() === "viewer" ? "viewer" : "editor");

  const canEditProjectScope = createMemo(
    () => props.allowManagement && role() === "viewer"
  );

  const toggleProject = (id: string) => {
    setSelectedProjects(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    props.onSubmit({
      name: name(),
      email: email(),
      role: role(),
      selectedProjects: [...selectedProjects()]
    });
  };

  return (
    <form
      data-testid={isEdit() ? "user-edit-form" : "user-create-form"}
      onSubmit={handleSubmit}
      class="animate-fade-in space-y-6"
    >
      <UserIdentityForm
        name={name()}
        email={email()}
        isEditMode={isEdit()}
        isEmailDisabled={isEdit()}
        onNameChange={setName}
        onEmailChange={setEmail}
      />

      <Show when={role() === "admin"}>
        <section
          data-testid="user-admin-role-locked"
          class="bg-surface-container-low border-outline-variant/15 space-y-4 rounded-xl border p-4 shadow-sm sm:p-8"
        >
          <div class="flex items-center gap-3">
            <div class="bg-primary/10 border-primary/20 text-primary flex h-10 w-10 items-center justify-center rounded-lg border">
              <MIcon name="admin_panel_settings" class="text-xl" />
            </div>
            <div>
              <h3 class="font-headline text-on-surface text-base font-bold">Admin</h3>
              <p class="text-on-surface-variant mt-1 text-xs">
                The first user is the system admin. This role cannot be reassigned or removed.
              </p>
            </div>
          </div>
        </section>
      </Show>

      <Show when={props.allowManagement && role() !== "admin"}>
        <UserRoleSelector role={assignableRole()} onChange={setRole} />
      </Show>

      <Show when={canEditProjectScope()}>
        <UserProjectScope
          projects={props.projects}
          selectedProjects={selectedProjects()}
          onToggleProject={toggleProject}
        />
      </Show>

      <div class="flex justify-end gap-3">
        <Button
          data-testid={isEdit() ? "user-edit-submit-button" : "invite-submit-button"}
          type="submit"
          disabled={props.isPending}
        >
          <MIcon name={isEdit() ? "save" : "auto_awesome"} class="text-[16px]" />
          {props.isPending
            ? "Processing…"
            : isEdit()
              ? "Save Changes"
              : "Generate Invitation Link"}
        </Button>
        <Button
          data-testid={isEdit() ? "user-edit-cancel-button" : "invite-cancel-button"}
          type="button"
          variant="outline"
          onClick={() => props.onCancel()}
        >
          <MIcon name="close" class="text-[16px]" />
          Cancel
        </Button>
      </div>
    </form>
  );
}
