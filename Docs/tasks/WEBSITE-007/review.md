# WEBSITE-007 — Review toàn nhánh trước Gate 2

> `change-reviewer` (model mạnh nhất), 2026-11-21. Phạm vi `f6d2a82..cc50cf3` (S1 BE T1–T5 + S2 FE F1–F5), chỉ đọc.
> Mục "Xử lý sau review" ở cuối do controller ghi.

## Kết luận

**Không có Critical.** Không vi phạm tenant #21; contract `website.v1` khớp BE, SDK sinh ra và body FE gửi; không vi phạm
#1/#23; không sửa tay file generated; banner `05`, #113, changelog đã có.

**Đạt Gate 2 sau F6**, với điều kiện xử lý W1 (phần FE), W2, W3, W5, W6. W4 (409 làm mất bản đang sửa) và phần BE của W1
là việc người quyết ở Gate 2.

> **Cập nhật 2026-11-22:** W1 (FE), W2, W3, W5, W6 đã xử lý (`8997a49`). F6 nghiệm thu 4/4 ĐẠT. **Gate 2 đóng** — W4, W1
> phần BE và "xoá mục cha" người chấp nhận như hiện tại, đưa vào feedback sau merge (`changelog.md` mục "F6").

## Critical

Không có. Đã kiểm:
- **Tenant #21:** `NavigationConfig` có `ShopId` + FK ghép `(website_id, shop_id)`. Mọi query (`GetNavigationHandler`,
  `SaveNavigationHandler`, `NavigationQueries.LoadPagesAsync`, Publish, Rollback) có `ShopId` từ `ITenantContext` trong
  câu query + Global Query Filter. Trang trong menu tra theo `ShopId + WebsiteId` ⇒ trang shop khác ra
  `NAV_PAGE_NOT_FOUND` (R1, có test). Body không có `shopId`. Route `RequireShopOwner`; Portal bọc `OwnerOnlyGate`.
- **Contract:** 3 DTO, `version: number|null`, `label`/`url` nullable, cấp 3 không `children` + `additionalProperties:false`
  — khớp `website.v1.json`, generated và `toRequestItems`. Lock sha256 `67c95b31…` khớp.
- **#23:** `SiteLayout` chỉ CSS + `<details>`; `ssr-hydrate` có ca menu 3 cấp.
- **#103:** URL trang chỉ qua `resolveUrl({kind:'page'})`; `site-view.tsx` dùng một `resolveUrl`.
- **XSS:** nhãn là text; `custom` lọc lại bằng `isSafeLinkUrl`; http(s) có `target=_blank rel="noopener noreferrer"`.

## Warning

### W1. BE có thể trả menu mà chính BE không cho lưu (> 12 mục cấp 1); FE không chặn trước
- BE: tự sinh từ **mọi** trang (`NavigationItems.cs:82`) và nối trang mới vào cuối cấp 1 `hidden:true` (`:98`), không
  trần; `Validate` (`:115`) đếm `items.Count > 12` **tính cả mục ẩn**. ⇒ shop ≥ 13 trang không lưu được menu tự sinh;
  shop đủ 12 mục tạo thêm trang (Flow 3) là hết lưu được. Publish không `Validate` nên snapshot có thể > 12 mục.
- FE: `validateMenu` (`navigation-tree.ts:309`) không kiểm số mục cấp 1 ⇒ 422 `path:"/items"` chỉ hiện lỗi chung.
- Đề xuất trước merge: thêm check > 12 vào `validateMenu` với câu rõ + test GET trả 13 mục.
- **Người quyết ở Gate 2 (BE/contract):** mục ẩn có được miễn giới hạn 12 không; tự sinh có cắt ở 12 không.

