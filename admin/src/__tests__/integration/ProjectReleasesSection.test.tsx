import { fireEvent, screen, waitFor } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { setActiveEnvironmentName } from '../../entities/project/model/active-environment';
import { projectKeys } from '../../entities/project/queries/keys';
import { server } from '../mocks/server';
import {
  renderProjectSections,
  resetProjectSectionsTestState,
} from './project-sections.test-utils';

describe('ProjectReleasesSection', () => {
  beforeEach(() => {
    resetProjectSectionsTestState();
  });

  it('publishes a configuration release without auto-activating when a release is already active', async () => {
    const publishRequests: Array<{ version: string; makeActive: boolean }> = [];
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ params, request }) => {
          const body = (await request.json()) as { version: string; makeActive: boolean };
          publishRequests.push(body);
          return HttpResponse.json(
            {
              project: params.projectId,
              environment: params.envName,
              version: body.version,
              entryCount: 3,
              isActive: body.makeActive,
              createdAt: new Date().toISOString(),
              actor: 'admin@example.com',
              entries: [],
            },
            { status: 201 },
          );
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-create-version-button'));
    fireEvent.input(await screen.findByTestId('release-version-input'), {
      target: { value: '1.2' },
    });
    fireEvent.click(screen.getByTestId('release-version-confirm-button'));

    fireEvent.click(await screen.findByTestId('release-create-confirm-button'));

    await waitFor(() => {
      expect(publishRequests).toEqual([{ version: '1.2.0', makeActive: false }]);
    });
  });

  it('keeps you on the parameters step when creating the release fails', async () => {
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        () => HttpResponse.json({ detail: 'Release already exists' }, { status: 409 }),
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-create-version-button'));
    fireEvent.input(await screen.findByTestId('release-version-input'), {
      target: { value: '1.2' },
    });
    fireEvent.click(screen.getByTestId('release-version-confirm-button'));

    fireEvent.click(await screen.findByTestId('release-create-confirm-button'));

    expect(await screen.findByText('Release already exists')).toBeInTheDocument();
    expect(screen.getByTestId('release-create-confirm-button')).toBeInTheDocument();
  });

  it('amends a release into a new patch without touching working config', async () => {
    const draftCalls: string[] = [];
    const publishRequests: Array<{
      version: string;
      makeActive: boolean;
      entries?: Array<{ key: string; value: string }>;
    }> = [];
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version/draft',
        ({ params }) => {
          draftCalls.push(String(params.version));
          return HttpResponse.json([]);
        },
      ),
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        ({ params }) =>
          HttpResponse.json({
            project: params.projectId,
            environment: params.envName,
            version: params.version,
            entryCount: 1,
            isActive: false,
            createdAt: '2024-01-01T00:00:00Z',
            actor: 'alice',
            entries: [
              { key: 'feature.x', value: 'true', contentType: 'boolean', scope: 'client' },
            ],
          }),
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequests.push((await request.json()) as (typeof publishRequests)[number]);
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    expect(screen.queryByTestId('release-version-dialog')).not.toBeInTheDocument();

    await screen.findByTestId('parameter-row-feature.x');

    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));
    await waitFor(() => {
      expect(publishRequests.length).toBe(1);
    });
    expect(publishRequests[0].version).toBe('1.1.1');
    expect(publishRequests[0].makeActive).toBe(false);
    expect(publishRequests[0].entries?.some(entry => entry.key === 'feature.x')).toBe(true);
    expect(draftCalls).toEqual([]);
  });

  it('retains invalid amended JSON until it is corrected and explicitly updated', async () => {
    let publishRequest: { entries?: Array<{ key: string; value: string }> } | undefined;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        ({ params }) =>
          HttpResponse.json({
            project: params.projectId,
            environment: params.envName,
            version: params.version,
            entryCount: 1,
            isActive: false,
            createdAt: '2024-01-01T00:00:00Z',
            actor: 'alice',
            entries: [
              {
                key: 'json.settings',
                value: '{"enabled":true}',
                contentType: 'json',
                scope: 'client',
              },
            ],
          }),
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json(
            {
              title: 'One or more validation errors occurred.',
              status: 400,
              detail: 'One or more validation errors occurred.',
              errors: {
                'Entries[0].Value': ["Value must be valid JSON when contentType is 'json'."],
              },
            },
            { status: 400 },
          );
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    const input = await screen.findByTestId('parameter-value-input-json.settings');
    fireEvent.input(input, {
      target: { value: '{"enabled":' },
    });
    expect(input).toHaveValue('{"enabled":');
    expect(screen.getByRole('alert')).toHaveTextContent(/invalid json:/i);
    expect(screen.getByTestId('parameter-update-json.settings')).toBeDisabled();
    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));

    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishRequest?.entries?.[0].value).toBe('{"enabled":true}');
    expect(input).toHaveValue('{"enabled":');
    expect(screen.getByTestId('release-amend-panel')).toBeInTheDocument();
  });

  it('copies a historical live snapshot into the isolated amend draft', async () => {
    let publishRequest: { entries?: Array<{ key: string; value: string; description?: string }> } | undefined;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        ({ params }) => HttpResponse.json({
          project: params.projectId,
          environment: params.envName,
          version: params.version,
          entryCount: 1,
          isActive: false,
          createdAt: '2026-08-20T08:00:00Z',
          actor: 'alice@example.com',
          entries: [
            { key: 'feature.x', value: 'true', contentType: 'boolean', scope: 'client' },
          ],
        }),
      ),
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key/history',
        ({ params }) => HttpResponse.json([
          {
            project: params.projectId,
            environment: params.envName,
            key: params.key,
            version: 1,
            value: 'false',
            contentType: 'boolean',
            scope: 'server',
            description: 'Historical switch',
            unit: null,
            createdAt: '2026-08-19T08:00:00Z',
            actor: 'bob@example.com',
          },
        ]),
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');
    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    fireEvent.click(await screen.findByTestId('parameter-edit-feature.x'));
    fireEvent.click(await screen.findByTestId('parameter-panel-history-tab'));
    expect(await screen.findByText(/changed by bob@example.com/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Use in draft' }));

    expect(await screen.findByTestId('parameter-edit-description-input')).toHaveValue('Historical switch');
    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));
    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishRequest?.entries?.[0]).toMatchObject({
      key: 'feature.x',
      value: 'false',
      description: 'Historical switch',
    });
  });

  it('activates a configuration release', async () => {
    const activeRequests: Array<{ version: string | null }> = [];
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/active-release',
        async ({ params, request }) => {
          const body = (await request.json()) as { version: string | null };
          activeRequests.push(body);
          return HttpResponse.json({
            project: params.projectId,
            name: params.envName,
            activeReleaseVersion: body.version,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: new Date().toISOString(),
          });
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    await screen.findByText('1.1.0');
    const activateButtons = screen.getAllByRole('button', { name: /activate/i });
    const enabledActivate = activateButtons.find(button => !button.hasAttribute('disabled'));
    expect(enabledActivate).toBeTruthy();
    fireEvent.click(enabledActivate!);
    expect(await screen.findByTestId('release-activate-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('release-activate-confirm-button'));

    await waitFor(() => {
      expect(activeRequests).toEqual([{ version: '1.1.0' }]);
    });
  });

  it('invalidates the confirmed environment when activation finishes after an environment switch', async () => {
    let resolveResponse: (() => void) | undefined;
    const activeRequests: Array<{ url: string; version: string | null }> = [];
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/active-release',
        async ({ params, request }) => {
          const body = (await request.json()) as { version: string | null };
          activeRequests.push({ url: request.url, version: body.version });
          await new Promise<void>(resolve => {
            resolveResponse = resolve;
          });
          return HttpResponse.json({
            project: params.projectId,
            name: params.envName,
            activeReleaseVersion: body.version,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: new Date().toISOString(),
          });
        },
      ),
    );

    const { queryClient } = renderProjectSections('/projects/my-app/releases');
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');

    await screen.findByText('1.1.0');
    const activateButtons = screen.getAllByRole('button', { name: /activate/i });
    const enabledActivate = activateButtons.find(button => !button.hasAttribute('disabled'));
    expect(enabledActivate).toBeTruthy();
    fireEvent.click(enabledActivate!);
    fireEvent.click(await screen.findByTestId('release-activate-confirm-button'));

    await waitFor(() => {
      expect(resolveResponse).toBeTypeOf('function');
    });
    expect(activeRequests).toEqual([
      {
        url: 'http://localhost:5027/admin/projects/my-app/environments/production/active-release',
        version: '1.1.0',
      },
    ]);

    setActiveEnvironmentName('my-app', 'staging');
    resolveResponse!();

    await waitFor(() => {
      expect(invalidateQueries).toHaveBeenCalledWith({
        queryKey: projectKeys.configReleases('my-app', 'production'),
      });
    });
    expect(invalidateQueries).not.toHaveBeenCalledWith({
      queryKey: projectKeys.configReleases('my-app', 'staging'),
    });
  });

  it('invalidates the confirmed environment when clearing active finishes after an environment switch', async () => {
    let resolveResponse: (() => void) | undefined;
    const clearRequests: string[] = [];
    server.use(
      http.delete(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/active-release',
        async ({ params, request }) => {
          clearRequests.push(request.url);
          await new Promise<void>(resolve => {
            resolveResponse = resolve;
          });
          return HttpResponse.json({
            project: params.projectId,
            name: params.envName,
            activeReleaseVersion: null,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: new Date().toISOString(),
          });
        },
      ),
    );

    const { queryClient } = renderProjectSections('/projects/my-app/releases');
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');

    fireEvent.click(await screen.findByRole('button', { name: 'Clear' }));
    fireEvent.click(await screen.findByTestId('release-clear-active-confirm-button'));

    await waitFor(() => {
      expect(resolveResponse).toBeTypeOf('function');
    });
    expect(clearRequests).toEqual([
      'http://localhost:5027/admin/projects/my-app/environments/production/active-release',
    ]);

    setActiveEnvironmentName('my-app', 'staging');
    resolveResponse!();

    await waitFor(() => {
      expect(invalidateQueries).toHaveBeenCalledWith({
        queryKey: projectKeys.configReleases('my-app', 'production'),
      });
    });
    expect(invalidateQueries).not.toHaveBeenCalledWith({
      queryKey: projectKeys.configReleases('my-app', 'staging'),
    });
  });

  it('opens release parameters on the parameters page without starting an amend flow', async () => {
    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-view-1.1.0'));

    expect(await screen.findByTestId('release-view-banner')).toBeInTheDocument();
    expect(await screen.findByTestId('project-parameters-heading')).toBeInTheDocument();
    expect(await screen.findByTestId('parameter-row-API_URL')).toBeInTheDocument();
    expect(screen.queryByTestId('release-create-confirm-button')).not.toBeInTheDocument();
  });

  it('deletes a non-active release after confirmation', async () => {
    const deleteRequests: string[] = [];
    server.use(
      http.delete(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        ({ params }) => {
          deleteRequests.push(String(params.version));
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    const activeDeleteButton = await screen.findByTestId('release-delete-1.0.0');
    expect(activeDeleteButton).toBeDisabled();
    fireEvent.click(screen.getByTestId('release-delete-1.1.0'));
    expect(await screen.findByTestId('release-delete-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('release-delete-confirm-button'));

    await waitFor(() => {
      expect(deleteRequests).toEqual(['1.1.0']);
    });
  });

  it('invalidates the confirmed environment when deletion finishes after an environment switch', async () => {
    let resolveResponse: (() => void) | undefined;
    const deleteRequests: string[] = [];
    server.use(
      http.delete(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        async ({ request }) => {
          deleteRequests.push(request.url);
          await new Promise<void>(resolve => {
            resolveResponse = resolve;
          });
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );

    const { queryClient } = renderProjectSections('/projects/my-app/releases');
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');

    fireEvent.click(await screen.findByTestId('release-delete-1.1.0'));
    fireEvent.click(await screen.findByTestId('release-delete-confirm-button'));

    await waitFor(() => {
      expect(resolveResponse).toBeTypeOf('function');
    });
    expect(deleteRequests).toEqual([
      'http://localhost:5027/admin/projects/my-app/environments/production/releases/1.1.0',
    ]);

    setActiveEnvironmentName('my-app', 'staging');
    resolveResponse!();

    await waitFor(() => {
      expect(invalidateQueries).toHaveBeenCalledWith({
        queryKey: projectKeys.configReleases('my-app', 'production'),
      });
    });
    expect(invalidateQueries).not.toHaveBeenCalledWith({
      queryKey: projectKeys.configReleases('my-app', 'staging'),
    });
  });
});
