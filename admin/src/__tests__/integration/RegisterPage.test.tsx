import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import RegisterPage from '../../pages/auth/RegisterPage';
import { mockToken } from '../mocks/data';

const ProjectsStub = () => <div data-testid="projects-stub">Projects</div>;

function renderRegisterPage() {
  window.history.pushState({}, '', '/register');
  return render(() => (
    <MetaProvider>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <ToastProvider>
          <Router>
            <Route path="/register" component={RegisterPage} />
            <Route path="/projects" component={ProjectsStub} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));
}

describe('RegisterPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('renders the Admin Registration heading', () => {
    renderRegisterPage();
    expect(screen.getByRole('heading', { name: /admin registration/i })).toBeInTheDocument();
  });

  it('renders email, password, and confirm password fields', () => {
    renderRegisterPage();
    expect(screen.getByPlaceholderText(/admin@nona\.dev/i)).toBeInTheDocument();
    // Two password fields share the same placeholder
    const passwordFields = screen.getAllByPlaceholderText(/•{4,}/);
    expect(passwordFields).toHaveLength(2);
  });

  it('renders the Create Account submit button', () => {
    renderRegisterPage();
    expect(screen.getByRole('button', { name: /create account/i })).toBeInTheDocument();
  });

  it('shows "Passwords do not match" error when passwords differ', async () => {
    renderRegisterPage();

    fireEvent.input(screen.getByPlaceholderText(/admin@nona\.dev/i), {
      target: { value: 'admin@example.com' },
    });
    const [pwField, confirmField] = screen.getAllByPlaceholderText(/•{4,}/);
    fireEvent.input(pwField, { target: { value: 'password123' } });
    fireEvent.input(confirmField, { target: { value: 'different456' } });

    fireEvent.submit(screen.getByRole('button', { name: /create account/i }).closest('form')!);

    await waitFor(() => {
      expect(screen.getByText(/passwords do not match/i)).toBeInTheDocument();
    });
  });

  it('stores token and navigates to /projects on successful registration', async () => {
    renderRegisterPage();

    fireEvent.input(screen.getByPlaceholderText(/admin@nona\.dev/i), {
      target: { value: 'admin@example.com' },
    });
    const [pwField, confirmField] = screen.getAllByPlaceholderText(/•{4,}/);
    fireEvent.input(pwField, { target: { value: 'password123' } });
    fireEvent.input(confirmField, { target: { value: 'password123' } });

    fireEvent.submit(screen.getByRole('button', { name: /create account/i }).closest('form')!);

    expect(await screen.findByTestId('projects-stub')).toBeInTheDocument();
    expect(localStorage.getItem('auth_token')).toBe(mockToken);
  });

  it('shows an error message when the API returns an error', async () => {
    server.use(
      http.post('http://localhost:5027/auth/register', () =>
        HttpResponse.json({ detail: 'Email already exists' }, { status: 409 }),
      ),
    );

    renderRegisterPage();

    fireEvent.input(screen.getByPlaceholderText(/admin@nona\.dev/i), {
      target: { value: 'existing@example.com' },
    });
    const [pwField, confirmField] = screen.getAllByPlaceholderText(/•{4,}/);
    fireEvent.input(pwField, { target: { value: 'password123' } });
    fireEvent.input(confirmField, { target: { value: 'password123' } });

    fireEvent.submit(screen.getByRole('button', { name: /create account/i }).closest('form')!);

    await waitFor(() => {
      expect(screen.getByText(/email already exists/i)).toBeInTheDocument();
    });
  });

  it('shows "Creating account…" while the request is pending', async () => {
    let resolveRegistration!: () => void;
    const pendingRegistration = new Promise<void>((resolve) => {
      resolveRegistration = resolve;
    });
    server.use(
      http.post('http://localhost:5027/auth/register', async () => {
        await pendingRegistration;
        return HttpResponse.json({ token: mockToken, role: 'admin', expiresAt: '2099-01-01T00:00:00Z' });
      }),
    );

    renderRegisterPage();

    fireEvent.input(screen.getByPlaceholderText(/admin@nona\.dev/i), {
      target: { value: 'admin@example.com' },
    });
    const [pwField, confirmField] = screen.getAllByPlaceholderText(/•{4,}/);
    fireEvent.input(pwField, { target: { value: 'password123' } });
    fireEvent.input(confirmField, { target: { value: 'password123' } });

    fireEvent.submit(screen.getByRole('button', { name: /create account/i }).closest('form')!);

    await waitFor(() => {
      expect(screen.getByText(/creating account/i)).toBeInTheDocument();
    });

    resolveRegistration();
    expect(await screen.findByTestId('projects-stub')).toBeInTheDocument();
  });
});
