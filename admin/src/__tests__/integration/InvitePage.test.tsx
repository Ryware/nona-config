import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { ToastProvider } from '../../shared/ui/toast';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import type { JSX } from 'solid-js';

const microsoftRedirectMock = vi.fn();
const googleRenderMock = vi.fn(async (
  container: HTMLElement,
  _clientId: string,
  _flowId: string,
  onRedirectStart: () => void,
) => {
  const button = document.createElement('button');
  button.textContent = 'Continue with Google';
  button.type = 'button';
  button.onclick = onRedirectStart;
  container.appendChild(button);

  return () => {
    container.replaceChildren();
  };
});

vi.mock('../../entities/auth/api/google-sso', () => ({
  renderGoogleSsoButton: (...args: Parameters<typeof googleRenderMock>) => googleRenderMock(...args),
}));

vi.mock('../../entities/auth/api/microsoft-sso', () => ({
  signInWithMicrosoftRedirect: (...args: Parameters<typeof microsoftRedirectMock>) => microsoftRedirectMock(...args),
}));

import InvitePage from '../../pages/auth/InvitePage';
import {
  beginSsoRedirect,
  completeSsoRedirect,
} from '../../entities/auth/api/sso-redirect';

function renderWithProviders(path: string, ui: () => JSX.Element) {
  window.history.pushState({}, '', path);
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="/invite/:token" component={ui} />
            <Route path="/projects" component={() => <div data-testid="projects-page-stub">Projects</div>} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('InvitePage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    googleRenderMock.mockClear();
    microsoftRedirectMock.mockReset();
    server.use(
      http.get('http://localhost:5027/auth/sso/config', () =>
        HttpResponse.json({
          google: { enabled: true, clientId: 'google-client-id' },
          microsoft: {
            enabled: true,
            clientId: 'microsoft-client-id',
            authority: 'https://login.microsoftonline.com/common',
            tenantId: 'common',
          },
        }),
      ),
    );
  });

  it('renders invited user details for a valid invitation', async () => {
    renderWithProviders('/invite/invite-token-123', InvitePage);

    expect(await screen.findByRole('heading', { name: /complete your invitation/i })).toBeInTheDocument();
    expect(screen.getAllByText(/invitee@example.com/i).length).toBeGreaterThan(0);
    expect(screen.getByText(/invited teammate/i)).toBeInTheDocument();
  });

  it('auto-logs in after setting a password', async () => {
    renderWithProviders('/invite/invite-token-123', InvitePage);

    fireEvent.input(await screen.findByLabelText(/create password/i), {
      target: { value: 'Password123!' },
    });
    fireEvent.input(screen.getByLabelText(/confirm password/i), {
      target: { value: 'Password123!' },
    });
    fireEvent.click(screen.getByRole('button', { name: /set password and continue/i }));

    await waitFor(() => {
      expect(localStorage.getItem('auth_token')).toBeTruthy();
      expect(screen.getByTestId('projects-page-stub')).toBeInTheDocument();
    });
  });

  it('rejects a weak password before accepting the invitation', async () => {
    renderWithProviders('/invite/invite-token-123', InvitePage);

    fireEvent.input(await screen.findByLabelText(/create password/i), {
      target: { value: 'short' },
    });
    fireEvent.input(screen.getByLabelText(/confirm password/i), {
      target: { value: 'short' },
    });
    fireEvent.click(screen.getByRole('button', { name: /set password and continue/i }));

    expect(await screen.findByText(/^password must be at least 8 characters long\.$/i)).toBeInTheDocument();
    expect(localStorage.getItem('auth_token')).toBeNull();
  });

  it('auto-logs in after successful Google SSO', async () => {
    window.history.replaceState({}, '', '/invite/invite-token-123');
    const flowId = beginSsoRedirect('google');
    completeSsoRedirect('google', flowId, { idToken: 'google-valid-token' });

    renderWithProviders('/invite/invite-token-123', InvitePage);

    await waitFor(() => {
      expect(localStorage.getItem('auth_token')).toBeTruthy();
      expect(screen.getByTestId('projects-page-stub')).toBeInTheDocument();
    });
  });

  it('shows a terminal error state for an invalid invitation', async () => {
    renderWithProviders('/invite/invalid-token', InvitePage);

    expect(await screen.findByRole('heading', { name: /invitation unavailable/i })).toBeInTheDocument();
    expect(screen.getAllByText(/invalid or has already been used/i).length).toBeGreaterThan(0);
  });

  it('shows SSO email mismatch errors', async () => {
    window.history.replaceState({}, '', '/invite/invite-token-123');
    const flowId = beginSsoRedirect('google');
    completeSsoRedirect('google', flowId, { idToken: 'google-mismatch-token' });

    renderWithProviders('/invite/invite-token-123', InvitePage);

    await waitFor(() => {
      expect(screen.getByText(/does not match the invited account email/i)).toBeInTheDocument();
    });
  });
});
