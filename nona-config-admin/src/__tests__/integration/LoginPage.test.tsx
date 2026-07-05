import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import LoginPage from '../../pages/auth/LoginPage';
import { mockToken } from '../mocks/data';
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

describe('LoginPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('renders email and password fields and a submit button', () => {
    renderWithProviders(LoginPage);

    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /login to console/i })).toBeInTheDocument();
  });

  it('displays error message on failed login', async () => {
    renderWithProviders(LoginPage);

    fireEvent.input(screen.getByLabelText(/email/i), { target: { value: 'wrong@example.com' } });
    fireEvent.input(screen.getByLabelText(/^password$/i), { target: { value: 'wrongpassword' } });
    fireEvent.click(screen.getByRole('button', { name: /login to console/i }));

    await waitFor(() => {
      expect(screen.getByText(/email or password is incorrect/i)).toBeInTheDocument();
    });
  });

  it('stores token in localStorage on successful login', async () => {
    renderWithProviders(LoginPage);

    fireEvent.input(screen.getByLabelText(/email/i), { target: { value: 'admin@example.com' } });
    fireEvent.input(screen.getByLabelText(/^password$/i), { target: { value: 'password' } });
    fireEvent.click(screen.getByRole('button', { name: /login to console/i }));

    await waitFor(() => {
      expect(localStorage.getItem('auth_token')).toBe(mockToken);
    });
  });

  it('shows "Signing in…" while login is pending', async () => {
    // Use a slow handler to catch the pending state
    server.use(
      http.post('http://localhost:5027/auth/login', async () => {
        await new Promise((resolve) => setTimeout(resolve, 200));
        return HttpResponse.json({ token: mockToken, role: 'admin', expiresAt: '2099-01-01T00:00:00Z' });
      }),
    );

    renderWithProviders(LoginPage);

    fireEvent.input(screen.getByLabelText(/email/i), { target: { value: 'admin@example.com' } });
    fireEvent.input(screen.getByLabelText(/^password$/i), { target: { value: 'password' } });
    fireEvent.click(screen.getByRole('button', { name: /login to console/i }));

    await waitFor(() => {
      expect(screen.getByText(/signing in/i)).toBeInTheDocument();
    });
  });

  it('redirects to /register when firstTime returns true', async () => {
    server.use(
      http.get('http://localhost:5027/auth/first-time', () => HttpResponse.json(true)),
    );

    renderWithProviders(LoginPage);

    await waitFor(() => {
      // The router navigates to /register — verify the URL changed
      expect(window.location.pathname).toBe('/register');
    });
  });
});
