import { MetaProvider } from '@solidjs/meta';
import { Route, Router } from '@solidjs/router';
import { render, screen } from '@solidjs/testing-library';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import App from '../../app/App';
import { queryClient as appQueryClient } from '../../app/query-client';
import { clearActiveProjectSlug, setActiveProjectSlug } from '../../entities/project/model/active-project';
import { Sidebar } from '../../widgets/app-shell/Sidebar';
import { mockProjects, mockToken } from '../mocks/data';
import { server } from '../mocks/server';

describe('role-aware navigation', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    appQueryClient.clear();
    clearActiveProjectSlug();
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'member@example.com', role: 'member' }),
    );
  });

  it.each(['/users', '/audit-logs'])('denies Members direct access to %s', async path => {
    window.history.pushState({}, '', path);

    render(() => <App />);

    expect(await screen.findByTestId('access-denied', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Team' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Audit Logs' })).not.toBeInTheDocument();
  });

  it.each([
    ['viewer', false],
    ['editor', true],
  ] as const)('%s project access controls secret-resource navigation', async (accessLevel, visible) => {
    setActiveProjectSlug(mockProjects[0].urlSlug);
    server.use(
      http.get('http://localhost:5027/admin/projects', () =>
        HttpResponse.json([{ ...mockProjects[0], accessLevel }]),
      ),
    );
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    window.history.pushState({}, '', `/projects/${mockProjects[0].urlSlug}`);

    render(() => (
      <MetaProvider>
        <QueryClientProvider client={queryClient}>
          <Router>
            <Route
              path="*"
              component={() => (
                <Sidebar
                  isOpen
                  onClose={() => undefined}
                  collapsed={false}
                  onToggleCollapse={() => undefined}
                />
              )}
            />
          </Router>
        </QueryClientProvider>
      </MetaProvider>
    ));

    await screen.findByRole('link', { name: 'Parameters' });
    if (visible) {
      expect(await screen.findByRole('link', { name: 'API Keys' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Shared Links' })).toBeInTheDocument();
    } else {
      expect(screen.queryByRole('link', { name: 'API Keys' })).not.toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Shared Links' })).not.toBeInTheDocument();
    }
  });
});
