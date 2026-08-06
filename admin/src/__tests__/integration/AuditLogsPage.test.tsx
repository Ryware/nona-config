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
        items.map(item => {
          const environment = (item as { environment: string | null }).environment;
          return environment ?? '__global__';
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

const primaryPointer = {
  button: 0,
  pointerId: 1,
  pointerType: 'mouse',
} as const;

async function openExportMenu() {
  const trigger = await screen.findByRole('button', { name: /export logs/i });
  fireEvent.pointerDown(trigger, primaryPointer);
  return screen.findByRole('menu');
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

  it('exposes the export trigger and items with menu semantics', async () => {
    renderAuditLogsPage();
    const trigger = await screen.findByRole('button', { name: /export logs/i });

    expect(trigger).toHaveAttribute('aria-expanded', 'false');
    const menu = await openExportMenu();

    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    expect(within(menu).getByRole('menuitem', { name: /export csv/i })).toBeInTheDocument();
    expect(within(menu).getByRole('menuitem', { name: /export json/i })).toBeInTheDocument();
  });

  it.each(['ArrowDown', 'Enter'])('opens the export menu with %s and focuses the first item', async key => {
    renderAuditLogsPage();
    const trigger = await screen.findByRole('button', { name: /export logs/i });
    trigger.focus();

    fireEvent.keyDown(trigger, { key });

    const csvItem = await screen.findByRole('menuitem', { name: /export csv/i });
    await waitFor(() => expect(csvItem).toHaveFocus());
  });

  it('dismisses the export menu with Escape and returns focus to the trigger', async () => {
    renderAuditLogsPage();
    const trigger = await screen.findByRole('button', { name: /export logs/i });
    trigger.focus();
    fireEvent.keyDown(trigger, { key: 'ArrowDown' });

    const csvItem = await screen.findByRole('menuitem', { name: /export csv/i });
    await waitFor(() => expect(csvItem).toHaveFocus());
    fireEvent.keyDown(csvItem, { key: 'Escape' });

    await waitFor(() => {
      expect(screen.queryByRole('menu')).not.toBeInTheDocument();
      expect(trigger).toHaveAttribute('aria-expanded', 'false');
      expect(trigger).toHaveFocus();
    });
  });

  it('dismisses the export menu on an outside pointer interaction after reopening', async () => {
    renderAuditLogsPage();
    const trigger = await screen.findByRole('button', { name: /export logs/i });
    trigger.focus();
    fireEvent.keyDown(trigger, { key: 'ArrowDown' });
    const csvItem = await screen.findByRole('menuitem', { name: /export csv/i });
    await waitFor(() => expect(csvItem).toHaveFocus());
    fireEvent.keyDown(csvItem, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByRole('menu')).not.toBeInTheDocument());

    const menu = await openExportMenu();
    expect(menu).toBeInTheDocument();
    fireEvent.pointerDown(document.body, primaryPointer);

    await waitFor(() => {
      expect(screen.queryByRole('menu')).not.toBeInTheDocument();
      expect(trigger).toHaveAttribute('aria-expanded', 'false');
    });
  });

  it('downloads a server-side export with the active filters', async () => {
    const exportUrls: URL[] = [];
    server.use(
      http.get('http://localhost:5027/admin/audit-logs/export', ({ request }) => {
        exportUrls.push(new URL(request.url));
        return new HttpResponse('Time,Actor\n', {
          headers: {
            'Content-Type': 'text/csv',
            'Content-Disposition': 'attachment; filename="filtered-audit.csv"',
          },
        });
      }),
    );
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:audit-export');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    renderAuditLogsPage();
    const list = await screen.findByTestId('audit-log-list');
    await waitFor(() => expect(list).toHaveTextContent(mockProjects[0].name));
    fireEvent.input(screen.getByPlaceholderText(/filter audit trail/i), {
      target: { value: mockProjects[0].name },
    });
    await waitFor(() => expect(list).not.toHaveTextContent(mockProjects[1].name));

    const menu = await openExportMenu();
    fireEvent.click(within(menu).getByRole('menuitem', { name: /export csv/i }));

    await waitFor(() => expect(exportUrls).toHaveLength(1));
    const exportUrl = exportUrls[0];
    expect(exportUrl?.searchParams.get('format')).toBe('csv');
    expect(exportUrl?.searchParams.get('search')).toBe(mockProjects[0].name);
    expect(exportUrl?.searchParams.has('page')).toBe(false);
    expect(exportUrls.filter(url => url.searchParams.get('format') === 'json')).toHaveLength(0);
    expect(createObjectUrl).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
  });

  it('downloads exactly one JSON export without requesting CSV', async () => {
    const exportUrls: URL[] = [];
    server.use(
      http.get('http://localhost:5027/admin/audit-logs/export', ({ request }) => {
        exportUrls.push(new URL(request.url));
        return new HttpResponse('[]', {
          headers: {
            'Content-Type': 'application/json',
            'Content-Disposition': 'attachment; filename="audit.json"',
          },
        });
      }),
    );
    const createObjectUrl = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:audit-export');
    vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);

    renderAuditLogsPage();
    const menu = await openExportMenu();
    fireEvent.click(within(menu).getByRole('menuitem', { name: /export json/i }));

    await waitFor(() => expect(exportUrls).toHaveLength(1));
    expect(exportUrls[0]?.searchParams.get('format')).toBe('json');
    expect(exportUrls.filter(url => url.searchParams.get('format') === 'csv')).toHaveLength(0);
    expect(createObjectUrl).toHaveBeenCalledOnce();
    expect(click).toHaveBeenCalledOnce();
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

  it('offers direct navigation to both the last and first pages', async () => {
    const requestedPages: string[] = [];
    server.use(
      http.get('http://localhost:5027/admin/audit-logs', ({ request }) => {
        const url = new URL(request.url);
        const page = Number(url.searchParams.get('page'));
        requestedPages.push(String(page));

        return HttpResponse.json({
          ...auditPage([{
            id: `page-${page}`,
            actor: 'audit.user@example.test',
            actorIsSystem: false,
            actionKind: 'update',
            action: 'Updated Parameter',
            target: `page-${page}-target`,
            project: 'sample-project',
            environment: 'production',
            createdAt: '2026-07-29T12:00:00Z',
          }]),
          page,
          totalCount: 10_197,
          totalPages: 408,
        });
      }),
    );

    renderAuditLogsPage();
    expect(await screen.findByText('page-1-target')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '408' }));
    expect(await screen.findByText('page-408-target')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '1' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '1' }));
    expect(await screen.findByText('page-1-target')).toBeInTheDocument();
    expect(requestedPages).toEqual(['1', '408', '1']);
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

  it('requests global-scope entries from the server', async () => {
    const requestedEnvironments: Array<string | null> = [];
    server.use(
      http.get('http://localhost:5027/admin/audit-logs', ({ request }) => {
        const environment = new URL(request.url).searchParams.get('environment');
        requestedEnvironments.push(environment);
        const item = {
          id: 'global-entry',
          actor: 'audit.user@example.test',
          actorIsSystem: false,
          actionKind: 'create',
          action: 'Created Project',
          target: 'global-target',
          project: 'sample-project',
          environment: null,
          createdAt: '2026-07-29T12:00:00Z',
        };

        return HttpResponse.json({
          ...auditPage([item]),
          environments: ['__global__', 'production'],
        });
      }),
    );

    renderAuditLogsPage();
    expect(await screen.findByText('global-target')).toBeInTheDocument();

    fireEvent.pointerDown(screen.getByRole('button', { name: 'Environment' }), {
      button: 0,
      pointerId: 1,
      pointerType: 'mouse',
    });
    fireEvent.click(await screen.findByText('Global Scope'));

    await waitFor(() => expect(requestedEnvironments).toEqual([null, '__global__']));
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
