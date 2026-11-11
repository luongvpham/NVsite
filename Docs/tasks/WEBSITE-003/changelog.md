# WEBSITE-003 — changelog (lệch so với plan)

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
