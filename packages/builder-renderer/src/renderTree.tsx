import type { ComponentNode } from '@vsite/builder-components';
import { registryMap } from '@vsite/builder-components';
import { Fragment, type ReactNode } from 'react';

/**
 * Nhận Component Tree JSON, tra registryMap[type/variant], render đệ quy children.
 * Isomorphic — KHÔNG đụng window/document ở đây (#23).
 */
function renderNode(node: ComponentNode): ReactNode {
  const Component = registryMap[`${node.type}/${node.variant}`];

  if (!Component) {
    // Không nên xảy ra ở runtime — invariant #7 (registry-map thiếu component cho variant)
    // đã hard-fail lúc gen-registry. Còn lại là phòng hờ chạy tree cũ với registry đã đổi.
    console.error(`[builder-renderer] Không tìm thấy component cho '${node.type}/${node.variant}'`);
    return null;
  }

  const children = node.children?.map((child) => <Fragment key={child.id}>{renderNode(child)}</Fragment>);

  return (
    <Component key={node.id} {...node.props}>
      {children}
    </Component>
  );
}

export function RenderTree({ tree }: { tree: ComponentNode }) {
  return <>{renderNode(tree)}</>;
}

