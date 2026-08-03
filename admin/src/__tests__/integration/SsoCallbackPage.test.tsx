import { render, screen, waitFor } from '@solidjs/testing-library';
import { Route, Router } from '@solidjs/router';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ThemeProvider } from '../../shared/hooks/useTheme';
import { server } from '../mocks/server';

const consumeGoogleCredentialMock = vi.fn();
const handleMicrosoftRedirectMock = vi.fn();

vi.mock('../../entities/auth/api/google-sso', () => ({
  consumeGoogleRedirectCredential: (...args: Parameters<typeof consumeGoogleCredentialMock>) =>
    consumeGoogleCredentialMock(...args),
}));

vi.mock('../../entities/auth/api/microsoft-sso', () => ({
  handleMicrosoftRedirect: (...args: Parameters<typeof handleMicrosoftRedirectMock>) =>
    handleMicrosoftRedirectMock(...args),
}));

import {
  beginSsoRedirect,
  consumeSsoRedirectResult,
} from '../../entities/auth/api/sso-redirect';
import SsoCallbackPage from '../../pages/auth/SsoCallbackPage';

describe('SsoCallbackPage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    consumeGoogleCredentialMock.mockReset();
    handleMicrosoftRedirectMock.mockReset();
  });

  it('collects the Google credential and routes back to login', async () => {
    window.history.replaceState({}, '', '/login?redirect=%2Fprojects');
    const flowId = beginSsoRedirect('google');
    window.history.replaceState({}, '', `/sso/callback/google?flow=${flowId}`);
    consumeGoogleCredentialMock.mockResolvedValue('google-valid-token');

    renderCallbackRoutes();

    expect(await screen.findByTestId('login-return')).toBeInTheDocument();
    expect(consumeGoogleCredentialMock).toHaveBeenCalledWith(flowId);
    expect(consumeSsoRedirectResult()).toEqual({
      provider: 'google',
      idToken: 'google-valid-token',
    });
  });

  it('handles the Microsoft result and routes back to an invitation', async () => {
    window.history.replaceState({}, '', '/invite/invite-token-123');
    beginSsoRedirect('microsoft');
    window.history.replaceState({}, '', '/sso/callback/microsoft');
    handleMicrosoftRedirectMock.mockResolvedValue('microsoft-valid-token');
    server.use(
      http.get('http://localhost:5027/auth/sso/config', () =>
        HttpResponse.json({
          google: { enabled: false, clientId: null },
          microsoft: {
            enabled: true,
            clientId: 'microsoft-client-id',
            authority: 'https://login.microsoftonline.com/common',
            tenantId: 'common',
          },
        }),
      ),
    );

    renderCallbackRoutes();

    expect(await screen.findByTestId('invite-return')).toBeInTheDocument();
    expect(handleMicrosoftRedirectMock).toHaveBeenCalledWith(
      'microsoft-client-id',
      'https://login.microsoftonline.com/common',
    );
    expect(consumeSsoRedirectResult()).toEqual({
      provider: 'microsoft',
      idToken: 'microsoft-valid-token',
    });
  });

  it('routes Google callback verification errors back to the login page', async () => {
    window.history.replaceState({}, '', '/login');
    const flowId = beginSsoRedirect('google');
    window.history.replaceState(
      {},
      '',
      `/sso/callback/google?flow=${flowId}&error=csrf`,
    );

    renderCallbackRoutes();

    expect(await screen.findByTestId('login-return')).toBeInTheDocument();
    expect(consumeGoogleCredentialMock).not.toHaveBeenCalled();
    expect(consumeSsoRedirectResult()).toEqual({
      provider: 'google',
      error: 'Google sign-in could not be verified. Please try again.',
    });
  });

  it('shows a terminal error when the redirect context is missing', async () => {
    window.history.replaceState({}, '', '/sso/callback/google');

    renderCallbackRoutes();

    await waitFor(() => {
      expect(screen.getByText(/missing or has expired/i)).toBeInTheDocument();
    });
  });
});

function renderCallbackRoutes() {
  return render(() => (
    <ThemeProvider>
      <Router>
        <Route path="/sso/callback/:provider" component={SsoCallbackPage} />
        <Route path="/login" component={() => <div data-testid="login-return">Login</div>} />
        <Route
          path="/invite/:token"
          component={() => <div data-testid="invite-return">Invitation</div>}
        />
      </Router>
    </ThemeProvider>
  ));
}
