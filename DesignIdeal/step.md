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