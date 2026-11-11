import type { ComponentNode } from '@vsite/builder-components';
import {
  createResolveUrl,
  registryMap,
  RenderContextProvider,
  useRenderContext,
  type SitePageRef,
} from '@vsite/builder-components';
import { Fragment, type ReactNode } from 'react';

/** Gốc của Component Tree trong draft/published: `{ id:'root', type:'Page', children[] }` (05 §6). */
export interface PageRoot {
  schemaVersion?: number;
  id: 'root';
  type: 'Page';
  children: ComponentNode[];
}

/**
 * Móc để người gọi (editor) bọc/viền từng node. `rendered` là phần tử đã render của node
 * (kèm con). Trả về `rendered` nguyên vẹn nếu không muốn đổi gì. Renderer không biết gì về editor (#23).
 */
export type DecorateNode = (node: ComponentNode, rendered: ReactNode) => ReactNode;

/**
 * Nhận Component Tree JSON, tra registryMap[type/variant], render đệ quy children.
 * Isomorphic — KHÔNG đụng window/document ở đây (#23).
 */
function renderNode(node: ComponentNode, decorate?: DecorateNode): ReactNode {
  const Component = registryMap[`${node.type}/${node.variant}`];

  if (!Component) {
    // Không nên xảy ra ở runtime — invariant #7 (registry-map thiếu component cho variant)
    // đã hard-fail lúc gen-registry. Còn lại là phòng hờ chạy tree cũ với registry đã đổi.
    console.error(`[builder-renderer] Không tìm thấy component cho '${node.type}/${node.variant}'`);
    return null;
  }

  const children = node.children?.map((child) => (
    <Fragment key={child.id}>{renderNode(child, decorate)}</Fragment>
  ));

  const rendered = (
    <Component key={node.id} {...node.props}>
      {children}
    </Component>
  );

  return decorate ? decorate(node, rendered) : rendered;
}

function isPageRoot(tree: ComponentNode | PageRoot): tree is PageRoot {
  return (tree as { type: string }).type === 'Page';
}

export interface RenderTreeProps {
  tree: ComponentNode | PageRoot;
  decorateNode?: DecorateNode;
  /** #11/#103 — chỉ có tác dụng khi truyền `pages`; không truyền thì giữ context của Provider bên ngoài. */
  basePath?: string;
  locale?: string;
  defaultLocale?: string;
  /** Danh sách trang của shop — có thì `resolveUrl` dựng URL thật theo #103 (không có → giữ resolveUrl cũ). */
  pages?: readonly SitePageRef[];
}

export function RenderTree({ tree, decorateNode, basePath, locale, defaultLocale, pages }: RenderTreeProps) {
  const parent = useRenderContext();
  const body = isPageRoot(tree) ? (
    <>
      {tree.children.map((child) => (
        <Fragment key={child.id}>{renderNode(child, decorateNode)}</Fragment>
      ))}
    </>
  ) : (
    <>{renderNode(tree, decorateNode)}</>
  );
  if (!pages) return body;

  const next = {
    basePath: basePath ?? parent.basePath,
    locale: locale ?? parent.locale,
    defaultLocale: defaultLocale ?? parent.defaultLocale,
  };
  return (
    <RenderContextProvider value={{ ...parent, ...next, resolveUrl: createResolveUrl({ ...next, pages }) }}>
      {body}
    </RenderContextProvider>
  );
}
