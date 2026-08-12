import { screen } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { mockProjects } from '../mocks/data';
import { server } from '../mocks/server';
import {
  renderProjectSections,
  resetProjectSectionsTestState,
} from './project-sections.test-utils';

describe('ProjectSharedLinksSection', () => {
  beforeEach(() => {
    resetProjectSectionsTestState();
  });

  it('renders shared links on the dedicated shared links page', async () => {
    renderProjectSections('/projects/my-app/shared-links');

    expect(await screen.findByTestId('project-shared-links-heading')).toBeInTheDocument();
    expect(await screen.findByText('API_URL')).toBeInTheDocument();
  });

  it('denies project Viewers direct access to shared links', async () => {
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'viewer@example.com', role: 'member' }),
    );
    server.use(
      http.get('http://localhost:5027/admin/projects', () =>
        HttpResponse.json([{ ...mockProjects[0], accessLevel: 'viewer' }]),
      ),
    );

    renderProjectSections('/projects/my-app/shared-links');

    expect(await screen.findByTestId('access-denied')).toBeInTheDocument();
    expect(screen.queryByTestId('project-shared-links-heading')).not.toBeInTheDocument();
  });
});
