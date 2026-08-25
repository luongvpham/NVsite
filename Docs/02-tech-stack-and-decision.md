# vsite — Tech Stack & Quyết Định Đã Chốt

> Tài liệu này ghi lại **những gì đã thống nhất**. Mọi thay đổi so với tài liệu này cần được ghi nhận lại tại đây.
>
> **Tài liệu liên quan:** `01-project-ideal.md` (ý tưởng & tính năng) · `03-identity-entity-design.md` (thiết kế entity Identity/Shop chi tiết — chuẩn cho Quyết định #29/#30/#31) · `04-listing-and-review-design.md` (Listing/Review/Lead — chuẩn cho Quyết định #35/#37/#38)
>
> **Lần rà soát nhất quán gần nhất:** đã đối chiếu chéo cả 4 tài liệu; các mâu thuẫn còn tồn (tên app cũ, quyền ghi `Review` của token shop, hai danh sách reserved route, đánh số Phase) đã được đóng — xem Quyết định #39.

---

## 1. Backend

### 1.1 Kiến trúc

- **.NET 9**
- **Modular Monolith** — không dùng microservices ở giai đoạn này
- **Clean Architecture + DDD Lite**
- Ranh giới module rõ ràng để có thể tách thành service riêng về sau nếu cần

**Lý do chốt:** Team nhỏ, chưa có traffic lớn. Microservices sớm sẽ tạo overhead vận hành không đáng có, trong khi Modular Monolith vẫn giữ được kỷ luật ranh giới.

### 1.2 Danh sách module

> **Quy ước đánh số Phase:** "MVP" = **Phase 1** trong lộ trình ở `01-project-ideal.md` mục 8. Không dùng "MVP" như một mốc riêng nằm ngoài lộ trình — xem Quyết định #39.

| Module | Phase |
|---|---|
| Identity | Phase 1 (MVP) |
| Shop | Phase 1 (MVP) |
| **Category** (taxonomy toàn cục — Quyết định #35) | Phase 1 (MVP) |
| **Listing** (tin đăng marketplace — Quyết định #38) | Phase 1 (MVP) |
| **Review** (đánh giá — chuyển từ Phase 2 lên) | Phase 1 (MVP) |
| **Lead** (lượt liên hệ — nguồn của "thống kê cơ bản", `04` §7) | Phase 1 (MVP) |
| Search | Phase 1 (MVP) |
| Service | Phase 2 |
| Website (builder + renderer) | Phase 2 |
| Product | Phase 2 |
| Customer | Phase 2 |
| Booking | Phase 3 |
| Notification | Phase 3 |
| Analytics (BI nâng cao, ngoài `Lead`) | Phase 4 |
| Payment | Phase 4 |

**Quyết định:** Payment / Analytics / Notification giai đoạn đầu **dùng dịch vụ ngoài**, không tự xây.

**Vì sao `Review` lên MVP:** vsite không xử lý giao dịch (mục 2 của `01-project-ideal.md`), nên đánh giá là **tài sản tin cậy duy nhất** phân biệt vsite với việc lướt Google Maps hay Facebook group. Không có đánh giá thì Phase 1 không kiểm chứng được điều gì.

**Vì sao `Service`/`Website` xuống Phase 2:** lộ trình ở `01-project-ideal.md` mục 8 chốt Phase 1 là **danh bạ dịch vụ** (Listing + Review), Website Builder mới là Phase 2. Để hai module này ở MVP là mâu thuẫn trực tiếp với lộ trình — và cũng phá đúng mục tiêu "kiểm chứng nhu cầu trước khi xây phần tốn công nhất". `Listing` **không** phụ thuộc `Service` (Quyết định #38: không auto-map), nên cắt được sạch.

**Thứ tự phụ thuộc bắt buộc:** `Identity` → `Shop` → `Category` → `Listing` → `Search` / `Review` / `Lead`. `Category` phải xong trước `Listing` vì Listing, Search, Review và Menu đều neo vào nó; `Identity`/`Shop` trước vì `Listing.ShopId` NOT NULL và `Review.UserId` NOT NULL.

### 1.3 Tech stack

| Lớp | Công nghệ | Ghi chú |
|---|---|---|
| Framework | ASP.NET Core Web API, EF Core | |
| Database | **PostgreSQL + PostGIS** | Nguồn sự thật (transactional). PostGIS lưu toạ độ, không dùng để query geo-search trực tiếp — xem Quyết định #26 |
| Search index | **Elasticsearch** | Full-text + geo-search, dùng từ MVP (Quyết định #26). Read index phái sinh, sync qua Hangfire từ PostgreSQL |
| Cache | Redis | Tenant resolution, search result, Component Registry |
| Background job | Hangfire | Publish website, sitemap, gửi mail, dọn token hết hạn |
| Storage | S3 / MinIO | Ảnh shop, ảnh trong component |
| Realtime | SignalR | AI streaming, thông báo booking |
| Reverse proxy | **Caddy** | On-Demand TLS cho custom domain |
| Logging | Serilog | |
| Observability | OpenTelemetry + Prometheus + Grafana | |

---

## 2. Frontend

### 2.1 Base stack (dùng chung cho cả 2 app)

```
React 19
TypeScript strict
TanStack Query v5      — server state
TanStack Router v1     — routing type-safe
TanStack Table v8      — bảng dữ liệu
Zustand                — client / UI state
shadcn/ui              — component UI
Tailwind CSS           — styling
Axios                  — HTTP client + interceptor
Zod                    — schema validation
React Hook Form        — form
react-i18next          — đa ngôn ngữ
MSW v2                 — mock API
```

> **Đã loại bỏ `oidc-client-ts`** — xem Quyết định #3.
> **Vite** vẫn là build tool nền cho `apps/portal` (CSR thuần). Cho `apps/web`, build/dev server do **TanStack Start** đảm nhiệm (chạy trên Vite bên dưới) — xem Quyết định #23.

### 2.2 Bổ sung riêng cho `apps/portal` (route `/website` — website builder)

```
dnd-kit    — kéo thả component
Lexical    — rich text editing
zundo      — undo/redo middleware cho Zustand
immer      — immutable update Component Tree
```

### 2.3 Monorepo — Turborepo

**Chốt lại (xem Quyết định #22): 2 app, không phải 3.** Tên app cũ (`customer-web`, `shop-admin`, `website-builder`) được thay bằng cách chia theo *rendering strategy*, không theo *nhóm chức năng* nữa:

| App | Gồm | Rendering | Domain |
|---|---|---|---|
| `apps/web` | Main (trang chung vsite.vn: tìm kiếm, bản đồ, landing SEO) + **Shop Profile** (`vsite.vn/shop/{slug}` — vsite render, chứa đánh giá) + Shop Site (website công khai của shop — output của `builder-renderer`) | **SSR** (TanStack Start) | `vsite.vn`, `{slug}.vsite.vn`, custom domain |
| `apps/portal` | Quản trị shop (**tin đăng, lead, đánh giá, hồ sơ shop** ở Phase 1; thêm dịch vụ/hàng hoá từ Phase 2 — nội dung của `shop-admin` cũ) + Website Builder (kéo-thả + AI Chat, route `/website`, Phase 2) | **CSR** thuần | `admin.vsite.vn` |

| Packages (dùng chung) | Dùng bởi |
|---|---|
| `ui` — design system | `apps/web`, `apps/portal` |
| `shared` — utils, hooks, types | `apps/web`, `apps/portal` |
| `api-sdk` — generated API client | `apps/web`, `apps/portal` |
| `builder-renderer` — JSON → React | `apps/web` (render Shop công khai), `apps/portal` (preview lúc edit) |
| `builder-core` — state + Operations Engine | **chỉ** `apps/portal` (chỉ cần khi edit) |
| `builder-components` — thư viện component | `apps/web`, `apps/portal` |
| `theme-engine` — token màu/font/spacing | `apps/web`, `apps/portal` |
| `ai-agent` — prompt, tool schema, validator | **chỉ** `apps/portal` |

**Ràng buộc dependency giữ nguyên và giờ có ý nghĩa cụ thể:** `builder-renderer` KHÔNG import `builder-core` — vì `apps/web` (Shop) chỉ render, không có Operations Engine, không cần biết gì về editor.

⚠️ **Shop Profile ≠ Shop Site.** Hai thứ khác nhau hoàn toàn, đều nằm trong `apps/web`:

| | Shop Profile | Shop Site |
|---|---|---|
| URL | `vsite.vn/shop/{slug}` | `spa-abc.com` · `spa-abc.vsite.vn` · `vsite.vn/{slug}` |
| Render bởi | vsite (mẫu thống nhất, code thường) | `builder-renderer` (Component Tree) |
| Ai kiểm soát nội dung | vsite | shop |
| Hiển thị `Review` | ✅ | ❌ **tuyệt đối không** |
| Shop `ExternalOnly` có? | ✅ (bản đơn giản) | ❌ |

Shop Profile **không** dùng `builder-renderer`. Đây là chỗ AI agent dễ nhầm — thấy "trang của shop" là với tay sang builder.

---

## 3. Các Quyết Định Đã Chốt

### Quyết định #1 — Modular Monolith, không microservices

**Chốt:** Modular Monolith với ranh giới module nghiêm ngặt.

**Ràng buộc:** Module KHÔNG được reference project của module khác. Giao tiếp cross-module qua Integration Event hoặc Public Contract interface.

---

### Quyết định #2 — PostGIS trước, Elasticsearch sau

> **⚠️ ĐÃ SUPERSEDE bởi Quyết định #26** — dùng Elasticsearch ngay từ MVP, không đợi ngưỡng. Giữ lại mục này để lưu lại lý do ban đầu.

**Chốt (cũ):** MVP dùng PostgreSQL + PostGIS cho geo-search, PostgreSQL FTS cho tìm kiếm văn bản.

**Ngưỡng migrate sang Elasticsearch (cần chốt con số cụ thể trước khi launch):**
- Số shop vượt ngưỡng (gợi ý: > 100k), **hoặc**
- p95 latency của search > 500ms

**Lý do ghi ngưỡng:** Tránh tình trạng "để đó rồi không bao giờ làm".

---

### Quyết định #3 — Auth: KHÔNG dùng OIDC / OpenIddict ⭐

**Chốt:** Không triển khai Identity Provider. Dùng ASP.NET Core Identity + JWT tự cấp.

**Lý do:** vsite không cần làm Identity Provider cho bên thứ ba. Mọi client đều là first-party (`apps/web`, `apps/portal`, và website shop do chính `apps/web` render). Redirect flow + PKCE + discovery endpoint của OIDC là chi phí không đổi lại được lợi ích gì trong mô hình này.

**Kiến trúc thay thế:**

| Thành phần | Giải pháp |
|---|---|
| User store, hash password, lockout, xác thực email | ASP.NET Core Identity |
| Cấp token | Endpoint `/auth/login` → JWT, TTL ngắn **10–15 phút** |
| Refresh | Refresh token **rotation**, lưu DB, revoke được |
| Google / Facebook | `.AddGoogle()` / `.AddFacebook()` — vsite là OAuth **client**, không phải server |
| Lưu token ở FE | Access token trong **memory**, không dùng localStorage |
| FE | Axios interceptor tự viết (~100 dòng), không cần thư viện |

**Điều kiện quay lại OIDC/OpenIddict** — chỉ khi xuất hiện một trong ba:
1. Cần SSO thật xuyên các domain
2. Mở API cho developer bên ngoài
3. Có đối tác doanh nghiệp yêu cầu SAML / OIDC federation

Việc migrate về sau không quá đau vì user store (ASP.NET Core Identity) giữ nguyên.

---

### Quyết định #4 — Session riêng biệt giữa vsite và mini-website của shop

**Chốt:** Mỗi site có token riêng. Không SSO xuyên domain.

**Lý do:** Đơn giản, không phụ thuộc third-party cookie (đang bị chặn dần trên Safari/Chrome). Khách vào `spa-abc.com` coi đó là "website của tiệm spa", không nhất thiết biết vsite là gì.

**Lưu ý quan trọng:** Session riêng biệt **≠** account riêng biệt.

---

### Quyết định #5 — Một user record toàn cục, token audience theo từng shop

**Chốt:** Cùng một Google account login vào nhiều shop → **một user record duy nhất**, token phát ra có audience riêng theo từng shop.

**Lý do:** Người dùng đổi tên/avatar một lần áp dụng mọi nơi; vsite thấy được lịch sử xuyên shop (giá trị lớn cho Analytics và cho chính khách hàng). Vẫn "riêng biệt" đúng nghĩa vì token không dùng chéo được giữa các shop.

**Phương án bị loại:** User record riêng cho mỗi shop — mất toàn bộ giá trị dữ liệu tổng hợp, và cùng một người sẽ tồn tại nhiều bản ghi trùng.

---

### Quyết định #6 — Social login qua callback cố định + handoff code ⭐

**Chốt:** Mọi social login (kể cả từ custom domain của shop) đều đi qua một callback URL cố định tại `api.vsite.vn`, sau đó redirect lần hai về domain của shop.

**Luồng:**

```
[1] spa-abc.com  →  api.vsite.vn/auth/external/start?provider=google&shop=spa-abc
                    · Tra ShopDomain trong DB → lấy callback URL
                    · Tạo state đã ký (shopId + nonce + expiry) + set cookie ngắn hạn
                    ↓
[2] Google  (redirect_uri = api.vsite.vn/auth/external/callback — CỐ ĐỊNH)
                    ↓
[3] api.vsite.vn/auth/external/callback
                    · Verify state + cookie + chưa dùng + chưa hết hạn
                    · Đổi code → profile → tìm/tạo user
                    · Sinh HANDOFF CODE (dùng 1 lần, TTL ~30 giây)
                    ↓
[4] spa-abc.com/auth/callback?code=handoff_xxx
                    ↓
[5] FE POST code → api.vsite.vn/auth/exchange → nhận access + refresh token
```

**Ba ràng buộc bắt buộc:**

| # | Ràng buộc | Lý do |
|---|---|---|
| 1 | **Không nhận `returnUrl` từ query.** Chỉ nhận `shop` slug rồi tra callback URL từ bảng `ShopDomain` | Open redirect không tồn tại về mặt cấu trúc — attacker không kiểm soát được URL đích |
| 2 | **Bước [4] truyền handoff code, tuyệt đối không truyền JWT** | Token trên URL rò qua browser history, `Referer` header, access log của proxy |
| 3 | **State phải gắn với browser bằng cookie**, không chỉ ký | Chữ ký chỉ chứng minh state do vsite tạo, không chống được login CSRF |

**Lợi ích:** Chỉ cần đăng ký redirect URI **một lần duy nhất** với Google/Facebook. Shop mới thêm vào không cần đụng gì tới cấu hình bên provider.

**Đánh đổi chấp nhận:** User thấy consent screen ghi tên "vsite" chứ không phải tên shop. Không tránh được, và cũng hợp lý vì tài khoản do vsite quản lý.

**Đơn giản hóa cho MVP:** ~~Social login chỉ có trên `vsite.vn`; trên mini-website của shop chỉ dùng username/password. Bỏ hẳn được độ phức tạp trên cho tới Phase 2.~~

> **⚠️ ĐÃ ĐỔI (xem Quyết định #26):** Social login bật ngay từ MVP cho **cả** `vsite.vn` lẫn custom domain — dùng chung đúng luồng callback cố định + handoff code ở trên (luồng này vốn đã được thiết kế tổng quát theo `shop` slug, nên không phát sinh thêm effort kỹ thuật khi bật sớm).

---

### Quyết định #7 — Multi-tenancy hỗ trợ cả 3 chế độ domain ⭐

**Chốt:** Hỗ trợ đồng thời path-based, subdomain, và custom domain.

| Dạng | Ví dụ | Đối tượng |
|---|---|---|
| Path | `vsite.vn/spa-abc` | Mặc định, mọi shop |
| Subdomain | `spa-abc.vsite.vn` | Shop muốn địa chỉ gọn |
| Custom domain | `spa-abc.com` | Shop có thương hiệu riêng |

**Schema `ShopDomain`:**

```
id
shop_id
kind          : enum { Path, Subdomain, Custom }
value         : "spa-abc" | "spa-abc" | "spa-abc.com"
is_primary    : bool   -- chỉ 1 primary per shop
verification  : enum { Pending, Verified, Failed }
verify_token  : TXT record value (cho Custom)
ssl_status    : enum { None, Issuing, Active, Failed }
created_at
```

**Tenant Resolution Middleware — thứ tự ưu tiên:**

```
[1] Host KHÔNG thuộc *.vsite.vn     → tra ShopDomain (kind=Custom, Host)
[2] Host = {slug}.vsite.vn          → tra ShopDomain (kind=Subdomain, slug)
[3] Host = vsite.vn, path đầu={slug}→ tra ShopDomain (kind=Path, slug)
[4] Không match                     → route của platform

→ TenantContext { ShopId, DomainKind, BasePath }
→ Cache Redis: host+path → shop_id, invalidate khi đổi domain
```

**Database:** Shared schema + `tenant_id`. Không dùng schema-per-tenant (nhiều shop nhỏ, chi phí vận hành sẽ quá cao).

---

### Quyết định #8 — Reserved slug list là bắt buộc

**Chốt:** Enforce ở tầng validation khi shop chọn slug.

**Lý do:** Nếu shop đăng ký slug `api` hoặc `auth`, hệ thống tự vỡ. Danh sách nên rộng hơn mức cần thiết.

⚠️ **Danh sách cụ thể KHÔNG nằm ở đây.** Nguồn sự thật duy nhất là `config/reserved-routes.json` (Quyết định #24). Trước đây mục này có một danh sách viết tay riêng — đã **xoá** vì hai danh sách song song chắc chắn lệch nhau, và lệch ở đây là lỗi routing/bảo mật thật. Quyết định #8 giờ chỉ chốt *nguyên tắc phải enforce*; Quyết định #24 chốt *nội dung và cách tiêu thụ*.

---

### Quyết định #9 — Caddy On-Demand TLS cho custom domain

**Chốt:** Caddy làm reverse proxy trước .NET, dùng On-Demand TLS với `ask` endpoint.

```
tls { on_demand }
on_demand_tls {
  ask http://api-internal/internal/tls-check
}
```

Khi có TLS handshake tới domain lạ → Caddy gọi `/internal/tls-check?domain=...` → backend trả 200 nếu domain đã `verified` trong `ShopDomain` → Caddy tự xin Let's Encrypt cert và cache.

**Ràng buộc:** Endpoint `tls-check` phải rate-limit và **chỉ trả 200 cho domain đã verified**. Nếu không sẽ bị lạm dụng làm cạn rate limit Let's Encrypt.

**Lý do chốt:** Đây đúng là use-case Caddy được thiết kế cho. Tiết kiệm hàng tuần công so với tự viết ACME client trong .NET.

---

### Quyết định #10 — SEO: 1 primary domain + 301 + canonical

**Chốt:** Mỗi shop chỉ có **một** primary domain. Hai dạng còn lại **301 redirect** về primary.

**Bắt buộc:**
- Thẻ `<link rel="canonical">` trỏ về primary
- `robots.txt` + `sitemap.xml` sinh riêng theo tenant, chỉ chứa primary URL

**Lý do:** Cùng một website truy cập được qua 3 URL là duplicate content, làm loãng SEO của cả platform lẫn shop. Vì `apps/web` là app hướng SEO, việc này ảnh hưởng trực tiếp giá trị cốt lõi sản phẩm.

---

### Quyết định #11 — `builder-renderer` phải nhận `basePath` ⚠️

**Chốt:** `builder-renderer` nhận `basePath` từ context. Mọi component sinh link **phải** đi qua helper `resolveUrl()`.

**Lý do:** Cùng một Component Tree, khi render tại `vsite.vn/spa-abc` thì mọi link nội bộ phải prefix `/spa-abc`; khi render tại `spa-abc.com` thì prefix rỗng.

**⚠️ Đây là invariant phải ghi rõ trong CLAUDE.md của `builder-renderer`** — loại lỗi mà AI agent sẽ vi phạm liên tục nếu không được nói trước. **Tuyệt đối không hardcode `href` trực tiếp trong component.**

---

### Quyết định #12 — Component Tree JSON, không lưu HTML

**Chốt:** Website lưu dưới dạng cây component JSON. Mỗi node: `type`, `variant`, `props`, `children`.

**Ràng buộc:** Builder và mini-website **dùng chung một bộ Renderer** — đảm bảo WYSIWYG thật sự.

---

### Quyết định #13 — Tách biệt Business Data / Presentation / Editor

**Chốt:**

```
Business Data   →  Services, Products, Reviews, Booking
Presentation    →  Component Tree, Theme, Layout
Editor          →  Drag & Drop, AI Chat, Property Panel
```

Component **không chứa dữ liệu**, chỉ bind tới nguồn (`ServiceGrid` → `Shop.Services`).

**Hệ quả:** Đổi template không mất dữ liệu; thêm dịch vụ không cần sửa giao diện; AI chỉ thao tác trên Component Tree.

---

### Quyết định #14 — AI sinh Operations, không sinh HTML/Tree ⭐

**Chốt:** AI trả về tập lệnh chỉnh sửa, không trả về cây component mới.

```json
{
  "operations": [
    { "op": "add", "targetId": "body", "index": 2,
      "component": { "type": "Testimonials", "variant": "Testimonials03" } },
    { "op": "update", "targetId": "comp_a3f21",
      "props": { "title": "Chào mừng đến Spa ABC" } },
    { "op": "remove", "targetId": "comp_b8e02" },
    { "op": "move", "targetId": "comp_c1d44", "toIndex": 0 }
  ]
}
```

**Chỉ có 5 operation type — không tự thêm:** `add` · `update` · `remove` · `move` · `changeTheme`

**Lợi ích:** Mỗi operation = một bước undo tự nhiên; token cost thấp; validate được từng lệnh; preview được trước khi apply.

**Ràng buộc:** MỌI thay đổi tree phải đi qua Operations Engine — dù đến từ Drag & Drop, AI Chat, hay Property Panel. Không bao giờ mutate tree trực tiếp.

---

### Quyết định #15 — Pipeline xử lý AI Chat

```
User chat
   ↓
[1] Intent Router        → theme change / component change / Q&A thường
   ↓
[2] Context Builder      → Component Tree rút gọn + Component Registry
                           + Selection Context + Business Data khả dụng
   ↓
[3] AI Agent (Tool Use)  → add_component, update_component_props,
                           remove_component, reorder_components, change_theme
                           componentType/variant dùng ENUM → không thể bịa
   ↓
[4] Zod Validator        → sai schema thì cho AI self-correct, không throw cho user
   ↓
[5] Apply vào draft      → Zustand + immer, chưa lưu DB
   ↓
[6] Preview render       → dùng chung builder-renderer
   ↓
[7] [✓ Giữ] persist DB   |   [✗ Hoàn tác] zundo rollback
```

**Undo grouping:** Operations từ một lượt AI chat = **một** group undo duy nhất. User gõ một câu, bấm undo một lần → về nguyên trạng.

---

### Quyết định #16 — Component Targeting: Selection Context ưu tiên

**Cơ chế chính:** Người dùng **click chọn component** trên preview trước khi chat → gửi kèm `selectedComponentId` → AI biết chính xác target.

**Cơ chế phụ:** Khi không click chọn, gửi kèm danh sách component trên trang (id + summary tự sinh từ props) để AI tự chọn.

**Quy tắc bắt buộc:** AI không chắc chắn → **hỏi lại**, không đoán bừa.

---

### Quyết định #17 — Component Registry: một manifest, codegen phần còn lại ⭐

**Chốt:** Không duplicate định nghĩa component ở nhiều nơi.

```
builder-components/registry/
  hero.manifest.ts       ← SINGLE SOURCE OF TRUTH
  gallery.manifest.ts
       │  pnpm gen:registry
       ▼
  ├── generated/component-types.ts      (TS union types)
  ├── generated/props-schemas.ts        (Zod schemas)
  ├── generated/ai-tool-schema.json     (enum cho AI Function Calling)
  ├── generated/property-panel.ts       (config Property Panel)
  └── generated/registry-map.ts         (type → React component)
```

**Lý do:** Thêm component mới = viết **một** file manifest + một file React component, rồi codegen. AI agent không còn cơ hội quên cập nhật AI tool enum hay Zod schema — vì chúng không tồn tại như file viết tay.

**Nguyên tắc tổng quát:**
> **codegen > skill > CLAUDE.md > hy vọng agent nhớ.**
> Chỗ nào đẩy lên codegen được thì đừng để ở tầng thấp hơn.

---

### Quyết định #18 — Contract-first: OpenAPI snapshot commit vào repo

**Chốt:** Thay script tự viết bằng **Orval**, và commit OpenAPI snapshot vào repo.

```
contracts/openapi/
  identity.v1.json
  shop.v1.json
  service.v1.json
  website.v1.json
  search.v1.json
```

**Orval sinh ra đúng những gì stack đang dùng:** TanStack Query hooks · Zod schemas · MSW handlers · TypeScript types.

**Task lifecycle:**

```
PHASE 0 · CONTRACT (cùng nhau, 10–15 phút)
  → cập nhật contracts/openapi/{module}.v1.json + task note
  → chốt: endpoint, DTO in/out, enum, error code, pagination, auth policy
  ⚠ Chưa viết code nào

        ┌──────────┴──────────┐
        ▼                     ▼
PHASE 1A · BE            PHASE 1B · FE (SONG SONG)
  Handler, validator,      pnpm gen:api → hooks + MSW
  endpoint, EF query,      Code UI với MSW mock
  test → regen swagger     Test bằng Vitest + MSW
  → diff phải = 0
        └──────────┬──────────┘
                   ▼
PHASE 2 · INTEGRATE
  Tắt MSW → chạy thật
  CI: contract drift check
```

**Lý do quan trọng nhất:** Phase 0 tạo ra artifact **có thể commit**, nên hai session AI agent (BE và FE) đọc cùng một nguồn sự thật. Không có phase này, hai agent sẽ tự bịa ra hai shape khác nhau.

---

### Quyết định #19 — Quy ước bắt buộc để codegen không vỡ

| Quy ước | Lý do |
|---|---|
| `JsonStringEnumConverter` — enum serialize dạng **string** | Enum số làm reorder thành breaking change ngầm |
| TS dùng **const object + union type**, không dùng `enum` của TS | `enum` TS có runtime cost, không hợp với Zod |
| Mọi endpoint bắt buộc có `[ProducesResponseType]` đủ status code | Thiếu thì OpenAPI không có error shape |
| Lỗi trả **ProblemDetails** (RFC 7807) có `error_code` machine-readable | FE cần code để map i18n message, không parse text |
| Pagination dùng **một shape duy nhất**: `{ items, total, page, pageSize }` | Mỗi module một kiểu là thảm họa |
| Nested REST cho child resource: `/shops/{shopId}/services/{id}` | Ownership validation ngay trong route |
| `api-sdk` là **generated**, tuyệt đối không sửa tay | Thêm header `// GENERATED — DO NOT EDIT` |

---

### Quyết định #20 — State ownership: Query vs Zustand

**Chốt:**

| Loại | Sở hữu bởi |
|---|---|
| Server state (shops, services, bookings, reviews) | **TanStack Query** |
| Client / UI state (modal, wizard step, filter chưa submit, Component Tree draft, selection) | **Zustand** (+ zundo cho undo) |

**Ràng buộc:** KHÔNG copy dữ liệu từ Query vào Zustand. Nếu thấy cần → sai thiết kế.

---

### Quyết định #21 — Tenant security invariants ⚠️

Vi phạm những điều dưới đây = lỗi bảo mật, không phải lỗi code style.

1. Mọi entity tenant-scoped **phải** có `ShopId`
2. Mọi query **phải** đi qua Global Query Filter theo `TenantContext`
3. Child resource **phải** validate ownership **trong câu query** (`WHERE ParentId = ...`), KHÔNG load rồi check ở memory
4. **Không bao giờ** nhận `ShopId` từ request body — chỉ lấy từ route hoặc `TenantContext`
5. Quyền theo shop kiểm tra ở **Authorization Handler**, không tin claim trong token (token cũ vẫn hiệu lực sau khi revoke quyền cho tới khi hết TTL)

---

### Quyết định #22 — FE: 2 app (`web`, `portal`) thay vì 3, chia theo rendering strategy ⭐

**Chốt:** Bỏ mô hình 3 app (`customer-web` / `shop-admin` / `website-builder`). Thay bằng **2 app**:

- `apps/web` — Main (trang chung vsite.vn) + Shop (website công khai của shop). SSR.
- `apps/portal` — quản trị shop + website builder (route `/website`). CSR.

**Lý do tách theo rendering strategy chứ không theo nhóm chức năng:** Main và Shop đều cần SEO (Quyết định #10, #23) nên bắt buộc SSR; Portal luôn sau login, không index bởi search engine, nên CSR là đủ và rẻ hơn. Gộp theo nhu cầu SSR/CSR quan trọng hơn gộp theo "ai dùng app này" — vì nó quyết định trực tiếp hạ tầng deploy (mục #24).

**Website Builder không còn là app riêng** — nó là 1 route (`/website`) bên trong `apps/portal`, vì bản chất nó cũng là công cụ *quản trị* (chủ shop dùng sau khi login), không phải nội dung công khai. Phần *output* của builder (website đã render ra cho khách xem) mới cần SSR, và đó là "Shop" trong `apps/web`.

**Packages dùng chung vẫn giữ nguyên** qua Turborepo — 2 app không đồng nghĩa 2 codebase tách rời (xem mục 2.3). Route "1 FE deployment cho đơn giản" ban đầu vẫn giữ tinh thần: 1 monorepo, 1 CI/CD pipeline, chỉ khác ở output build (2 deployable artifact).

---

### Quyết định #23 — SSR cho `apps/web` bằng TanStack Start; `apps/portal` giữ CSR ⭐

**Chốt:** 
- `apps/web` (Main + Shop) dùng **TanStack Start** để SSR.
- `apps/portal` giữ CSR thuần (Vite + TanStack Router, không SSR).

**Lý do chọn TanStack Start:** Team đã chốt TanStack Router + TanStack Query system-wide (mục 2.1). TanStack Start build trên chính TanStack Router, không cần đổi routing hay học lại pattern mới như phải làm nếu chuyển sang Next.js. Tại thời điểm chốt (8/2026), TanStack Start đã ở giai đoạn Release Candidate — API coi như ổn định, không còn beta.

**Lý do Portal không cần SSR:** Portal luôn nằm sau login, không được index bởi search engine, không có yêu cầu SEO nào từ `01-project-ideal.md`. Chạy SSR cho Portal chỉ tốn thêm compute vô ích.

**Rủi ro cần theo dõi:** TanStack Start chưa lên bản 1.0 chính thức — **lock version cụ thể** trong `package.json` (không dùng `^`/`~` cho package này), tránh breaking change bất ngờ khi API còn thay đổi trước 1.0.

**⚠️ Invariant mới cho `builder-renderer`:** Vì `apps/web` giờ SSR, `builder-renderer` phải render được **cả server-side lẫn client-side** (isomorphic) — không được dùng trực tiếp `window`/`document` trong logic render chính, chỉ dùng trong effect chạy sau hydrate. **Ghi rõ điều này trong CLAUDE.md của `builder-renderer`**, cùng nhóm với invariant `basePath`/`resolveUrl()` ở Quyết định #11 — đây là loại lỗi AI agent sẽ vi phạm liên tục nếu không nói trước.

---

### Quyết định #24 — Reserved routes: một nguồn JSON duy nhất, dùng chung FE + BE ⭐

**Chốt:** Danh sách reserved slug/route (`api`, `auth`, `admin`, `login`, `search`, `portal`, ...) và reserved subdomain (`www`, `admin`, `app`, `help`, ...) sống ở **một file JSON duy nhất** trong repo, không viết tay 2 lần ở FE và BE.

**Vị trí đề xuất:** `config/reserved-routes.json` ở root monorepo, cấu trúc:

```json
{
  "reservedPaths": ["api", "auth", "admin", "login", "logout", "register", "signup", "search", "portal", "shop", "assets", "static", "cdn", "help", "blog", "about", "terms", "privacy", "sitemap.xml", "robots.txt", ".well-known"],
  "reservedSubdomains": ["www", "admin", "app", "help", "api", "cdn", "static"]
}
```

**Cách tiêu thụ:**
- FE (`apps/web`, `apps/portal`): import trực tiếp JSON lúc build (Vite hỗ trợ import JSON native).
- BE (.NET): đọc file lúc startup (embed resource hoặc copy vào output), cache trong memory/Redis, dùng để validate khi shop chọn slug (Quyết định #8) và trong Tenant Resolution Middleware (Quyết định #7).

**Lý do:** Đúng nguyên tắc đã chốt ở Quyết định #17 — *"codegen > skill > CLAUDE.md > hy vọng agent nhớ"*. Hai danh sách viết tay riêng biệt ở FE và BE chắc chắn sẽ lệch nhau theo thời gian, và lệch ở đây là lỗi bảo mật/routing thật (shop có thể đăng ký trùng slug hệ thống nếu BE thiếu 1 route mà FE vừa thêm).

**Việc cần làm khi thêm route/subdomain mới:** chỉ sửa 1 file JSON này — không sửa tay ở FE hoặc BE riêng lẻ.

⚠️ **`shop` là bắt buộc** (mới thêm): trang hồ sơ shop nằm tại `vsite.vn/shop/{slug}`. Nếu không reserve, một shop có thể đăng ký slug `shop` và chiếm mất toàn bộ nhánh URL này.

**Vì sao trang hồ sơ không nằm ở `vsite.vn/{slug}`:** chỗ đó đã bị chiếm bởi website shop dùng `ShopDomain(kind = Path)` (Quyết định #7). Xung đột thật, phải tách nhánh. Website shop thắng vì đó là thứ shop trả tiền để sở hữu.

---

### Quyết định #25 — Portal URL: `admin.vsite.vn/*`

**Chốt:** Portal dùng subdomain riêng `admin.vsite.vn`, không dùng path `vsite.vn/portal/*`.

**Hệ quả:**
- `admin` phải nằm trong `reservedSubdomains` (Quyết định #24) — giờ chính thức bắt buộc.
- `portal` **không cần** nằm trong `reservedPaths` nữa vì không còn là path — nhưng vẫn nên giữ trong danh sách để phòng nhầm lẫn/đặt lại sau này.
- Portal là origin riêng biệt hoàn toàn với `vsite.vn` → cookie/session tách bạch tự nhiên, không phụ thuộc third-party cookie giữa `vsite.vn` và `admin.vsite.vn`.
- Khớp tự nhiên với việc tách deploy ở Quyết định #22 — Portal vốn đã ở subdomain riêng nên tách thành build artifact/deployment riêng là hệ quả logic, không phải quyết định tùy tiện.

---

### Quyết định #26 — Search Engine: Elasticsearch dùng ngay từ MVP ⭐

**Chốt:** Không đợi ngưỡng như Quyết định #2 cũ. Dùng **Elasticsearch** ngay từ MVP cho cả full-text search lẫn geo-search — thay thế PostgreSQL FTS. **Supersede Quyết định #2.**

⚠️ **Phạm vi index chốt tại Quyết định #38: chỉ index `Listing`.** Không index `Service`, không index `Product`. Mô tả cũ ("sản phẩm, dịch vụ, tin tức") là tàn dư của hệ VSite 4.8 và đã bị bỏ.

**Kiến trúc:**
- **PostgreSQL + PostGIS vẫn là nguồn sự thật** (transactional write) cho Shop/Service/Product/toạ độ
- **Elasticsearch là read index phái sinh**, không phải nơi ghi trực tiếp — sync qua Hangfire indexer job đọc domain event/outbox
- Elasticsearch đảm nhiệm **cả** full-text lẫn geo-query (native `geo_point`/`geo_distance`) — không tách riêng 2 hệ để tránh phải hợp kết quả giữa PostGIS và ES

**Lý do chọn Elasticsearch (không phải OpenSearch):** Team quen thuộc với hệ Lucene/ES. Vì vsite tự host và dùng nội bộ (không bán lại dịch vụ search cho bên thứ ba), rủi ro pháp lý với Elastic License 2.0 / AGPLv3 gần như không đáng kể.

**.NET client:** `Elastic.Clients.Elasticsearch` (official).

**Lưu ý vận hành:** Thêm 1 cluster cần quản lý (dù chạy single-node cho MVP) — team cần theo dõi tài nguyên (heap, disk) riêng cho ES ngoài PostgreSQL/Redis đã có.

---

### Quyết định #27 — Cấu trúc JWT claims

**Chốt:**

```
JWT payload:
  sub (userId)
  email
  phoneNumber
  name / avatar
  ownerShopIds: string[]     -- shop mà user là chủ, dùng để render shop switcher ở Portal
                                 không cần gọi thêm API khi vừa login
```

> ⚠️ **Làm rõ (Quyết định #31):** `ownerShopIds` **chỉ để render UI**. Tuyệt đối không dùng làm căn cứ authorize — mọi kiểm tra quyền phải query `UserShop(userId, shopId)` tại thời điểm request.

**KHÔNG đưa `belongShopIds` vào JWT.** Quan hệ user↔shop cụ thể (có phải member của shop X không, `IsOwner` với shop X hay không...) được **query trực tiếp DB tại thời điểm request**, dùng cặp `(userId lấy từ token, ShopId đã resolve từ TenantContext theo Quyết định #7/#21)`.

**Lý do:** Request luôn đã biết đang thao tác với **một** ShopId cụ thể (resolve từ route/subdomain/custom domain), nên chỉ cần check 1 quan hệ, không cần cả danh sách. `belongShopIds` với khách hàng marketplace có thể phình tới hàng chục/hàng trăm shop theo thời gian — nhét vào JWT sẽ làm token phình to vô ích ở mọi request, kể cả những request chỉ liên quan 1 shop.

**TTL và refresh:** giữ nguyên theo Quyết định #3 (10–15 phút, refresh token rotation).

---

### Quyết định #28 — Identity Model: 1 userId toàn cục, credential riêng theo từng shop, merge theo email/SĐT

> **⚠️ ĐÃ SUPERSEDE bởi Quyết định #29** — bỏ credential khỏi `UserShop`, email trở thành identity key luôn verified, không merge theo email/SĐT. Chi tiết đầy đủ tại `03-identity-entity-design.md`. Giữ lại mục này để lưu lý do ban đầu và mô tả rủi ro đã dẫn tới việc thay đổi.

**Chốt (cũ):**

- Một `userId` toàn cục duy nhất cho mỗi người dùng thật (khớp tinh thần Quyết định #5)
- Mỗi shop có bảng quan hệ riêng, ví dụ `UserShopCredential`: `userId`, `shopId`, `isOwner`, `username`, `passwordHash` — cho phép user có username/password **khác nhau** ở từng shop, nhưng cùng trỏ về 1 `userId`
- **Merge logic:** khi user đăng ký username/password mới ở 1 shop, hệ thống match theo **email hoặc số điện thoại** với user record đã tồn tại → nếu trùng, gắn vào `userId` đó; nếu không, tạo `userId` mới
- Social login (Google/Facebook) tiếp tục merge theo Quyết định #5 — an toàn vì provider đã tự verify email hộ

**⚠️ RỦI RO ĐÃ GHI NHẬN — CHƯA XỬ LÝ, cần quay lại trước khi launch:**

Merge theo email/SĐT khi đăng ký username/password **hiện không yêu cầu verify**. Kịch bản tấn công: kẻ tấn công biết email/SĐT của nạn nhân (thường công khai), đăng ký tài khoản mới tại **một shop khác** bằng email/SĐT đó + mật khẩu tự đặt → hệ thống merge vào đúng `userId` của nạn nhân → kẻ tấn công login bằng mật khẩu tự đặt của chính họ, nhận token đại diện cho `userId` của nạn nhân → truy cập được dữ liệu ở **mức user toàn cục** (lịch sử xuyên shop theo Quyết định #5, hồ sơ cá nhân, SĐT, địa chỉ đã lưu...), dù chưa từng biết mật khẩu thật của nạn nhân ở bất kỳ shop nào.

**Lưu ý quan trọng khi thiết kế:** username/password riêng theo từng shop chỉ bảo vệ việc *đăng nhập đúng shop đó* — không bảo vệ dữ liệu ở *mức user toàn cục* nếu bước merge tự động không được verify. Cần phân định rõ phần nào của hồ sơ user là toàn cục (rủi ro nếu merge sai) vs phần nào scoped theo shop (an toàn hơn).

**Hướng fix đề xuất cho sau này (chưa bắt buộc làm ngay):** yêu cầu xác minh email/SĐT (OTP hoặc magic link) trước khi merge vào `userId` đã tồn tại — tương tự cách Google/Facebook đã tự verify hộ ở luồng social login.

> **Kết quả thực tế:** hướng fix trên đã được đánh giá và **chưa đủ**. Verify email chỉ chặn được thời điểm *tạo* User, không chặn được thời điểm *gắn credential mới* vào User đã tồn tại — attack chỉ chuyển vị trí từ `User` sang `UserShopCredential`. Xem Quyết định #29.

---

### Quyết định #29 — Identity Model chốt lại: email là identity key, credential riêng theo shop, token shop bị giới hạn năng lực ⭐

**Supersede Quyết định #28.** Chi tiết đầy đủ (schema, ràng buộc DB, luồng đăng ký/đăng nhập, reasoning từng điểm) tại **`03-identity-entity-design.md`**.

**Bốn điểm cốt lõi:**

1. **Email là identity key toàn cục**, UNIQUE, và **luôn ở trạng thái đã verify**. Không tồn tại `User` có email chưa verify trong DB.
2. **`UserShop` giữ `PasswordSalt`/`PasswordHash` riêng theo shop**, bỏ `Username` (định danh đăng nhập là email). **Hồ sơ** (`FullName`/`AvatarUrl`/`Phone`) **dùng chung** ở `User`.
3. **Mỗi `User` bắt buộc có email đã verify HOẶC `ExternalLogin` với Zalo.** Enforce ở domain lúc tạo `User`, đỡ thêm bằng cột `PrimaryIdentityKind` + CHECK constraint.
4. **Không merge, chỉ link.** Trùng email khi đăng ký ở `vsite.vn` → báo "đã có tài khoản". Trùng email khi đăng ký ở domain shop mà chưa có membership → vẫn gửi verify bình thường (user không hề biết email đã tồn tại).

**Vì sao giữ password riêng theo shop:**

- Ảo giác tách biệt là **có chủ đích** (đúng tinh thần Quyết định #4) — khách vào `spa-abc.com` không cần biết vsite tồn tại.
- Dùng chung password thì đổi ở shop này làm hỏng đăng nhập ở shop kia — user không hiểu vì sao.
- Chrome lưu credential **theo origin**; password dùng chung làm autofill điền sai sau khi đổi ở origin khác.

**Vì sao KHÔNG dựng lại lỗ hổng của Quyết định #28:**

Lỗ hổng cũ không nằm ở *chỗ chứa credential*, mà ở chỗ **credential mới gắn được vào `User.Id` đã tồn tại mà không cần chứng minh sở hữu**. Hai lớp chặn, thiếu một là quay lại lỗ hổng cũ:

- **Quyết định #30** — đăng ký bằng email/password **luôn** verify email, kể cả lần thứ N tại shop khác. Kẻ tấn công không nhận được mail → không tạo được `UserShop`.
- **Quyết định #32** — token `shop:{shopId}` không đọc/sửa được credential global. Password shop yếu nhất không kéo theo mất tài khoản.

**Entity chốt:** `User` · `ExternalLogin` · `UserShop` · `Shop` · `Role` · `PendingRegistration`.

**Thời điểm tạo `UserShop`:** mọi lần **authenticate thành công trong context của một shop** → upsert `UserShop` (role `Customer`). Bao gồm cả đăng ký mới tại domain shop lẫn user đã có tài khoản vsite nay đăng nhập tại domain shop. Duyệt web ẩn danh thì không tạo gì. Cột `Source` (`RegisteredOnShop` / `LoggedInOnShop` / `InvitedByShop` / `ShopCreator`) + `LastActiveAt` phục vụ thống kê cho chủ shop. `Source` **bất biến sau khi tạo** — chỉ set ở nhánh INSERT.

⚠️ **Ranh giới dữ liệu:** Portal không bao giờ serialize thẳng entity `User` — phải qua DTO riêng cho góc nhìn shop. Chủ shop không được thấy user thuộc shop nào khác. Chi tiết ở §3.3 của `03-identity-entity-design.md`.

**✅ Rủi ro account-takeover ở Quyết định #28: ĐÃ ĐÓNG.**

---

### Quyết định #30 — Đăng ký: `User` chỉ sinh ra sau khi verify, qua bảng `PendingRegistration` ⭐

**Chốt:** Đăng ký bằng email/password **luôn** phải verify email (không chỉ khi trùng). Trong lúc chờ, dữ liệu nằm ở bảng staging `PendingRegistration` — **không** dùng `User.Status = PendingVerification`.

**Lý do:** nếu tạo `User` trước khi verify sẽ (a) vỡ invariant "email trong `User` luôn verified", (b) mở đường **email squatting** — attacker đăng ký `victim@gmail.com` không verify, chiếm slot UNIQUE, và nếu hệ thống auto-link khi nạn nhân login Google thì nạn nhân bước thẳng vào tài khoản của attacker.

**Với external provider:** chỉ auto-link theo email khi provider **khẳng định `email_verified = true`**.

| Provider | Lưu ý bắt buộc |
|---|---|
| Google | Phải đọc claim `email_verified` — Workspace có thể trả `false`. Bỏ qua claim này làm vô hiệu toàn bộ thiết kế |
| Facebook | Tài khoản đăng ký bằng SĐT không trả email → bắt buộc xử lý nhánh không email |
| Zalo | Không trả email. User Zalo-only (`Email = NULL`) là **case bình thường**, không phải ngoại lệ |

**Ràng buộc:** link verify luôn trỏ về `api.vsite.vn/auth/verify-email` (cố định); nếu đăng ký từ custom domain thì redirect lần hai về domain shop kèm **handoff code**, không kèm token — dùng lại đúng khuôn Quyết định #6.

⚠️ **Tuyệt đối không** làm biến thể "verify email xong thì set password mới người dùng vừa nhập" — đó là password-reset ngầm, nạn nhân không nhận được cảnh báo, và nó dựng lại đúng lỗ hổng vừa đóng.

**Khi context là domain shop:** password vừa nhập ghi vào `UserShop.PasswordHash`, **tuyệt đối không** ghi vào `User.PasswordHash`. Nếu ghi, password yếu đặt ở một shop trở thành password đăng nhập `vsite.vn` — mở đúng đường tấn công mà Quyết định #32 đang chặn.

**Quên mật khẩu cũng scoped:** reset token phải mang scope (`vsite.vn` hay `shopId` nào); reset ở `spa-abc.com` chỉ đổi password shop đó, revoke refresh token cùng scope, và mail thông báo phải nêu rõ đổi ở đâu.

---

### Quyết định #31 — Phân giải Role theo domain; Portal lấy `ShopId` từ route ⭐

**Chốt:** `User` của shop và `User` của vsite dùng **chung một bảng**; vai trò phân giải theo domain tại thời điểm request.

| Domain | `ShopId` resolve từ | Audience | Role authorize |
|---|---|---|---|
| `vsite.vn` | — (platform) | `vsite-main` | `User.RoleId` (Scope=Platform) |
| `{slug}.vsite.vn` · custom domain · `vsite.vn/{slug}` | Host / path (Quyết định #7) | `shop:{shopId}` | `UserShop(userId, shopId).RoleId` |
| `admin.vsite.vn` | **Route param** `/shops/{shopId}/...` | `vsite-portal` | `UserShop(userId, shopId).RoleId` |

**Vì sao Portal khác:** Portal là platform domain phục vụ nhiều shop qua shop switcher → `TenantContext` **không** resolve được từ host. Đây là trường hợp duy nhất `ShopId` không đến từ host, phải ghi rõ để không ai suy diễn nhầm là "lấy từ body cũng được".

**Bổ sung vào Tenant security invariants (Quyết định #21):**

6. Trên Portal, `ShopId` lấy từ **route param**, không bao giờ từ body — mở rộng của invariant #21.4.
7. `ownerShopIds` trong JWT (Quyết định #27) **chỉ dùng để render UI shop switcher**, tuyệt đối không làm căn cứ authorize. Authorization Handler query `UserShop(userId, shopId)` tại mỗi request.
8. Không có `UserShop` record → **không có role** ở shop đó. Không fallback về role mặc định.
9. `User.RoleId = PlatformAdmin` **không được** bypass Global Query Filter ngầm. Admin xem dữ liệu shop → **endpoint riêng + audit log riêng**. Tắt filter có điều kiện trong endpoint dùng chung là lỗ hổng chờ sẵn.

---

### Quyết định #32 — Ranh giới năng lực của token `shop:{shopId}` ⭐⚠️

**Chốt:** Token có audience `shop:{shopId}` **không** đọc hoặc sửa được bất cứ thứ gì ở mức credential global hoặc dữ liệu xuyên shop.

**Vì sao:** token này sinh ra từ password của **riêng shop đó** — thứ user được phép đặt khác nhau và có thể yếu hơn ở tiệm họ ít quan tâm (Quyết định #29). Nếu nó chạm được credential global thì **tài khoản chỉ an toàn bằng mật khẩu yếu nhất trong N shop**.

| Thao tác từ token `shop:{shopId}` | |
|---|---|
| Đọc `FullName`, `AvatarUrl`, `Phone`, `Email` của chính mình | ✅ |
| Sửa `FullName`, `AvatarUrl`, `Phone` | ✅ hồ sơ dùng chung, propagate mọi nơi |
| Đổi password **của chính shop đó** | ✅ |
| Đọc/ghi booking và dữ liệu nghiệp vụ **tại shop đó** | ✅ |
| **Viết/sửa `Review` trên vsite** | ❌ yêu cầu audience `vsite-main` — xem Quyết định #38 |
| **Đổi `User.Email`** | ❌ đổi email → "quên mật khẩu" → chiếm toàn bộ tài khoản |
| **Đặt/đổi password global (`vsite.vn`)** | ❌ leo thang scope shop → scope platform |
| **Thêm/xoá `ExternalLogin`** | ❌ gắn Google của attacker = cửa hậu vĩnh viễn |
| **Đổi password của shop khác** | ❌ |
| **Liệt kê shop khác mà user thuộc về** | ❌ phá ảo giác tách biệt + lộ đời tư |
| **Đọc booking/review ở shop khác** | ❌ |

Quy tắc một câu: **hồ sơ thì được, credential và dữ liệu xuyên shop thì không.**

**Cách enforce:**
- Policy `RequireGlobalScope` gắn cho mọi endpoint chạm tới credential/identity.
- **Test tự động bắt buộc** — mỗi endpoint identity có test khẳng định token `shop:*` bị từ chối. Chỉ cần **một** endpoint quên là toàn bộ thiết kế identity sụp; không dựa vào code review.
- **Lockout/rate-limit tách theo scope** — dò password ở Shop C không được khóa tài khoản ở Shop A (nếu không, đây thành vector DoS nhắm vào một user cụ thể).

⚠️ **Ghi invariant này vào `CLAUDE.md` của module Identity**, cùng nhóm với `basePath`/`resolveUrl()` (Quyết định #11) và isomorphic renderer (Quyết định #23) — loại lỗi AI agent sẽ vi phạm liên tục nếu không nói trước.

---

### Quyết định #33 — Page model: `Composable` vs `System` ⭐

**Chốt:** Component Tree (Quyết định #12/#14) **không** áp cho mọi trang. Mỗi trang có `PageKind`, quyết định builder và AI được phép làm gì.

| `PageKind` | Ví dụ | Builder cho phép | AI Chat cho phép |
|---|---|---|---|
| `Composable` | Trang chủ, Giới thiệu, Landing | Kéo-thả tự do: `add` · `remove` · `move` · `update` · `changeTheme` | Toàn bộ 5 operation |
| `System` | Chi tiết dịch vụ, danh sách dịch vụ theo danh mục, đặt lịch, liên hệ | **Chỉ** đổi variant + theme + bật/tắt slot cố định | **Chỉ** `update` props và `changeTheme` |

**Lý do:** Hệ VSite 4.8 cũ chỉ để Home/Introduct là widget-stack; chi tiết sản phẩm, giỏ hàng, chi tiết bài viết đều là action/view code cứng. Đây là ranh giới đúng và cần giữ. Nếu không chốt, AI Chat sẽ được yêu cầu *"xoá nút đặt lịch ở trang chi tiết dịch vụ"* và Operations Engine sẽ vui vẻ làm — hỏng luồng nghiệp vụ mà không ai phát hiện cho tới khi mất khách.

**Cách enforce:** trường `allowedInPageKinds` trong **Component Registry manifest** (Quyết định #17), codegen ra cả Zod schema lẫn AI tool schema. Đúng nguyên tắc *codegen > skill > CLAUDE.md > hy vọng agent nhớ* — không đặt ràng buộc này ở tầng tài liệu.

---

### Quyết định #34 — Navigation là dữ liệu derived + manual overlay ⭐

**Chốt:** Menu điều hướng của website shop **không** lưu như một cây node thủ công trong Component Tree. Nó được **tính lại** từ các nguồn dữ liệu, cộng thêm một lớp tuỳ chỉnh của shop.

```
NavigationConfig (per shop)
  autoSources : [ShopServiceGroup, StaticPage, Contact]
  items       : [{ sourceRef | customUrl, order, hidden, labelOverride }]
```

⚠️ **Nguồn tự động KHÔNG phải `ServiceCategory`.** `ServiceCategory` là taxonomy **toàn cục của marketplace** do vsite quản trị (Quyết định #35) — dùng nó làm menu website shop là kéo phân loại của vsite vào sản phẩm mà shop trả tiền để sở hữu, ngược với Quyết định #4. Menu lấy từ **nhóm dịch vụ hiển thị của chính shop** (`ShopServiceGroup`, per-shop, thiết kế cùng module `Service` ở Phase 2). Hai tầng phân loại này tách bạch: một cho tìm kiếm chung, một cho trình bày trên website shop.

**Lý do giữ phần derived:** mỗi loại nội dung tự quản lý field điều hướng của mình; thêm một loại menu item mới về sau (blog, sự kiện) không phải sửa lại toàn bộ hệ menu.

**Lý do KHÔNG lặp lại cách của hệ cũ:** VSite 4.8 sort menu bằng một số `Index` toàn cục rải rác ở 4 màn hình admin tách biệt, và **không có màn hình sắp xếp toàn bộ menu** — thứ tự cuối cùng là hệ quả ngầm của việc admin gõ đúng số ở từng nơi. Với đối tượng chủ spa/salon không rành kỹ thuật, đây là UX không chấp nhận được.

**Chốt UX:** **một** màn hình kéo-thả duy nhất cho toàn bộ menu. Item mới sinh từ nguồn tự động được append vào cuối và chờ shop sắp.

**Tách bạch ba khái niệm** (giữ nguyên từ hệ cũ, phần này họ làm đúng):

| Khái niệm | Hệ cũ | Hệ mới |
|---|---|---|
| Nguồn dữ liệu gốc | `SCategory`, `SMenuNews`, `TradeMark` | entity tương ứng |
| Model hiển thị đã gộp | `MenuUI` | props của component `Header` |
| Theme render thanh menu | `EMenu` (`MType`) | `variant` (`Header01/02/03`) |

Ánh xạ 1-1, không phát sinh khái niệm mới.

---

### Quyết định #35 — `ServiceCategory` toàn cục; đánh giá neo `Listing`, shop được làm lại từ đầu ⭐

**Chốt hai điểm.**

**(1) Danh mục dịch vụ là entity toàn cục do vsite quản trị**, không phải config JSON per-tenant như hệ cũ. Shop chỉ được **chọn** node lá, không được tự tạo danh mục.

**Vì sao khác hẳn hệ cũ:** VSite 4.8 để mỗi tenant tự vẽ cây danh mục riêng trong JSON blob `SiteConfig` — hợp lý vì các website đó không liên quan gì nhau. vsite mới **không thể** làm vậy: nếu shop A ghi "Massage body" còn shop B ghi "Massage toàn thân" thì search không gom được, landing page khu vực rỗng nghĩa, và toàn bộ giá trị marketplace biến mất.

**(2) `Review` neo vào `Listing`.** Shop gỡ tin đăng thì đánh giá đi theo; đăng lại là bắt đầu từ con số 0.

**Vì sao chọn hướng này** — đã cân nhắc phương án neo vào một entity bền vững ở tầng shop (`ShopCategory`) và **loại bỏ**: nếu đánh giá bám vĩnh viễn theo `(Shop, ngành)`, shop bị đánh giá xấu nặng sẽ **rời nền tảng luôn**, không còn lý do trả phí đăng tin khi điểm số đã hỏng không sửa được. vsite mất cả shop lẫn cơ hội để shop đó cải thiện.

Reset **không miễn phí**: shop mất toàn bộ uy tín đã xây, quay về trạng thái không ai biết mình là ai, phải cố gắng đạt đánh giá tốt hơn. Hình phạt thật, nhưng có lối ra — khác với án chung thân.

⚠️ **Rủi ro chấp nhận, chưa đóng hoàn toàn:** shop có thể coi reset là chiến thuật định kỳ (gom đánh giá xấu → gỡ tin → đăng lại). Ba lớp giảm nhẹ **bắt buộc** triển khai cùng lúc:

1. Hiển thị `ListedSince` công khai — shop reset liên tục luôn hiện "đăng tin từ tuần trước"
2. Cooldown 30 ngày khi đăng lại cùng ngành
3. Đếm số lần reset (nội bộ) → hàng đợi kiểm tra thủ công

**Hệ quả kèm theo:** listing chưa có đánh giá **không** được mượn điểm chung của shop (mượn điểm là đường vòng vô hiệu hoá toàn bộ cơ chế reset), và listing mới **không** bị đẩy xuống đáy xếp hạng (nếu không, "làm lại từ đầu" thành án tử).

Chi tiết schema và ba lớp giảm nhẹ tại `04-listing-and-review-design.md` §3.2.

---

### Quyết định #36 — Anti-pattern cấm kế thừa từ VSite .NET Framework 4.8 ⚠️

**Bối cảnh:** hệ cũ được dùng làm tài liệu tham chiếu. Nhiều pattern trong đó **không** phù hợp với stack và mô hình mới. Danh sách này phải nằm trong `CLAUDE.md` ở root — AI agent đọc code cũ để tham khảo sẽ sao chép chúng nếu không được nói trước.

| Anti-pattern | Vì sao cấm | Thay bằng |
|---|---|---|
| **Một cột JSON `SiteConfig`** chứa category + attribute + menu + home + contact | Không diff được, không audit, hai staff sửa cùng lúc là ghi đè nhau | Tách bảng + `RowVersion` optimistic concurrency. Component Tree vẫn JSON (Quyết định #12) nhưng **per-page, có version** |
| **`CategoryID` int không FK**, match bằng giá trị | PostgreSQL có FK thật. Category giờ là entity nghiệp vụ, không phải config UI | UUID + FK + composite FK khi cần kiểm ownership |
| **Bitmask `Int64`** cho Tags/Promotions | Trần cứng 63 giá trị/site, không đọc được bằng mắt, không index tốt, không sync sang ES sạch | Bảng nối hoặc `text[]` + GIN index |
| **In-memory cache toàn bộ dữ liệu tenant + filter bằng LINQ** (`SiteData`) | Chính tài liệu hệ cũ ghi rõ: không scale. Quyết định #26 đã thay bằng ES | Elasticsearch |
| **`MSEOPage { object Item; object Item2; }`** | Mâu thuẫn trực tiếp TypeScript strict + Orval codegen + Zod — không sinh type từ `object` | DTO có kiểu rõ ràng cho từng trang; SEO là field `seo` lồng bên trong |
| **Không validate server-side khi lưu dữ liệu động** | Vi phạm Quyết định #19. Dữ liệu "trôi" khỏi schema là bug im lặng | FluentValidation/Zod theo schema |
| **Tham số chết trong chữ ký controller** | Hệ cũ có action nhận `ENewsType`, `locationID` nhưng không dùng. Với AI agent thì nguy hiểm gấp bội — agent giả định tham số có tác dụng và viết code dựa trên đó | Dọn ngay khi phát hiện |
| **Một action gánh nhiều nhánh rẽ theo enum** (`FilterSNews` render 5 loại trang) | Chính tài liệu hệ cũ thừa nhận làm giảm độ rõ ràng | Mỗi loại trang một route/handler riêng |

**Pattern từ hệ cũ ĐƯỢC giữ lại** (đã đúng, chỉ đổi cách hiện thực):

1. Phân biệt trang lắp-từ-widget vs trang có nghiệp vụ riêng → Quyết định #33
2. Menu là dữ liệu derived → Quyết định #34
3. Giải nén dữ liệu **một lần lúc build index**, filter trên cấu trúc phẳng → chính là ES document denormalize qua Hangfire
4. Facet-count động, ẩn lựa chọn 0 kết quả → ES aggregations
5. URL lọc SEO-friendly encode trong path (kèm ràng buộc `noindex` mới ở Quyết định #38)
6. DTO riêng cho từng loại trang, không dùng chung một DTO "to nhất"
7. Không lưu dữ liệu suy ra được (breadcrumb, URL) — **nhưng `Slug` phải lưu và bất biến**, vì đổi slug là mất SEO

---

### Quyết định #37 — `Shop.Kind`: `Hosted` vs `ExternalOnly`; hai luồng doanh thu ⭐

**Chốt:** vsite bán hai sản phẩm độc lập (mục 2.1 của `01-project-ideal.md`): (1) phí duy trì website, (2) phí đăng tin marketplace. Shop mua một hoặc cả hai.

```
Shop.Kind        enum   { Hosted, ExternalOnly }
Shop.ExternalUrl string?   -- NOT NULL khi Kind = ExternalOnly
```

**Vì sao `Listing.ShopId` KHÔNG được nullable:** một tin đăng không có chủ thì không có ai để xuất hoá đơn (doanh thu #2), không ai phản hồi được đánh giá, không ai chịu trách nhiệm khi nội dung giả mạo, và không ai nhận thống kê lead — mà thống kê lead chính là thứ chứng minh giá trị để shop tiếp tục trả tiền. `ExternalOnly` giải quyết đúng nhu cầu "shop đã có website riêng" mà không phá bốn thứ trên.

**Lợi ích phụ đáng kể:** `ExternalOnly` là **phễu** cho doanh thu #1. Shop vào bằng cửa rẻ, thấy hiệu quả, nâng cấp `Kind = Hosted` mà không mất tài khoản, đánh giá hay lịch sử lead.

⚠️ **Khi đổi `Hosted → ExternalOnly`** (shop ngừng trả phí website): mọi `Listing` đang trỏ `ShopHome`/`ShopPage` **phải** chuyển sang `ExternalUrl` hoặc bị `Unpublished`. Chặn ở tầng application, không phát hiện bằng job — website tắt mà listing vẫn trỏ vào là 404 cho khách đến từ vsite, và vsite mang tiếng.

---

### Quyết định #38 — Listing opt-in, không auto-map; đánh giá tự do có kiểm soát ⭐

**Chốt bốn điểm:**

1. **vsite KHÔNG tự động đăng dịch vụ của shop lên marketplace.** Shop chủ động tạo `Listing`, tự viết nội dung. **Không** map dữ liệu từ website shop sang tin đăng.
2. **ES index `Listing`, KHÔNG index `Service`.** Search không cần lọc `IsPublished` lúc query, không sợ rò dịch vụ shop chưa muốn công khai, và document ES có schema ổn định.
3. **Đánh giá tự do** — user đã đăng nhập được đánh giá, không cần chứng minh đã dùng dịch vụ. Đã cân nhắc và **loại bỏ** cơ chế scan mã xác thực (`ServiceEncounter`): đòi hỏi thay đổi hành vi cả hai bên, tỉ lệ áp dụng gần 0 khi nền tảng chưa có người dùng, và shop kiểm soát việc phát mã nên vẫn thiên lệch chọn mẫu.
4. **Đánh giá chỉ hiển thị trên `vsite.vn`**, không bao giờ trên website shop (Quyết định #4 — ảo giác tách biệt).

**"Tự do" nghĩa là không cần chứng minh đã dùng dịch vụ, KHÔNG phải không có kiểm soát.** Bốn lớp bắt buộc: đăng nhập · rate-limit theo user và IP · không xoá được và không sửa sau 24h · quy trình khiếu nại có thời hạn cam kết. Chi tiết `04-listing-and-review-design.md` §6.6.

**Bổ sung vào Quyết định #10 (SEO):** mọi URL có **≥2 điều kiện lọc** phải `noindex`. Tổ hợp filter sinh không gian URL vô hạn; không có quy tắc này thì crawl budget của toàn platform bị đốt sạch.

**Bổ sung vào Quyết định #26 (Elasticsearch) — reindex trigger:** `ListingUpdated` · `ReviewCountChanged` · `ShopUpdated` · **`ShopDomainChanged`** · `ShopSuspended`. Trigger thứ tư rất dễ quên; triệu chứng là shop đổi domain xong mà kết quả tìm kiếm vẫn dẫn về domain cũ.

**Bổ sung vào Quyết định #32 (năng lực token):** viết/sửa `Review` yêu cầu audience `vsite-main`, **từ chối** token `shop:{shopId}`. Nếu quên, chủ shop dùng token shop của chính mình để tạo đánh giá 5 sao giả.

---

### Quyết định #39 — Chuẩn hoá đánh số Phase và các mâu thuẫn liên tài liệu ⭐

**Bối cảnh:** bốn tài liệu được viết ở các thời điểm khác nhau; một số mục của tài liệu sau sửa ngầm tài liệu trước mà không xoá bản cũ. Quyết định này đóng toàn bộ số đó lại, để AI agent không gặp hai câu trả lời khác nhau cho cùng một câu hỏi.

**(1) "MVP" = "Phase 1".** Lộ trình ở `01-project-ideal.md` mục 8 là nguồn sự thật duy nhất cho việc *khi nào làm gì*. Mọi bảng tính năng phải ghi Phase khớp với lộ trình đó:

| Hạng mục | Trước đây ghi | Chốt |
|---|---|---|
| Tìm kiếm · Listing · hồ sơ shop · Review · Lead · kiểm duyệt | MVP | **Phase 1** |
| Quản lý dịch vụ (`Service`) · Website Builder · theme · publish · subdomain | MVP | **Phase 2** |
| Quản lý hàng hoá (`Product`) · lưu yêu thích · landing SEO khu vực · ảnh trong đánh giá | MVP / Phase 2 | **Phase 2** |
| AI Chat Builder · tên miền riêng + SSL · booking | Phase 2 | **Phase 3** |
| Marketplace template · nhiều chi nhánh · thanh toán/gói · phân quyền nhân viên | Phase 2 / Phase 3 | **Phase 4** |

**(2) Tên app cũ bị xoá hẳn.** `customer-web` / `shop-admin` / `website-builder` chỉ được nhắc tới như *lịch sử* trong Quyết định #22. Mọi chỗ khác dùng `apps/web` và `apps/portal`.

**(3) URL trang hồ sơ shop là `vsite.vn/shop/{slug}`**, không phải `vsite.vn/{slug}` — chỗ đó thuộc về website shop (`ShopDomain.kind = Path`). Áp dụng cho cả `04-listing-and-review-design.md` §2.1 và §5.

**(4) Quyền ghi `Review`:**

| Hành động | Audience bắt buộc | Ghi chú |
|---|---|---|
| Khách viết/sửa đánh giá | `vsite-main` | Token `shop:{shopId}` bị **từ chối** (Quyết định #38) |
| Shop phản hồi đánh giá | `vsite-portal` + role `Owner`/`Manager` tại shop đó | Phản hồi là thao tác quản trị, làm ở Portal — không phải ngoại lệ của quy tắc trên |
| Shop báo cáo đánh giá vi phạm | `vsite-portal` | Không có quyền gỡ (`04` §6.6 lớp 3) |

**(5) Một khái niệm — một chỗ định nghĩa.** Khi hai tài liệu cùng mô tả một entity, tài liệu **thiết kế chi tiết** thắng và tài liệu kia chỉ trỏ tới:

| Entity | Nguồn sự thật |
|---|---|
| `User` · `ExternalLogin` · `UserShop` · `Role` · `PendingRegistration` | `03-identity-entity-design.md` |
| `Shop` (đầy đủ, gồm `Kind`/`ExternalUrl`) · `ServiceCategory` · `Listing` · `Review` · `Lead` | `04-listing-and-review-design.md` |
| `ShopDomain` | Quyết định #7 (tài liệu này) |
| Quyết định kiến trúc, ràng buộc dependency, quy ước codegen | tài liệu này |

---

## 4. Ràng Buộc Dependency

### Backend
```
Domain ← Application ← Infrastructure ← Api

· Domain: không reference gì ngoài BCL. Không EF Core, không MediatR.
· Module KHÔNG reference project của module khác.
· Cross-module: Integration Event hoặc Public Contract interface.
```

### Frontend
```
apps → packages       (packages KHÔNG BAO GIỜ import từ apps)

· builder-* không import từ apps — phải dùng được độc lập
· builder-renderer KHÔNG import builder-core
  (renderer phải chạy được ở apps/web, nơi không có editor — xem Quyết định #22)
· builder-renderer phải isomorphic (chạy được cả SSR lẫn CSR)
  — xem invariant ở Quyết định #23
```

---

## 5. Checklist Khi Thêm Operation Type Mới

Thêm operation type mới vào `builder-core` phải cập nhật **đồng thời 6 chỗ**. Thiếu một → coi như chưa xong.

1. Operation type union
2. Zod schema của operation
3. `apply()` reducer
4. `invert()` cho undo
5. AI tool schema ở `packages/ai-agent`
6. Test cho cả `apply` và `invert`

---

## 6. Điểm Còn Trống Cần Chốt

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | ~~Con số cụ thể cho ngưỡng migrate Elasticsearch~~ | ✅ Đã resolve — dùng Elasticsearch từ MVP, xem Quyết định #26 |
| 2 | ~~Social login trên custom domain: bật ở Phase 2 hay giữ username/password~~ | ✅ Đã resolve — bật từ MVP, xem Quyết định #6 (updated) |
| 3 | Cấu trúc token audience theo shop (claim nào, TTL bao nhiêu) | ✅ Đã resolve phần claim — xem Quyết định #27. TTL giữ theo #3 |
| 4 | ~~Merge userId theo email/SĐT chưa verify — rủi ro account-takeover xuyên shop~~ | ✅ **Đã resolve** — bỏ credential khỏi `UserShop`, không merge. Xem Quyết định #29/#30 và `03-identity-entity-design.md` |
| 5 | **[MỚI]** Zalo user ID là app-scoped — chốt dùng 1 Zalo app duy nhất hay coi mỗi app là 1 Provider | ⚠️ Cần chốt trước khi tích hợp Zalo Login |
| 6 | **[MỚI]** Xoá tài khoản: giải phóng `EmailNormalized` thế nào để user đăng ký lại được bằng email cũ | ⚠️ Cần chốt trước launch |
| 7 | **[MỚI]** Permission matrix chi tiết theo Shop role (Manager / Staff / Accountant được làm gì) | ⏳ Phase 4, cùng tính năng "Phân quyền nhân viên" (Quyết định #39) |
| 8 | **[MỚI]** Mô hình giá cho hai luồng doanh thu (thuê bao / theo listing / theo lead) | ⚠️ Entity đã sẵn sàng cho cả ba — cần chốt trước khi bật thu phí |
| 9 | **[MỚI]** Ngưỡng và quy trình kiểm duyệt listing (auto-approve tới khi nào) | ⚠️ Cần chốt trước launch |
| 10 | **[MỚI]** SLA xử lý khiếu nại đánh giá (shop báo cáo → vsite phản hồi trong bao lâu) | ⚠️ Rủi ro pháp lý — cần chốt trước launch |
| 11 | **[MỚI]** Ảnh trong đánh giá: hạ tầng kiểm duyệt + strip EXIF + hàng đợi gỡ bỏ 24h | ⏳ Phase 2, điều kiện đủ tại `04` §6.7 |
| 12 | **[MỚI]** `ShopServiceGroup` — nhóm dịch vụ hiển thị per-shop, nguồn cho menu website (Quyết định #34) | ⏳ Thiết kế cùng module `Service` ở Phase 2 |
