import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import UsersPage from '../../pages/users/UsersPage';
import { mockUsers, mockToken } from '../mocks/data';
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
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[0].email, role: 'admin', isAdmin: true }));
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
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[0].email, role: 'admin', isAdmin: true }));

    renderWithProviders(() => <UsersPage />);

    const selfRemoveButton = await screen.findByTestId(`team-remove-${mockUsers[0].id}`);

    expect(selfRemoveButton).toBeDisabled();
    expect(selfRemoveButton).toHaveAccessibleName(/cannot remove your own account/i);
  });

  it('still allows deleting other users when the current user is listed', async () => {
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[0].email, role: 'admin', isAdmin: true }));

    renderWithProviders(() => <UsersPage />);

    const otherRemoveButton = await screen.findByTestId(`team-remove-${mockUsers[1].id}`);
    fireEvent.click(otherRemoveButton);

    await waitFor(() => {
      expect(screen.getByText(/from this instance/i)).toBeInTheDocument();
    });
  });

  it('"Invite Member" button reveals the inline invite form', async () => {
    renderWithProviders(() => <UsersPage />);

    fireEvent.click(await screen.findByRole('button', { name: /invite member/i }));

    expect(await screen.findByTestId('user-create-form')).toBeInTheDocument();
    expect(
      await screen.findByRole('button', { name: /generate invitation link/i }),
    ).toBeInTheDocument();
  });

  it('submitting the inline invite form shows the invitation link dialog', async () => {
    renderWithProviders(() => <UsersPage />);

    fireEvent.click(await screen.findByRole('button', { name: /invite member/i }));
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

  it('hides management actions when persisted current user is viewer despite stale session role', async () => {
    localStorage.setItem('auth_session', JSON.stringify({ email: mockUsers[1].email, role: 'editor', isAdmin: false }));

    renderWithProviders(() => <UsersPage />);

    await screen.findByText(mockUsers[1].email);

    await waitFor(() => {
      expect(screen.queryByRole('button', { name: /invite member/i })).not.toBeInTheDocument();
    });
    expect(screen.queryByTestId(`team-remove-${mockUsers[0].id}`)).not.toBeInTheDocument();
  });
});
