import { http, HttpResponse } from 'msw';
import {
  mockProjects,
  mockApiKeys,
  mockEnvironments,
  mockConfigEntries,
  mockShareLinks,
  mockUsers,
  mockToken,
} from './data';

const getBaseUrl = () => {
  try {
    const url = import.meta.env.VITE_API_URL;
    if (url && url !== "undefined") {
      if (url.startsWith("http")) {
        return url.replace(/\/$/, "");
      }
      if (typeof window !== "undefined") {
        return new URL(url, window.location.origin).toString().replace(/\/$/, "");
      }
      return url.replace(/\/$/, "");
    }
  } catch {}
  return "http://localhost:5027";
};

const BASE = getBaseUrl();

export const handlers = [
  // ── Auth ────────────────────────────────────────────────────────────────────
  http.post(`${BASE}/auth/login`, async ({ request }) => {
    const body = await request.json() as { email: string; password: string };
    if (body.email === 'admin@example.com' && body.password === 'password') {
      return HttpResponse.json({ token: mockToken, role: 'admin', expiresAt: '2099-01-01T00:00:00Z' });
    }
    return HttpResponse.json({ error: 'Invalid credentials' }, { status: 401 });
  }),

  http.post(`${BASE}/auth/register`, async () => {
    return HttpResponse.json({
      success: true,
      response: { token: mockToken, role: 'admin', expiresAt: '2099-01-01T00:00:00Z' },
      error: null,
    });
  }),

  http.get(`${BASE}/auth/first-time`, () => {
    return HttpResponse.json(false);
  }),

  http.get(`${BASE}/auth/sso/config`, () => {
    return HttpResponse.json({
      google: { enabled: false, clientId: null },
      microsoft: { enabled: false, clientId: null, authority: null, tenantId: null },
    });
  }),

  http.post(`${BASE}/auth/sso/google`, async ({ request }) => {
    const body = await request.json() as { idToken?: string };
    if (body.idToken === 'google-valid-token') {
      return HttpResponse.json({ token: mockToken, role: 'admin', expiresAt: '2099-01-01T00:00:00Z' });
    }

    return HttpResponse.json({ error: 'Authentication failed' }, { status: 401 });
  }),

  http.post(`${BASE}/auth/sso/microsoft`, async ({ request }) => {
    const body = await request.json() as { idToken?: string };
    if (body.idToken === 'microsoft-valid-token') {
      return HttpResponse.json({ token: mockToken, role: 'admin', expiresAt: '2099-01-01T00:00:00Z' });
    }

    return HttpResponse.json({ error: 'Authentication failed' }, { status: 401 });
  }),

  http.get(`${BASE}/auth/invitations/:token`, ({ params }) => {
    if (params.token === 'invalid-token') {
      return HttpResponse.json(
        { error: 'Invitation is invalid or has already been used.', errorCode: 'invitation_invalid_or_used' },
        { status: 404 },
      );
    }

    return HttpResponse.json({
      email: 'invitee@example.com',
      name: 'Invited Teammate',
    });
  }),

  http.post(`${BASE}/auth/invitations/:token/password`, async ({ params, request }) => {
    if (params.token === 'invalid-token') {
      return HttpResponse.json(
        { error: 'Invitation is invalid or has already been used.', errorCode: 'invitation_invalid_or_used' },
        { status: 404 },
      );
    }

    const body = await request.json() as { newPassword?: string };
    if (!body.newPassword) {
      return HttpResponse.json({ error: 'Password is required' }, { status: 400 });
    }

    return HttpResponse.json({ token: mockToken, role: 'viewer', expiresAt: '2099-01-01T00:00:00Z' });
  }),

  http.post(`${BASE}/auth/invitations/:token/sso/google`, async ({ params, request }) => {
    if (params.token === 'invalid-token') {
      return HttpResponse.json(
        { error: 'Invitation is invalid or has already been used.', errorCode: 'invitation_invalid_or_used' },
        { status: 404 },
      );
    }

    const body = await request.json() as { idToken?: string };
    if (body.idToken === 'google-valid-token') {
      return HttpResponse.json({ token: mockToken, role: 'viewer', expiresAt: '2099-01-01T00:00:00Z' });
    }

    if (body.idToken === 'google-mismatch-token') {
      return HttpResponse.json(
        { error: 'Authentication failed', errorCode: 'invitation_sso_email_mismatch' },
        { status: 401 },
      );
    }

    return HttpResponse.json({ error: 'Authentication failed' }, { status: 401 });
  }),

  http.post(`${BASE}/auth/invitations/:token/sso/microsoft`, async ({ params, request }) => {
    if (params.token === 'invalid-token') {
      return HttpResponse.json(
        { error: 'Invitation is invalid or has already been used.', errorCode: 'invitation_invalid_or_used' },
        { status: 404 },
      );
    }

    const body = await request.json() as { idToken?: string };
    if (body.idToken === 'microsoft-valid-token') {
      return HttpResponse.json({ token: mockToken, role: 'viewer', expiresAt: '2099-01-01T00:00:00Z' });
    }

    return HttpResponse.json({ error: 'Authentication failed' }, { status: 401 });
  }),

  // ── Projects ─────────────────────────────────────────────────────────────────
  http.get(`${BASE}/admin/projects`, () => {
    return HttpResponse.json(mockProjects);
  }),

  http.get(`${BASE}/admin/projects/:slug`, ({ params }) => {
    const project = mockProjects.find((p) => p.urlSlug === params.slug);
    if (!project) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(project);
  }),

  http.post(`${BASE}/admin/projects`, async ({ request }) => {
    const body = await request.json() as { name: string; description?: string };
    const created = {
      id: 'proj-new',
      urlSlug: body.name,
      name: body.name,
      description: body.description ?? '',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    return HttpResponse.json(created, { status: 201 });
  }),

  http.put(`${BASE}/admin/projects/:slug`, async ({ params, request }) => {
    const body = await request.json() as { name: string; description?: string };
    const project = mockProjects.find((p) => p.urlSlug === params.slug);
    if (!project) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json({ ...project, ...body, updatedAt: new Date().toISOString() });
  }),

  http.delete(`${BASE}/admin/projects/:slug`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(`${BASE}/admin/projects/:projectId/api-keys`, ({ params }) => {
    return HttpResponse.json(mockApiKeys.filter((apiKey) => apiKey.project === params.projectId));
  }),

  http.post(`${BASE}/admin/projects/:projectId/api-keys`, async ({ params, request }) => {
    const body = await request.json() as { name: string; environment?: string | null; scope?: "client" | "server" | "all" };
    return HttpResponse.json({
      id: 'key-new',
      name: body.name,
      key: 'ak_test_new1234567890',
      project: params.projectId,
      environment: body.environment ?? null,
      scope: body.scope ?? 'client',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }, { status: 201 });
  }),

  http.delete(`${BASE}/admin/projects/:projectId/api-keys/:apiKeyId`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  // ── Environments ─────────────────────────────────────────────────────────────
  http.get(`${BASE}/admin/projects/:projectId/environments`, ({ params }) => {
    const envs = mockEnvironments.filter((e) => e.project === params.projectId);
    return HttpResponse.json(envs);
  }),

  http.get(`${BASE}/admin/projects/:projectId/environments/:envName`, ({ params }) => {
    const env = mockEnvironments.find(
      (e) => e.project === params.projectId && e.name === params.envName,
    );
    if (!env) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(env);
  }),

  http.post(`${BASE}/admin/projects/:projectId/environments`, async ({ params, request }) => {
    const body = await request.json() as { name: string };
    return HttpResponse.json({
      project: params.projectId,
      name: body.name,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    }, { status: 201 });
  }),

  http.delete(`${BASE}/admin/projects/:projectId/environments/:envName`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  // ── Config Entries ────────────────────────────────────────────────────────────
  http.get(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries`, ({ params }) => {
    const entries = mockConfigEntries.filter(
      (c) => c.project === params.projectId && c.environment === params.envName,
    );
    return HttpResponse.json(entries);
  }),

  http.get(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key/share-links`, ({ params }) => {
    const links = mockShareLinks.filter(
      (link) =>
        link.project === params.projectId &&
        link.environment === params.envName &&
        link.key === params.key,
    );
    return HttpResponse.json(links);
  }),

  http.post(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key/share-links`, async ({ params, request }) => {
    const body = await request.json() as { expiration?: string; canEdit?: boolean };
    return HttpResponse.json({
      id: 99,
      token: 'AbCdEf1234567890',
      project: params.projectId,
      environment: params.envName,
      key: params.key,
      canEdit: body.canEdit ?? true,
      createdBy: 'admin@example.com',
      createdAt: new Date().toISOString(),
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      revokedAt: null,
    }, { status: 201 });
  }),

  http.delete(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key/share-links/:shareLinkId`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key`, ({ params }) => {
    const entry = mockConfigEntries.find(
      (c) => c.project === params.projectId && c.environment === params.envName && c.key === params.key,
    );
    if (!entry) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(entry);
  }),

  http.get(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key/history`, ({ params }) => {
    const entry = mockConfigEntries.find(
      (c) => c.project === params.projectId && c.environment === params.envName && c.key === params.key,
    );
    if (!entry) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json([
      {
        project: entry.project,
        environment: entry.environment,
        key: entry.key,
        version: entry.activeVersion,
        value: entry.value,
        contentType: entry.contentType,
        scope: entry.scope,
        createdAt: entry.updatedAt,
        actor: 'admin@example.com',
      },
    ]);
  }),

  http.post(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key/rollback`, async ({ params, request }) => {
    const body = await request.json() as { version?: number };
    const entry = mockConfigEntries.find(
      (c) => c.project === params.projectId && c.environment === params.envName && c.key === params.key,
    );
    if (!entry || !body.version) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json({
      ...entry,
      activeVersion: entry.activeVersion + 1,
      updatedAt: new Date().toISOString(),
    });
  }),

  http.put(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key`, async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    return HttpResponse.json({
      ...mockConfigEntries[0],
      project: params.projectId,
      environment: params.envName,
      key: params.key,
      ...body,
      activeVersion: mockConfigEntries[0].activeVersion + 1,
      updatedAt: new Date().toISOString(),
    });
  }),

  http.delete(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  // ── Public Share Links ───────────────────────────────────────────────────────
  http.get(`${BASE}/public/share-links/:token`, ({ params }) => {
    if (params.token === 'Revoked123456789') {
      return HttpResponse.json(
        { error: 'This share link has been revoked.', errorCode: 'share_link_revoked' },
        { status: 410 },
      );
    }

    const entry = mockConfigEntries[0];
    return HttpResponse.json({
      environment: entry.environment,
      key: entry.key,
      value: entry.value,
      contentType: entry.contentType,
      canEdit: true,
      expiresAt: '2099-01-01T00:00:00Z',
    });
  }),

  http.put(`${BASE}/public/share-links/:token`, async ({ request }) => {
    const body = await request.json() as { value?: string };
    const entry = mockConfigEntries[0];
    return HttpResponse.json({
      environment: entry.environment,
      key: entry.key,
      value: body.value ?? entry.value,
      contentType: entry.contentType,
      canEdit: true,
      expiresAt: '2099-01-01T00:00:00Z',
    });
  }),

  // ── Users ─────────────────────────────────────────────────────────────────────
  http.get(`${BASE}/admin/users`, () => {
    return HttpResponse.json(mockUsers);
  }),

  http.get(`${BASE}/admin/users/:id`, ({ params }) => {
    const user = mockUsers.find((u) => u.id === params.id);
    if (!user) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(user);
  }),

  http.post(`${BASE}/admin/users`, async ({ request }) => {
    const body = await request.json() as { name?: string; email: string; role?: string };
    if (!body.name) {
      return HttpResponse.json({ error: 'Name is required' }, { status: 400 });
    }

    return HttpResponse.json({
      user: {
        id: 'user-new',
        email: body.email,
        name: body.name,
        isAdmin: false,
        role: body.role ?? 'viewer',
        scope: 'all',
        projects: [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      invitationToken: 'invite-token-123',
    }, { status: 201 });
  }),

  http.put(`${BASE}/admin/users/:id`, async ({ params, request }) => {
    const body = await request.json() as Record<string, unknown>;
    const user = mockUsers.find((u) => u.id === params.id);
    if (!user) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json({ ...user, ...body, updatedAt: new Date().toISOString() });
  }),

  http.delete(`${BASE}/admin/users/:id`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  // ── Dashboard ────────────────────────────────────────────────────────────────
  http.get(`${BASE}/admin/dashboard/counts`, () => {
    return HttpResponse.json({
      projects: mockProjects.length,
      users: mockUsers.length,
      configEntries: mockConfigEntries.length,
    });
  }),

  // ── Forgot Password ──────────────────────────────────────────────────────────
  http.post(`${BASE}/auth/forgot-password`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  // ── Audit Logs ───────────────────────────────────────────────────────────────
  http.get(`${BASE}/admin/audit-logs`, () => {
    return HttpResponse.json([
      {
        id: 'log-proj-1',
        actor: 'admin@example.com',
        actorIsSystem: false,
        action: 'create_project',
        target: 'my-app',
        project: 'my-app',
        environment: null,
        createdAt: '2024-01-01T00:00:00Z',
      },
      {
        id: 'log-proj-2',
        actor: 'admin@example.com',
        actorIsSystem: false,
        action: 'create_project',
        target: 'backend-api',
        project: 'backend-api',
        environment: null,
        createdAt: '2024-01-02T00:00:00Z',
      },
      {
        id: 'log-user-1',
        actor: 'admin@example.com',
        actorIsSystem: false,
        action: 'invite_user',
        target: 'alice@example.com',
        project: null,
        environment: null,
        createdAt: '2024-01-03T00:00:00Z',
      },
      {
        id: 'log-user-2',
        actor: 'admin@example.com',
        actorIsSystem: false,
        action: 'invite_user',
        target: 'bob@example.com',
        project: null,
        environment: null,
        createdAt: '2024-01-04T00:00:00Z',
      },
    ]);
  }),
];
