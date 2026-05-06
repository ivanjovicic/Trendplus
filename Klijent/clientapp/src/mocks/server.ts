import { setupServer } from "msw/node";

// Central MSW server for unit/integration tests. Handlers can be added by tests.
export const server = setupServer();
