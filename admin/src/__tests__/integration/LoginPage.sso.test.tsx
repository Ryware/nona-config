import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
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

import LoginPage from '../../pages/auth/LoginPage';
import {
  beginSsoRedirect,
  completeSsoRedirect,
  getPendingSsoFlow,
  type SsoProviderName,
} from '../../entities/auth/api/sso-redirect';

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

describe('LoginPage SSO', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/login');
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

  it('shows Google and Microsoft SSO entry points when enabled', async () => {
    microsoftRedirectMock.mockResolvedValue(undefined);

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /continue with microsoft/i })).toBeInTheDocument();
    });
  });

  it('starts Google SSO as a redirect without logging in through a popup callback', async () => {
    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /continue with google/i }));

    expect(getPendingSsoFlow('google')).toMatch(/^[a-f0-9]{32}$/);
    expect(localStorage.getItem('auth_token')).toBeNull();
  });

  it('stores token after returning from a successful Google redirect', async () => {
    seedRedirectResult('google', 'google-valid-token');

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(localStorage.getItem('auth_token')).toBeTruthy();
    });
  });

  it('preserves the session-only preference across an SSO redirect', async () => {
    const firstRender = renderWithProviders(LoginPage);
    fireEvent.click(screen.getByText(/remember me on this device/i));
    fireEvent.click(await screen.findByRole('button', { name: /continue with google/i }));

    const flowId = getPendingSsoFlow('google');
    expect(flowId).not.toBeNull();
    firstRender.unmount();
    completeSsoRedirect('google', flowId!, { idToken: 'google-valid-token' });

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(sessionStorage.getItem('auth_token')).toBeTruthy();
    });
    expect(localStorage.getItem('auth_token')).toBeNull();
  });

  it('shows backend rejection after returning from Microsoft SSO', async () => {
    seedRedirectResult('microsoft', 'bad-token');
    server.use(
      http.post('http://localhost:5027/auth/sso/microsoft', () =>
        HttpResponse.json({ detail: 'Authentication failed' }, { status: 401 }),
      ),
    );

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByText(/authentication failed/i)).toBeInTheDocument();
    });
  });

  it('shows a registration hint when SSO account is not registered locally', async () => {
    seedRedirectResult('google', 'google-valid-token');
    server.use(
      http.post('http://localhost:5027/auth/sso/google', () =>
        HttpResponse.json(
          { detail: 'Authentication failed', errorCode: 'sso_user_not_registered' },
          { status: 401 },
        ),
      ),
    );

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByText(/this account is not registered in the app/i)).toBeInTheDocument();
    });
  });

  it('shows Microsoft redirect initiation errors', async () => {
    microsoftRedirectMock.mockRejectedValue(new Error('Microsoft sign-in is unavailable right now.'));

    renderWithProviders(LoginPage);

    fireEvent.click(await screen.findByRole('button', { name: /continue with microsoft/i }));

    await waitFor(() => {
      expect(screen.getByText(/microsoft sign-in is unavailable right now/i)).toBeInTheDocument();
    });
  });
});

function seedRedirectResult(provider: SsoProviderName, idToken: string) {
  const flowId = beginSsoRedirect(provider);
  completeSsoRedirect(provider, flowId, { idToken });
}