### W2. `apps/web` "không đọc draft": lint không chặn API menu
`eslint.config.mjs:14-37` `WEBSITE_DRAFT_SDK_NAMES`/`WEBSITE_DRAFT_MSW_NAMES` chưa có `getNavigation`, `saveNavigation`,
`useGetNavigation`, `useSaveNavigation`, key/options/mutation helpers, schema zod, mock MSW. Không test nào kiểm danh sách
đủ. Sửa trước merge; lâu dài nên sinh tự động hoặc test so với export generated (#17).

### W3. Tài liệu còn ghi "FE chưa"
`DECISIONS.md:161` (#113 "FE (S2) chưa") · `plan.md:3` banner, checkbox §11 · `changelog.md` trỏ tới mục "Đóng S2" và
"mục F6" chưa tồn tại.

### W4. 409 sau **mọi** publish/rollback có chạm dòng menu; 409 bỏ hết bản đang sửa
`PublishWebsiteHandler.cs:181` `MarkPublished()` và `RollbackPublicationHandler.cs:58` `MarkDirty()` đều đổi `xmin`. Tab A
mở màn Menu, tab B publish/rollback, tab A Lưu ⇒ 409 ⇒ `reloadAfterConflict` bỏ thay đổi chưa lưu. `brief.md` và
`backend/docs/modules/website.md:141` chỉ nhắc ca "chưa từng lưu". Publish/rollback không đổi `Items`, nên "Giữ bản của
tôi" (GET lấy `version` mới rồi PUT lại bản local) an toàn — **Gate 2 quyết** có thêm không.

### W5. Comment `JsonSafe` ở `apps/web/src/server/load-site-core.ts:23-26` sai
Lấy `navigation.items` làm ví dụ `unknown`; lý do thật là `tracking`.

### W6. `pnpm test` toàn repo đỏ ở `full-test.log`
`gen-registry` byte-identical > 60 s; portal `-editor-autosave` > 5 s — flake khi chạy song song (TOOLING-004). Chạy lại
và ghi vào changelog.

## Suggestion

- S1. `site-layout.tsx:4-8`: doc comment `SiteLayout` nằm trên `SiteMenuItem`.
- S2. `site-preview.tsx:154`: link `custom` tương đối (`/khuyen-mai`) bị `matchSiteUrl` coi là `deleted` ⇒ dải "trỏ tới
  trang đã xoá" gây hiểu lầm.
- S3. `useMenuDirty` báo "chưa publish" cả với website chưa publish lần nào (G6) — cân nhắc ẩn khi `currentPublicationId === null`.
- S4. Chính sách query "editor nháp" lặp ở editor-shell, use-theme-draft, navigation-editor; bẫy `gcTime` gộp `Math.max`
  chỉ lộ ở review F5 ⇒ gom `draftQueryOptions()` dùng chung, có test.
- S5. Không có test khẳng định rollback invalidate `getNavigation` (`publication-history.test.tsx` chỉ thêm handler).
- S6. `navigation-editor.tsx` ~750 dòng — tách `NavRow`, `treeKeyboardCoordinates`.

## Triage các Minor đã hoãn

| Nguồn | Minor | Quyết |
|---|---|---|
| F4 | `validateMenu` chưa chặn > 12 mục cấp 1 | Sửa trước merge (W1) |
| F3 | Comment `JsonSafe` | Sửa trước merge (W5) |
| F2 | Doc comment `SiteLayout` lạc chỗ | Nên sửa cùng (S1) |
| F4 | Xoá cha xoá cả con, không hỏi lại | Người quyết ở Gate 2 (UX) |
| F4 | 409 bỏ bản đang sửa | Người quyết ở Gate 2 (W4) |
| F2 | `.vs-nav-mobile>ul` absolute, header không `position:relative`; vùng chạm ▸ nhỏ; ▸ chưa `aria-hidden` | F6 kiểm; lệch thì sửa trong nhánh |
| F5 | GET lúc mở lỗi mà còn cache cũ ⇒ form từ cache cũ | Theo dõi (tối đa một lần 409 rồi tự tải lại) |
| F5 | Invalidate trong `onSettled` cả khi lỗi | Không cần làm |
| F4 | Tràn 10 mục/cha chỉ báo sau khi thả; React key là message; file dài | Theo dõi |
| F2 | 767px/rem cứng, aria tiếng Việt cứng, `<style>` lặp, thiếu test rel lồng sâu, `as never` | Theo dõi (đa ngôn ngữ: bước L) |
| F1 | `href`/nhãn `custom` không trim; không trần độ sâu; thiếu test `tel:`, `custom` có con | Theo dõi (BE `Normalize` đã trim) |
| F3 | Test R6 `not.toContain('<nav')` dễ gãy | Theo dõi |
| T2 | `Parse` ném `JsonException`; `Merge` không lọc dữ liệu hỏng; `ReadArray` bỏ cấp sâu im lặng; comment cũ; `ForSnapshot` không trần độ sâu | Theo dõi (chỉ dữ liệu hỏng) |
| T2 | Thiếu test độ sâu 3 | Theo dõi (changelog + 00-INDEX §4) |
| T4 | Publish thêm 2 query trang; schema chưa ép "`url` chỉ ở `custom`" | Theo dõi |
| T3 | `params` 422/409 không có type; bind body có kiểu 500/400 rỗng (có từ trước) | Theo dõi (00-INDEX §4) |

## F6 phải kiểm cụ thể

1. **Luồng chính:** sắp menu 3 cấp, có link Fanpage `https://…` và một `tel:`, publish, mở `{slug}.vsite.local`: thứ tự
   đúng; nhãn rỗng ⇒ tên trang; Fanpage mở tab mới có `rel`, `tel:` cùng tab; dropdown desktop mở bằng hover **và** Tab,
   tới cấp 3, mục cuối bên phải không tràn mép; mobile (≤ 767px): ☰ mở, ▸ mở con mà không đi link cha, panel đúng chỗ khi
   đã cuộn, vùng chạm đủ; tắt JS vẫn chạy; không cảnh báo hydrate.
2. **Kéo-thả trình duyệt thật:** chuột + bàn phím (Space, ↑↓, ←→); chặn cấp 4 khi kéo mục có con; thông báo thả sai chỗ.
3. **Flow 3:** trang mới ⇒ cuối, "Đang ẩn"; xoá trang trong menu ⇒ mất ở màn Menu và site sau publish; tắt trang có con ⇒
   cả nhóm mất ở site **và** preview (R5); shop có 12 mục cấp 1 tạo thêm trang rồi Lưu ⇒ thông báo rõ (W1).
4. **Rollback:** về bản trước Bước 7 (`items: []`) ⇒ header không menu, không lỗi parse (R6), tổng quan + hộp publish báo
   "Menu có thay đổi chưa publish"; về bản có menu ⇒ site hiện menu cũ.
5. **Hai tab:** (a) cùng lưu ⇒ tab sau 409 có thông báo, form tải lại; (b) tab A `version:null`, tab B publish, tab A Lưu ⇒
   409; (c) tab A đã lưu và để mở, tab B publish/rollback, tab A sửa tiếp rồi Lưu ⇒ 409 (W4 — ghi lại cho người quyết).
6. **Preview Portal:** menu nháp khớp site; click menu không rời editor; dropdown không bị `overflow-y-auto` cắt; link
   `custom` tương đối hiện gì (S2).
7. **Cache:** lưu menu, về tổng quan ⇒ dấu "chưa publish" bật ngay; publish xong ⇒ tắt; mở lại màn Menu sau publish không 409 giả.
8. **Integration tắt MSW (plan §11):** `getNavigation`/`saveNavigation` qua proxy `/api`; 403 với Manager; 404 với shop chưa có website.

## Ma sát quy trình

1. **Đổi phạm vi giữa chừng** (1 cấp → 3 cấp khi đang T3): viết đè plan/data-needs/#113, làm lại T2/T3, tách 3 DTO vì
   `Microsoft.AspNetCore.OpenApi` 9 sinh `items:{}` cho DTO tự tham chiếu — nên ghi thành ràng buộc ở `backend/CLAUDE.md` §OpenAPI.
2. **Plan dùng tiêu đề `### F<n>:`** thay vì "Task N": controller phải tự dựng brief cho subagent. Thống nhất định dạng trong template plan.
3. **Không smoke được màn có dữ liệu sau F4:** Portal không có MSW trên trình duyệt ⇒ dồn smoke, kéo-thả thật, CSS mobile,
   hai tab vào F6. Cân nhắc chế độ `VITE_MSW=1` cho Portal.
4. **jsdom không có layout:** test bàn phím dnd-kit phải giả `getBoundingClientRect`.
5. **`pnpm test` toàn repo flake khi chạy song song** (gen-registry 60 s, autosave portal 5 s, RichTextEditor `aria-pressed`)
   — "xanh toàn repo" không còn là tín hiệu tin được; đáng một task riêng.
6. **Danh sách cấm import viết tay** (`WEBSITE_DRAFT_SDK_NAMES`) bị quên khi thêm endpoint (W2) — đúng loại việc #17 bảo đẩy lên codegen/test.
7. **Bẫy TanStack Query** `gcTime` gộp `Math.max` giữa observer cùng key, lộ ở review F5 sau khi F4 đã sửa chính sách query một lần (S4).
8. **Bind body có kiểu trả 500/400 rỗng** (có từ trước) buộc `saveNavigation` đọc body thô.
9. **Workaround cũ trong `website.md`** (`OutputPath` ngoài repo) làm ~270 test Website đỏ giả ở T1.
10. **T5 ban đầu không có `brief.md`** trong checklist ⇒ viết bù. Nên đưa "brief.md BE→FE" vào checklist kết thúc S1 của Lane C.
11. **Dòng nhiễu `ReferenceError: module is not defined`** trong vitest `apps/web` (exit 0, có từ trước) bắt mọi reviewer kiểm lại.

## Xử lý sau review (controller)

| Mục | Xử lý |
|---|---|
| W1 | FE sửa (`8997a49`): `validateMenu` chặn > 12 mục cấp 1 (tính cả ẩn), không gửi PUT, có test. **Phần BE (mục ẩn có tính vào 12 không, tự sinh có cắt ở 12 không) — người quyết ở Gate 2** |
| W2 | Sửa (`8997a49`): thêm export menu (SDK, zod, MSW) vào danh sách cấm; thử import tạm ⇒ lint đỏ |
| W3 | Sửa (tài liệu): #113, banner plan, changelog mục "Đóng S2" + "F6" |
| W4 | `brief.md` bổ sung. `backend/docs/modules/website.md:141` **chưa sửa** (S2 không sửa `backend/`). "Giữ bản của tôi" khi 409 — **người quyết ở Gate 2** |
| W5 | Sửa (`8997a49`) |
| W6 | `pnpm test` toàn repo chạy lại: 10/10 xanh; sửa gốc một flake có từ trước (`68e2ed3`, RichTextEditor) |
| S1, S5 | Sửa (`8997a49`); S5 test pass ngay — chỉ thiếu test |
| S2, S3, S4, S6 | Theo dõi (00-INDEX §4) |
