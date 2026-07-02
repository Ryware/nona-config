import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import AuditLogsPage from '../../pages/audit-logs/AuditLogsPage';
import { mockProjects, mockUsers, mockToken } from '../mocks/data';

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

  it('shows an empty log when there are no projects or users', async () => {
    server.use(
      http.get('http://localhost:5027/admin/audit-logs', () => HttpResponse.json([])),
    );

    renderAuditLogsPage();

    // List should show the empty state message
    await waitFor(
      () => expect(screen.getByText(/no activity recorded yet/i)).toBeInTheDocument(),
      { timeout: 3000 }
    );
  });
});
