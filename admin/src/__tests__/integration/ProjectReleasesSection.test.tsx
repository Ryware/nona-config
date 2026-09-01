import { fireEvent, screen, waitFor, within } from '@solidjs/testing-library';
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
    const publishRequests: Array<{
      version: string;
      makeActive: boolean;
      entries?: Array<{ key: string; value: string; contentType: string; scope: string }>;
    }> = [];
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

    expect(await screen.findByTestId('release-create-panel')).toBeInTheDocument();
    const createRelease = await screen.findByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);

    await waitFor(() => {
      expect(publishRequests).toEqual([
        {
          version: '1.2.0',
          makeActive: false,
          entries: [
            {
              key: 'API_URL',
              value: 'https://api.example.com',
              contentType: 'text',
              scope: 'server',
            },
            {
              key: 'MAX_RETRIES',
              value: '3',
              contentType: 'number',
              scope: 'all',
            },
            {
              key: 'FEATURE_FLAGS',
              value: '{"dark_mode": true}',
              contentType: 'json',
              scope: 'client',
            },
          ],
        },
      ]);
    });
  });

  it('keeps you on the parameters step when creating the release fails', async () => {
    const publishRequests: Array<{ entries?: Array<{ key: string; value: string }> }> = [];
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequests.push((await request.json()) as (typeof publishRequests)[number]);
          return publishRequests.length === 1
            ? HttpResponse.json({ detail: 'Temporary publish failure' }, { status: 503 })
            : HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-create-version-button'));
    fireEvent.input(await screen.findByTestId('release-version-input'), {
      target: { value: '1.2' },
    });
    fireEvent.click(screen.getByTestId('release-version-confirm-button'));

    const createRelease = await screen.findByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    const input = screen.getByTestId('parameter-value-input-API_URL');
    fireEvent.input(input, { target: { value: 'https://retry.example.com' } });
    fireEvent.click(screen.getByTestId('parameter-update-API_URL'));
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);

    expect(await screen.findByText('Temporary publish failure')).toBeInTheDocument();
    expect(screen.getByTestId('release-create-confirm-button')).toBeInTheDocument();
    expect(input).toHaveValue('https://retry.example.com');

    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);
    expect(await screen.findByTestId('project-releases-heading')).toBeInTheDocument();
    expect(publishRequests).toHaveLength(2);
    expect(
      publishRequests.every(
        request => request.entries?.find(entry => entry.key === 'API_URL')?.value
          === 'https://retry.example.com',
      ),
    ).toBe(true);
  });

  it('publishes an applied create-draft value without updating working configuration', async () => {
    let workingUpdateCount = 0;
    let publishRequest: {
      entries?: Array<{ key: string; value: string }>;
    } | undefined;
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        () => {
          workingUpdateCount += 1;
          return HttpResponse.json({});
        },
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app?release=1.2.0');

    const createRelease = await screen.findByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    const input = screen.getByTestId('parameter-value-input-API_URL');
    fireEvent.input(input, { target: { value: 'https://draft.example.com' } });

    expect(screen.getByTestId('parameter-update-API_URL')).toHaveTextContent('Apply to draft');
    expect(createRelease).toBeDisabled();
    expect(
      screen.getByText('Apply or revert inline edits before creating the release.'),
    ).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-update-API_URL'));
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);

    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishRequest?.entries?.find(entry => entry.key === 'API_URL')?.value).toBe(
      'https://draft.example.com',
    );
    expect(workingUpdateCount).toBe(0);
  });

  it('blocks publishing an invalid unapplied create-draft value', async () => {
    let publishCount = 0;
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        () => {
          publishCount += 1;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app?release=1.2.0');

    const createRelease = await screen.findByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.input(screen.getByTestId('parameter-value-input-FEATURE_FLAGS'), {
      target: { value: '{invalid' },
    });

    expect(screen.getByTestId('parameter-update-FEATURE_FLAGS')).toBeDisabled();
    expect(createRelease).toBeDisabled();
    fireEvent.click(createRelease);
    expect(publishCount).toBe(0);
  });

  it('adds and removes parameters only inside the create draft', async () => {
    let workingWriteCount = 0;
    let publishRequest: { entries?: Array<{ key: string; value: string }> } | undefined;
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        () => {
          workingWriteCount += 1;
          return HttpResponse.json({});
        },
      ),
      http.delete(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        () => {
          workingWriteCount += 1;
          return new HttpResponse(null, { status: 204 });
        },
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app?release=1.2.0');

    await screen.findByTestId('parameter-row-API_URL');
    fireEvent.click(screen.getByRole('button', { name: /add parameter/i }));
    fireEvent.input(await screen.findByTestId('parameter-key-input'), {
      target: { value: 'DRAFT_ONLY' },
    });
    fireEvent.input(screen.getByTestId('parameter-value-input'), {
      target: { value: 'draft-value' },
    });
    fireEvent.input(screen.getByTestId('parameter-edit-description-input'), {
      target: { value: 'Draft-only parameter' },
    });
    expect(screen.getByTestId('release-create-confirm-button')).toBeDisabled();
    expect(
      screen.getByText('Save or discard parameter editor changes before creating the release.'),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('parameter-create-submit-button'));

    expect(await screen.findByTestId('parameter-row-DRAFT_ONLY')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('parameter-panel-close-button'));
    await waitFor(() => expect(screen.queryByTestId('parameter-side-panel')).not.toBeInTheDocument());
    fireEvent.click(screen.getByTestId('parameter-delete-API_URL'));
    const removeDialog = await screen.findByTestId('delete-release-create-parameter-dialog');
    fireEvent.click(within(removeDialog).getByRole('button', { name: 'Remove Parameter' }));
    await waitFor(() => expect(screen.queryByTestId('parameter-row-API_URL')).not.toBeInTheDocument());

    const createRelease = screen.getByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);

    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishRequest?.entries).toContainEqual({
      key: 'DRAFT_ONLY',
      value: 'draft-value',
      contentType: 'text',
      scope: 'all',
      description: 'Draft-only parameter',
      unit: null,
    });
    expect(publishRequest?.entries?.some(entry => entry.key === 'API_URL')).toBe(false);
    expect(workingWriteCount).toBe(0);
  });

  it('keeps a create draft frozen when the working query cache changes', async () => {
    let publishRequest: { entries?: Array<{ key: string; value: string }> } | undefined;
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    const { queryClient } = renderProjectSections('/projects/my-app?release=1.2.0');
    const input = await screen.findByTestId('parameter-value-input-API_URL');
    expect(input).toHaveValue('https://api.example.com');

    const queryKey = projectKeys.configEntries('my-app', 'production');
    const workingEntries = queryClient.getQueryData<Array<Record<string, unknown>>>(queryKey) ?? [];
    queryClient.setQueryData(
      queryKey,
      workingEntries.map(entry =>
        entry.key === 'API_URL' ? { ...entry, value: 'https://changed-elsewhere.example.com' } : entry,
      ),
    );

    expect(input).toHaveValue('https://api.example.com');
    const createRelease = screen.getByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);

    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishRequest?.entries?.find(entry => entry.key === 'API_URL')?.value).toBe(
      'https://api.example.com',
    );
  });

  it('publishes an explicitly loaded empty create draft', async () => {
    let publishRequest: { entries?: unknown[] } | undefined;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        () => HttpResponse.json([]),
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app?release=1.2.0');

    expect(await screen.findByText('This release has no parameters.')).toBeInTheDocument();
    const createRelease = screen.getByTestId('release-create-confirm-button');
    await waitFor(() => expect(createRelease).toBeEnabled());
    fireEvent.click(createRelease);

    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishRequest?.entries).toEqual([]);
  });

  it('does not turn a working-parameter load failure into an empty create draft', async () => {
    let loadCount = 0;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        () => {
          loadCount += 1;
          return HttpResponse.json(
            { detail: 'Working parameters are unavailable' },
            { status: 503 },
          );
        },
      ),
    );

    renderProjectSections('/projects/my-app?release=1.2.0');

    expect(await screen.findByText('Working parameters are unavailable')).toBeInTheDocument();
    expect(screen.getByTestId('release-create-confirm-button')).toBeDisabled();
    expect(screen.queryByText('This release has no parameters.')).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));
    await waitFor(() => expect(loadCount).toBe(2));
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

    localStorage.setItem('nona_parameter_density', JSON.stringify('comfortable'));
    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    expect(screen.queryByTestId('release-version-dialog')).not.toBeInTheDocument();

    await screen.findByTestId('parameter-row-feature.x');
    expect(screen.getByTestId('parameter-table')).toHaveAttribute('data-density', 'compact');
    expect(screen.queryByRole('group', { name: 'Parameter spacing' })).not.toBeInTheDocument();
    expect(localStorage.getItem('nona_parameter_density')).toBeNull();

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
    expect(screen.getByTestId('release-amend-confirm-button')).toBeDisabled();
    expect(screen.getByText('Apply or revert inline edits before creating the release.')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));

    expect(publishRequest).toBeUndefined();
    expect(input).toHaveValue('{"enabled":');
    expect(screen.getByTestId('release-amend-panel')).toBeInTheDocument();
  });

  it('requires a valid inline change to be applied to the amend draft before publishing', async () => {
    let publishRequest: { entries?: Array<{ key: string; value: string }> } | undefined;
    let publishCount = 0;
    let workingUpdateCount = 0;
    let activationCount = 0;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        ({ params }) => HttpResponse.json({
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
          publishCount += 1;
          publishRequest = (await request.json()) as typeof publishRequest;
          return HttpResponse.json({}, { status: 201 });
        },
      ),
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        () => {
          workingUpdateCount += 1;
          return HttpResponse.json({});
        },
      ),
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/active-release',
        () => {
          activationCount += 1;
          return HttpResponse.json({});
        },
      ),
    );

    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    fireEvent.click(await screen.findByRole('switch', { name: 'Value for feature.x' }));

    const apply = screen.getByTestId('parameter-update-feature.x');
    expect(apply).toHaveTextContent('Apply to draft');
    expect(apply).toBeEnabled();
    expect(screen.getByTestId('release-amend-confirm-button')).toBeDisabled();
    expect(screen.getByText('Apply or revert inline edits before creating the release.')).toBeInTheDocument();
    expect(publishRequest).toBeUndefined();

    fireEvent.click(screen.getByRole('switch', { name: 'Value for feature.x' }));
    expect(screen.getByTestId('release-amend-confirm-button')).toBeEnabled();
    expect(screen.queryByText('Apply or revert inline edits before creating the release.')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('switch', { name: 'Value for feature.x' }));
    expect(screen.getByTestId('release-amend-confirm-button')).toBeDisabled();
    fireEvent.click(apply);
    expect(screen.getByTestId('release-amend-confirm-button')).toBeEnabled();
    expect(screen.queryByText('Apply or revert inline edits before creating the release.')).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));

    await waitFor(() => expect(publishRequest).toBeDefined());
    expect(publishCount).toBe(1);
    expect(workingUpdateCount).toBe(0);
    expect(activationCount).toBe(0);
    expect(publishRequest?.entries?.[0].value).toBe('false');
    await waitFor(() => expect(window.location.pathname).toBe('/projects/my-app/releases'));
    expect(window.location.search).toBe('');
    expect(screen.queryByTestId('release-exit-dialog')).not.toBeInTheDocument();
    expect(screen.queryByTestId('parameter-discard-dialog')).not.toBeInTheDocument();
    expect(await screen.findByText('Release published')).toBeInTheDocument();
    expect((await screen.findByText('Active release:')).parentElement).toHaveTextContent(
      /Active release:\s*1\.0\.0/,
    );
  });

  it('keeps an applied amend draft retryable when publishing fails', async () => {
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        ({ params }) => HttpResponse.json({
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
        () => HttpResponse.json({ detail: 'Temporary publish failure' }, { status: 503 }),
      ),
    );

    renderProjectSections('/projects/my-app/releases');
    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    fireEvent.click(await screen.findByRole('switch', { name: 'Value for feature.x' }));
    fireEvent.click(screen.getByTestId('parameter-update-feature.x'));
    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));

    expect(await screen.findByText('Temporary publish failure')).toBeInTheDocument();
    expect(screen.getByTestId('release-amend-panel')).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'Value for feature.x' })).toHaveAttribute('aria-checked', 'false');
    await waitFor(() => expect(screen.getByTestId('release-amend-confirm-button')).toBeEnabled());
    expect(window.location.search).toBe('?release=1.1.1&amend=1.1.0');
  });

  it('exits a clean amend directly and prompts once before discarding a dirty amend', async () => {
    renderProjectSections('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    await screen.findByTestId('parameter-row-API_URL');
    fireEvent.click(screen.getByTestId('release-amend-cancel-button'));

    await waitFor(() => expect(window.location.pathname).toBe('/projects/my-app/releases'));
    expect(screen.queryByTestId('release-exit-dialog')).not.toBeInTheDocument();
    expect(screen.queryByTestId('parameter-discard-dialog')).not.toBeInTheDocument();

    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    const input = await screen.findByTestId('parameter-value-input-API_URL');
    fireEvent.input(input, { target: { value: 'https://amended.example.com' } });
    fireEvent.click(screen.getByTestId('parameter-update-API_URL'));
    fireEvent.click(screen.getByTestId('release-amend-cancel-button'));

    expect(await screen.findByTestId('parameter-discard-dialog')).toBeInTheDocument();
    expect(screen.queryByTestId('release-exit-dialog')).not.toBeInTheDocument();
    expect(window.location.search).toBe('?release=1.1.1&amend=1.1.0');

    fireEvent.click(screen.getByTestId('parameter-discard-confirm-button'));
    await waitFor(() => expect(window.location.pathname).toBe('/projects/my-app/releases'));
    expect(window.location.search).toBe('');
    expect(screen.queryByTestId('parameter-discard-dialog')).not.toBeInTheDocument();
    expect(screen.queryByTestId('release-exit-dialog')).not.toBeInTheDocument();
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
