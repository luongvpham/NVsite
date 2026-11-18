import { useEffect } from 'react';
import type { DecorateNode } from './renderTree';

/**
 * Bọc mỗi node bằng `id` để link `anchor` (`#nodeId`, xem `builder-components/src/site-url.ts`) có đích.
 * `display: contents` giữ nguyên bố cục nhưng wrapper không có hộp → trình duyệt không tự cuộn tới được,
 * nên cần `useAnchorScroll`. Isomorphic: không đụng window/document (#23).
 */
export const decorateWithAnchor: DecorateNode = (node, rendered) => (
  <div id={node.id} style={{ display: 'contents' }}>
    {rendered}
  </div>
);

/* eslint-disable no-restricted-globals -- chỉ chạy trong effect sau hydrate (#23), không phải logic render */
function scrollToHash(): void {
  const raw = window.location.hash.slice(1);
  if (!raw) return;
  let id = raw;
  try {
    id = decodeURIComponent(raw);
  } catch {
    // Hash mã hoá hỏng: dùng nguyên văn.
  }
  const wrapper = document.getElementById(id);
  if (!wrapper) return;
  // Wrapper `display: contents` không cuộn được → cuộn phần tử con đầu; không có con thì không làm gì.
  wrapper.firstElementChild?.scrollIntoView();
}

/**
 * Sau hydrate và mỗi lần `hashchange`, cuộn tới nội dung của node có id = hash. window/document chỉ dùng
 * trong effect (#23).
 */
export function useAnchorScroll(): void {
  useEffect(() => {
    scrollToHash();
    window.addEventListener('hashchange', scrollToHash);
    return () => {
      window.removeEventListener('hashchange', scrollToHash);
    };
  }, []);
}
/* eslint-enable no-restricted-globals */
