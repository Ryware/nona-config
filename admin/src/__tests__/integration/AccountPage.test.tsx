import { MetaProvider } from '@solidjs/meta';
import { Route, Router } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { fireEvent, render, screen, waitFor } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import AccountPage from '../../pages/account/AccountPage';
import { ToastProvider } from '../../shared/ui/toast';
import { mockToken } from '../mocks/data';
import { server } from '../mocks/server';

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="*" component={AccountPage} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('AccountPage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'admin@example.com', role: 'admin' }),
    );
  });

  it('shows password change fields for a password account', async () => {
    renderPage();

    expect(await screen.findByRole('heading', { name: /change password/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/current password/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^new password$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/confirm new password/i)).toBeInTheDocument();
  });

  it('shows an SSO-managed message instead of the form for passwordless accounts', async () => {
    server.use(
      http.get('http://localhost:5027/auth/me', () =>
        HttpResponse.json({
          email: 'sso@example.com',
          name: 'SSO User',
          role: 'viewer',
          passwordEnabled: false,
        }),
      ),
    );
    renderPage();

    expect(await screen.findByTestId('password-managed-by-sso')).toHaveTextContent(
      /password managed by sso/i,
    );
    expect(screen.queryByLabelText(/current password/i)).not.toBeInTheDocument();
  });

  it('validates confirmation and displays an incorrect-current-password error', async () => {
    renderPage();

    fireEvent.input(await screen.findByLabelText(/current password/i), {
      target: { value: 'current' },
    });
    fireEvent.input(screen.getByLabelText(/^new password$/i), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.input(screen.getByLabelText(/confirm new password/i), {
      target: { value: 'DifferentPassword123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: /^change password$/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/passwords do not match/i);

    fireEvent.input(screen.getByLabelText(/current password/i), { target: { value: 'wrong' } });
    fireEvent.input(screen.getByLabelText(/confirm new password/i), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: /^change password$/i }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/current password is incorrect/i);
  });

  it('rejects a weak new password before changing it', async () => {
    renderPage();

    fireEvent.input(await screen.findByLabelText(/current password/i), {
      target: { value: 'current' },
    });
    fireEvent.input(screen.getByLabelText(/^new password$/i), {
      target: { value: 'short' },
    });
    fireEvent.input(screen.getByLabelText(/confirm new password/i), {
      target: { value: 'short' },
    });
    fireEvent.click(screen.getByRole('button', { name: /^change password$/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/at least 8 characters/i);
  });

  it('changes the password, clears the form, and keeps the session', async () => {
    renderPage();

    const current = await screen.findByLabelText(/current password/i) as HTMLInputElement;
    const next = screen.getByLabelText(/^new password$/i) as HTMLInputElement;
    const confirm = screen.getByLabelText(/confirm new password/i) as HTMLInputElement;
    fireEvent.input(current, { target: { value: 'current' } });
    fireEvent.input(next, { target: { value: 'NewPassword123!' } });
    fireEvent.input(confirm, { target: { value: 'NewPassword123!' } });
    fireEvent.click(screen.getByRole('button', { name: /^change password$/i }));

    await waitFor(() => expect(screen.getByText(/password changed successfully/i)).toBeInTheDocument());
    expect(current.value).toBe('');
    expect(next.value).toBe('');
    expect(confirm.value).toBe('');
    expect(localStorage.getItem('auth_token')).toBe(mockToken);
  });
});
