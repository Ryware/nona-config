import { MetaProvider } from '@solidjs/meta';
import { Route, Router } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { fireEvent, render, screen, waitFor } from '@solidjs/testing-library';
import { beforeEach, describe, expect, it } from 'vitest';
import ResetPasswordPage from '../../pages/auth/ResetPasswordPage';
import { ToastProvider } from '../../shared/ui/toast';

function renderPage(token = 'valid-token') {
  window.history.replaceState({}, '', `/reset-password/${token}`);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="/reset-password/:token" component={ResetPasswordPage} />
            <Route path="/login" component={() => <div data-testid="login-page">Login</div>} />
            <Route path="/projects" component={() => <div data-testid="projects-page">Projects</div>} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('ResetPasswordPage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('renders account details for a valid reset link', async () => {
    renderPage();

    expect(await screen.findByRole('heading', { name: /set a new password/i })).toBeInTheDocument();
    expect(screen.getByText(/reset@example.com/i)).toBeInTheDocument();
  });

  it('validates matching passwords before submitting', async () => {
    renderPage();

    fireEvent.input(await screen.findByLabelText(/^new password$/i), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.input(screen.getByLabelText(/confirm password/i), {
      target: { value: 'DifferentPassword123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: /set new password/i }));

    expect(await screen.findByText(/passwords do not match/i)).toBeInTheDocument();
  });

  it('rejects a weak password before resetting', async () => {
    renderPage();

    fireEvent.input(await screen.findByLabelText(/^new password$/i), {
      target: { value: 'short' },
    });
    fireEvent.input(screen.getByLabelText(/confirm password/i), {
      target: { value: 'short' },
    });
    fireEvent.click(screen.getByRole('button', { name: /set new password/i }));

    expect(await screen.findByText(/^password must be at least 8 characters long\.$/i)).toBeInTheDocument();
    expect(window.location.pathname).toBe('/reset-password/valid-token');
  });

  it('returns to login with confirmation after resetting', async () => {
    renderPage();

    fireEvent.input(await screen.findByLabelText(/^new password$/i), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.input(screen.getByLabelText(/confirm password/i), {
      target: { value: 'NewPassword123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: /set new password/i }));

    await waitFor(() => {
      expect(screen.getByTestId('login-page')).toBeInTheDocument();
      expect(window.location.search).toBe('?passwordReset=success');
    });
  });

  it('shows confirmation to an authenticated user and preserves the session on return', async () => {
    localStorage.setItem('auth_token', 'active-session-token');
    renderPage();

    await submitValidPassword();

    expect(await screen.findByTestId('password-reset-complete')).toHaveTextContent(
      /sign in with your new password/i,
    );
    expect(window.location.pathname).toBe('/reset-password/valid-token');
    expect(localStorage.getItem('auth_token')).toBe('active-session-token');

    fireEvent.click(screen.getByTestId('password-reset-return-console'));

    expect(await screen.findByTestId('projects-page')).toBeInTheDocument();
    expect(localStorage.getItem('auth_token')).toBe('active-session-token');
  });

  it('clears an authenticated session before returning to login', async () => {
    localStorage.setItem('auth_token', 'active-session-token');
    sessionStorage.setItem('auth_token', 'tab-session-token');
    renderPage();

    await submitValidPassword();
    fireEvent.click(await screen.findByTestId('password-reset-sign-in'));

    expect(await screen.findByTestId('login-page')).toBeInTheDocument();
    expect(window.location.search).toBe('?passwordReset=success');
    expect(localStorage.getItem('auth_token')).toBeNull();
    expect(sessionStorage.getItem('auth_token')).toBeNull();
  });

  it('shows a terminal error for an invalid or used link', async () => {
    renderPage('invalid-token');

    expect(await screen.findByRole('heading', { name: /reset link unavailable/i })).toBeInTheDocument();
    expect(screen.getAllByText(/invalid, expired, or has already been used/i).length).toBeGreaterThan(0);
  });
});

async function submitValidPassword() {
  fireEvent.input(await screen.findByLabelText(/^new password$/i), {
    target: { value: 'NewPassword123!' },
  });
  fireEvent.input(screen.getByLabelText(/confirm password/i), {
    target: { value: 'NewPassword123!' },
  });
  fireEvent.click(screen.getByRole('button', { name: /set new password/i }));
}
