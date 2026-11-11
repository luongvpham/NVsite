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
