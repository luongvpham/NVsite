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
/** Một mục trong `mediaMap` — tree chỉ lưu `imageId`, `mediaMap` tra ra storageKey thật (#71, #74). */
export interface MediaRef {
  storageKey: string;
  preset: string | null;
}

export interface RenderContextValue {
  /** Quyết định #11 — cùng tree render ở vsite.vn/{slug} và ở custom domain phải ra khác href. */
  basePath: string;
  /** Bước 4 — imageId → MediaRef, nạp trước khi render (SSR loader / dev-registry). Mặc định {}. */
  mediaMap: Readonly<Record<string, MediaRef>>;
  resolveImage: (imageId: string, preset: string) => string;
  resolveUrl: (link: LinkValue) => string;
}

const PLACEHOLDER_PRESET_PATH = (preset: string) => `/_dev/placeholder/${preset}.svg`;

/**
 * Duy nhất một nơi biết cách nối `storageKey` thành URL ảnh thật (brief MEDIA-001 §"Ảnh không đi
 * qua API" — "Chỉ resolveImage() được nối chuỗi /media/"). `resolveImage` gọi hàm này bên trong.
 * Export riêng để nơi cần hiện ảnh KHÔNG qua tree/mediaMap (vd. Media Library picker ở apps/portal
 * — ảnh thư viện chưa có `imageId` trong tree) vẫn dùng đúng một quy ước URL, không tự nối chuỗi
 * `/media/` lần thứ hai. Thuần, isomorphic — không đụng window/document (#23).
 */
export function mediaUrl(storageKey: string): string {
  return `/media/${storageKey}`;
}

/**
 * Bước 4 — thân thật. Isomorphic, KHÔNG đụng window/document (#23).
 * - imageId không có trong mediaMap → placeholder theo preset yêu cầu (dev-only, giữ nguyên
 *   `devPlaceholderImagePlugin` ở apps/portal làm SVG đúng kích thước).
 * - imageId có, nhưng preset đã "bake" (`mediaRef.preset`) khác preset yêu cầu (kể cả bản Library
 *   gốc, `preset: null`, luôn coi là lệch) → console.warn đúng 1 lần, vẫn trả URL thật (#74).
 * - Ảnh không đi qua API — chỉ nối chuỗi `/media/{storageKey}` (brief MEDIA-001 §"Ảnh không đi qua API").
 */
export function createResolveImage(
  mediaMap: Readonly<Record<string, MediaRef>>,
): RenderContextValue['resolveImage'] {
  return (imageId, preset) => {
    const mediaRef = mediaMap[imageId];
    if (!mediaRef) {
      return PLACEHOLDER_PRESET_PATH(preset);
    }
    if (mediaRef.preset !== preset) {
      console.warn(
        `[resolveImage] preset lệch cho imageId="${imageId}": mediaRef.preset="${mediaRef.preset}", yêu cầu="${preset}". Vẫn trả ảnh gốc.`,
      );
    }
    return mediaUrl(mediaRef.storageKey);
  };
}

const EMPTY_MEDIA_MAP: Readonly<Record<string, MediaRef>> = {};
const defaultResolveImage: RenderContextValue['resolveImage'] = createResolveImage(EMPTY_MEDIA_MAP);

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
  mediaMap: EMPTY_MEDIA_MAP,
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
  const mediaMap = value?.mediaMap ?? defaultContextValue.mediaMap;
  // resolveImage mặc định phải đọc đúng mediaMap đã merge — nếu caller chỉ truyền `mediaMap` mà
  // không tự override `resolveImage`, thân thật vẫn phải tra đúng map đó, không phải EMPTY_MEDIA_MAP.
  const resolveImage = value?.resolveImage ?? createResolveImage(mediaMap);
  const merged: RenderContextValue = { ...defaultContextValue, ...value, mediaMap, resolveImage };
  return <RenderContext.Provider value={merged}>{children}</RenderContext.Provider>;
}

export function useRenderContext(): RenderContextValue {
  return useContext(RenderContext);
}
