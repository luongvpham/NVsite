import { countNodes, maxDepth } from './query';
import type { TreeEnvelope } from './types';

/**
 * Giới hạn cứng của BE (`PageTreeLimits`, WEBSITE-001 G6): FE tự chặn trước khi gửi, đừng đợi 413/422.
 * Đổi số ở BE (`appsettings`) thì phải đổi ở đây — brief ghi cố định 1 MB / sâu 20 / 2000 node / theme 64 KB.
 */
export const MAX_TREE_BYTES = 1_048_576;
/** Con trực tiếp của gốc = tầng 1. */
export const MAX_DEPTH = 20;
export const MAX_NODES = 2000;
export const THEME_MAX_BYTES = 65_536;

export interface TreeMeasure {
  /** Byte UTF-8 của JSON gọn — cùng cách BE đo (`Encoding.UTF8.GetByteCount`), không phải số ký tự. */
  bytes: number;
  depth: number;
  nodes: number;
}

const encoder = new TextEncoder();
export const byteLength = (value: unknown): number => encoder.encode(JSON.stringify(value)).length;

export function measureTree(tree: TreeEnvelope): TreeMeasure {
  return { bytes: byteLength(tree), depth: maxDepth(tree), nodes: countNodes(tree) };
}

export type LimitCheck =
  | { ok: true }
  | { ok: false; code: 'TREE_BODY_TOO_LARGE' | 'TREE_TOO_DEEP' | 'TREE_TOO_MANY_NODES' | 'THEME_BODY_TOO_LARGE'; message: string };

/** Mã trả về trùng tên mã BE để một bảng thông báo lỗi dùng cho cả hai nguồn. */
export function checkTreeLimits(tree: TreeEnvelope): LimitCheck {
  const { bytes, depth, nodes } = measureTree(tree);
  if (depth > MAX_DEPTH) return { ok: false, code: 'TREE_TOO_DEEP', message: `Trang lồng quá ${String(MAX_DEPTH)} tầng.` };
  if (nodes > MAX_NODES) return { ok: false, code: 'TREE_TOO_MANY_NODES', message: `Trang có quá ${String(MAX_NODES)} khối.` };
  if (bytes > MAX_TREE_BYTES) return { ok: false, code: 'TREE_BODY_TOO_LARGE', message: 'Trang quá lớn để lưu.' };
  return { ok: true };
}

export function checkThemeLimit(tokens: unknown): LimitCheck {
  if (byteLength(tokens) > THEME_MAX_BYTES) {
    return { ok: false, code: 'THEME_BODY_TOO_LARGE', message: 'Cấu hình giao diện quá lớn để lưu.' };
  }
  return { ok: true };
}
