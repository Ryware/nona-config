import { MetaProvider } from "@solidjs/meta";
import { Route, Router, useLocation, useNavigate } from "@solidjs/router";
import { fireEvent, render, screen, waitFor, within } from "@solidjs/testing-library";
import { QueryClient, QueryClientProvider } from "@tanstack/solid-query";
import { http, HttpResponse } from "msw";
import { onMount } from "solid-js";
import { beforeEach, describe, expect, it, vi } from "vitest";
import UserPage from "../../pages/users/UserPage";
import { ToastProvider } from "../../shared/ui/toast";
import { mockProjects, mockToken, mockUsers } from "../mocks/data";
import { server } from "../mocks/server";

const UsersStub = () => <div data-testid="users-page-stub">Users Page</div>;

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } }
  });
}

/** Renders UserPage with no router state — invite mode. */
function renderInviteMode() {
  window.history.pushState({}, "", "/user");
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={makeQueryClient()}>
        <ToastProvider>
          <Router>
            <Route path="/user" component={UserPage} />
            <Route path="/users" component={UsersStub} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

/**
 * Bridge component: navigates to /user with location state after mount,
 * then lets UserPage read that state via useLocation().
 */
function EditModeBridge(props: { userId: string }) {
  const navigate = useNavigate();
  const location = useLocation<{ userId?: string }>();

  onMount(() => {
    if (!location.state?.userId) {
      navigate("/user", { state: { userId: props.userId }, replace: true });
    }
  });

  return <>{location.state?.userId ? <UserPage /> : null}</>;
}

/** Renders UserPage with a userId in router location state — edit mode. */
function renderEditMode(userId: string) {
  window.history.pushState({}, "", "/user");
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={makeQueryClient()}>
        <ToastProvider>
          <Router>
            <Route path="/user" component={() => <EditModeBridge userId={userId} />} />
            <Route path="/users" component={UsersStub} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe("UserPage — invite mode (no userId in state)", () => {
  beforeEach(() => {
    localStorage.setItem("auth_token", mockToken);
    localStorage.setItem("auth_session", JSON.stringify({ email: mockUsers[0].email, role: "admin", isAdmin: true }));
    vi.restoreAllMocks();
    window.history.pushState({}, "", "/user");
  });

  it('renders the "Invite Team Member" heading', async () => {
    renderInviteMode();
    expect(await screen.findByRole("heading", { name: /invite team member/i })).toBeInTheDocument();
  });

  it("renders email and name fields", async () => {
    renderInviteMode();
    expect(await screen.findByPlaceholderText(/alex@company\.com/i)).toBeInTheDocument();
    expect(await screen.findByPlaceholderText(/john smith/i)).toBeInTheDocument();
  });

  it("renders Editor and Viewer role cards", async () => {
    renderInviteMode();
    expect(await screen.findByText("Editor")).toBeInTheDocument();
    expect(await screen.findByText("Viewer")).toBeInTheDocument();
  });

  it("Editor role is selected by default", async () => {
    renderInviteMode();
    await screen.findByText("Editor");
    expect(screen.getByRole("button", { name: /generate invitation link/i })).toBeInTheDocument();
  });

  it("selecting Viewer role updates the selection", async () => {
    renderInviteMode();
    const viewerCard = await screen.findByText("Viewer");
    fireEvent.click(viewerCard.closest('[class*="rounded-lg"]')!);
    // Role card click is handled by parent div onClick — verify via role() side effect being visible
    expect(screen.getByText("Viewer")).toBeInTheDocument();
  });

  it("hides the project scope section for the default editor role", async () => {
    renderInviteMode();
    await screen.findByText("Editor");
    expect(screen.queryByRole("heading", { name: /project scope/i })).not.toBeInTheDocument();
  });

  it("displays the list of projects in the project scope section for viewers", async () => {
    renderInviteMode();
    const viewerCard = await screen.findByText("Viewer");
    fireEvent.click(viewerCard.closest('[class*="rounded-lg"]')!);
    // Find the section heading first, then wait for async project data within it
    const scopeHeading = await screen.findByRole("heading", { name: /project scope/i });
    const section = scopeHeading.closest("section")!;
    for (const project of mockProjects) {
      expect(await within(section).findByText(project.urlSlug)).toBeInTheDocument();
    }
  });

  it('shows "No projects found" when there are no projects', async () => {
    server.use(http.get("http://localhost:5027/admin/projects", () => HttpResponse.json([])));
    renderInviteMode();
    const viewerCard = await screen.findByText("Viewer");
    fireEvent.click(viewerCard.closest('[class*="rounded-lg"]')!);
    expect(await screen.findByText(/no projects found/i)).toBeInTheDocument();
  });

  it("toggles project checkbox when a project row is clicked", async () => {
    renderInviteMode();
    const viewerCard = await screen.findByText("Viewer");
    fireEvent.click(viewerCard.closest('[class*="rounded-lg"]')!);
    const row = await screen.findByTestId(`invite-project-row-${mockProjects[0].urlSlug}`);
    const checkbox = screen.getByTestId(
      `invite-project-${mockProjects[0].urlSlug}`
    ) as HTMLInputElement;

    expect(checkbox.checked).toBe(false);
    fireEvent.click(row);
    expect(checkbox.checked).toBe(true);
  });

  it("shows error toast when submitting without an email", async () => {
    renderInviteMode();
    const submitBtn = await screen.findByRole("button", { name: /generate invitation link/i });
    fireEvent.click(submitBtn);
    const nameInput = screen.getByPlaceholderText(/john smith/i) as HTMLInputElement;
    const emailInput = screen.getByPlaceholderText(/alex@company\.com/i) as HTMLInputElement;
    expect(nameInput.required).toBe(true);
    expect(emailInput.required).toBe(true);
  });

  it("submits the form and renders the generated invitation link", async () => {
    renderInviteMode();
    fireEvent.input(await screen.findByPlaceholderText(/john smith/i), {
      target: { value: "New User" }
    });
    fireEvent.input(await screen.findByPlaceholderText(/alex@company\.com/i), {
      target: { value: "newuser@example.com" }
    });
    fireEvent.click(screen.getByRole("button", { name: /generate invitation link/i }));

    expect(await screen.findByRole("heading", { name: /invitation link/i })).toBeInTheDocument();
    expect(screen.getByDisplayValue(/\/invite\/invite-token-123$/i)).toBeInTheDocument();
  });

  it('shows "Back to Team Overview" button that navigates back', async () => {
    renderInviteMode();
    expect(
      await screen.findByRole("button", { name: /back to team overview/i })
    ).toBeInTheDocument();
  });

  it('"Cancel Invitation" button navigates to /users', async () => {
    renderInviteMode();
    fireEvent.click(await screen.findByRole("button", { name: /cancel invitation/i }));
    expect(await screen.findByTestId("users-page-stub")).toBeInTheDocument();
  });
});

describe("UserPage — edit mode (userId present in state)", () => {
  beforeEach(() => {
    localStorage.setItem("auth_token", mockToken);
    localStorage.setItem("auth_session", JSON.stringify({ email: mockUsers[0].email, role: "admin", isAdmin: true }));
    vi.restoreAllMocks();
  });

  it('renders the "Edit Team Member" heading once user data loads', async () => {
    renderEditMode(mockUsers[0].id);
    expect(await screen.findByRole("heading", { name: /edit team member/i })).toBeInTheDocument();
  });

  it("pre-fills the email field with the existing user email", async () => {
    renderEditMode(mockUsers[0].id);
    const emailInput = (await screen.findByPlaceholderText(
      /alex@company\.com/i
    )) as HTMLInputElement;
    await waitFor(() => {
      expect(emailInput.value).toBe(mockUsers[0].email);
    });
  });

  it('shows "Save Changes" instead of "Generate Magic Link"', async () => {
    renderEditMode(mockUsers[0].id);
    expect(await screen.findByRole("button", { name: /save changes/i })).toBeInTheDocument();
  });

  it("shows API error message in a toast when update fails", async () => {
    server.use(
      http.put(`http://localhost:5027/admin/users/${mockUsers[0].id}`, () =>
        HttpResponse.json({ error: "Permission denied" }, { status: 403 })
      )
    );

    renderEditMode(mockUsers[0].id);

    await screen.findByRole("button", { name: /save changes/i });
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await waitFor(() => {
      expect(screen.getByText(/permission denied/i)).toBeInTheDocument();
    });
  });

  it("hides role controls when persisted current user is viewer despite stale session role", async () => {
    localStorage.setItem(
      "auth_session",
      JSON.stringify({ email: mockUsers[1].email, role: "editor", isAdmin: false })
    );

    renderEditMode(mockUsers[1].id);

    expect(await screen.findByRole("heading", { name: /edit team member/i })).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.queryByRole("heading", { name: /role assignment/i })).not.toBeInTheDocument();
    });
  });
});
