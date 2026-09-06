import { getSampleMock } from '@vsite/api-sdk/mocks';
import { setupServer } from 'msw/node';

export const server = setupServer(...getSampleMock());
