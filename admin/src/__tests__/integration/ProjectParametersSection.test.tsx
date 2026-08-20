import { fireEvent, screen, waitFor, within } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { setActiveEnvironmentName } from '../../entities/project/model/active-environment';
import { projectKeys } from '../../entities/project/queries/keys';
import { mockProjects } from '../mocks/data';
import { server } from '../mocks/server';
import {
  renderProjectSections,
  resetProjectSectionsTestState,
  writeClipboard,
} from './project-sections.test-utils';

vi.mock('@solid-primitives/clipboard', () => ({
  writeClipboard: vi.fn(() => Promise.resolve()),
}));

describe('ProjectParametersSection', () => {
  beforeEach(() => {
    resetProjectSectionsTestState();
  });

  it('renders the parameters section without the legacy project header', async () => {
    renderProjectSections('/projects/my-app');

    expect(await screen.findByTestId('project-parameters-heading')).toBeInTheDocument();
    expect(screen.queryByTestId('project-detail-heading')).not.toBeInTheDocument();
  });

  it('shows config entries when an environment is selected', async () => {
    renderProjectSections('/projects/my-app');

    expect(await screen.findByTestId('parameter-row-API_URL')).toBeInTheDocument();
    expect(await screen.findByTestId('parameter-row-MAX_RETRIES')).toBeInTheDocument();
  });

  it('defaults to compact spacing and restores the saved density', async () => {
    const page = renderProjectSections('/projects/my-app');
    const section = await screen.findByTestId('project-parameters-section');
    expect(section).toHaveAttribute('data-density', 'compact');

    fireEvent.click(await screen.findByTestId('parameters-density-comfortable'));
    expect(section).toHaveAttribute('data-density', 'comfortable');
    expect(JSON.parse(localStorage.getItem('nona_parameter_density') ?? 'null')).toBe('comfortable');

    page.unmount();
    renderProjectSections('/projects/my-app');
    expect(await screen.findByTestId('project-parameters-section')).toHaveAttribute(
      'data-density',
      'comfortable',
    );
  });

  it('keeps project Viewers read-only', async () => {
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'viewer@example.com', role: 'member' }),
    );
    server.use(
      http.get('http://localhost:5027/admin/projects', () =>
        HttpResponse.json([{ ...mockProjects[0], accessLevel: 'viewer' }]),
      ),
    );

    renderProjectSections('/projects/my-app');

    expect(await screen.findByTestId('parameter-row-API_URL')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add parameter/i })).not.toBeInTheDocument();
    expect(screen.queryByTestId('parameter-share-API_URL')).not.toBeInTheDocument();
  });

  it('keeps global admins manageable when the compatibility API omits accessLevel', async () => {
    server.use(
      http.get('http://localhost:5027/admin/projects', () => {
        const { accessLevel: _accessLevel, ...projectWithoutAccessLevel } = mockProjects[0];
        return HttpResponse.json([projectWithoutAccessLevel]);
      }),
    );

    renderProjectSections('/projects/my-app');

    expect(await screen.findByRole('button', { name: /add parameter/i })).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('parameter-edit-API_URL'));
    expect(await screen.findByTestId('parameter-panel-share-button')).toBeInTheDocument();
  });

  it('opens parameter details in a fixed side panel', async () => {
    renderProjectSections('/projects/my-app');

    fireEvent.click(await screen.findByTestId('parameter-row-API_URL'));

    expect(await screen.findByTestId('parameter-side-panel')).toHaveAttribute('data-entry-key', 'API_URL');
    expect(screen.getByTestId('parameter-row-API_URL')).toBeInTheDocument();
  });

  it('opens without remounting the list or eagerly requesting history and share data', async () => {
    let entriesRequests = 0;
    let historyRequests = 0;
    let shareRequests = 0;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        () => {
          entriesRequests += 1;
          return HttpResponse.json([
            {
              project: 'my-app',
              environment: 'production',
              key: 'API_URL',
              value: 'https://api.example.com',
              contentType: 'text',
              scope: 'server',
              activeVersion: 1,
              createdAt: '2024-01-01T00:00:00Z',
              updatedAt: '2024-01-01T00:00:00Z',
            },
          ]);
        },
      ),
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key/history',
        () => {
          historyRequests += 1;
          return HttpResponse.json([]);
        },
      ),
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key/share-links',
        () => {
          shareRequests += 1;
          return HttpResponse.json([]);
        },
      ),
    );

    renderProjectSections('/projects/my-app');
    await screen.findByTestId('parameter-row-API_URL');
    const originalTable = screen.getByTestId('parameter-table');

    fireEvent.click(screen.getByTestId('parameter-edit-API_URL'));
    expect(await screen.findByTestId('parameter-side-panel')).toBeInTheDocument();
    expect(screen.getByTestId('parameter-table')).toBe(originalTable);
    expect(screen.getAllByTestId('parameter-side-panel')).toHaveLength(1);
    expect(entriesRequests).toBe(1);
    expect(historyRequests).toBe(0);
    expect(shareRequests).toBe(0);

    fireEvent.click(screen.getByTestId('parameter-panel-history-tab'));
    await waitFor(() => expect(historyRequests).toBe(1));
    expect(shareRequests).toBe(0);
  });

  it('prompts for dirty panel changes and restores focus to the exact opener', async () => {
    renderProjectSections('/projects/my-app');
    const opener = await screen.findByTestId('parameter-edit-API_URL');
    fireEvent.click(opener);
    fireEvent.input(await screen.findByTestId('parameter-edit-description-input'), {
      target: { value: 'Unsaved description' },
    });

    fireEvent.click(screen.getByTestId('parameter-panel-close-button'));
    expect(await screen.findByTestId('parameter-discard-dialog')).toBeInTheDocument();
    expect(screen.getByTestId('parameter-side-panel')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-discard-confirm-button'));
    await waitFor(() => expect(screen.queryByTestId('parameter-side-panel')).not.toBeInTheDocument());
    await waitFor(() => expect(opener).toHaveFocus());
  });

  it('closes from Escape and outside click', async () => {
    renderProjectSections('/projects/my-app');
    const opener = await screen.findByTestId('parameter-edit-API_URL');

    fireEvent.click(opener);
    await screen.findByTestId('parameter-side-panel');
    fireEvent.keyDown(document, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByTestId('parameter-side-panel')).not.toBeInTheDocument());

    fireEvent.click(opener);
    await screen.findByTestId('parameter-side-panel');
    const overlay = screen.getByTestId('parameter-panel-overlay');
    fireEvent.pointerDown(overlay, { pointerId: 1, pointerType: 'mouse' });
    fireEvent.pointerUp(overlay, { pointerId: 1, pointerType: 'mouse' });
    fireEvent.click(overlay);
    await waitFor(() => expect(screen.queryByTestId('parameter-side-panel')).not.toBeInTheDocument());
  });

  it('loads complete history on demand and restores by creating a new version', async () => {
    let rollbackVersion: number | undefined;
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        ({ params }) => HttpResponse.json([
          {
            project: params.projectId,
            environment: params.envName,
            key: 'API_URL',
            value: 'https://api.example.com',
            contentType: 'text',
            scope: 'server',
            description: 'Current endpoint',
            unit: null,
            activeVersion: 2,
            createdAt: '2026-08-19T08:00:00Z',
            updatedAt: '2026-08-20T08:00:00Z',
          },
        ]),
      ),
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key/history',
        ({ params }) => HttpResponse.json([
          {
            project: params.projectId,
            environment: params.envName,
            key: params.key,
            version: 2,
            value: 'https://api.example.com',
            contentType: 'text',
            scope: 'server',
            description: 'Current endpoint',
            unit: null,
            createdAt: '2026-08-20T08:00:00Z',
            actor: 'alice@example.com',
          },
          {
            project: params.projectId,
            environment: params.envName,
            key: params.key,
            version: 1,
            value: 'https://old.example.com',
            contentType: 'text',
            scope: 'client',
            description: 'Old endpoint',
            unit: null,
            createdAt: '2026-08-19T08:00:00Z',
            actor: 'bob@example.com',
          },
        ]),
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key/rollback',
        async ({ params, request }) => {
          rollbackVersion = ((await request.json()) as { version: number }).version;
          return HttpResponse.json({
            project: params.projectId,
            environment: params.envName,
            key: params.key,
            value: 'https://old.example.com',
            contentType: 'text',
            scope: 'client',
            description: 'Old endpoint',
            unit: null,
            activeVersion: 3,
            createdAt: '2026-08-19T08:00:00Z',
            updatedAt: '2026-08-20T09:00:00Z',
          });
        },
      ),
    );

    renderProjectSections('/projects/my-app');
    fireEvent.click(await screen.findByTestId('parameter-edit-API_URL'));
    fireEvent.click(await screen.findByTestId('parameter-panel-history-tab'));

    expect(await screen.findByText(/changed by bob@example.com/i)).toBeInTheDocument();
    expect(screen.getByText('In use')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Restore' }));
    await waitFor(() => expect(rollbackVersion).toBe(1));
    expect(screen.getByTestId('parameter-side-panel')).toBeInTheDocument();
    expect(screen.getByTestId('parameter-edit-description-input')).toHaveValue('Old endpoint');
  });

  it('shows release snapshot history read-only and marks the captured value', async () => {
    renderProjectSections('/projects/my-app?viewRelease=1.1.0');
    const row = await screen.findByTestId('parameter-row-API_URL');
    expect(within(row).queryByRole('button', { name: 'Update' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-edit-API_URL'));
    fireEvent.click(await screen.findByTestId('parameter-panel-history-tab'));
    expect(await screen.findByText('Captured in release')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Restore' })).not.toBeInTheDocument();
  });

  it('updates a parameter with the selected datatype', async () => {
    let updateRequest:
      | { value: string; contentType: string; scope: string }
      | undefined;

    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        async ({ params, request }) => {
          updateRequest = (await request.json()) as {
            value: string;
            contentType: string;
            scope: string;
          };

          return HttpResponse.json({
            project: params.projectId,
            environment: params.envName,
            key: params.key,
            ...updateRequest,
            activeVersion: 2,
            createdAt: '2024-01-01T00:00:00Z',
            updatedAt: '2024-01-02T00:00:00Z',
          });
        },
      ),
    );

    renderProjectSections('/projects/my-app');

    fireEvent.click(await screen.findByTestId('parameter-row-API_URL'));

    const datatypeSelect = await screen.findByLabelText(/^datatype$/i);
    expect(datatypeSelect).toHaveTextContent('text');

    fireEvent.pointerDown(datatypeSelect, { pointerId: 1, pointerType: 'mouse' });
    fireEvent.pointerUp(datatypeSelect, { pointerId: 1, pointerType: 'mouse' });

    const numberOption = await screen.findByRole('option', { name: 'number' });
    fireEvent.pointerDown(numberOption, { pointerId: 1, pointerType: 'mouse' });
    fireEvent.pointerUp(numberOption, { pointerId: 1, pointerType: 'mouse' });

    const saveButton = screen.getByTestId('parameter-edit-save-button');
    expect(saveButton).toBeDisabled();

    fireEvent.input(screen.getByTestId('parameter-edit-value-input'), {
      target: { value: '42' },
    });
    fireEvent.click(saveButton);

    await waitFor(() => {
      expect(updateRequest).toEqual({
        value: '42',
        contentType: 'number',
        scope: 'server',
        description: '',
        unit: null,
      });
    });
  });

  it('shows prompt to select environment when none is active', async () => {
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/environments', () =>
        HttpResponse.json([]),
      ),
    );

    renderProjectSections('/projects/my-app');

    await waitFor(() => {
      expect(
        screen.getByText(/select an active environment from the header to view its parameters/i),
      ).toBeInTheDocument();
    });
  });

  it('shows "Add Parameter" form when button is clicked and env is active', async () => {
    renderProjectSections('/projects/my-app');

    const addParamButton = await screen.findByRole('button', { name: /add parameter/i });
    fireEvent.click(addParamButton);

    await waitFor(() => {
      expect(screen.getByTestId('parameter-side-panel')).toBeInTheDocument();
      expect(screen.getByTestId('parameter-key-input')).toBeInTheDocument();
      expect(screen.getByTestId('parameter-value-input')).toBeInTheDocument();
    });
  });

  it('auto-opens the parameter form when the environment has no parameters', async () => {
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        () => HttpResponse.json([]),
      ),
    );

    renderProjectSections('/projects/my-app');

    expect(await screen.findByTestId('parameter-key-input')).toBeInTheDocument();
  });

  it('creates a parameter in the panel and keeps the same panel open in edit mode', async () => {
    const entries = [
      {
        project: 'my-app',
        environment: 'production',
        key: 'API_URL',
        value: 'https://api.example.com',
        contentType: 'text',
        scope: 'server',
        activeVersion: 1,
        createdAt: '2024-01-01T00:00:00Z',
        updatedAt: '2024-01-01T00:00:00Z',
      },
    ];
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        () => HttpResponse.json(entries),
      ),
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        async ({ params, request }) => {
          const body = (await request.json()) as Record<string, unknown>;
          const created = {
            ...entries[0],
            ...body,
            key: String(params.key),
            activeVersion: 1,
            description: body.description,
            unit: body.unit,
          };
          entries.push(created as (typeof entries)[number]);
          return HttpResponse.json(created);
        },
      ),
    );

    renderProjectSections('/projects/my-app');
    fireEvent.click(await screen.findByRole('button', { name: /add parameter/i }));
    fireEvent.input(await screen.findByTestId('parameter-key-input'), {
      target: { value: 'Checkout:Timeout' },
    });
    fireEvent.input(screen.getByTestId('parameter-value-input'), {
      target: { value: '30s' },
    });
    fireEvent.input(screen.getByTestId('parameter-edit-description-input'), {
      target: { value: 'Checkout timeout' },
    });
    fireEvent.click(screen.getByTestId('parameter-create-submit-button'));

    await waitFor(() => {
      expect(screen.getByTestId('parameter-side-panel')).toHaveAttribute(
        'data-entry-key',
        'Checkout:Timeout',
      );
    });
    expect(screen.getByTestId('parameter-edit-save-button')).toBeDisabled();
    expect(await screen.findByTestId('parameter-row-Checkout:Timeout')).toBeInTheDocument();
  });

  it('shows backend validation message when parameter creation fails', async () => {
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        () =>
          HttpResponse.json(
            { detail: "Value must be 'true' or 'false' when contentType is 'boolean'." },
            { status: 400 },
          ),
      ),
    );

    renderProjectSections('/projects/my-app');

    const addParamButton = await screen.findByRole('button', { name: /add parameter/i });
    fireEvent.click(addParamButton);

    fireEvent.input(await screen.findByTestId('parameter-key-input'), {
      target: { value: 'sdfgsdfg' },
    });
    fireEvent.input(await screen.findByTestId('parameter-value-input'), {
      target: { value: 'not-a-boolean' },
    });
    fireEvent.click(screen.getByTestId('parameter-create-submit-button'));

    expect(
      await screen.findByText("Value must be 'true' or 'false' when contentType is 'boolean'."),
    ).toBeInTheDocument();
  });

  it('shows the Projects fallback when slug does not match any project', async () => {
    renderProjectSections('/projects/nonexistent-project');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /^projects$/i })).toBeInTheDocument();
    });
  });

  it('config entries reload when switching environments', async () => {
    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries',
        ({ params }) => {
          if (params.envName === 'staging') {
            return HttpResponse.json([
              {
                project: 'my-app',
                environment: 'staging',
                key: 'STAGING_ONLY_KEY',
                value: 'staging-value',
                contentType: 'text',
                scope: 'all',
                activeVersion: 1,
                createdAt: '2024-01-01T00:00:00Z',
                updatedAt: '2024-01-01T00:00:00Z',
              },
            ]);
          }
          return HttpResponse.json([]);
        },
      ),
    );

    const environmentPage = renderProjectSections('/projects/my-app/environments');

    const stagingTab = await screen.findByText('staging');
    fireEvent.click(stagingTab);

    environmentPage.unmount();
    renderProjectSections('/projects/my-app');

    expect(await screen.findByTestId('parameter-row-STAGING_ONLY_KEY')).toBeInTheDocument();
  });

  it('generates a shareable link and refreshes the shared links page', async () => {
    const { queryClient } = renderProjectSections('/projects/my-app');
    const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries');

    expect(await screen.findByTestId('parameter-row-API_URL')).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('parameter-edit-API_URL'));
    const shareButton = await screen.findByTestId('parameter-panel-share-button');
    expect(shareButton).toBeEnabled();
    expect(shareButton.parentElement).not.toHaveAttribute('title');
    fireEvent.click(shareButton);

    expect(await screen.findByTestId('parameter-share-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('parameter-share-create-button'));

    const generatedUrl = await screen.findByTestId('parameter-share-generated-url');
    expect(generatedUrl).toHaveValue(`${window.location.origin}/share/AbCdEf1234567890`);
    expect(invalidateQueries).toHaveBeenCalledWith({
      queryKey: projectKeys.environmentShareLinks('my-app', 'production'),
    });
  });

  it('asks before exiting release parameter changes', async () => {
    renderProjectSections('/projects/my-app?release=1.2.0');

    fireEvent.click(await screen.findByTestId('release-create-cancel-button'));

    expect(await screen.findByTestId('release-exit-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('release-exit-cancel-button'));

    expect(screen.queryByTestId('release-exit-dialog')).not.toBeInTheDocument();
    expect(screen.getByTestId('release-create-confirm-button')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('release-create-cancel-button'));
    fireEvent.click(await screen.findByTestId('release-exit-confirm-button'));

    expect(await screen.findByTestId('project-releases-heading')).toBeInTheDocument();
  });

  it('replaces the amend buffer before publishing after an environment switch', async () => {
    let releaseStagingSource: (() => void) | undefined;
    const publishRequests: Array<{ url: string; body: string }> = [];

    server.use(
      http.get(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version',
        async ({ params }) => {
          const environmentName = String(params.envName);
          if (environmentName === 'staging') {
            await new Promise<void>(resolve => {
              releaseStagingSource = resolve;
            });
          }

          const entry =
            environmentName === 'production'
              ? {
                  key: 'PRODUCTION_SECRET',
                  value: 'production-secret-value',
                  contentType: 'text',
                  scope: 'server',
                }
              : {
                  key: 'STAGING_ONLY_KEY',
                  value: 'staging-value',
                  contentType: 'text',
                  scope: 'server',
                };

          return HttpResponse.json({
            project: params.projectId,
            environment: environmentName,
            version: params.version,
            entryCount: 1,
            isActive: false,
            createdAt: '2024-01-01T00:00:00Z',
            actor: 'alice',
            entries: [entry],
          });
        },
      ),
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ request }) => {
          publishRequests.push({
            url: request.url,
            body: await request.text(),
          });
          return HttpResponse.json({}, { status: 201 });
        },
      ),
    );

    renderProjectSections('/projects/my-app?release=1.1.1&amend=1.1.0');

    expect(await screen.findByTestId('parameter-row-PRODUCTION_SECRET')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /^add parameter$/i }));
    fireEvent.input(await screen.findByTestId('parameter-key-input'), {
      target: { value: 'PRODUCTION_SECRET' },
    });
    fireEvent.input(screen.getByTestId('parameter-value-input'), {
      target: { value: 'production-draft-value' },
    });
    fireEvent.click(screen.getByTestId('parameter-create-submit-button'));
    expect(
      screen.getByText('Parameter key already exists. Keys are case-insensitive.', {
        selector: '#parameter-panel-key-error',
      }),
    ).toBeInTheDocument();

    setActiveEnvironmentName('my-app', 'staging');

    await waitFor(() => {
      expect(releaseStagingSource).toBeTypeOf('function');
    });
    expect(screen.queryByTestId('parameter-row-PRODUCTION_SECRET')).not.toBeInTheDocument();
    expect(screen.getByTestId('release-amend-confirm-button')).toBeDisabled();

    releaseStagingSource!();

    expect(await screen.findByTestId('parameter-row-STAGING_ONLY_KEY')).toBeInTheDocument();
    expect(screen.queryByTestId('parameter-row-PRODUCTION_SECRET')).not.toBeInTheDocument();
    expect(screen.queryByTestId('parameter-side-panel')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));

    await waitFor(() => {
      expect(publishRequests).toHaveLength(1);
    });
    expect(publishRequests[0].url).toContain('/environments/staging/releases');
    expect(publishRequests[0].url).not.toContain('/environments/production/');
    expect(publishRequests[0].body).toContain('STAGING_ONLY_KEY');
    expect(publishRequests[0].body).not.toContain('production-secret-value');
    expect(publishRequests[0].body).not.toContain('production-draft-value');
  });

  it('copies a share link from history', async () => {
    renderProjectSections('/projects/my-app');

    expect(await screen.findByTestId('parameter-row-API_URL')).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('parameter-edit-API_URL'));
    fireEvent.click(await screen.findByTestId('parameter-panel-share-button'));

    expect(await screen.findByTestId('parameter-share-dialog')).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('parameter-share-copy-1'));

    await waitFor(() => {
      expect(writeClipboard).toHaveBeenCalledWith(
        `${window.location.origin}/share/HistoryToken1234`,
      );
    });
    const toast = await screen.findByText('Copied to clipboard');
    expect(toast.closest('[role="status"]')).toHaveClass('z-[110]');
  });
});
