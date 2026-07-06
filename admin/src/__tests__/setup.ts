import '@testing-library/jest-dom';
import { server } from './mocks/server';
import { vi } from 'vitest';

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  server.resetHandlers();
  vi.restoreAllMocks();
});
afterAll(() => server.close());

