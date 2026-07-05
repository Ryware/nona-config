import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { ToastProvider } from '../../shared/ui/toast';
import DashboardPage from '../../pages/dashboard/DashboardPage';
import { mockToken } from '../mocks/data';
import type { JSX } from 'solid-js';

function renderWithProviders(ui: () => JSX.Element) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="*" component={ui} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('DashboardPage', () => {
  beforeEach(() => {
    localStorage.setItem('auth_token', mockToken);
    vi.restoreAllMocks();
  });

  it('renders the Dashboard heading', async () => {
    renderWithProviders(() => <DashboardPage />);
    // Check page title / heading is rendered
    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
  });

  it('displays the statistics cards counts', async () => {
    renderWithProviders(() => <DashboardPage />);
    
    // Stats cards render values after mock counts API returns
    expect(await screen.findAllByText('Projects')).not.toHaveLength(0);
    expect(screen.getByText('Config Entries')).toBeInTheDocument();
    expect(screen.getByText('Team Members')).toBeInTheDocument();

    // Verify values from mocks: mockProjects is 2, mockUsers is 2, mockConfigEntries is 3
    expect(await screen.findAllByText('2')).not.toHaveLength(0);
    expect(await screen.findAllByText('3')).not.toHaveLength(0);
  });

  it('contains quick action navigation links', async () => {
    renderWithProviders(() => <DashboardPage />);

    expect(await screen.findByRole('link', { name: /manage projects/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /team management/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /security audit logs/i })).toBeInTheDocument();
  });
});
