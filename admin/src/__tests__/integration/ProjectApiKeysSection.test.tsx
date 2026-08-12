import { fireEvent, screen, waitFor } from '@solidjs/testing-library';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

import { setActiveEnvironmentName } from '../../entities/project/model/active-environment';
import { mockProjects } from '../mocks/data';
import { server } from '../mocks/server';
import {
  renderProjectSections,
  resetProjectSectionsTestState,
} from './project-sections.test-utils';

describe('ProjectApiKeysSection', () => {
  beforeEach(() => {
    resetProjectSectionsTestState();
  });

  it('renders API keys on the dedicated api keys page', async () => {
    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();
    expect(await screen.findByText('Web Client')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add environment/i })).not.toBeInTheDocument();
  });

  it('denies project Viewers direct access to API keys', async () => {
    localStorage.setItem(
      'auth_session',
      JSON.stringify({ email: 'viewer@example.com', role: 'member' }),
    );
    server.use(
      http.get('http://localhost:5027/admin/projects', () =>
        HttpResponse.json([{ ...mockProjects[0], accessLevel: 'viewer' }]),
      ),
    );

    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('access-denied')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /add api key/i })).not.toBeInTheDocument();
  });

  it('opens the API key form when the add button is clicked', async () => {
    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();
    expect(screen.queryByTestId('api-key-name-input')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));

    expect(await screen.findByTestId('api-key-name-input')).toBeInTheDocument();
  });

  it('creates an API key for the active selected environment', async () => {
    const createRequests: Array<{
      name: string;
      environment?: string | null;
      scope?: string | null;
    }> = [];

    server.use(
      http.post('http://localhost:5027/admin/projects/:projectId/api-keys', async ({ request }) => {
        const body = (await request.json()) as {
          name: string;
          environment?: string | null;
          scope?: 'client' | 'server' | 'all';
        };
        createRequests.push(body);

        return HttpResponse.json(
          {
            id: 'key-new',
            name: body.name,
            key: 'ak_test_new1234567890',
            project: 'my-app',
            environment: body.environment ?? null,
            scope: body.scope ?? 'client',
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
          },
          { status: 201 },
        );
      }),
    );

    setActiveEnvironmentName('my-app', 'staging');

    renderProjectSections('/projects/my-app/api-keys');

    expect(await screen.findByTestId('project-api-keys-heading')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /add api key/i }));
    fireEvent.input(await screen.findByTestId('api-key-name-input'), {
      target: { value: 'Staging Client Key' },
    });
    fireEvent.click(screen.getByTestId('api-key-create-button'));

    await waitFor(() => {
      expect(createRequests).toEqual([
        {
          name: 'Staging Client Key',
          environment: 'staging',
          scope: 'client',
        },
      ]);
    });
  });
});
