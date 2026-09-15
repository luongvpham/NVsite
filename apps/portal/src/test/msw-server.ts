import { setupServer } from 'msw/node';
import { getIdentityMock, getShopMock } from '@vsite/api-sdk/mocks';

// Handler mặc định sinh từ contract (faker-based) — test override bằng server.use() khi cần
// state thật (vd. shop vừa tạo phải xuất hiện trong danh sách).
export const server = setupServer(...getIdentityMock(), ...getShopMock());
