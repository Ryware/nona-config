import { describe, it, expect, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { projectService } from '../../../entities/project/api/project.service';
import { environmentService } from '../../../entities/project/api/environment.service';
import { configEntryService } from '../../../entities/project/api/config-entry.service';
import { userService } from '../../../entities/user/api/user.service';
import { mockProjects, mockEnvironments, mockConfigEntries, mockUsers, mockToken } from '../../mocks/data';
import { server } from '../../mocks/server';

const BASE = 'http://localhost:5027';

beforeEach(() => {
  localStorage.setItem('auth_token', mockToken);
});

// ── projectService ──────────────────────────────────────────────────────────
describe('projectService', () => {
  it('getAll returns all projects', async () => {
    const result = await projectService.getAll();
    expect(result).toHaveLength(mockProjects.length);
    expect(result[0].urlSlug).toBe('my-app');
  });

  it('getById returns the matching project', async () => {
    const result = await projectService.getById('my-app');
    expect(result.id).toBe('proj-1');
    expect(result.name).toBe('my-app');
  });

  it('getById throws 404 for unknown slug', async () => {
    await expect(projectService.getById('does-not-exist')).rejects.toThrow();
  });

  it('create returns new project with given name', async () => {
    const result = await projectService.create({ name: 'new-project', description: 'A new one' });
    expect(result.name).toBe('new-project');
    expect(result.urlSlug).toBe('new-project');
  });

  it('update returns updated project', async () => {
    const result = await projectService.update('my-app', { name: 'my-app', description: 'Updated desc' });
    expect(result.description).toBe('Updated desc');
  });

  it('delete resolves without error', async () => {
    await expect(projectService.delete('my-app')).resolves.toBeDefined();
  });
});

// ── environmentService ───────────────────────────────────────────────────────
describe('environmentService', () => {
  it('getAll returns environments for a project', async () => {
    const result = await environmentService.getAll('my-app');
    expect(result).toHaveLength(mockEnvironments.length);
    expect(result[0].name).toBe('production');
  });

  it('getById returns the matching environment', async () => {
    const result = await environmentService.getById('my-app', 'production');
    expect(result.name).toBe('production');
    expect(result.project).toBe('my-app');
  });

  it('create returns new environment', async () => {
    const result = await environmentService.create({ projectId: 'my-app', name: 'development' });
    expect(result.name).toBe('development');
    expect(result.project).toBe('my-app');
  });

  it('delete resolves without error', async () => {
    await expect(environmentService.delete('my-app', 'staging')).resolves.toBeDefined();
  });
});

// ── configEntryService ───────────────────────────────────────────────────────
describe('configEntryService', () => {
  it('getAll returns config entries for a project and environment', async () => {
    const result = await configEntryService.getAll('my-app', 'production');
    expect(result).toHaveLength(mockConfigEntries.length);
    expect(result[0].key).toBe('API_URL');
  });

  it('getById returns the matching config entry', async () => {
    const result = await configEntryService.getById('my-app', 'production', 'API_URL');
    expect(result.key).toBe('API_URL');
    expect(result.value).toBe('https://api.example.com');
  });

  it('upsert updates a config entry value', async () => {
    const result = await configEntryService.upsert('my-app', 'production', 'API_URL', {
      value: 'https://new-api.example.com',
      contentType: 'text',
      scope: 'server',
    });
    expect(result.value).toBe('https://new-api.example.com');
  });

  it('upsert encodes URL-significant key characters before sending the request', async () => {
    let requestedPath = '';
    server.use(
      http.put(`${BASE}/admin/projects/:projectId/environments/:envName/config-entries/:key`, ({ request }) => {
        requestedPath = new URL(request.url).pathname;
        return HttpResponse.json({
          project: 'my-app',
          environment: 'production',
          key: 'feature?flag#one',
          value: 'true',
          contentType: 'boolean',
          scope: 'client',
          activeVersion: 2,
          createdAt: '2026-06-21T10:00:00Z',
          updatedAt: '2026-06-22T10:00:00Z',
        });
      }),
    );

    await configEntryService.upsert('my-app', 'production', 'feature?flag#one', {
      value: 'true',
      contentType: 'boolean',
      scope: 'client',
    });

    expect(requestedPath).toBe('/admin/projects/my-app/environments/production/config-entries/feature%3Fflag%23one');
  });

  it('history returns config entry versions', async () => {
    const result = await configEntryService.history('my-app', 'production', 'API_URL');
    expect(result[0].version).toBe(1);
    expect(result[0].value).toBe('https://api.example.com');
  });

  it('rollback returns the new active config entry', async () => {
    const result = await configEntryService.rollback('my-app', 'production', 'API_URL', {
      version: 1,
    });
    expect(result.key).toBe('API_URL');
    expect(result.activeVersion).toBeGreaterThan(1);
  });

  it('delete resolves without error', async () => {
    await expect(configEntryService.delete('my-app', 'production', 'API_URL')).resolves.toBeDefined();
  });
});

// ── userService ──────────────────────────────────────────────────────────────
describe('userService', () => {
  it('getAll returns all users', async () => {
    const result = await userService.getAll();
    expect(result).toHaveLength(mockUsers.length);
    expect(result[0].email).toBe('alice@example.com');
  });

  it('getById returns the matching user', async () => {
    const result = await userService.getById('user-1');
    expect(result.id).toBe('user-1');
    expect(result.role).toBe('editor');
  });

  it('getById throws 404 for unknown id', async () => {
    await expect(userService.getById('no-such-user')).rejects.toThrow();
  });

  it('create returns new user', async () => {
    const result = await userService.create({
      name: 'Charlie Viewer',
      email: 'charlie@example.com',
      role: 'viewer',
    });
    expect(result.user.email).toBe('charlie@example.com');
    expect(result.user.role).toBe('viewer');
    expect(result.invitationToken).toBe('invite-token-123');
  });

  it('update returns updated user', async () => {
    const result = await userService.update('user-2', { role: 'admin' });
    expect(result.id).toBe('user-2');
    expect((result as any).role).toBe('admin');
  });

  it('delete resolves without error', async () => {
    await expect(userService.delete('user-1')).resolves.toBeDefined();
  });
});
