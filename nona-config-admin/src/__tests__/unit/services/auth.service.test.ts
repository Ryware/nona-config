import { describe, it, expect, beforeEach, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';
import { authService } from '../../../entities/auth/api/auth.service';
import { mockToken } from '../../mocks/data';

const BASE = 'http://localhost:5027';

describe('authService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  describe('login', () => {
    it('returns login response from API', async () => {
      const result = await authService.login({ email: 'admin@example.com', password: 'password' });
      expect(result.token).toBe(mockToken);
      expect(result.role).toBe('admin');
    });

    it('throws on invalid credentials (401)', async () => {
      await expect(
        authService.login({ email: 'wrong@example.com', password: 'wrong' }),
      ).rejects.toThrow('Invalid credentials');
    });
  });

  describe('register', () => {
    it('returns success result with token', async () => {
      const result = await authService.register({ email: 'new@example.com', password: 'secret' });
      expect(result.success).toBe(true);
      expect(result.response?.token).toBe(mockToken);
    });
  });

  describe('logout', () => {
    it('removes auth_token from localStorage', () => {
      localStorage.setItem('auth_token', mockToken);
      const mockLocation = { href: '' } as Location;
      vi.spyOn(window, 'location', 'get').mockReturnValue(mockLocation);

      authService.logout();

      expect(localStorage.getItem('auth_token')).toBeNull();
    });

    it('redirects to /login', () => {
      const mockLocation = { href: '' } as Location;
      vi.spyOn(window, 'location', 'get').mockReturnValue(mockLocation);

      authService.logout();

      expect(mockLocation.href).toBe('/login');
    });
  });

  describe('isAuthenticated', () => {
    it('returns true when token is in localStorage', () => {
      localStorage.setItem('auth_token', mockToken);
      expect(authService.isAuthenticated()).toBe(true);
    });

    it('returns false when no token in localStorage', () => {
      expect(authService.isAuthenticated()).toBe(false);
    });
  });

  describe('getToken', () => {
    it('returns token from localStorage', () => {
      localStorage.setItem('auth_token', mockToken);
      expect(authService.getToken()).toBe(mockToken);
    });

    it('returns null when no token', () => {
      expect(authService.getToken()).toBeNull();
    });
  });

  describe('firstTime', () => {
    it('returns false when first-time check returns false', async () => {
      localStorage.setItem('auth_token', mockToken);
      const result = await authService.firstTime();
      expect(result).toBe(false);
    });

    it('returns true when first-time check returns true', async () => {
      localStorage.setItem('auth_token', mockToken);
      server.use(
        http.get(`${BASE}/auth/first-time`, () => HttpResponse.json(true)),
      );
      const result = await authService.firstTime();
      expect(result).toBe(true);
    });
  });

  describe('SSO', () => {
    it('returns SSO runtime config', async () => {
      const result = await authService.getSsoConfig();
      expect(result.google.enabled).toBe(false);
      expect(result.microsoft.enabled).toBe(false);
    });

    it('logs in with Google token', async () => {
      const result = await authService.loginWithGoogle('google-valid-token');
      expect(result.token).toBe(mockToken);
    });

    it('logs in with Microsoft token', async () => {
      const result = await authService.loginWithMicrosoft('microsoft-valid-token');
      expect(result.token).toBe(mockToken);
    });
  });

  describe('requestPasswordReset / resetPassword', () => {
    it('successfully calls requestPasswordReset', async () => {
      await expect(authService.requestPasswordReset({ email: 'x@x.com' })).resolves.toBeUndefined();
    });

    it('throws not implemented error for resetPassword', async () => {
      await expect(
        authService.resetPassword({ token: 'tok', newPassword: 'pw' }),
      ).rejects.toThrow('Password reset functionality is not yet implemented');
    });
  });
});
