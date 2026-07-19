import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@solidjs/testing-library';
import { Router, Route } from '@solidjs/router';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { MetaProvider } from '@solidjs/meta';
import { http, HttpResponse } from 'msw';
import { server } from '../mocks/server';
import { ToastProvider } from '../../shared/ui/toast';
import ProjectsPage from '../../pages/projects/ProjectsPage';
import { mockProjects, mockToken } from '../mocks/data';
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

describe('ProjectsPage', () => {
  beforeEach(() => {
    localStorage.setItem('auth_token', mockToken);
    localStorage.setItem('auth_session', JSON.stringify({ email: 'admin@example.com', role: 'admin', isAdmin: true }));
    vi.restoreAllMocks();
  });

  it('renders the page heading', async () => {
    renderWithProviders(() => <ProjectsPage />);
    // Use heading role to avoid matching the sidebar "Projects" nav link
    expect(await screen.findByRole('heading', { name: 'Projects' })).toBeInTheDocument();
  });

  it('lists projects returned by the API', async () => {
    renderWithProviders(() => <ProjectsPage />);

    for (const project of mockProjects) {
      // Project name appears in sidebar nav AND in the page grid h3 — check heading specifically
      expect(await screen.findByRole('heading', { name: project.name })).toBeInTheDocument();
    }
  });

  it('shows empty state when there are no projects', async () => {
    server.use(
      http.get('http://localhost:5027/admin/projects', () => HttpResponse.json([])),
    );

    renderWithProviders(() => <ProjectsPage />);

    await waitFor(() => {
      // "No projects yet" appears in both sidebar and page content — check at least one exists
      expect(screen.getAllByText(/no projects yet/i).length).toBeGreaterThanOrEqual(1);
    });
    expect(screen.getByLabelText(/project name/i)).toBeInTheDocument();
  });

  it('shows the create project form when "Create New Project" is clicked', async () => {
    renderWithProviders(() => <ProjectsPage />);

    expect(screen.queryByLabelText(/project name/i)).not.toBeInTheDocument();

    const newProjectButtons = await screen.findAllByRole('button', { name: /new project/i });
    fireEvent.click(newProjectButtons[0]);

    expect(screen.getByLabelText(/project name/i)).toBeInTheDocument();
  });

  it('shows validation error for empty project name', async () => {
    renderWithProviders(() => <ProjectsPage />);

    const newProjectButtons = await screen.findAllByRole('button', { name: /new project/i });
    fireEvent.click(newProjectButtons[0]);

    // Submit form with empty name
    const form = screen.getByLabelText(/project name/i).closest('form')!;
    fireEvent.submit(form);

    await waitFor(() => {
      expect(screen.getByText(/project name is required/i)).toBeInTheDocument();
    });
  });

  it('shows validation error for invalid project name characters', async () => {
    renderWithProviders(() => <ProjectsPage />);

    const newProjectButtons = await screen.findAllByRole('button', { name: /new project/i });
    fireEvent.click(newProjectButtons[0]);

    fireEvent.input(screen.getByLabelText(/project name/i), {
      target: { value: 'invalid name!' },
    });

    const form = screen.getByLabelText(/project name/i).closest('form')!;
    fireEvent.submit(form);

    await waitFor(() => {
      expect(screen.getByText(/use only letters, numbers, and hyphens/i)).toBeInTheDocument();
    });
  });

  it('creates a project and hides the form on success', async () => {
    renderWithProviders(() => <ProjectsPage />);

    const newProjectButtons = await screen.findAllByRole('button', { name: /new project/i });
    fireEvent.click(newProjectButtons[0]);

    fireEvent.input(screen.getByLabelText(/project name/i), {
      target: { value: 'new-project' },
    });

    const form = screen.getByLabelText(/project name/i).closest('form')!;
    fireEvent.submit(form);

    await waitFor(() => {
      // Form should be hidden after successful creation
      expect(screen.queryByLabelText(/project name/i)).not.toBeInTheDocument();
    });
  });

  it('opens delete confirmation when delete button is clicked', async () => {
    renderWithProviders(() => <ProjectsPage />);

    // Wait for projects to load
    await screen.findByRole('heading', { name: mockProjects[0].name });

    // Delete button accessible name comes from inner icon text "delete_outline"
    const deleteButtons = await screen.findAllByTitle(/delete/i);
    fireEvent.click(deleteButtons[0]);

    await waitFor(() => {
      // Confirmation modal heading is "Delete Project?"
      expect(screen.getByRole('heading', { name: /delete project/i })).toBeInTheDocument();
    });
  });
});
