import { screen } from '@solidjs/testing-library';
import { beforeEach, describe, expect, it } from 'vitest';

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
});
