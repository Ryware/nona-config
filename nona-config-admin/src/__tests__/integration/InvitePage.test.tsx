import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { ToastProvider } from '../../shared/ui/toast';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
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

import InvitePage from '../../pages/auth/InvitePage';

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

  it('auto-logs in after successful Google SSO', async () => {
    renderWithProviders('/invite/invite-token-123', InvitePage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /continue with google/i }));

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
    googleRenderMock.mockImplementationOnce(async (
      container: HTMLElement,
      _clientId: string,
      onCredential: (token: string) => void,
    ) => {
      const button = document.createElement('button');
      button.textContent = 'Continue with Google';
      button.type = 'button';
      button.onclick = () => {
        void onCredential('google-mismatch-token');
      };
      container.appendChild(button);

      return () => {
        container.replaceChildren();
      };
    });

    renderWithProviders('/invite/invite-token-123', InvitePage);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /continue with google/i })).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /continue with google/i }));

    await waitFor(() => {
      expect(screen.getByText(/does not match the invited account email/i)).toBeInTheDocument();
    });
  });
});
