import { createContext, useContext, type ReactNode } from 'react';
import type { LinkValue } from '../meta/value-shapes';

/**
 * Nguồn sự thật của render context nằm ở ĐÂY (builder-components), không phải builder-renderer,
 * dù 07 §3 vẽ context.tsx trong builder-renderer — lý do: component lá (Hero01.tsx...) sống
 * trong builder-components và phải gọi ctx.resolveImage()/ctx.resolveUrl() (#11, #53). Nếu context
 * sống trong builder-renderer thì builder-components phải import ngược builder-renderer, trong khi
 * builder-renderer đã import builder-components (registryMap) — thành phụ thuộc vòng.
 * builder-renderer re-export lại y nguyên (xem packages/builder-renderer/src/context.tsx) để giữ
 * đúng bề mặt API mà tài liệu mô tả.
 */
export interface RenderContextValue {
  /** Quyết định #11 — cùng tree render ở vsite.vn/{slug} và ở custom domain phải ra khác href. */
  basePath: string;
  resolveImage: (imageId: string, preset: string) => string;
  resolveUrl: (link: LinkValue) => string;
}

// Bước 2: stub. Bước 4 thay thân hàm, KHÔNG đổi chữ ký (07 §7.1).
const defaultResolveImage: RenderContextValue['resolveImage'] = (_imageId, preset) =>
  `/_dev/placeholder/${preset}.svg`;

// Bước 2: stub cho page/systemPage/productCategory (chưa có DB) — trả thẳng url cho external,
// #nodeId cho anchor (07 §7.3).
const defaultResolveUrl: RenderContextValue['resolveUrl'] = (link) => {
  switch (link.kind) {
    case 'external':
      return link.url;
    case 'anchor':
      return `#${link.nodeId}`;
    case 'page':
    case 'systemPage':
    case 'productCategory':
      return '#';
  }
};

const defaultContextValue: RenderContextValue = {
  basePath: '',
  resolveImage: defaultResolveImage,
  resolveUrl: defaultResolveUrl,
};

const RenderContext = createContext<RenderContextValue>(defaultContextValue);

export function RenderContextProvider({
  value,
  children,
}: {
  value?: Partial<RenderContextValue>;
  children: ReactNode;
}) {
  const merged: RenderContextValue = { ...defaultContextValue, ...value };
  return <RenderContext.Provider value={merged}>{children}</RenderContext.Provider>;
}

export function useRenderContext(): RenderContextValue {
  return useContext(RenderContext);
}
