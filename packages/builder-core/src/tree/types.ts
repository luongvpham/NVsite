/** Id gốc cố định của cây (khớp `treeEnvelopeSchema`: `id: "root"`, `type: "Page"`). */
export const ROOT_ID = 'root';

/** Loại trang quyết định bảng phép/cấm của Operations Engine (`05` §11, #33). `const object + union` (#19). */
export const PageKind = {
  Composable: 'Composable',
  System: 'System',
} as const;
export type PageKind = (typeof PageKind)[keyof typeof PageKind];

/** Node của Component Tree (`05` §6). Hình dạng khớp `treeEnvelopeSchema` của builder-components. */
export interface TreeNode {
  id: string;
  type: string;
  variant?: string;
  props?: Record<string, unknown>;
  children?: TreeNode[];
  i18n?: Record<string, Record<string, unknown> | undefined>;
}

export interface TreeEnvelope {
  schemaVersion: number;
  id: typeof ROOT_ID;
  type: 'Page';
  children: TreeNode[];
}

/** Một phần tử của đường dẫn trong `props` — khớp `PathSegment` của Inspector (`getAtPath`/`setAtPath`). */
export type PropPathSegment = string | number;
