import { fireEvent, screen, waitFor } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { writeClipboard } from '@solid-primitives/clipboard';

import { setActiveEnvironmentName } from '../../entities/project/model/active-environment';
import { projectKeys } from '../../entities/project/queries/keys';
import { mockProjects } from '../mocks/data';
import { server } from '../mocks/server';
import {
  renderProjectSections,
  resetProjectSectionsTestState,
} from './project-sections.test-utils';

vi.mock('@solid-primitives/clipboard', () => ({
  writeClipboard: vi.fn(() => Promise.resolve()),
}));

describe('ProjectApiKeysSection', () => {
  beforeEach(() => {
    resetProjectSectionsTestState();
  });

  it('renders API keys on the dedicated api keys page', async () => {
    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();
    expect(await screen.findByText('Web Client')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add environment/i })).not.toBeInTheDocument();
  });

  it('renders only a masked fingerprint for listed keys', async () => {
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/api-keys', () =>
        HttpResponse.json([
          {
            id: 'key-1',
            name: 'Web Client',
            fingerprint: '90ABCDEF',
            project: 'my-app',
            environment: 'production',
            scope: 'client',
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-01T00:00:00Z',
          },
        ]),
      ),
    );

    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('api-key-value-key-1')).toHaveTextContent('••••••••90ABCDEF');
    expect(screen.queryByRole('button', { name: /reveal api key/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /copy api key/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /regenerate .* api key/i })).not.toBeInTheDocument();
  });

  it('denies project Viewers direct access to API keys', async () => {
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'viewer@example.com', role: 'member' }),
    );
    server.use(
      http.get('http://localhost:5027/admin/projects', () =>
        HttpResponse.json([{ ...mockProjects[0], accessLevel: 'viewer' }]),
      ),
    );

    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('access-denied')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add api key/i })).not.toBeInTheDocument();
  });

  it('opens the API key form when the add button is clicked', async () => {
    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();
    expect(screen.queryByTestId('api-key-name-input')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));

    expect(await screen.findByTestId('api-key-name-input')).toBeInTheDocument();
  });

  it('shows a created API key once and clears it when dismissed', async () => {
    const secret = '0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF';
    const createRequests: Array<{
      name: string;
      environment?: string | null;
      scope?: string | null;
    }> = [];

    server.use(
      http.post('http://localhost:5027/admin/projects/:projectId/api-keys', async ({ request }) => {
        const body = (await request.json()) as {
          name: string;
          environment?: string | null;
          scope?: 'client' | 'server' | 'all';
        };
        createRequests.push(body);

        return HttpResponse.json(
          {
            id: 'key-new',
            name: body.name,
            key: secret,
            fingerprint: '89ABCDEF',
            project: 'my-app',
            environment: body.environment ?? null,
            scope: body.scope ?? 'client',
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
          { status: 201 },
        );
      }),
    );

    setActiveEnvironmentName('my-app', 'staging');

    const { queryClient } = renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));
    fireEvent.input(await screen.findByTestId('api-key-name-input'), {
      target: { value: 'Staging Client Key' },
    });
    fireEvent.click(screen.getByTestId('api-key-create-button'));

    await waitFor(() => {
      expect(createRequests).toEqual([
        {
          name: 'Staging Client Key',
          environment: 'staging',
          scope: 'client',
        },
      ]);
    });

    expect(await screen.findByText(/cannot be recovered/i)).toBeInTheDocument();
    expect(screen.getByTestId('one-time-api-key-value')).toHaveTextContent(secret);

    fireEvent.click(screen.getByRole('button', { name: /copy new api key/i }));
    await waitFor(() => expect(writeClipboard).toHaveBeenCalledWith(secret));

    fireEvent.click(screen.getByRole('button', { name: /dismiss api key/i }));
    expect(screen.queryByText(secret)).not.toBeInTheDocument();
    expect(JSON.stringify(queryClient.getQueryData(projectKeys.apiKeys('my-app'))) ?? '').not.toContain(secret);
    expect(JSON.stringify(queryClient.getMutationCache().getAll().map(mutation => mutation.state.data)))
      .not.toContain(secret);
  });

  it('clears the previous one-time secret before another create attempt', async () => {
    const previousSecret = 'FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210';
    let createRequests = 0;
    server.use(
      http.post('http://localhost:5027/admin/projects/:projectId/api-keys', ({ params }) => {
        createRequests += 1;
        if (createRequests > 1) {
          return HttpResponse.json({ detail: 'Create failed' }, { status: 500 });
        }
        return HttpResponse.json({
          id: 'key-new',
          name: 'Temporary Key',
          key: previousSecret,
          fingerprint: '76543210',
          project: params.projectId,
          environment: null,
          scope: 'client',
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        }, { status: 201 });
      }),
    );

    renderProjectSections('/projects/my-app/api-keys');
    await screen.findByText('Web Client');

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));
    fireEvent.input(await screen.findByTestId('api-key-name-input'), {
      target: { value: 'Temporary Key' },
    });
    fireEvent.click(screen.getByTestId('api-key-create-button'));
    expect(await screen.findByTestId('one-time-api-key-value')).toHaveTextContent(previousSecret);
    await waitFor(() => {
      expect(screen.queryByTestId('api-key-name-input')).not.toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));
    const replacementNameInput = await screen.findByTestId('api-key-name-input');
    fireEvent.input(replacementNameInput, {
      target: { value: 'Another Key' },
    });
    const createButton = screen.getByTestId('api-key-create-button');
    await waitFor(() => expect(createButton).toBeEnabled());
    fireEvent.click(createButton);

    await waitFor(() => expect(createRequests).toBe(2));
    await waitFor(() => expect(screen.queryByText(previousSecret)).not.toBeInTheDocument());
  });

  it('clears a newly created secret when that key is deleted', async () => {
    const secret = 'FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210';
    let created = false;
    let deleted = false;
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/api-keys', () =>
        HttpResponse.json([{
          id: 'key-1',
          name: 'Web Client',
          fingerprint: '90ABCDEF',
          project: 'my-app',
          environment: 'production',
          scope: 'client',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        }, ...(created && !deleted ? [{
          id: 'key-new',
          name: 'Temporary Key',
          fingerprint: '76543210',
          project: 'my-app',
          environment: null,
          scope: 'client',
          createdAt: '2026-01-01T00:00:00Z',
          updatedAt: '2026-01-01T00:00:00Z',
        }] : [])]),
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/api-keys',
        ({ params }) => {
          created = true;
          return HttpResponse.json({
            id: 'key-new',
            name: 'Temporary Key',
            key: secret,
            fingerprint: '76543210',
            project: params.projectId,
            environment: null,
            scope: 'client',
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          }, { status: 201 });
        },
      ),
      http.delete(
        'http://localhost:5027/admin/projects/:projectId/api-keys/:apiKeyId',
        ({ params }) => {
          if (params.apiKeyId === 'key-new') deleted = true;
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );

    renderProjectSections('/projects/my-app/api-keys');
    await screen.findByText('Web Client');
    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));
    fireEvent.input(await screen.findByTestId('api-key-name-input'), {
      target: { value: 'Temporary Key' },
    });
    fireEvent.click(screen.getByTestId('api-key-create-button'));
    expect(await screen.findByTestId('one-time-api-key-value')).toHaveTextContent(secret);
    expect(await screen.findByText('Temporary Key')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /delete temporary key api key/i }));
    fireEvent.click(await screen.findByTestId('delete-api-key-confirm-button'));

    await waitFor(() => expect(deleted).toBe(true));
    await waitFor(() => expect(screen.queryByText(secret)).not.toBeInTheDocument());
    await waitFor(() => expect(screen.queryByText('Temporary Key')).not.toBeInTheDocument());
  });

  it('keeps a delayed API key creation scoped to its original project', async () => {
    const secret = 'FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210';
    let releaseCreateResponse: (() => void) | undefined;
    let requestedProjectId: string | undefined;
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/api-keys',
        async ({ params }) => {
          requestedProjectId = String(params.projectId);
          await new Promise<void>(resolve => {
            releaseCreateResponse = resolve;
          });

          return HttpResponse.json({
            id: 'key-new',
            name: 'Temporary Key',
            key: secret,
            fingerprint: '76543210',
            project: params.projectId,
            environment: null,
            scope: 'client',
            createdAt: '2026-01-01T00:00:00Z',
            updatedAt: '2026-01-01T00:00:00Z',
          }, { status: 201 });
        },
      ),
    );

    const { queryClient } = renderProjectSections('/projects/my-app/api-keys');
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');
    await screen.findByText('Web Client');
    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));
    fireEvent.input(await screen.findByTestId('api-key-name-input'), {
      target: { value: 'Temporary Key' },
    });
    fireEvent.click(screen.getByTestId('api-key-create-button'));
    await waitFor(() => expect(releaseCreateResponse).toBeTypeOf('function'));
    expect(requestedProjectId).toBe('my-app');

    window.history.pushState({}, '', '/projects/backend-api/api-keys');
    window.dispatchEvent(new PopStateEvent('popstate'));

    await waitFor(() => expect(window.location.pathname).toBe('/projects/backend-api/api-keys'));
    releaseCreateResponse?.();

    await waitFor(() => {
      expect(invalidateQueries).toHaveBeenCalledWith({
        queryKey: projectKeys.apiKeys('my-app'),
      });
    });
    expect(invalidateQueries).not.toHaveBeenCalledWith({
      queryKey: projectKeys.apiKeys('backend-api'),
    });
    expect(screen.queryByText(/cannot be recovered/i)).not.toBeInTheDocument();
    expect(screen.queryByTestId('one-time-api-key-value')).not.toBeInTheDocument();
    expect(screen.queryByText(secret)).not.toBeInTheDocument();
    expect(JSON.stringify(queryClient.getMutationCache().getAll().map(mutation => mutation.state.data)))
      .not.toContain(secret);
  });

  it('requires confirmation before deletion and removes the deleted row', async () => {
    let deleted = false;
    let deleteRequests = 0;
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/api-keys', () =>
        HttpResponse.json(deleted ? [] : [{
          id: 'key-1',
          name: 'Web Client',
          fingerprint: '90ABCDEF',
          project: 'my-app',
          environment: 'production',
          scope: 'client',
          createdAt: '2024-01-01T00:00:00Z',
          updatedAt: '2024-01-01T00:00:00Z',
        }]),
      ),
      http.delete(
        'http://localhost:5027/admin/projects/:projectId/api-keys/:apiKeyId',
        () => {
          deleteRequests += 1;
          deleted = true;
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );

    renderProjectSections('/projects/my-app/api-keys');
    await screen.findByText('Web Client');

    fireEvent.click(screen.getByRole('button', { name: /delete web client api key/i }));
    expect(await screen.findByTestId('delete-api-key-dialog')).toBeInTheDocument();
    expect(deleteRequests).toBe(0);

    fireEvent.click(screen.getByTestId('delete-api-key-confirm-button'));

    await waitFor(() => expect(deleteRequests).toBe(1));
    await waitFor(() => expect(screen.queryByText('Web Client')).not.toBeInTheDocument());
  });
});
