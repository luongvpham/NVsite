import type { TreeValidationError, TreeValidationParams } from '@vsite/shared';
import { resolvePointer, type PropPathSegment, type TreeEnvelope } from '@vsite/builder-core';

export interface NodeErrors {
  /** Đường dẫn trong `props` của node bị lỗi — mỗi phần tử là một ô/nhóm ô của Inspector cần tô đỏ. Rỗng khi lỗi ở chính node. */
  propPaths: PropPathSegment[][];
  errors: TreeValidationError[];
}

export interface MappedTreeErrors {
  /** nodeId → lỗi gắn được vào node đó (canvas viền đỏ, Inspector ô đỏ). */
  byNode: Map<string, NodeErrors>;
  /** Lỗi KHÔNG gắn được vào node: path gốc/vỏ (`''`, `/schemaVersion`…) hoặc node không còn trong cây (R5). Hiện ở banner. */
  banner: TreeValidationError[];
  /** BE chỉ trả tối đa 50 lỗi; còn nữa → "còn lỗi khác". */
  truncated: boolean;
  total: number;
}

/**
 * Đổi `params.errors` của 422 (JSON Pointer vào cây ĐÃ GỬI) thành lỗi theo node của cây HIỆN TẠI.
 *
 * Người dùng có thể đã sửa tiếp sau lần gửi nên pointer có thể trỏ node đã xoá/di chuyển (R5): không tìm thấy thì
 * KHÔNG tô đỏ, nhưng vẫn giữ lỗi ở banner để người dùng biết còn lỗi. Không bao giờ ném.
 */
export function mapTreeErrors(tree: TreeEnvelope, params: TreeValidationParams): MappedTreeErrors {
  const byNode = new Map<string, NodeErrors>();
  const banner: TreeValidationError[] = [];

  for (const error of params.errors) {
    const resolved = resolvePointer(tree, error.path);
    if (!resolved?.nodeId) {
      banner.push(error);
      continue;
    }
    const entry = byNode.get(resolved.nodeId) ?? { propPaths: [], errors: [] };
    entry.errors.push(error);
    if (resolved.propPath.length > 0) entry.propPaths.push(resolved.propPath);
    byNode.set(resolved.nodeId, entry);
  }

  return { byNode, banner, truncated: params.truncated, total: params.errors.length };
}
