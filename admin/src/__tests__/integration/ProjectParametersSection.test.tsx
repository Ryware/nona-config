import { fireEvent, screen, waitFor } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { setActiveEnvironmentName } from '../../entities/project/model/active-environment';
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

    expect(await screen.findByText('API_URL')).toBeInTheDocument();
    expect(await screen.findByText('MAX_RETRIES')).toBeInTheDocument();
  });

  it('opens parameter details inline as an accordion', async () => {
    renderProjectSections('/projects/my-app');

    fireEvent.click(await screen.findByTestId('parameter-row-API_URL'));

    expect(await screen.findByTestId('parameter-accordion-API_URL')).toBeInTheDocument();
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
      expect(screen.getByLabelText(/^key$/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/^value$/i)).toBeInTheDocument();
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

    expect(await screen.findByText('STAGING_ONLY_KEY')).toBeInTheDocument();
  });

  it('generates a shareable link for a parameter', async () => {
    renderProjectSections('/projects/my-app');

    expect(await screen.findByText('API_URL')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-share-API_URL'));

    expect(await screen.findByTestId('parameter-share-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('parameter-share-create-button'));

    const generatedUrl = await screen.findByTestId('parameter-share-generated-url');
    expect(generatedUrl).toHaveValue(`${window.location.origin}/share/AbCdEf1234567890`);
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

    expect(await screen.findByTestId('amend-row-PRODUCTION_SECRET')).toBeInTheDocument();

    fireEvent.input(screen.getByTestId('amend-new-key'), {
      target: { value: 'PRODUCTION_SECRET' },
    });
    fireEvent.input(screen.getByTestId('amend-new-value'), {
      target: { value: 'production-draft-value' },
    });
    fireEvent.click(screen.getByTestId('amend-add-button'));
    expect(screen.getByText('That key already exists.')).toBeInTheDocument();

    setActiveEnvironmentName('my-app', 'staging');

    await waitFor(() => {
      expect(releaseStagingSource).toBeTypeOf('function');
    });
    expect(screen.queryByTestId('amend-row-PRODUCTION_SECRET')).not.toBeInTheDocument();
    expect(screen.getByTestId('release-amend-confirm-button')).toBeDisabled();

    releaseStagingSource!();

    expect(await screen.findByTestId('amend-row-STAGING_ONLY_KEY')).toBeInTheDocument();
    expect(screen.queryByTestId('amend-row-PRODUCTION_SECRET')).not.toBeInTheDocument();
    expect(screen.getByTestId('amend-new-key')).toHaveValue('');
    expect(screen.getByTestId('amend-new-value')).toHaveValue('');
    expect(screen.queryByText('That key already exists.')).not.toBeInTheDocument();

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

    expect(await screen.findByText('API_URL')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-share-API_URL'));

    expect(await screen.findByTestId('parameter-share-dialog')).toBeInTheDocument();
    fireEvent.click(await screen.findByTestId('parameter-share-copy-1'));

    await waitFor(() => {
      expect(writeClipboard).toHaveBeenCalledWith(
        `${window.location.origin}/share/HistoryToken1234`,
      );
    });
  });
});
