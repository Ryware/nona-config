import { writeClipboard } from '@solid-primitives/clipboard';
import { MetaProvider } from '@solidjs/meta';
import { Route, Router } from '@solidjs/router';
import { render } from '@solidjs/testing-library';
import { QueryClient, QueryClientProvider } from '@tanstack/solid-query';
import { vi } from 'vitest';

import {
  clearActiveEnvironmentName,
} from '../../entities/project/model/active-environment';
import { clearActiveProjectSlug } from '../../entities/project/model/active-project';
import ApiKeysSection from '../../pages/projects/sections/ApiKeysSection';
import EnvironmentsSection from '../../pages/projects/sections/EnvironmentsSection';
import ParametersSection from '../../pages/projects/sections/ParametersSection';
import ReleasesSection from '../../pages/projects/sections/ReleasesSection';
import SharedLinksSection from '../../pages/projects/sections/SharedLinksSection';
import { ToastProvider } from '../../shared/ui/toast';
import { mockToken } from '../mocks/data';

export { writeClipboard };

export function resetProjectSectionsTestState() {
  clearActiveEnvironmentName('my-app');
  clearActiveProjectSlug();
  localStorage.removeItem('active_environment_by_project');
  localStorage.removeItem('active_project_slug');
  localStorage.setItem('auth_token', mockToken);
  localStorage.setItem(
    'auth_session',
    JSON.stringify({ email: 'admin@example.com', role: 'admin' }),
  );
  vi.clearAllMocks();
  if (vi.isMockFunction(writeClipboard)) {
    vi.mocked(writeClipboard).mockResolvedValue(undefined);
  }
  window.history.pushState({}, '', '/');
}

export function renderProjectSections(path = '/projects/my-app') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  window.history.pushState({}, '', path);

  const renderResult = render(() => (
    <MetaProvider>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <Router>
            <Route path="/projects/:slug" component={ParametersSection} />
            <Route path="/projects/:slug/environments" component={EnvironmentsSection} />
            <Route path="/projects/:slug/shared-links" component={SharedLinksSection} />
            <Route path="/projects/:slug/api-keys" component={ApiKeysSection} />
            <Route path="/projects/:slug/releases" component={ReleasesSection} />
          </Router>
        </ToastProvider>
      </QueryClientProvider>
    </MetaProvider>
  ));

  return Object.assign(renderResult, { queryClient });
}
