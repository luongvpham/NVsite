import type { TreeNode } from './types';

// Cùng bảng chữ với NODE_ID_PATTERN (`^c_[A-Za-z0-9_-]{5,}$`) của envelope — 64 ký tự nên `byte & 63` không lệch xác suất.
const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-';
const ID_LENGTH = 8;
const MAX_ATTEMPTS = 20;

/** `c_` + 8 ký tự ngẫu nhiên mật mã; thử lại nếu trùng id đã có trong trang (id phải duy nhất toàn cây). */
export function generateNodeId(existing: ReadonlySet<string>): string {
  for (let attempt = 0; attempt < MAX_ATTEMPTS; attempt++) {
    const bytes = crypto.getRandomValues(new Uint8Array(ID_LENGTH));
    const id = `c_${Array.from(bytes, (byte) => ALPHABET.charAt(byte & 63)).join('')}`;
    if (!existing.has(id)) return id;
  }
  throw new Error('Không sinh được id node mới (trùng liên tục).');
}

/**
 * Node mới chỉ có `id/type/variant` — **không** điền props mặc định. Component tự có giá trị mặc định khi thiếu
 * prop, nên cây không phình và không lệch khi manifest đổi (#43 additive). `requiresProps` chưa enforce (bắt ở publish, 8b).
 */
export function createNode(type: string, variant: string, existingIds: ReadonlySet<string>): TreeNode {
  return { id: generateNodeId(existingIds), type, variant };
}
