import { describe, it, expect, beforeEach, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../../mocks/server';
import type { ApiRequestError } from '../../../shared/api/client';
import { ApiClient } from '../../../shared/api/client';

const BASE = 'http://localhost:5027';

describe('ApiClient', () => {
  let client: ApiClient;

  beforeEach(() => {
    client = new ApiClient();
    localStorage.clear();
    vi.spyOn(window, 'location', 'get').mockReturnValue({
      ...window.location,
      href: '',
    } as Location);
  });

  describe('auth header', () => {
    it('includes Authorization header when token is in localStorage', async () => {
      localStorage.setItem('auth_token', 'my-test-token');

      let capturedAuth: string | null = null;
      server.use(
        http.get(`${BASE}/test-auth`, ({ request }) => {
          capturedAuth = request.headers.get('Authorization');
          return HttpResponse.json({ ok: true });
        }),
      );

      await client.get('/test-auth');
      expect(capturedAuth).toBe('Bearer my-test-token');
    });

    it('omits Authorization header when no token in localStorage', async () => {
      let capturedAuth: string | null = 'present';
      server.use(
        http.get(`${BASE}/test-no-auth`, ({ request }) => {
          capturedAuth = request.headers.get('Authorization');
          return HttpResponse.json({ ok: true });
        }),
      );

      await client.get('/test-no-auth');
      expect(capturedAuth).toBeNull();
    });
  });

  describe('error handling', () => {
    it('throws with error.error field when present', async () => {
      server.use(
        http.get(`${BASE}/test-error`, () =>
          HttpResponse.json({ error: 'Something went wrong' }, { status: 400 }),
        ),
      );

      await expect(client.get('/test-error')).rejects.toThrow('Something went wrong');
    });

    it('throws with error.message field as fallback', async () => {
      server.use(
        http.get(`${BASE}/test-message`, () =>
          HttpResponse.json({ message: 'Bad request' }, { status: 400 }),
        ),
      );

      await expect(client.get('/test-message')).rejects.toThrow('Bad request');
    });

    it('throws generic message when no error fields in body', async () => {
      server.use(
        http.get(`${BASE}/test-generic`, () =>
          HttpResponse.json({}, { status: 500 }),
        ),
      );

      await expect(client.get('/test-generic')).rejects.toThrow('Request failed');
    });

    it('preserves backend errorCode on thrown API errors', async () => {
      server.use(
        http.get(`${BASE}/test-error-code`, () =>
          HttpResponse.json({ error: 'Authentication failed', errorCode: 'sso_user_not_registered' }, { status: 401 }),
        ),
      );

      await expect(client.get('/test-error-code')).rejects.toMatchObject({
        name: 'ApiRequestError',
        message: 'Authentication failed',
        code: 'sso_user_not_registered',
      } satisfies Partial<ApiRequestError>);
    });
  });

  describe('204 No Content', () => {
    it('returns empty object for 204 responses', async () => {
      server.use(
        http.delete(`${BASE}/test-delete`, () =>
          new HttpResponse(null, { status: 204 }),
        ),
      );

      const result = await client.delete('/test-delete');
      expect(result).toEqual({});
    });
  });

  describe('401 redirect behaviour', () => {
    it('dispatches auth:unauthorized on 401 for non-allowlisted endpoints', async () => {
      localStorage.setItem('auth_token', 'expired-token');
      const unauthorizedSpy = vi.fn();
      window.addEventListener('auth:unauthorized', unauthorizedSpy);

      server.use(
        http.get(`${BASE}/admin/projects`, () =>
          HttpResponse.json({ error: 'Unauthorized' }, { status: 401 }),
        ),
      );

      await expect(client.get('/admin/projects')).rejects.toThrow();
      expect(unauthorizedSpy).toHaveBeenCalledTimes(1);

      window.removeEventListener('auth:unauthorized', unauthorizedSpy);
    });

    it('does NOT redirect for allowlisted endpoint /auth/login on 401', async () => {
      const mockLocation = { href: '' } as Location;
      vi.spyOn(window, 'location', 'get').mockReturnValue(mockLocation);

      // Override the default handler to return 401 for login
      server.use(
        http.post(`${BASE}/auth/login`, () =>
          HttpResponse.json({ error: 'Invalid credentials' }, { status: 401 }),
        ),
      );

      await expect(client.post('/auth/login', { email: 'x', password: 'y' })).rejects.toThrow();
      expect(mockLocation.href).toBe('');
    });

    it('does NOT redirect for allowlisted SSO endpoint on 401', async () => {
      const mockLocation = { href: '' } as Location;
      vi.spyOn(window, 'location', 'get').mockReturnValue(mockLocation);

      server.use(
        http.post(`${BASE}/auth/sso/google`, () =>
          HttpResponse.json({ error: 'Authentication failed' }, { status: 401 }),
        ),
      );

      await expect(client.post('/auth/sso/google', { idToken: 'bad-token' })).rejects.toThrow();
      expect(mockLocation.href).toBe('');
    });
  });

  describe('HTTP methods', () => {
    it('sends GET request', async () => {
      server.use(
        http.get(`${BASE}/items`, () => HttpResponse.json([1, 2, 3])),
      );
      const result = await client.get<number[]>('/items');
      expect(result).toEqual([1, 2, 3]);
    });

    it('sends POST request with JSON body', async () => {
      let receivedBody: unknown;
      server.use(
        http.post(`${BASE}/items`, async ({ request }) => {
          receivedBody = await request.json();
          return HttpResponse.json({ id: 1 }, { status: 201 });
        }),
      );
      await client.post('/items', { name: 'test' });
      expect(receivedBody).toEqual({ name: 'test' });
    });

    it('sends PUT request with JSON body', async () => {
      let receivedBody: unknown;
      server.use(
        http.put(`${BASE}/items/1`, async ({ request }) => {
          receivedBody = await request.json();
          return HttpResponse.json({ id: 1 });
        }),
      );
      await client.put('/items/1', { name: 'updated' });
      expect(receivedBody).toEqual({ name: 'updated' });
    });

    it('sends DELETE request', async () => {
      let method: string | undefined;
      server.use(
        http.delete(`${BASE}/items/1`, ({ request }) => {
          method = request.method;
          return new HttpResponse(null, { status: 204 });
        }),
      );
      await client.delete('/items/1');
      expect(method).toBe('DELETE');
    });
  });
});
