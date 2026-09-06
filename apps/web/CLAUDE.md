# apps/web — vsite

**SSR** bằng TanStack Start (Quyết định #22, #23). Gồm ba loại trang, đều render trong app này:

| | URL | Render bởi | Hiển thị `Review` |
|---|---|---|---|
| **Main** | `vsite.vn` (tìm kiếm, bản đồ, landing SEO) | code thường | — |
| **Shop Profile** | `vsite.vn/shop/{slug}` | vsite, mẫu thống nhất | ✅ |
| **Shop Site** | `spa-abc.com`, `{slug}.vsite.vn`, `vsite.vn/{slug}` | `builder-renderer` (Component Tree) | ❌ tuyệt đối không |

⚠️ **Shop Profile ≠ Shop Site.** Đây là chỗ dễ nhầm nhất — thấy "trang của shop" là dễ với tay sang `builder-renderer`. Shop Profile **không** dùng `builder-renderer`, nó là code thường như Main.

## SEO (Quyết định #10)

- Mỗi shop chỉ có **một** primary domain (bảng `ShopDomain`). Hai dạng còn lại phải **301 redirect** về primary.
- `<link rel="canonical">` luôn trỏ về primary domain.
- `robots.txt` + `sitemap.xml` sinh riêng theo tenant, chỉ chứa primary URL.

## Tenant resolution (Quyết định #7)

Middleware/loader phải resolve `TenantContext { ShopId, DomainKind, BasePath }` theo thứ tự: custom domain → subdomain → path-based → platform route. Không tự đoán tenant bằng cách khác.

## SSR

`apps/web` **phải** SSR — Main và Shop đều cần SEO.

- **Lock version chính xác** của TanStack Start trong `package.json`. Không dùng `^` hay `~` (Quyết định #23).
- Code chạy ở cả server lẫn client. Không đụng `window`/`document` trong logic render chính; chỉ dùng trong effect chạy sau hydrate.
- Data fetching qua loader của TanStack Start/Router, không fetch trong `useEffect` cho nội dung cần index.

---

## Reserved routes

Import trực tiếp từ `config/reserved-routes.json` (Vite hỗ trợ import JSON native). **Không viết tay danh sách thứ hai** — lệch với BE là lỗi routing/bảo mật thật (Quyết định #24).

Tenant resolution theo host/path đọc từ chính file đó.

---

## API

```
contracts/openapi/*.json  ──pnpm gen:api (Orval)──→  packages/api-sdk/
```

- **Không sửa tay bất cứ file nào trong `packages/api-sdk/`.** Có header `// GENERATED — DO NOT EDIT`.
- Contract sai hoặc thiếu → **DỪNG và báo**. Không tự sửa contract, không workaround.
- Code với **MSW mock trước**, chỉ bật API thật ở bước integration. Mock sinh từ chính contract nên nếu UI chạy với mock thì chắc chắn không bám vào hành vi ngoài contract.
- Dùng **API model** từ `api-sdk`, không tự định nghĩa lại type để "mirror" entity của BE.

---

## State (Quyết định #20)

| Loại | Sở hữu bởi |
|---|---|
| Server state (shop, listing, review, search result) | **TanStack Query** |
| Client / UI state (modal, filter chưa submit, bước wizard) | **Zustand** |

**KHÔNG copy dữ liệu từ Query vào Zustand.** Thấy cần làm vậy nghĩa là thiết kế đang sai.

---

## Quy ước

- TypeScript **strict**. Enum dùng `const object + union type`, **không** dùng `enum` của TS (#19).
- Validation dùng Zod schema **sinh từ contract**, không viết tay lại.
- Form: React Hook Form + Zod resolver.
- i18n: `react-i18next`. Error message map từ `error_code` trong ProblemDetails, không parse text.
- Mọi màn hình có dữ liệu: **loading · error · empty** đủ cả ba. Không bỏ empty.

---

## Phạm vi ghi

| | |
|---|---|
| ✅ Được ghi | `apps/web/**` · `packages/ui` · `packages/shared` · `packages/theme-engine` |
| ❌ Cấm ghi | `packages/api-sdk/**` (generated) · `backend/**` · `contracts/**` · `config/**` · `DesignIdeal/**` |

---

## Phase 1 — chưa có gì trong này

Ở Phase 1, `apps/web` chỉ có **Main** và **Shop Profile**. `builder-renderer` và Shop Site là Phase 2. Đừng scaffold sẵn route cho builder.

## Version pin

**Lock version chính xác cho TanStack Start** trong `package.json` — không dùng `^`/`~` (Quyết định #23, lý do: TanStack Start chưa 1.0, API còn đổi).

## State ownership (Quyết định #20)

Server state (shops, listings, reviews) → TanStack Query. Client/UI state → Zustand. Không copy dữ liệu từ Query sang Zustand.

## Reserved routes

Import `config/reserved-routes.json` build-time (Vite JSON import native). Đây là nguồn duy nhất — không viết tay danh sách thứ hai (Quyết định #24).