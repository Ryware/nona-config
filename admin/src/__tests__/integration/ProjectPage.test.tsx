import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { writeClipboard } from '@solid-primitives/clipboard';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import ProjectPage from '../../pages/projects/ProjectPage';
import { mockToken } from '../mocks/data';

vi.mock('@solid-primitives/clipboard', () => ({
  writeClipboard: vi.fn(() => Promise.resolve()),
}));

function renderProjectPage(slug = 'my-app') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  // Set URL so the router matches the route and useParams returns the slug
  window.history.pushState({}, '', `/projects/${slug}`);

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="/projects/:slug" component={ProjectPage} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('ProjectPage', () => {
  beforeEach(() => {
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem('auth_session', JSON.stringify({ email: 'admin@example.com', role: 'admin', isAdmin: true }));
    vi.restoreAllMocks();
    vi.mocked(writeClipboard).mockResolvedValue(undefined);
    window.history.pushState({}, '', '/');
  });

  it('renders the project name', async () => {
    renderProjectPage('my-app');
    // Use heading role to distinguish from sidebar nav span
    expect(await screen.findByRole('heading', { name: 'my-app' })).toBeInTheDocument();
  });

  it('displays environments returned by the API', async () => {
    renderProjectPage('my-app');

    expect(await screen.findByText('production')).toBeInTheDocument();
    expect(await screen.findByText('staging')).toBeInTheDocument();
  });

  it('shows config entries when an environment is selected', async () => {
    renderProjectPage('my-app');

    // Production is auto-selected as the first environment by createEffect
    // Config entries load automatically — no manual click needed
    expect(await screen.findByText('API_URL')).toBeInTheDocument();
    expect(await screen.findByText('MAX_RETRIES')).toBeInTheDocument();
  });

  it('shows prompt to select environment when none is active', async () => {
    // Return no environments so none is auto-selected
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/environments', () =>
        HttpResponse.json([]),
      ),
    );

    renderProjectPage('my-app');

    await waitFor(() => {
      expect(
        screen.getByText(/select an environment above to view its parameters/i),
      ).toBeInTheDocument();
    });
  });

  it('shows "Add Environment" form when button is clicked', async () => {
    renderProjectPage('my-app');

    const addEnvButton = await screen.findByRole('button', { name: /add environment/i });
    fireEvent.click(addEnvButton);

    expect(screen.getByLabelText(/environment name/i)).toBeInTheDocument();
  });

  it('shows "Add Parameter" form when button is clicked and env is active', async () => {
    renderProjectPage('my-app');

    // Production is auto-selected by createEffect — just wait for the button to appear
    const addParamButton = await screen.findByRole('button', { name: /add parameter/i });
    fireEvent.click(addParamButton);

    await waitFor(() => {
      expect(screen.getByLabelText(/^key$/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/^value$/i)).toBeInTheDocument();
    });
  });

  it('shows backend validation message when parameter creation fails', async () => {
    server.use(
      http.put(
        'http://localhost:5027/admin/projects/:projectId/environments/:envName/config-entries/:key',
        () =>
          HttpResponse.json(
            { error: "Value must be 'true' or 'false' when contentType is 'boolean'." },
            { status: 400 },
          ),
      ),
    );

    renderProjectPage('my-app');

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
    renderProjectPage('nonexistent-project');

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

    renderProjectPage('my-app');

    // Select staging environment
    const stagingTab = await screen.findByText('staging');
    fireEvent.click(stagingTab);

    expect(await screen.findByText('STAGING_ONLY_KEY')).toBeInTheDocument();
  });

  it('generates a shareable link for a parameter', async () => {
    renderProjectPage('my-app');

    expect(await screen.findByText('API_URL')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('parameter-share-API_URL'));

    expect(await screen.findByTestId('parameter-share-dialog')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('parameter-share-create-button'));

    const generatedUrl = await screen.findByTestId('parameter-share-generated-url');
    expect(generatedUrl).toHaveValue(`${window.location.origin}/share/AbCdEf1234567890`);
  });

  it('copies a share link from history', async () => {
    renderProjectPage('my-app');

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
