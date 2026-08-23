import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { ThemeProvider } from '../../shared/hooks/useTheme';
import { ToastProvider } from '../../shared/ui/toast';
import SharedParameterPage from '../../pages/shared/SharedParameterPage';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';

function renderSharedPage(token = 'AbCdEf1234567890') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  window.history.pushState({}, '', `/share/${token}`);

  return render(() => (
    <MetaProvider>
      <ThemeProvider>
        <QueryClientProvider client={queryClient}>
          <ToastProvider>
            <Router>
              <Route path="/share/:token" component={SharedParameterPage} />
            </Router>
          </ToastProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </MetaProvider>
  ));
}

describe('SharedParameterPage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
    document.cookie = 'nona_theme=; Max-Age=0; Path=/';
    window.history.pushState({}, '', '/');
  });

  it('renders a shared parameter without requiring auth', async () => {
    renderSharedPage();

    expect(await screen.findByTestId('shared-parameter-page')).toBeInTheDocument();
    expect(screen.getByTestId('shared-parameter-key')).toHaveTextContent('API_URL');
    expect(screen.getByTestId('shared-parameter-environment')).toHaveTextContent('production');
    expect(screen.getByTestId('shared-parameter-value-input')).toHaveValue('https://api.example.com');
    expect(document.head.querySelector('meta[name="robots"]')).toHaveAttribute(
      'content',
      'noindex,nofollow',
    );

    const themeToggle = screen.getByRole('button', { name: 'Switch to dark theme' });
    expect(document.documentElement).toHaveAttribute('data-theme', 'light');

    fireEvent.click(themeToggle);

    await waitFor(() => {
      expect(document.documentElement).toHaveAttribute('data-theme', 'dark');
      expect(themeToggle).toHaveAccessibleName('Switch to light theme');
    });
  });

  it('updates an editable shared parameter', async () => {
    renderSharedPage();

    const input = await screen.findByTestId('shared-parameter-value-input');
    fireEvent.input(input, { target: { value: 'https://shared.example.com' } });
    fireEvent.click(screen.getByTestId('shared-parameter-save-button'));

    await waitFor(() => {
      expect(screen.getByTestId('shared-parameter-value-input')).toHaveValue(
        'https://shared.example.com',
      );
    });
  });

  it('shows the configured unit for a shared number parameter', async () => {
    server.use(
      http.get('http://localhost:5027/public/share-links/:token', () =>
        HttpResponse.json({
          environment: 'production',
          key: 'Checkout:Timeout',
          value: '250',
          contentType: 'number',
          canEdit: true,
          expiresAt: '2099-01-01T00:00:00Z',
          unit: 'ms',
        }),
      ),
    );

    renderSharedPage();

    expect(await screen.findByTestId('shared-parameter-value-input')).toHaveValue(250);
    expect(screen.getByText('ms')).toBeInTheDocument();
  });

  it('shows a clear error for revoked links', async () => {
    renderSharedPage('Revoked123456789');

    expect(await screen.findByTestId('shared-parameter-error')).toHaveTextContent(
      'This share link has been revoked.',
    );
    expect(screen.getByRole('button', { name: /Switch to (dark|light) theme/ })).toBeInTheDocument();
  });
});
