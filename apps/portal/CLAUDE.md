# apps/portal — vsite

**CSR thuần** — Vite + TanStack Router, **không** cài TanStack Start (Quyết định #22, #23). Không SSR: luôn sau login, không cần SEO.

Domain: `admin.vsite.vn/*` (Quyết định #25) — origin riêng biệt hoàn toàn với `apps/web`.

## Gồm hai phần

1. **Quản trị shop** — tin đăng, lead, đánh giá, hồ sơ shop (Phase 1); dịch vụ/hàng hoá (Phase 2).
2. **Website Builder** — route `/website` (Phase 2), kéo-thả + AI Chat. Không phải app riêng.

## Không SSR

Portal luôn nằm sau login, không được search engine index, không có yêu cầu SEO nào. Chạy SSR ở đây chỉ tốn compute vô ích (Quyết định #23).

Không cài TanStack Start vào app này. Không viết code phòng hờ cho SSR.

---

## Phạm vi theo Phase

| Phase 1 | Phase 2 |
|---|---|
| Quản lý **tin đăng** (`Listing`) | Dịch vụ (`Service`) |
| Xem **lead** và thống kê cơ bản | Hàng hoá (`Product`) |
| **Phản hồi đánh giá** (`Review`) | **Website Builder** — route `/website` |
| Hồ sơ shop | Thư viện media (`MediaAsset`) |

**Website Builder không phải app riêng** — nó là một route bên trong portal, vì bản chất cũng là công cụ quản trị (Quyết định #22). Phần *output* của builder (website khách xem) nằm ở `apps/web`.

Ở Phase 1: **đừng scaffold route `/website`**, đừng cài `dnd-kit`/`Lexical`/`zundo`/`immer`. Chúng chỉ vào khi Phase 2 bắt đầu.

---

## Quyền và phản hồi đánh giá

Shop **phản hồi** đánh giá qua portal, cần audience `vsite-portal` + role `Owner`/`Manager` tại shop đó. Đây là thao tác quản trị.

Shop **không** viết được `Review` (việc đó cần audience `vsite-main`) và **không** có quyền gỡ đánh giá — chỉ báo cáo vi phạm.

---

## API

```
contracts/openapi/*.json  ──pnpm gen:api (Orval)──→  packages/api-sdk/
```

- **Không sửa tay `packages/api-sdk/`.**
- Contract sai hoặc thiếu → **DỪNG và báo**.
- Code với **MSW mock trước**, bật API thật ở bước integration.
- Access token giữ **trong memory**, không dùng `localStorage` (Quyết định #3). Refresh qua Axios interceptor tự viết.

---

## State (Quyết định #20)

| Loại | Sở hữu bởi |
|---|---|
| Server state (listing, lead, review, hồ sơ shop) | **TanStack Query** |
| Client / UI state (modal, bước wizard, filter chưa submit) | **Zustand** |
| *(Phase 2)* Component Tree draft, selection trong builder | **Zustand** + `zundo` cho undo |

**KHÔNG copy dữ liệu từ Query vào Zustand.** Thấy cần làm vậy nghĩa là thiết kế đang sai.

---

## Quy ước

- TypeScript **strict**. Enum dùng `const object + union type`, không dùng `enum` của TS (#19).
- Bảng dữ liệu dùng TanStack Table.
- Validation dùng Zod schema **sinh từ contract**.
- Error message map từ `error_code` trong ProblemDetails.
- Mọi màn hình có dữ liệu: **loading · error · empty** đủ cả ba.

---

## Phạm vi ghi

| | |
|---|---|
| ✅ Được ghi | `apps/portal/**` · `packages/ui` · `packages/shared` · `packages/theme-engine` |
| ❌ Cấm ghi | `packages/api-sdk/**` (generated) · `backend/**` · `contracts/**` · `config/**` · `DesignIdeal/**` |

*(Phase 2 mở thêm: `packages/builder-core`, `packages/builder-components`, `packages/ai-agent`.)*


## State ownership (Quyết định #20)

Server state (shops, listings, leads) → TanStack Query. Client/UI state (modal, wizard step, filter chưa submit, Component Tree draft, selection) → Zustand (+ zundo cho undo, Phase 2). **Không copy dữ liệu từ Query sang Zustand** — nếu thấy cần thì thiết kế sai.

## Reserved routes

Import `config/reserved-routes.json` build-time. Nguồn duy nhất, không viết tay danh sách thứ hai (Quyết định #24).

## Packages Phase 2 (chưa dùng ở Bước 1)

`builder-core` (state + Operations Engine) và `ai-agent` (prompt, tool schema) **chỉ** dùng bởi `apps/portal`, không phải `apps/web`. Chưa tạo ở Bước 1 — xem `DesignIdeal/ai-agent-development-workflow.md` §15.
