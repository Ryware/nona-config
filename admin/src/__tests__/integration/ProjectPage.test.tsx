import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { writeClipboard } from '@solid-primitives/clipboard';
import { server } from '../mocks/server';
import { clearActiveEnvironmentName } from '../../entities/project/model/active-environment';
import { clearActiveProjectSlug } from '../../entities/project/model/active-project';
import { ToastProvider } from '../../shared/ui/toast';
import ProjectPage, {
  ProjectApiKeysPage,
  ProjectEnvironmentsPage,
  ProjectShareLinksPage,
  ProjectReleasesPage,
} from '../../pages/projects/ProjectPage';
import { mockToken } from '../mocks/data';

vi.mock('@solid-primitives/clipboard', () => ({
  writeClipboard: vi.fn(() => Promise.resolve()),
}));

function renderProjectPage(path = '/projects/my-app') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  window.history.pushState({}, '', path);

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="/projects/:slug" component={ProjectPage} />
            <Route path="/projects/:slug/environments" component={ProjectEnvironmentsPage} />
            <Route path="/projects/:slug/shared-links" component={ProjectShareLinksPage} />
            <Route path="/projects/:slug/api-keys" component={ProjectApiKeysPage} />
            <Route path="/projects/:slug/releases" component={ProjectReleasesPage} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('ProjectPage', () => {
  beforeEach(() => {
    clearActiveEnvironmentName('my-app');
    clearActiveProjectSlug();
    localStorage.removeItem('active_environment_by_project');
    localStorage.removeItem('active_project_slug');
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem('auth_session', JSON.stringify({ email: 'admin@example.com', role: 'admin', isAdmin: true }));
    vi.restoreAllMocks();
    vi.mocked(writeClipboard).mockResolvedValue(undefined);
    window.history.pushState({}, '', '/');
  });

  it('renders the parameters section without the legacy project header', async () => {
    renderProjectPage('/projects/my-app');

    expect(await screen.findByTestId('project-parameters-heading')).toBeInTheDocument();
    expect(screen.queryByTestId('project-detail-heading')).not.toBeInTheDocument();
  });

  it('displays environments returned by the API', async () => {
    renderProjectPage('/projects/my-app/environments');

    expect(await screen.findByText('production')).toBeInTheDocument();
    expect(await screen.findByText('staging')).toBeInTheDocument();
  });

  it('shows config entries when an environment is selected', async () => {
    renderProjectPage('/projects/my-app');

    // Production is auto-selected as the first environment by createEffect
    // Config entries load automatically — no manual click needed
    expect(await screen.findByText('API_URL')).toBeInTheDocument();
    expect(await screen.findByText('MAX_RETRIES')).toBeInTheDocument();
  });

  it('opens parameter details inline as an accordion', async () => {
    renderProjectPage('/projects/my-app');

    fireEvent.click(await screen.findByTestId('parameter-row-API_URL'));

    expect(await screen.findByTestId('parameter-accordion-API_URL')).toBeInTheDocument();
  });

  it('shows prompt to select environment when none is active', async () => {
    // Return no environments so none is auto-selected
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/environments', () =>
        HttpResponse.json([]),
      ),
    );

    renderProjectPage('/projects/my-app');

    await waitFor(() => {
      expect(
        screen.getByText(/select an active environment from the header to view its parameters/i),
      ).toBeInTheDocument();
    });
  });

  it('shows "Add Environment" form when button is clicked', async () => {
    renderProjectPage('/projects/my-app/environments');

    const addEnvButton = await screen.findByRole('button', { name: /add environment/i });
    fireEvent.click(addEnvButton);

    expect(screen.getByLabelText(/environment name/i)).toBeInTheDocument();
  });

  it('auto-opens the environment form when there are no environments', async () => {
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/environments', () =>
        HttpResponse.json([]),
      ),
    );

    renderProjectPage('/projects/my-app/environments');

    expect(await screen.findByLabelText(/environment name/i)).toBeInTheDocument();
  });

  it('shows "Add Parameter" form when button is clicked and env is active', async () => {
    renderProjectPage('/projects/my-app');

    // Production is auto-selected by createEffect — just wait for the button to appear
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

    renderProjectPage('/projects/my-app');

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

    renderProjectPage('/projects/my-app');

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
    renderProjectPage('/projects/nonexistent-project');

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /^projects$/i })).toBeInTheDocument();
    });
  });

  it('config entries reload when switching environments', async () => {
    // Staging returns different config entries
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
          // fallthrough to default handler for production
          return HttpResponse.json([]);
        },
      ),
    );

    const environmentPage = renderProjectPage('/projects/my-app/environments');

    const stagingTab = await screen.findByText('staging');
    fireEvent.click(stagingTab);

    environmentPage.unmount();
    renderProjectPage('/projects/my-app');

    expect(await screen.findByText('STAGING_ONLY_KEY')).toBeInTheDocument();
  });

  it('generates a shareable link for a parameter', async () => {
    renderProjectPage('/projects/my-app');

    expect(await screen.findByText('API_URL')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-share-API_URL'));

    expect(await screen.findByTestId('parameter-share-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('parameter-share-create-button'));

    const generatedUrl = await screen.findByTestId('parameter-share-generated-url');
    expect(generatedUrl).toHaveValue(`${window.location.origin}/share/AbCdEf1234567890`);
  });

  it('renders API keys on the dedicated api keys page', async () => {
    renderProjectPage('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();
    expect(await screen.findByText('Web Client')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add environment/i })).not.toBeInTheDocument();
  });

  it('opens the API key form when the add button is clicked', async () => {
    renderProjectPage('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();
    expect(screen.queryByTestId('api-key-name-input')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));

    expect(await screen.findByTestId('api-key-name-input')).toBeInTheDocument();
  });

  it('renders shared links on the dedicated shared links page', async () => {
    renderProjectPage('/projects/my-app/shared-links');

    expect(await screen.findByTestId('project-shared-links-heading')).toBeInTheDocument();
    expect(await screen.findByText('API_URL')).toBeInTheDocument();
  });

  it('publishes a configuration release without auto-activating when a release is already active', async () => {
    const publishRequests: Array<{ version: string; makeActive: boolean }> = [];
    server.use(
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases',
        async ({ params, request }) => {
          const body = await request.json() as { version: string; makeActive: boolean };
          publishRequests.push(body);
          return HttpResponse.json({
            project: params.projectId,
            environment: params.envName,
            version: body.version,
            entryCount: 3,
            isActive: body.makeActive,
            createdAt: new Date().toISOString(),
            actor: 'admin@example.com',
            entries: [],
          }, { status: 201 });
        },
      ),
    );

    renderProjectPage('/projects/my-app/releases');

    // Guided flow: Create a version -> enter version -> land on the parameters
    // step -> Create release. Publishing must not auto-activate here.
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

    renderProjectPage('/projects/my-app/releases');

    fireEvent.click(await screen.findByTestId('release-create-version-button'));
    fireEvent.input(await screen.findByTestId('release-version-input'), {
      target: { value: '1.2' },
    });
    fireEvent.click(screen.getByTestId('release-version-confirm-button'));

    fireEvent.click(await screen.findByTestId('release-create-confirm-button'));

    expect(await screen.findByText('Release already exists')).toBeInTheDocument();
    // Still on the parameters step so the user can retry.
    expect(screen.getByTestId('release-create-confirm-button')).toBeInTheDocument();
  });

  it('amends a release into a new patch (publish-from-payload, working config untouched)', async () => {
    const draftCalls: string[] = [];
    const publishRequests: Array<{
      version: string;
      makeActive: boolean;
      entries?: Array<{ key: string; value: string }>;
    }> = [];
    server.use(
      // The draft endpoint is gone; assert it is never called.
      http.post(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/releases/:version/draft',
        ({ params }) => {
          draftCalls.push(String(params.version));
          return HttpResponse.json([]);
        },
      ),
      // Amend seeds its buffer from the source release's parameters.
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

    renderProjectPage('/projects/my-app/releases');

    // Amend -> auto next patch, jump straight into the buffer editor (no dialog).
    fireEvent.click(await screen.findByTestId('release-amend-1.1.0'));
    expect(screen.queryByTestId('release-version-dialog')).not.toBeInTheDocument();

    // The source release's parameters seed the editable buffer.
    await screen.findByTestId('amend-row-feature.x');

    fireEvent.click(screen.getByTestId('release-amend-confirm-button'));
    await waitFor(() => {
      expect(publishRequests.length).toBe(1);
    });
    expect(publishRequests[0].version).toBe('1.1.1');
    expect(publishRequests[0].makeActive).toBe(false);
    expect(publishRequests[0].entries?.some(entry => entry.key === 'feature.x')).toBe(true);
    // Publish-from-payload: the removed draft endpoint is never hit.
    expect(draftCalls).toEqual([]);
  });

  it('activates a configuration release', async () => {
    const activeRequests: Array<{ version: string | null }> = [];
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/active-release',
        async ({ params, request }) => {
          const body = await request.json() as { version: string | null };
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

    renderProjectPage('/projects/my-app/releases');

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

  it('opens release parameters on the parameters page without starting an amend flow', async () => {
    renderProjectPage('/projects/my-app/releases');

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

    renderProjectPage('/projects/my-app/releases');

    const activeDeleteButton = await screen.findByTestId('release-delete-1.0.0');
    expect(activeDeleteButton).toBeDisabled();
    fireEvent.click(screen.getByTestId('release-delete-1.1.0'));
    expect(await screen.findByTestId('release-delete-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('release-delete-confirm-button'));

    await waitFor(() => {
      expect(deleteRequests).toEqual(['1.1.0']);
    });
  });

  it('copies a share link from history', async () => {
    renderProjectPage('/projects/my-app');

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
