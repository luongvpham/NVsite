# WEBSITE-003 — changelog (lệch so với plan)

> **Đã đóng 2026-11-12** — nghiệm thu tay (trình duyệt, BE thật) + Gate 2 do người duyệt. Review: [`review.md`](review.md).

## T1 — createResolveUrl / matchSiteUrl / API RenderTree
- `RenderTree` bọc context mới **gộp với context của Provider bên ngoài** (`{...parent, ...}`), không thay thế — để `mediaMap`/`resolveImage` của canvas không mất. Plan không nói rõ.
- `locale`/`defaultLocale` mặc định `'vi'`/`'vi'` trong context (không prefix locale khi không truyền).
- `matchSiteUrl`: các route System dùng chung (`san-pham` ∈ ProductListing và ProductDetail) tra ngược về loại **khai báo đầu tiên** trong `config/system-pages.json` (ProductListing). Hệ quả: link `systemPage: ProductDetail` chưa có trang được báo là `ProductListing` thiếu. Chấp nhận — cả hai đều "chưa có trang"; xem lại khi Phase 2 bật seed.
- Link tới trang `NotFound` (`slug null`) và `systemPage: NotFound` → `#` (khớp #103: NotFound không có URL); `matchSiteUrl('#')` → `deleted`.
- #11 ghi "vsite.vn/{slug}" nhưng Shop Site thật là `{slug}.vsite.vn` / domain riêng → `basePath = ''` trên site thật.

## T2 — SiteLayout
- `shop.logoUrl` kiểu `string | null` (khớp `ShopDto`); `<img alt="">` vì tên shop đã nằm cạnh logo (tránh đọc đôi với screen reader).
- Thêm `<main>` bọc `children` (plan chỉ nêu Header/Footer).

## T3 — snapshot SSR↔CSR + lint
- Tách hai file: `ssr-hydrate.node.test.tsx` (Node, `renderToString` + snapshot HTML) và `ssr-hydrate.test.tsx` (jsdom, `hydrateRoot` + bắt `console.error`/`onRecoverableError`). Không gộp được một file vì mỗi file một môi trường vitest.
- Fixture liệt kê tay trong `tests/helpers/site-app.tsx` (5 file `valid-*` có `tree`; `valid-system-edit-content` là cặp stored/incoming nên bỏ): renderer không có `@types/node` để glob, và không muốn sửa package.json/lockfile. Thêm `valid-*` mới ở builder-components → thêm một dòng.
- So HTML sau hydrate qua DOM đã parse và bỏ `<link rel=preload>`: React 19 nhấc preload của `<img>` lên `<head>`, và `&#x27;` ↔ `'` chỉ là khác cách serialize, không phải lệch.
- Kiểm đột biến bằng `Math.random()` trong Footer thay cho `new Date()` (cùng lớp lỗi, nhưng `Date` có thể trùng ms nên không ổn định) → 5/5 hydrate test FAIL; đã hoàn nguyên.
- Lint: `packages/**` cấm import `@vsite/portal|web`, `apps/**`, `**/apps/**`. Gộp vào block `no-restricted-imports` sẵn có của renderer/components (rule flat-config ghi đè nhau nên không tách block riêng cho hai package này); các package còn lại một block mới. Xác nhận bằng file tạm import apps → lint FAIL ở cả renderer và theme-engine, rồi xoá.

## T4 — Preview trong editor Portal
- Test viết ở mức component (`site-preview.test.tsx`, store thật + MSW) cộng 2 test tích hợp ở `routes/-editor-preview.test.tsx` (nút mở/đóng, phím Delete bị tắt khi mở).
- `useGetShop(shopId)` — dùng `GET /shops/{id}` có sẵn; không endpoint mới.
- Link `anchor`: mỗi node được bọc `<div id={node.id} style="display:contents">` (qua `decorateNode`) để `#nodeId` cuộn tới được. Wrapper không ảnh hưởng bố cục nhưng có thêm một phần tử DOM so với site thật.
- "← Trang trước" lưu cả `notice`; quay lại thì dải báo của màn trước được khôi phục. Trang `NotFound` chưa tồn tại (`pageId = null`) → thân trang trống + dải báo.
- Escape đóng preview bằng listener `document` của lớp phủ; phím tắt soạn trang (`useEditorShortcuts`) nhận cờ `enabled=false` khi preview mở.
- Trang đích ≠ trang đang sửa dùng `staleTime: 0` + `gcTime: 0` (sửa sau review I1: ban đầu để mặc định vì "chỉ đọc" — sai, vì chung query key với editor và TanStack lấy gcTime lớn nhất nên cache draft cũ sống 5 phút, mở lại editor ra cây/version cũ).

## Sau review (C1, C2, I1, I2)
- C1/C2: banner `05`/`07` thêm WEBSITE-003 (Tasks, Changelog, Stale) + `pnpm gen:doc-index`.
- I1: `gcTime: 0` cho draft trang khác trong preview (test cache). I2: anchor cuộn tới `firstElementChild` của wrapper `display: contents` (test).

## Chưa làm xong / theo dõi
1. ~~Nghiệm thu tay trên trình duyệt với BE thật~~ ✅ người đã chạy 2026-11-12.
2. Route `san-pham` dùng chung ProductListing/ProductDetail (xem T1) — xem lại khi Phase 2 bật seed.
3. Anchor `#nodeId` trên site thật (8b): renderer không render `id` cho node; preview tự bọc wrapper. 8b phải quyết định.
4. 8b: năm © phải do server tính rồi truyền xuống client (tránh lệch hydrate).
5. Minor hoãn của review: M1 (lịch sử lặp khi bấm link về trang đang xem), M2 (`setView` trong updater `setHistory`), M3 (`auxclick`/middle-click), M4 (focus trap lớp phủ), M5 (lọc scheme trước `window.open`), M6 (unmount root hydrate trong test), M7 (fixture liệt kê tay), M8 (thiếu test themeDraft / systemPage có trang nhưng tắt), M9 (comment `context.tsx` còn "vsite.vn/{slug}"), M10 (message lint dùng chung cho apps/web), M11 (RenderTree bỏ qua basePath khi không có `pages`).
