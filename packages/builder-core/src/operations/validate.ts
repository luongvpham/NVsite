import { opRules, themeTokensSchema } from '@vsite/builder-components';
import { MAX_DEPTH, MAX_NODES, checkThemeLimit } from '../tree/limits';
import {
  collectIds,
  countNodes,
  findNode,
  isSelfOrDescendant,
  subtreeHeight,
  type FoundNode,
} from '../tree/query';
import { PageKind, ROOT_ID, type TreeEnvelope, type TreeNode } from '../tree/types';
import { checkProps, isKnownPropPath, typeRuleOf } from './props';
import { setIn } from './paths';
import { OK, OperationErrorCode, fail, type OperationResult } from './result';
import type { AddOperation, MoveOperation, Operation, UpdateOperation } from './types';

export interface ValidationContext {
  tree: TreeEnvelope;
  pageKind: PageKind;
}

/** Luật đặt chỗ của một type (`generated/op-rules.ts` — dữ liệu cho Operations Engine, `step.md` 5b). */
interface PlacementRule {
  acceptsChildren: boolean;
  allowedChildTypes: readonly string[] | null;
  allowedInPageKinds: readonly string[];
  maxPerPage: number | null;
  editableInSystemPageProps: readonly string[];
}
const placement = opRules as unknown as Record<string, PlacementRule>;
const placementOf = (type: string): PlacementRule | undefined => (Object.hasOwn(placement, type) ? placement[type] : undefined);

/**
 * Kiểm một op theo `05` §11, theo đúng thứ tự:
 *   [1] PageKind cho phép loại op này · [2] component.allowedInPageKinds · [3] đích tồn tại · [4] props theo
 *   Zod/manifest · [5] binding.source trong whitelist (CẤM `Review`) · rồi giới hạn độ sâu/số node.
 * Hàm thuần: không ném lỗi, không đổi `ctx.tree`.
 */
export function validateOperation(op: Operation, ctx: ValidationContext): OperationResult {
  switch (op.type) {
    case 'add':
      return validateAdd(op, ctx);
    case 'remove':
      return validateRemove(op.nodeId, ctx);
    case 'move':
      return validateMove(op, ctx);
    case 'update':
      return validateUpdate(op, ctx);
    case 'changeTheme':
      return validateThemeTokens(op.tokens);
  }
}

/** `changeTheme` không phụ thuộc cây nên Store gọi được cả khi chưa nạp draft. Kích thước kiểm trước để khỏi duyệt khối khổng lồ. */
export function validateThemeTokens(tokens: unknown): OperationResult {
  const size = checkThemeLimit(tokens);
  if (!size.ok) return fail(OperationErrorCode.ThemeBodyTooLarge);
  const parsed = themeTokensSchema.safeParse(tokens);
  if (!parsed.success) {
    const first = parsed.error.issues[0];
    return fail(OperationErrorCode.ThemeTokenInvalid, first ? first.path.join('.') : undefined);
  }
  return OK;
}

/** Cha đích: gốc hoặc một node; kèm độ sâu của cha (gốc = 0). */
function resolveParent(
  tree: TreeEnvelope,
  parentId: string,
): { children: readonly TreeNode[]; node: TreeNode | null; depth: number } | undefined {
  if (parentId === ROOT_ID) return { children: tree.children, node: null, depth: 0 };
  const found = findNode(tree, parentId);
  return found ? { children: found.node.children ?? [], node: found.node, depth: found.depth } : undefined;
}

/** Cha có nhận một node `childType` làm con không (acceptsChildren / allowedChildTypes). Gốc nhận mọi loại. */
function childAllowed(parentNode: TreeNode | null, childType: string): OperationResult {
  if (!parentNode) return OK;
  const rule = placementOf(parentNode.type);
  if (!rule?.acceptsChildren) return fail(OperationErrorCode.NodeChildrenNotAccepted);
  if (rule.allowedChildTypes && !rule.allowedChildTypes.includes(childType)) {
    return fail(OperationErrorCode.NodeChildTypeNotAllowed);
  }
  return OK;
}

function countOfType(tree: TreeEnvelope, type: string): number {
  let count = 0;
  const stack = [...tree.children];
  while (stack.length > 0) {
    const node = stack.pop();
    if (!node) break;
    if (node.type === type) count += 1;
    stack.push(...(node.children ?? []));
  }
  return count;
}

