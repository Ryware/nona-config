import { Route, Router } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { render, screen } from '@solidjs/testing-library';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Sidebar } from '../../widgets/app-shell/Sidebar';
import { mockToken } from '../mocks/data';

describe('Sidebar account link', () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'admin@example.com', role: 'admin' }),
    );
  });

  it('links the signed-in user card to the Account page', async () => {
    const queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
    render(() => (
      <QueryClientProvider client={queryClient}>
        <Router>
          <Route
            path="*"
            component={() => (
              <Sidebar
                isOpen
                onClose={vi.fn()}
                collapsed={false}
                onToggleCollapse={vi.fn()}
              />
            )}
          />
        </Router>
      </QueryClientProvider>
    ));

    expect(await screen.findByTestId('sidebar-account-link')).toHaveAttribute('href', '/account');
  });
});
