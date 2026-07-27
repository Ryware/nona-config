import { fireEvent, screen } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { server } from '../mocks/server';
import {
  renderProjectSections,
  resetProjectSectionsTestState,
} from './project-sections.test-utils';

describe('ProjectEnvironmentsSection', () => {
  beforeEach(() => {
    resetProjectSectionsTestState();
  });

  it('displays environments returned by the API', async () => {
    renderProjectSections('/projects/my-app/environments');

    expect(await screen.findByText('production')).toBeInTheDocument();
    expect(await screen.findByText('staging')).toBeInTheDocument();
  });

  it('shows "Add Environment" form when button is clicked', async () => {
    renderProjectSections('/projects/my-app/environments');

    const addEnvButton = await screen.findByRole('button', { name: /add environment/i });
    fireEvent.click(addEnvButton);

    expect(screen.getByLabelText(/environment name/i)).toBeInTheDocument();
  });

  it('auto-opens the environment form when there are no environments', async () => {
    server.use(
      http.get('http://localhost:5027/admin/projects/:projectId/environments', () =>
        HttpResponse.json([]),
      ),
    );

    renderProjectSections('/projects/my-app/environments');

    expect(await screen.findByLabelText(/environment name/i)).toBeInTheDocument();
  });
});
