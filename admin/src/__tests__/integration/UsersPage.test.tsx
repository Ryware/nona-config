import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import UsersPage from '../../pages/users/UsersPage';
import { mockProjects, mockUsers, mockToken } from '../mocks/data';
import type { JSX } from 'solid-js';

function renderWithProviders(ui: () => JSX.Element) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="*" component={ui} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('UsersPage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[0].email, role: 'admin' }));
    vi.restoreAllMocks();
  });

  it('renders the Team Management heading', async () => {
    renderWithProviders(() => <UsersPage />);
    expect(await screen.findByRole('heading', { name: /team/i })).toBeInTheDocument();
  });

  it('lists all users returned by the API', async () => {
    renderWithProviders(() => <UsersPage />);

    for (const user of mockUsers) {
      expect(await screen.findByText(user.email)).toBeInTheDocument();
    }
  });

  it('prevents the current admin from demoting their own role', async () => {
    renderWithProviders(() => <UsersPage />);

    const emailCell = await screen.findByText(mockUsers[0].email);
    expect(screen.getByText('Admin')).toBeInTheDocument();

    fireEvent.click(emailCell.closest('tr')!);

    expect(await screen.findByTestId('user-admin-role-locked')).toHaveTextContent(
      /cannot demote your own admin account/i,
    );
    expect(screen.queryByTestId('invite-role-editor')).not.toBeInTheDocument();
    expect(screen.queryByTestId('invite-role-viewer')).not.toBeInTheDocument();
  });

  it('shows empty state when there are no users', async () => {
    server.use(
      http.get('http://localhost:5027/admin/users', () => HttpResponse.json([])),
    );

    renderWithProviders(() => <UsersPage />);

    await waitFor(() => {
      expect(screen.getByText(/no team members yet/i)).toBeInTheDocument();
    });
  });

  it('displays correct total member count', async () => {
    renderWithProviders(() => <UsersPage />);

    await waitFor(() => {
      expect(screen.getByText(String(mockUsers.length))).toBeInTheDocument();
    });
  });

  it('expands an inline edit form when clicking a user row', async () => {
    renderWithProviders(() => <UsersPage />);

    const emailCell = await screen.findByText(mockUsers[0].email);
    // Click the row (the <tr> ancestor)
    fireEvent.click(emailCell.closest('tr')!);

    // Inline edit form expands under the row (no navigation)
    expect(await screen.findByTestId(`team-edit-row-${mockUsers[0].id}`)).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: /save changes/i })).toBeInTheDocument();
  });

  it('opens delete confirmation dialog when trash icon is clicked', async () => {
    renderWithProviders(() => <UsersPage />);

    await screen.findByText(mockUsers[0].email);

    fireEvent.click(await screen.findByTestId(`team-remove-${mockUsers[1].id}`));

    await waitFor(() => {
      // Confirmation modal has unique text "from this instance?"
      expect(screen.getByText(/from this instance/i)).toBeInTheDocument();
    });
  });

  it('disables deletion for the current user', async () => {
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[0].email, role: 'admin' }));

    renderWithProviders(() => <UsersPage />);

    const selfRemoveButton = await screen.findByTestId(`team-remove-${mockUsers[0].id}`);

    expect(selfRemoveButton).toBeDisabled();
    expect(selfRemoveButton).toHaveAccessibleName(/cannot remove your own account/i);
  });

  it('still allows deleting other users when the current user is listed', async () => {
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[0].email, role: 'admin' }));

    renderWithProviders(() => <UsersPage />);

    const otherRemoveButton = await screen.findByTestId(`team-remove-${mockUsers[1].id}`);
    fireEvent.click(otherRemoveButton);

    await waitFor(() => {
      expect(screen.getByText(/from this instance/i)).toBeInTheDocument();
    });
  });

  it('"Invite User" button reveals the inline invite form', async () => {
    renderWithProviders(() => <UsersPage />);

    fireEvent.click(await screen.findByRole('button', { name: /invite user/i }));

    expect(await screen.findByTestId('user-create-form')).toBeInTheDocument();
    expect(
      await screen.findByRole('button', { name: /generate invitation link/i }),
    ).toBeInTheDocument();
    expect(screen.getByTestId('invite-role-admin')).toBeInTheDocument();
    expect(screen.getByTestId('invite-role-member')).toHaveAttribute('aria-checked', 'true');

    const accessSelect = await screen.findByTestId(
      `invite-project-${mockProjects[0].urlSlug}`,
    ) as HTMLSelectElement;
    expect(Array.from(accessSelect.options).map(option => option.text)).toEqual([
      'None',
      'Viewer',
      'Editor',
    ]);
  });

  it('submitting the inline invite form shows the invitation link dialog', async () => {
    renderWithProviders(() => <UsersPage />);

    fireEvent.click(await screen.findByRole('button', { name: /invite user/i }));
    await screen.findByTestId('user-create-form');

    fireEvent.input(await screen.findByPlaceholderText(/john smith/i), {
      target: { value: 'New User' },
    });
    fireEvent.input(await screen.findByPlaceholderText(/alex@company\.com/i), {
      target: { value: 'newuser@example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /generate invitation link/i }));

    expect(await screen.findByTestId('invite-link-heading')).toBeInTheDocument();
    expect(screen.getByDisplayValue(/\/invite\/invite-token-123$/i)).toBeInTheDocument();
  });

  it('creates an Admin without project assignments', async () => {
    let requestedRole = '';
    let projectAssignmentCalls = 0;
    server.use(
      http.post('http://localhost:5027/admin/users', async ({ request }) => {
        const body = await request.json() as { role: string };
        requestedRole = body.role;
        return HttpResponse.json(
          {
            user: { ...mockUsers[0], id: 'new-admin', email: 'new-admin@example.com' },
            invitationToken: 'admin-invite-token',
          },
          { status: 201 },
        );
      }),
      http.put('http://localhost:5027/admin/users/:id/projects/:project', () => {
        projectAssignmentCalls += 1;
        return HttpResponse.json({});
      }),
    );
    renderWithProviders(() => <UsersPage />);

    fireEvent.click(await screen.findByRole('button', { name: /invite user/i }));
    fireEvent.click(await screen.findByTestId('invite-role-admin'));
    expect(screen.queryByRole('heading', { name: 'Project Scope' })).not.toBeInTheDocument();
    fireEvent.input(await screen.findByPlaceholderText(/john smith/i), {
      target: { value: 'New Admin' },
    });
    fireEvent.input(await screen.findByPlaceholderText(/alex@company\.com/i), {
      target: { value: 'new-admin@example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /generate invitation link/i }));

    await waitFor(() => expect(requestedRole).toBe('admin'));
    expect(projectAssignmentCalls).toBe(0);
  });

  it('pre-fills the email when editing a member inline', async () => {
    renderWithProviders(() => <UsersPage />);

    const emailCell = await screen.findByText(mockUsers[0].email);
    fireEvent.click(emailCell.closest('tr')!);

    const emailInput = (await screen.findByPlaceholderText(
      /alex@company\.com/i,
    )) as HTMLInputElement;
    await waitFor(() => {
      expect(emailInput.value).toBe(mockUsers[0].email);
    });
  });

  it('protects deletion of the last admin', async () => {
    localStorage.setItem('auth_session', JSON.stringify({ email: 'other-admin@example.com', role: 'admin' }));

    renderWithProviders(() => <UsersPage />);

    const removeButton = await screen.findByTestId(`team-remove-${mockUsers[0].id}`);

    expect(removeButton).toBeDisabled();
    expect(removeButton).toHaveAccessibleName(/last admin cannot be removed/i);
  });
});
