import path from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Đường dẫn dùng chung của gen-registry / check-additive / registry-lock.
 *
 * Ghi đè được bằng biến môi trường — CHỈ để test chạy trên BẢN SAO tạm của `registry/` (TOOLING-001):
 * trước đây test đột biến sửa thẳng `registry/*.manifest.ts` đã commit rồi khôi phục bằng
 * `try/finally`; worker bị kill giữa chừng là file hỏng nằm lại trong repo (00-INDEX nợ MEDIA-001 #16).
 */
export const PKG_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
export const REPO_ROOT = path.resolve(PKG_ROOT, '..', '..');
export const REGISTRY_DIR = process.env.VSITE_REGISTRY_DIR ?? path.join(PKG_ROOT, 'registry');
export const GENERATED_DIR = process.env.VSITE_GENERATED_DIR ?? path.join(PKG_ROOT, 'generated');
export const LOCK_FILE = process.env.VSITE_REGISTRY_LOCK ?? path.join(PKG_ROOT, 'registry.lock.json');

const overridden = ['VSITE_REGISTRY_DIR', 'VSITE_GENERATED_DIR', 'VSITE_REGISTRY_LOCK'].filter((k) => process.env[k]);
if (overridden.length > 0) {
  // Lộ ra ngay nếu biến của test rò sang shell/CI — gen/check đang KHÔNG chạy trên registry thật.
  console.warn(`[registry] Đường dẫn bị ghi đè bởi ${overridden.join(', ')} — chỉ dùng cho test.`);
}
