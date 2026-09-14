# vsite — Thứ tự triển khai

> **STATUS:** `STABLE` · **Tasks:** `—` · **Changelog:** `—` · **Stale:** `—`
> **Cửa vào:** [`00-INDEX.md`](00-INDEX.md) §3 — bảng tiến độ có trạng thái từng bước.
>
> Bước 1, 2, 3 đã xong. ⚠️ Dòng "chốt sanitize whitelist trước bước 5" ở Bước 5 dưới đây **đã được
> xử lý ở Bước 2** (`config/sanitize-profiles.json`, Quyết định #67) — không còn là điều kiện chặn.

1.  Framework FE + BE
      + Orval pipeline, contracts/openapi/, MSW, Zod dùng chung,
        config/reserved-routes.json, module boundary (không cross-reference)

2.  Component Manifest Schema + codegen + 3–4 component mẫu   ← thuần build-time
      Chọn component KHÔNG cần data: Hero, Header, RichText, Gallery
      (đừng chọn ProductGrid — Product chưa tồn tại)

3.  Identity + Role + UserShop + Shop + PendingRegistration
      + Global Query Filter + auth policy + bộ test token scope

4.  MediaAsset + image proxy + preset whitelist

5.  Website → Theme → Page → PageDraft + Operations Engine + Zod validate
      ⚠️ chốt sanitize whitelist (05 §25 #3) TRƯỚC bước này

6.  builder-renderer (dùng chung portal + web)  → đây là lúc test flow build website

7.  NavigationConfig

8.  SitePublication + publish/rollback + cache
      ⚠️ chốt cache/TTL (05 §25 #4) TRƯỚC bước này

9.  Service (06 §9)  ← module nhỏ, test Binding Resolver end-to-end rẻ nhất
10. Product + ProductImage + Attribute/Variant + Elasticsearch + facet
11. WebsiteTemplate