function validateAdd(op: AddOperation, { tree, pageKind }: ValidationContext): OperationResult {
  // [1] trang System có cấu trúc khoá: không thêm/xoá/đổi chỗ node.
  if (pageKind === PageKind.System) return fail(OperationErrorCode.SystemPageStructureChanged);

  // [3] đích tồn tại + vị trí hợp lệ.
  const parent = resolveParent(tree, op.parentId);
  if (!parent) return fail(OperationErrorCode.TargetNotFound, op.parentId);
  if (op.index > parent.children.length) return fail(OperationErrorCode.IndexOutOfRange);

  const { node } = op;
  if (collectIds(tree).has(node.id)) return fail(OperationErrorCode.NodeIdDuplicate, node.id);

  const typeRule = typeRuleOf(node.type);
  const placementRule = placementOf(node.type);
  if (!typeRule || !placementRule) return fail(OperationErrorCode.NodeTypeUnknown, node.type);
  if (node.variant === undefined || !typeRule.variants.includes(node.variant)) {
    return fail(OperationErrorCode.NodeVariantUnknown, node.variant ?? 'thiếu variant');
  }
  if (node.i18n !== undefined) return fail(OperationErrorCode.I18nNotEnabled);

  // [2] component cho phép ở loại trang này.
  if (!placementRule.allowedInPageKinds.includes(pageKind)) return fail(OperationErrorCode.NodeNotAllowedInPageKind);

  const parentOk = childAllowed(parent.node, node.type);
  if (!parentOk.ok) return parentOk;

  const { maxPerPage } = placementRule;
  if (maxPerPage !== null && countOfType(tree, node.type) + 1 > maxPerPage) {
    return fail(OperationErrorCode.NodeMaxPerPageExceeded, node.type);
  }

  // [4][5] props: khoá lạ, shape, binding (cấm Review), link.
  if (node.props) {
    const propError = checkProps(node.type, node.props);
    if (propError) return fail(propError as OperationErrorCode);
  }

  if (parent.depth + 1 > MAX_DEPTH) return fail(OperationErrorCode.TreeTooDeep);
  if (countNodes(tree) + 1 > MAX_NODES) return fail(OperationErrorCode.TreeTooManyNodes);
  return OK;
}

function validateRemove(nodeId: string, { tree, pageKind }: ValidationContext): OperationResult {
  if (pageKind === PageKind.System) return fail(OperationErrorCode.SystemPageStructureChanged);
  if (!findNode(tree, nodeId)) return fail(OperationErrorCode.TargetNotFound, nodeId);
  return OK;
}

function validateMove(op: MoveOperation, { tree, pageKind }: ValidationContext): OperationResult {
  if (pageKind === PageKind.System) return fail(OperationErrorCode.SystemPageStructureChanged);

  const found = findNode(tree, op.nodeId);
  if (!found) return fail(OperationErrorCode.TargetNotFound, op.nodeId);
  const target = resolveParent(tree, op.toParentId);
  if (!target) return fail(OperationErrorCode.TargetNotFound, op.toParentId);
  if (op.toParentId !== ROOT_ID && isSelfOrDescendant(tree, op.nodeId, op.toParentId)) {
    return fail(OperationErrorCode.MoveIntoDescendant);
  }

  const parentOk = childAllowed(target.node, found.node.type);
  if (!parentOk.ok) return parentOk;

  // `toIndex` tính trong danh sách con của đích SAU khi gỡ node khỏi chỗ cũ.
  const sameParent = found.parentId === op.toParentId;
  const siblingsAfterRemoval = sameParent ? target.children.length - 1 : target.children.length;
  if (op.toIndex > siblingsAfterRemoval) return fail(OperationErrorCode.IndexOutOfRange);

  if (target.depth + subtreeHeight(found.node) > MAX_DEPTH) return fail(OperationErrorCode.TreeTooDeep);
  return OK;
}

function validateUpdate(op: UpdateOperation, { tree, pageKind }: ValidationContext): OperationResult {
  const found: FoundNode | undefined = findNode(tree, op.nodeId);
  if (!found) return fail(OperationErrorCode.TargetNotFound, op.nodeId);
  const { node } = found;
  const [top] = op.path;
  if (typeof top !== 'string') return fail(OperationErrorCode.PropUnknown);

  // [1] trang System: chỉ prop thuộc `editableInSystemPageProps`.
  if (pageKind === PageKind.System && !placementOf(node.type)?.editableInSystemPageProps.includes(top)) {
    return fail(OperationErrorCode.SystemPagePropNotEditable, top);
  }

  if (!typeRuleOf(node.type)) return fail(OperationErrorCode.NodeTypeUnknown, node.type);
  if (!isKnownPropPath(node.type, op.path)) return fail(OperationErrorCode.PropUnknown, op.path.join('.'));

  // [4][5] chỉ kiểm prop cấp 1 đang bị đụng tới — prop khác đã sai sẵn không chặn việc sửa này.
  const nextProps = setIn(node.props ?? {}, op.path, op.value) as Record<string, unknown>;
  const propError = checkProps(node.type, nextProps, top);
  return propError ? fail(propError as OperationErrorCode, top) : OK;
}
