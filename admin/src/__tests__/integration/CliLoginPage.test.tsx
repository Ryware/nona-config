import { Route, Router } from '@solidjs/router';
import { MetaProvider } from '@solidjs/meta';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { fireEvent, render, screen, waitFor } from '@solidjs/testing-library';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ToastProvider } from '../../shared/ui/toast';
import { mockToken } from '../mocks/data';
import CliLoginPage from '../../pages/auth/CliLoginPage';
import { ThemeProvider } from '../../shared/hooks/useTheme';

function renderCliLogin(path: string) {
  window.history.pushState({}, '', path);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(() => (
    <MetaProvider>
      <ThemeProvider>
        <QueryClientProvider client={queryClient}>
          <ToastProvider>
            <Router>
              <Route path="/cli-login" component={CliLoginPage} />
            </Router>
          </ToastProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </MetaProvider>
  ));
}

function cliLoginPath(state = 'state-123', redirect = `${window.location.origin}/callback`) {
  return `/cli-login?cli_state=${encodeURIComponent(state)}&cli_redirect=${encodeURIComponent(redirect)}`;
}

describe('CliLoginPage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    vi.restoreAllMocks();
    window.history.pushState({}, '', '/');
  });

  it('redirects authenticated users to the CLI callback', async () => {
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'admin@example.com', role: 'admin' }),
    );

    renderCliLogin(cliLoginPath('state-auth'));

    await waitFor(() => {
      expect(window.location.pathname).toBe('/callback');
    });

    const callback = new URL(window.location.href);
    expect(callback.searchParams.get('token')).toBe(mockToken);
    expect(callback.searchParams.get('state')).toBe('state-auth');
    expect(callback.searchParams.get('username')).toBe('admin@example.com');
    expect(callback.searchParams.get('role')).toBe('admin');
    expect(callback.searchParams.get('expires_at')).toBeTruthy();
  });

  it('redirects to the CLI callback after password login', async () => {
    renderCliLogin(cliLoginPath('state-login'));

    fireEvent.input(screen.getByLabelText(/email/i), { target: { value: 'admin@example.com' } });
    fireEvent.input(screen.getByLabelText(/^password$/i), { target: { value: 'password' } });
    fireEvent.click(screen.getByRole('button', { name: /login to console/i }));

    await waitFor(() => {
      expect(window.location.pathname).toBe('/callback');
    });

    const callback = new URL(window.location.href);
    expect(callback.searchParams.get('token')).toBe(mockToken);
    expect(callback.searchParams.get('state')).toBe('state-login');
    expect(callback.searchParams.get('role')).toBe('admin');
    expect(callback.searchParams.get('expires_at')).toBe('2099-01-01T00:00:00Z');
  });

  it('rejects non-loopback CLI callback URLs', async () => {
    renderCliLogin(cliLoginPath('state-bad', 'https://evil.example/callback'));

    expect(await screen.findByTestId('cli-login-error')).toBeInTheDocument();
    expect(screen.getByText(/loopback \/callback URL/i)).toBeInTheDocument();
    expect(window.location.pathname).toBe('/cli-login');
  });
});
