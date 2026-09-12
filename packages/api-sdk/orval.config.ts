import { defineConfig } from 'orval';

const header = () => ['GENERATED — DO NOT EDIT (pnpm gen:api)'];

// Input luôn là contract ĐÃ DUYỆT (contracts/openapi/{module}.v{n}.json), không bao giờ .staging/.
// Sample đã bị xoá (docs/tasks/CLEANUP-SAMPLE.md) — config module thật đầu tiên (Identity, Bước 3)
// thêm vào đây theo đúng pattern cũ (xem git history của file này trước khi Sample bị xoá).
export default defineConfig({});
