import { z } from 'zod';
import { NODE_ID_PATTERN } from '@vsite/builder-components';
import { ROOT_ID } from '../tree/types';
import { fail, type OperationErrorCode, type OperationResult } from './result';

/**
 * 5 operation (#14) — mọi thay đổi tree đi qua đây, bất kể từ kéo-thả, Inspector hay AI Chat (`05` §11).
 * Tên khớp `generated/ai-tool-schema.json` (`add_component`/`update_component_props`/`remove_component`/
 * `move_component`) để AI tái dùng sau này; `update` mang MỘT đường dẫn trong `props` giống `onChange(path, value)` của Inspector.
 *
 * Thêm operation = cập nhật đủ 6 chỗ (xem `packages/builder-core/CLAUDE.md`).
 */
const nodeId = z.string().regex(new RegExp(NODE_ID_PATTERN));
const parentId = z.union([z.literal(ROOT_ID), nodeId]);

/** Node mới KHÔNG mang `children`: thêm cả nhánh = nhiều op `add` (cha trước con). `i18n` nhận vào để báo `I18N_NOT_ENABLED`. */
const newNodeSchema = z
  .object({
    id: nodeId,
    type: z.string().min(1),
    variant: z.string().min(1).optional(),
    props: z.record(z.string(), z.unknown()).optional(),
    i18n: z.unknown().optional(),
  })
  .strict();

const pathSegment = z.union([z.string().min(1), z.number().int().min(0)]);

export const operationSchema = z.discriminatedUnion('type', [
  z.object({ type: z.literal('add'), parentId, index: z.number().int().min(0), node: newNodeSchema }).strict(),
  z.object({ type: z.literal('remove'), nodeId }).strict(),
  z
    .object({ type: z.literal('move'), nodeId, toParentId: parentId, toIndex: z.number().int().min(0) })
    .strict(),
  // `value` bỏ trống/undefined = xoá prop (hoặc gỡ phần tử mảng) tại `path`.
  z.object({ type: z.literal('update'), nodeId, path: z.array(pathSegment).min(1), value: z.unknown() }).strict(),
  z.object({ type: z.literal('changeTheme'), tokens: z.unknown() }).strict(),
]);

export type Operation = z.infer<typeof operationSchema>;
export type AddOperation = Extract<Operation, { type: 'add' }>;
export type RemoveOperation = Extract<Operation, { type: 'remove' }>;
export type MoveOperation = Extract<Operation, { type: 'move' }>;
export type UpdateOperation = Extract<Operation, { type: 'update' }>;
export type ChangeThemeOperation = Extract<Operation, { type: 'changeTheme' }>;

export type ParsedOperation = { ok: true; op: Operation } | { ok: false; code: OperationErrorCode; message: string };

/** Kiểm hình dạng op (không kiểm luật nghiệp vụ). Không ném lỗi — op từ AI/UI có thể sai. */
export function parseOperation(input: unknown): ParsedOperation {
  const parsed = operationSchema.safeParse(input);
  if (parsed.success) return { ok: true, op: parsed.data };
  const first = parsed.error.issues[0];
  const where = first ? `${first.path.join('.') || 'op'}: ${first.message}` : undefined;
  const failure: OperationResult & { ok: false } = fail('INVALID_OPERATION', where);
  return { ok: false, code: 'INVALID_OPERATION', message: failure.message };
}
