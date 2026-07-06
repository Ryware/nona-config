import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import type { JSX } from 'solid-js';

const microsoftPopupMock = vi.fn();
const googleRenderMock = vi.fn(async (
  container: HTMLElement,
  _clientId: string,
  onCredential: (token: string) => void,
) => {
  const button = document.createElement('button');
  button.textContent = 'Continue with Google';
  button.type = 'button';
  button.onclick = () => {
    void onCredential('google-valid-token');
  };
  container.appendChild(button);

  return () => {
    container.replaceChildren();
  };
});

vi.mock('../../entities/auth/api/google-sso', () => ({
  renderGoogleSsoButton: (...args: Parameters<typeof googleRenderMock>) => googleRenderMock(...args),
}));

vi.mock('../../entities/auth/api/microsoft-sso', () => ({
  signInWithMicrosoftPopup: (...args: Parameters<typeof microsoftPopupMock>) => microsoftPopupMock(...args),
}));

import LoginPage from '../../pages/auth/LoginPage';

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
    localStorage.clear();
    googleRenderMock.mockClear();
    microsoftPopupMock.mockReset();

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
    microsoftPopupMock.mockResolvedValue('microsoft-valid-token');

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /continue with microsoft/i })).toBeInTheDocument();
    });
  });

  it('stores token in localStorage after successful Google SSO login', async () => {
    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /continue with google/i }));

    await waitFor(() => {
      expect(localStorage.getItem('auth_token')).toBeTruthy();
    });
  });

  it('shows backend rejection for failed Microsoft SSO login', async () => {
    microsoftPopupMock.mockResolvedValue('bad-token');
    server.use(
      http.post('http://localhost:5027/auth/sso/microsoft', () =>
        HttpResponse.json({ error: 'Authentication failed' }, { status: 401 }),
      ),
    );

    renderWithProviders(LoginPage);

    fireEvent.click(await screen.findByRole('button', { name: /continue with microsoft/i }));

    await waitFor(() => {
      expect(screen.getByText(/authentication failed/i)).toBeInTheDocument();
    });
  });

  it('shows a registration hint when SSO account is not registered locally', async () => {
    server.use(
      http.post('http://localhost:5027/auth/sso/google', () =>
        HttpResponse.json(
          { error: 'Authentication failed', errorCode: 'sso_user_not_registered' },
          { status: 401 },
        ),
      ),
    );

    renderWithProviders(LoginPage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /continue with google/i }));

    await waitFor(() => {
      expect(screen.getByText(/this account is not registered in the app/i)).toBeInTheDocument();
    });
  });

  it('shows popup cancellation errors from Microsoft sign-in', async () => {
    microsoftPopupMock.mockRejectedValue(new Error('Microsoft sign-in was cancelled.'));

    renderWithProviders(LoginPage);

    fireEvent.click(await screen.findByRole('button', { name: /continue with microsoft/i }));

    await waitFor(() => {
      expect(screen.getByText(/microsoft sign-in was cancelled/i)).toBeInTheDocument();
    });
  });
});
