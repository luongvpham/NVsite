import { setupServer } from 'msw/node';

// Sample đã bị xoá (docs/tasks/CLEANUP-SAMPLE.md) — module thật đầu tiên (Identity, Bước 3) thêm
// handler thật vào đây (`...getXxxMock()` từ `@vsite/api-sdk/mocks`).
export const server = setupServer();
