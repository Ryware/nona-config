import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { ToastProvider } from '../../shared/ui/toast';
import SharedParameterPage from '../../pages/shared/SharedParameterPage';

function renderSharedPage(token = 'AbCdEf1234567890') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  window.history.pushState({}, '', `/share/${token}`);

  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="/share/:token" component={SharedParameterPage} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('SharedParameterPage', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
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

  it('shows a clear error for revoked links', async () => {
    renderSharedPage('Revoked123456789');

    expect(await screen.findByTestId('shared-parameter-error')).toHaveTextContent(
      'This share link has been revoked.',
    );
  });
});
