import { beforeEach, describe, expect, it } from 'vitest';
import {
  beginSsoRedirect,
  completeSsoRedirect,
  consumeSsoRedirectResult,
  getPendingSsoFlow,
} from '../../../entities/auth/api/sso-redirect';

describe('SSO redirect state', () => {
  beforeEach(() => {
    sessionStorage.clear();
    window.history.replaceState({}, '', '/login?redirect=%2Fprojects');
  });

  it('preserves the return path and exposes the result once', () => {
    const flowId = beginSsoRedirect('google');

    expect(getPendingSsoFlow('google')).toBe(flowId);
    expect(completeSsoRedirect('google', flowId, { idToken: 'id-token' }))
      .toBe('/login?redirect=%2Fprojects');
    expect(consumeSsoRedirectResult()).toEqual({
      provider: 'google',
      idToken: 'id-token',
    });
    expect(consumeSsoRedirectResult()).toBeNull();
  });

  it('rejects redirects started outside supported authentication pages', () => {
    window.history.replaceState({}, '', '/projects');

    expect(() => beginSsoRedirect('microsoft')).toThrow(
      /supported sign-in page/i,
    );
  });
});
