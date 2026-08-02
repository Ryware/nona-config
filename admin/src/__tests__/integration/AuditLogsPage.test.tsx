import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import AuditLogsPage from '../../pages/audit-logs/AuditLogsPage';
import { mockProjects, mockUsers, mockToken } from '../mocks/data';

function auditPage(items: object[]) {
  return {
    items,
    page: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : Math.ceil(items.length / 25),
    actions: [...new Set(items.map(item => (item as { action: string }).action))],
    environments: [
      ...new Set(
        items.flatMap(item => {
          const environment = (item as { environment: string | null }).environment;
          return environment ? [environment] : [];
        }),
      ),
    ],
  };
}

function renderAuditLogsPage() {
  window.history.pushState({}, '', '/audit-logs');
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <ToastProvider>
          <Router>
            <Route path="*" component={AuditLogsPage} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('AuditLogsPage', () => {
  beforeEach(() => {
    localStorage.setItem('auth_token', mockToken);
    localStorage.removeItem('nonaconfig_audit_logs');
    localStorage.removeItem('nonaconfig_param_history');
    vi.restoreAllMocks();
  });

  it('renders the Audit Logs heading', async () => {
    renderAuditLogsPage();
    expect(await screen.findByRole('heading', { name: /audit logs/i })).toBeInTheDocument();
  });

  it('renders a "Created Project" entry for each project', async () => {
    renderAuditLogsPage();
    const list = await screen.findByTestId('audit-log-list');
    // Wait for actual data to render (skeleton has no text content)
    await waitFor(() => expect(list).toHaveTextContent(mockProjects[0].name));
    for (const project of mockProjects) {
      expect(list).toHaveTextContent(project.name);
    }
  });

  it('renders an "Invited User" entry for each user', async () => {
    renderAuditLogsPage();
    for (const user of mockUsers) {
      expect(await screen.findByText(user.email)).toBeInTheDocument();
    }
  });

  it('renders the search input', async () => {
    renderAuditLogsPage();
    expect(await screen.findByPlaceholderText(/filter audit trail/i)).toBeInTheDocument();
  });

  it('renders the Export Logs button', async () => {
    renderAuditLogsPage();
    expect(await screen.findByRole('button', { name: /export logs/i })).toBeInTheDocument();
  });

  it('requests bounded pages from the server and loads the next page', async () => {
    const requestedPages: string[] = [];
    server.use(
      http.get('http://localhost:5027/admin/audit-logs', ({ request }) => {
        const url = new URL(request.url);
        requestedPages.push(url.searchParams.get('page') ?? '');
        const page = Number(url.searchParams.get('page'));
        const item = {
          id: `page-${page}`,
          actor: 'audit.user@example.test',
          actorIsSystem: false,
          actionKind: 'update',
          action: 'Updated Parameter',
          target: `page-${page}-target`,
          project: 'sample-project',
          environment: 'production',
          createdAt: '2026-07-29T12:00:00Z',
        };
        return HttpResponse.json({
          ...auditPage([item]),
          page,
          totalCount: 50,
          totalPages: 2,
        });
      }),
    );

    renderAuditLogsPage();
    expect(await screen.findByText('page-1-target')).toBeInTheDocument();
    expect(requestedPages).toEqual(['1']);

    fireEvent.click(screen.getByRole('button', { name: /next page/i }));

    expect(await screen.findByText('page-2-target')).toBeInTheDocument();
    expect(requestedPages).toEqual(['1', '2']);
  });

  it('filters entries by search text', async () => {
    renderAuditLogsPage();

    // Wait for the list to populate
    const list = await screen.findByTestId('audit-log-list');
    await waitFor(() => expect(list).toHaveTextContent(mockProjects[0].name));

    const searchInput = screen.getByPlaceholderText(/filter audit trail/i);
    fireEvent.input(searchInput, { target: { value: mockProjects[0].name } });

    await waitFor(() => {
      expect(list).toHaveTextContent(mockProjects[0].name);
      expect(list).not.toHaveTextContent(mockProjects[1].name);
    });
  });

  it('shows all entries after clearing the search', async () => {
    renderAuditLogsPage();
    const list = await screen.findByTestId('audit-log-list');
    await waitFor(() => expect(list).toHaveTextContent(mockProjects[0].name));

    const searchInput = screen.getByPlaceholderText(/filter audit trail/i);
    fireEvent.input(searchInput, { target: { value: mockProjects[0].name } });

    await waitFor(() => {
      expect(list).not.toHaveTextContent(mockProjects[1].name);
    });

    fireEvent.input(searchInput, { target: { value: '' } });

    await waitFor(() => {
      expect(list).toHaveTextContent(mockProjects[1].name);
    });
  });

  it('renders action badges from backend actionKind values', async () => {
    server.use(
      http.get('http://localhost:5027/admin/audit-logs', () =>
        HttpResponse.json(auditPage([
          {
            id: 'release-published',
            actor: 'audit.user@example.test',
            actorIsSystem: false,
            actionKind: 'create',
            action: 'Published Config Release',
            target: '1.3.1',
            project: 'sample-project',
            environment: 'production',
            createdAt: '2026-07-29T12:00:00Z',
          },
          {
            id: 'release-active',
            actor: 'audit.user@example.test',
            actorIsSystem: false,
            actionKind: 'update',
            action: 'Set Active Config Release',
            target: '1.3.1',
            project: 'sample-project',
            environment: 'production',
            createdAt: '2026-07-29T12:01:00Z',
          },
          {
            id: 'unusual-delete',
            actor: 'audit.user@example.test',
            actorIsSystem: false,
            actionKind: 'delete',
            action: 'Unusual Action Text',
            target: '1.3.0',
            project: 'sample-project',
            environment: 'production',
            createdAt: '2026-07-29T12:02:00Z',
          },
          {
            id: 'share-access',
            actor: 'System',
            actorIsSystem: true,
            actionKind: 'activity',
            action: 'Share Link Accessed',
            target: 'API_URL',
            project: 'sample-project',
            environment: 'production',
            createdAt: '2026-07-29T12:03:00Z',
          },
        ])),
      ),
    );

    renderAuditLogsPage();

    for (const [id, badge] of [
      ['release-published', 'Created'],
      ['release-active', 'Updated'],
      ['unusual-delete', 'Deleted'],
      ['share-access', 'Activity'],
    ] as const) {
      const row = await screen.findByTestId(`audit-row-${id}`);
      expect(within(row).getByText(badge, { selector: 'span' })).toBeInTheDocument();
    }

    const userRow = screen.getByTestId('audit-row-release-active');
    expect(within(userRow).queryByText('System', { selector: 'span' })).not.toBeInTheDocument();

    const systemRow = screen.getByTestId('audit-row-share-access');
    expect(within(systemRow).getByText('System', { selector: 'span' })).toBeInTheDocument();
  });

  it('shows an empty log when there are no projects or users', async () => {
    server.use(
      http.get('http://localhost:5027/admin/audit-logs', () => HttpResponse.json(auditPage([]))),
    );

    renderAuditLogsPage();

    // List should show the empty state message
    await waitFor(
      () => expect(screen.getByText(/no activity recorded yet/i)).toBeInTheDocument(),
      { timeout: 3000 }
    );
  });
});
