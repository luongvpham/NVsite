# WEBSITE-007 — changelog (Bước 7: `NavigationConfig`, menu website shop)

> **Trạng thái:** S1 (BE) xong T1–T5, Gate 1 đã qua (#89, chỉ `NEW_ENDPOINT`, promote 2026-11-21 —
> [`contract-diff.md`](contract-diff.md)). S2 (FE) xong F1–F5 (2026-11-21). F6 nghiệm thu tay với BE thật **4/4 ĐẠT**
> (2026-11-22). **Gate 2 đóng 2026-11-22** — câu hỏi còn mở chuyển sang danh sách feedback sau merge (mục "F6").
> Nhánh `feature/WEBSITE-007`. Spec: `DesignIdeal/05` §8, §7 · plan [`plan.md`](plan.md) · #113.

## Đổi phạm vi giữa chừng (người chốt 2026-11-21)

Plan chốt 2026-11-20 là menu con **một cấp**. Khi làm T3, người đổi thành **tối đa 3 cấp**
(cấp 1 → con → cháu), và contract diễn đạt độ sâu bằng **ba kiểu DTO tường minh**. Lý do kỹ thuật:
`Microsoft.AspNetCore.OpenApi` 9.0.9 sinh `children: { items: {} }` cho DTO tự tham chiếu, nên Orval ra
`unknown[]`. Plan §1/§4/§12 G4, `data-needs.md` và #113 đã được viết đè (commit `3b569eb`). T2 và T3 làm lại
cho 3 cấp. Giới hạn: ≤ 12 mục cấp 1, ≤ 10 mục mỗi cha ở cấp 2 và 3.

## T1 — entity `NavigationConfig` + bảng (`c59cb64`)

- `NavigationConfig : TenantAuditableEntity`. PK `Id` = website id (cột `website_id`, `WebsiteId` chỉ là bí danh
  đọc như `Theme`). `Items` jsonb (document `{ schemaVersion: 1, items }`), `IsDirty`, `Version` = `xmin`.
  Bảng `navigation_config`, FK ghép `(website_id, shop_id) → website (id, shop_id)` (#92). Migration
  `20261120161227_AddNavigationConfig`.
- `DocumentKind.Navigation` / `DocumentSchemaVersions.Navigation = 1` trong `DocumentVersioning`.
- **Giả định tự đặt:** `Create` khởi tạo `IsDirty = true` (như `PageDraft`). `Replace` với chuỗi rỗng ném
  `DomainException NAVIGATION_ITEMS_REQUIRED`. Index FK mặc định `ix_navigation_config_website_id_shop_id`
  trùng tiền tố PK, giữ cho giống `theme`/`page_draft`.
- **Không bao giờ xoá mềm `NavigationConfig`.** Entity kế thừa soft delete nhưng PK là `website_id`. Một dòng
  đã xoá mềm bị filter giấu mà vẫn giữ PK, nên mọi lần lưu sau sẽ là 409 vĩnh viễn. Hiện chỉ có comment
  trong `SaveNavigationHandler` chặn chuyện này, chưa có test.

## T2 — ghép + kiểm menu, hàm thuần (`0f0a59d`, `212acb5`, `a592aca`)

`Vsite.Application/Website/Navigation/NavigationItems.cs`: `NavItem`, `NavPage`, `NavError`, `NavigationErrorCodes`;
`Merge`, `Normalize`, `Validate`, `ForSnapshot`, `Parse`, `Serialize`, `ToJsonArray` (document lưu, **có**
`hidden`), `ToSnapshotJsonArray` (snapshot, **không** `hidden`; `children` chỉ ở cấp 1–2 và chỉ khi khác rỗng).

- **Lệch có chủ đích so với plan §9 T2:**
  - Thêm `Normalize`: trim nhãn/URL, nhãn rỗng thành `null`, mục `page:` bỏ `url`. Từ T4, hàm này còn đưa khoá `page:{guid}` về
    **GUID chữ thường**. Lý do: FE so khoá với `pages[].id` chữ thường; GET/PUT trả khoá chữ thường dù client gửi chữ hoa.
  - `ToSnapshotJsonArray` **không tự lọc**. Phải gọi `ToSnapshotJsonArray(ForSnapshot(Merge(...)))`.
- **Giả định tự đặt:**
  - Khoá `page:` sai dạng ⇒ `NAV_PAGE_NOT_FOUND`.
  - URL rỗng hoặc dài quá 2048 ⇒ `NAV_URL_UNSAFE`.
  - Trùng trang được báo ở lần xuất hiện **thứ hai**, xét xuyên mọi cấp.
  - Dữ liệu lưu sâu quá 3 cấp bị bỏ khi đọc (có comment ở code). Trang nằm trong phần bị bỏ sẽ quay lại cuối cấp 1, ở trạng thái ẩn.
- **Test:** `NavigationItemsTests`. Test `Normalize` chữ thường (T4) được viết cùng lúc với code, **không có bằng chứng RED**.

## T3 — `GET` / `PUT …/website/navigation` (`b8ebd97`, `1315775`, `de414c7`)

- Route `getNavigation` / `saveNavigation`, Owner-only (#100). Bảng mã lỗi ở `contract-diff.md` và
  `backend/docs/modules/website.md`.
- **Lệch có chủ đích:**
  - **Ba kiểu DTO** thay cho một DTO đệ quy (xem "Đổi phạm vi"). Cấp 3 dùng `JsonUnmappedMemberHandling.Disallow`,
    nên gửi cấp 4 nhận **400 `NAVIGATION_BODY_INVALID`**. `NAV_TOO_DEEP` chỉ còn ở `Validate` và không bao giờ ra qua HTTP.
  - `PUT` **đọc body thô có trần** (`ReadBoundedBodyAsync`, 256 KB ⇒ 413 `NAVIGATION_BODY_TOO_LARGE`), như
    `savePageDraft`/`updateTheme`; `.Accepts<SaveNavigationRequest>` giữ type cho OpenAPI. Lý do: bind body có kiểu
    khi lỗi trả **500 ở Development và 400 rỗng ở Production**, không có `error_code` (xem "Chưa làm xong").
  - `key` và `hidden` là `[JsonRequired]` ở cả ba cấp. Trước đó thiếu `hidden` bị hiểu thành `false`, làm hiện lại mục đang ẩn.
  - `detail` của 400 chỉ chứa đường dẫn cấu trúc: chỉ số mảng và tên field đã biết, tên lạ thay bằng `?`, tối đa 200 ký tự.
    Không lặp lại input.
- **Giả định tự đặt** (A1–A9 ở `contract-diff.md`):
  - 422 `NAVIGATION_INVALID` dùng vỏ của `THEME_INVALID`.
  - 409 `params { currentVersion, updatedAt }` đều null khi chưa có dòng.
  - Chưa có website ⇒ 404 `WEBSITE_NOT_FOUND`.
  - Mọi 23505 khi lưu ⇒ 409, vì unique duy nhất trên bảng là PK.
- `ShopScopedRouteFilterTests.expectedMinimum` 10 → 16 (số route website/media thật).

## T4 — publish / rollback mang menu + shape snapshot (`fa279ce`)

- `envelope-schema.ts`: `navigation.items` từ `z.array(z.unknown())` thành shape 3 cấp, strict ở mọi cấp
  (`{ key, label: string|null, url?, children? }`, cấp 3 không `children`). Zod ≡ JSON Schema dựng từ cùng
  `NAV_KEY_PATTERN`. Kiểu TS `SiteNavigationItem` dùng chuỗi template, không dùng `enum` (#19). `schemaVersion` giữ **1** (additive);
  snapshot `items: []` cũ vẫn hợp lệ (R6).
- `PublishWebsiteHandler`: kiểm `PUBLISH_BLOCKED` trước (menu không bao giờ chặn publish) → `Merge` →
  `ForSnapshot(trang có trong snapshot)` → `SnapshotBuilder.Build(…, items)`. Chưa có dòng menu ⇒ tạo dòng từ menu tự
  sinh rồi `MarkPublished`; có dòng ⇒ `MarkPublished`. Tất cả trong một `SaveChanges`. Race với PUT menu đầu tiên (23505)
  hoặc xmin của menu ⇒ 409 `PUBLISH_CONFLICT`.
- `RollbackPublicationHandler`: có dòng menu ⇒ `MarkDirty()`; không có ⇒ không làm gì.
- `GET /api/site` không đổi; `PublishedSiteBoundaryTests` không sửa, vẫn xanh.
- **Hệ quả (chấp nhận, plan Flow 4):** publish khi menu chưa từng lưu sẽ **tạo dòng**. Tab đang mở màn Menu với
  `version: null` bấm Lưu sau đó sẽ nhận 409. FE invalidate query menu sau publish/rollback (F5).
- Publish thêm 2 query (nạp dòng menu + nạp trang cho `Merge`). Có thể gộp với query trang đang bật đã có.

## T5 — đóng S1 (2026-11-21)

- `dotnet test backend/vsite.sln`: **1024 pass, 0 fail, 1 skip** (skip có từ trước, `ShopEndpointTests`, không liên quan Docker).
  Chi tiết: IntegrationTests 990/991, ArchitectureTests 22, ComponentSchemaTests 12. Chạy với Docker thật, không có nợ
  `DOCKER-TEST-DEBT`. `@vsite/builder-components` 556/556 · `@vsite/api-sdk` 12/12 + `tsc` sạch.
- `contract-sync`: chỉ 2 `NEW_ENDPOINT`. Orval chạy thử trên staging không ra `…Dto2`. Promote
  (`gate1-pre-production-89`), `pnpm gen:api`.
- `website.md` (đã sửa ở đây): workaround "`Vsite.Api.exe` khoá DLL" cũ đặt `OutputPath` **ngoài repo**, làm ~270 test Website đỏ giả
  (`TreeFixtures` đi ngược lên tìm `pnpm-workspace.yaml`). Đã sửa thành `BaseOutputPath` trong repo.

## S2 — Frontend (2026-11-21)

Không sửa `backend/`, `contracts/`, `packages/api-sdk/src/generated/**`. Không có vòng `D{n}`: contract đủ field.

### F1 — `resolveNavigation` (`6812937`)
- `packages/builder-components/src/site-navigation.ts`, export ở `src/index.ts`. Đầu vào nhận cả mục snapshot
  (không `hidden`) lẫn mục nháp của GET (có `hidden`): mục `hidden: true` bị bỏ kèm cây con — để preview Portal và
  site dùng **một** hàm lọc.
- `page:{id}` so khớp `pages[].id` **không phân biệt hoa thường**, URL chỉ qua `resolveUrl({ kind: 'page', pageId })`
  (#103). Trang thiếu / slug null ⇒ bỏ, cha thiếu ⇒ bỏ cả nhóm (R5). `custom` chỉ giữ khi `isSafeLinkUrl` (R3),
  `external` = scheme http(s). Khoá lạ (nguồn tương lai) ⇒ bỏ.

### F2 — `SiteLayout` menu 3 cấp + mobile (`8b6c76f`, `22e81df`)
- Desktop: dropdown CSS `:hover`/`:focus-within`. Mobile: `<details><summary>` hamburger; mục có con dùng
  `<summary>` riêng (▸) cạnh link cha — sửa sau review (link nằm trong `summary` thì chạm vào là đi link, không mở được con).
- Link ngoài `target="_blank" rel="noopener noreferrer"`, nhãn là text (R4). Không JS; `ssr-hydrate` có ca menu (R7).
- Cả danh sách desktop lẫn mobile cùng nằm trong DOM, CSS ẩn/hiện ở 767px.

### F3 — `apps/web` hiện menu (`5987c9c`)
- `site-view.tsx` dựng `resolveUrl` một lần, truyền `resolveNavigation(snapshot.navigation.items, snapshot.pages, …)`.
- **Giữ** workaround `JsonSafe` ở `load-site-core.ts`: bỏ thì `tsc` đỏ vì `snapshot.tracking` (và cây trang) vẫn là
  `unknown`. Kiểu menu cụ thể chưa đủ.

### F4 — màn Menu Portal (`d5ac764`, `d7c3d97`)
- Route `/shops/$shopId/website/menu` bọc `OwnerOnlyGate`, lối vào ở `website-overview.tsx`. Thêm `@dnd-kit/sortable`.
- `navigation-tree.ts` (thao tác cây thuần) + `navigation-editor.tsx`. Kéo-thả nhiều cấp theo mẫu SortableTree của
  dnd-kit: danh sách phẳng có `depth`, đổi cấp bằng độ lệch ngang; chặn cấp 4 tính cả chiều cao cây con đang kéo.
  Bàn phím: ↑/↓ đổi chỗ, ←/→ đổi cấp.
- Gửi đủ `key,label,url,hidden,children` (cấp 3 không `children`, mục `page:` gửi `url: null`), `version` từ GET rồi từ PUT.
  Chặn giới hạn ở UI. 422 tô dòng theo JSON Pointer; 409 tải lại và báo. Thêm mã lỗi `NAVIGATION_*`/`NAV_*` vào `error-messages.ts`.
- Sau review: query menu dùng chính sách của editor nháp (`staleTime: Infinity`, `gcTime: 0`, không refetch khi focus,
  `retry: false`); refetch nền lỗi không còn thay form bằng màn lỗi.
- Smoke: `vite build` + dev server chạy, route nạp được. **Chưa xem màn có dữ liệu**: Portal không có MSW trên trình
  duyệt, cần BE ⇒ gộp vào F6.

### F5 — nhắc menu chưa publish + preview (`905077f`, `dc6ad8f`)
- Hook `use-menu-dirty.ts`; "Menu có thay đổi chưa publish" ở tổng quan website và hộp publish (ẩn sau khi publish xong).
- Invalidate `getNavigation` sau publish và rollback (`onSettled`).
- Preview: mục nháp + **chỉ trang đang bật** ⇒ `resolveNavigation`, khớp với site.
- Sau review: TanStack gộp `gcTime` theo `Math.max`, nên khi tổng quan đã giữ cache thì `gcTime: 0` của editor mất tác
  dụng ⇒ editor dựng form từ `version` cũ ⇒ 409 giả. Sửa: editor `refetchOnMount: 'always'` và chỉ dựng form sau lần
  GET của chính nó.

### Kiểm chứng S2
- `@vsite/builder-components` 573/573 · `@vsite/builder-renderer` 44/44 · `@vsite/web` 272/272 (dòng nhiễu cũ
  `ReferenceError: module is not defined`, exit 0) · `apps/portal` 415/415. Typecheck + lint sạch ở các gói chạm.
- Toàn repo `pnpm build && pnpm test`: xem mục "Đóng S2" bên dưới.
- Nghiệm thu tay F6: **4/4 ĐẠT** (2026-11-22) — xem mục "F6" bên dưới.

### Đóng S2 (2026-11-21)
- `change-reviewer` toàn nhánh ([`review.md`](review.md)): 0 Critical, W1–W6. Sửa trong nhánh (`8997a49`):
  `validateMenu` chặn > 12 mục cấp 1 (tính cả mục ẩn, như BE) và không gửi PUT (W1 phần FE) · thêm mọi export menu của
  SDK/zod/MSW vào danh sách cấm import của `apps/web` (W2 — đã thử import tạm `useGetNavigation` để thấy lint đỏ) ·
  comment `JsonSafe` (W5) · doc comment `SiteLayout` (S1) · test rollback refetch `getNavigation` (S5, chỉ thiếu test).
  Tài liệu: #113, banner plan, `brief.md` (W4 — 409 sau mọi publish/rollback chạm dòng menu) (W3).
- Flake có từ trước, sửa gốc (`68e2ed3`): test `RichTextEditor` khẳng định `aria-pressed` ngay sau `onChange`, trong khi
  toolbar cập nhật ở lượt render sau (`registerUpdateListener` → `setState`) ⇒ đỏ ngẫu nhiên khi `pnpm test` chạy song song.
  Còn flake `gen-registry` "byte-identical" > 60 s khi tải nặng (đã biết từ TOOLING-004) — không sửa ở đây.
- `pnpm build` 10/10 task (exit 0). `pnpm test` toàn repo lần 3 (sau `68e2ed3`): **10/10 task xanh**, exit 0 —
  builder-components 573 · builder-core 137 · builder-renderer 44 · web 272 · portal 415 · api-sdk 12 · shared 10 ·
  theme-engine 49. Sau `8997a49`: portal 419, web 272, builder-renderer 44 (chạy riêng từng gói). `pnpm check:docs` xanh.
- **Không sửa** `backend/docs/modules/website.md:141` (cùng nội dung W4) — người dặn S2 không sửa `backend/`.

### F6 — nghiệm thu tay với BE thật (2026-11-22): 4/4 ĐẠT

> Nhánh `feature/WEBSITE-007` @ `8462a2c`. Người nghiệm thu thao tác trên trình duyệt thật; agent đối chiếu DB
> (`navigation_config`) và HTML site qua Host header.
> - BE: `Vsite.Api` build lại từ nhánh (`:5270`) — process cũ (build 2026-11-19, chưa có endpoint menu) trả 404 cho
>   `…/website/navigation`. DB dev: người chạy migration `WebsiteRowVersion` + `AddNavigationConfig`.
> - Portal dev `admin.vsite.local:5173`, không MSW — `getNavigation`/`saveNavigation` qua proxy `/api` (plan §11
>   "integration tắt MSW"). Web dev `:3000` với `API_ORIGIN=http://localhost:5270 ROOT_DOMAIN=vsite.local`.
> - Shop `smokef21793979477503` (`2b250e9b-…`). Để publish được, agent xoá thẳng trong DB 11 khối rỗng (7 Gallery ở
>   "Trang blog"; 3 Gallery + 1 RichText ở "Trang about") — chúng cao 0px nên không chọn được trên canvas (feedback 2
>   bên dưới); bản sao cây cũ ở scratchpad của session. Người tự xoá khối ServiceGrid thiếu `source` (feedback 3).

| # | Bước (plan §10 F6) | Kết quả |
|---|---|---|
| 1 | Sắp menu 3 cấp (menu con + link custom) → publish → site hiện đúng; dropdown + mobile | **ĐẠT** — agent đối chiếu: thứ tự site khớp `navigation_config`, nhãn `null` ⇒ tên trang, trang đang tắt ("Liên hệ mới") bị bỏ khỏi site (R5), URL qua `createResolveUrl`, `isDirty` hạ sau publish. Người kiểm menu 3 cấp, dropdown, mobile: ổn; CSS/giao diện để feedback 4 |
| 2 | Trang mới ⇒ cuối, ẩn · xoá trang trong menu ⇒ site sau publish không còn | **ĐẠT** (người kiểm) |
| 3 | Rollback ⇒ site về menu cũ, màn Menu báo chưa publish | **ĐẠT** (người kiểm) |
| 4 | Hai tab cùng sửa ⇒ tab sau 409 có thông báo | **ĐẠT** (người kiểm) |

Smoke màn Menu có dữ liệu (checkbox F4 dồn sang đây) và integration tắt MSW (plan §11) đạt cùng các bước trên.

**Gate 2 — đóng 2026-11-22.** Người duyệt chấp nhận hành vi hiện tại cho ba câu hỏi còn mở (W1 phần BE, W4 "Giữ bản của
tôi", xoá mục cha không hỏi lại) và đưa chúng vào danh sách feedback sau merge.

**Feedback sau merge** (người sẽ mở task fix riêng từ `master` sau khi merge nhánh này):

| # | Vấn đề | Thuộc | Ghi chú |
|---|---|---|---|
| 1 | Lỗi publish chỉ in `message` ("Prop bắt buộc 'items' bị thiếu." × n), không nói khối nào | Portal, có từ WEBSITE-006 | BE đã trả `path` JSON Pointer; `publish-errors.tsx` bỏ qua. Dò `path` trên cây ⇒ "Khối n · {nhãn manifest} — thiếu “{nhãn prop}”" + nút "Chọn khối". Không chạm contract |
| 2 | Khối rỗng (Gallery không ảnh/tiêu đề, RichText trống) cao 0px trên canvas ⇒ không thấy, không chọn, không xoá được | Portal editor + component | Khung khối cần chiều cao tối thiểu + placeholder; cân nhắc bảng cấu trúc trang |
| 3 | ServiceGrid bắt buộc `source` nhưng ô `binding` ở Inspector là stub (Bước 9) ⇒ mọi Lưới dịch vụ chặn publish | builder-components | Đề xuất: ẩn ServiceGrid khỏi palette tới Bước 9 |
| 4 | CSS/giao diện menu (typography, kiểu dropdown, mobile) | builder-renderer | Nội dung đúng, chỉ trau chuốt |
| 5 | Gate 2 còn mở: mục ẩn tính vào giới hạn 12 + tự sinh không cắt ở 12 (W1, BE) · "Giữ bản của tôi" khi 409 (W4) · xoá mục cha có hỏi lại / giữ con | BE + Portal | Hiện chấp nhận như đang chạy |

## Lệch so với `DesignIdeal/05` §8

- Không có field `order`: thứ tự là vị trí trong mảng (G2).
- Một trang chỉ xuất hiện một lần trong menu (`NAV_PAGE_DUPLICATE`, G1).
- Menu lồng tối đa 3 cấp (05 không nói độ sâu).
- Nguồn mục chỉ có `page:{id}` và `custom`. `serviceGroup` để Bước 9; `sysProductListing`/`productCategory` để 10c.
- Chưa có nhãn đa ngôn ngữ `labels` (#98, bước L).
- ✅ *Job dọn mục mồ côi đã làm ở WEBSITE-008 (#118):* `website.retention` bỏ mục trỏ trang đã xoá khỏi menu không sửa trong 24h. Lúc đọc/render vẫn bỏ mục mồ côi như cũ.

## Chưa làm xong / theo dõi

- Feedback sau merge 1–5: xem mục "F6".
- Việc nhỏ FE còn để ngỏ (review S2, Minor): mở màn Menu mà GET lỗi khi đang có cache cũ thì form dựng từ cache cũ ·
  tràn 10 mục/cha chỉ báo sau khi thả · `navigation-editor.tsx` ~750 dòng · `href` của `custom` không trim.
  (Chặn > 12 mục cấp 1 và comment `JsonSafe` đã sửa ở `8997a49`.)
- Từ review toàn nhánh (theo dõi): link `custom` tương đối trong preview hiện dải "trang đã xoá" (S2) · dấu "chưa
  publish" hiện cả với website chưa publish lần nào (S3) · gom chính sách query "editor nháp" thành helper dùng chung (S4) ·
  `backend/docs/modules/website.md:141` chưa ghi 409 sau mọi publish/rollback (W4).
- Câu hỏi Gate 2 (W1 phần BE, W4, xoá mục cha): người chấp nhận hành vi hiện tại 2026-11-22, chuyển thành feedback 5.
- **Bind body có kiểu trả 500 (Development) / 400 rỗng (Production) không có `error_code`.** Lỗi có từ trước, ảnh hưởng
  mọi route nhận body có kiểu, ngoài phạm vi task. `saveNavigation` đã né bằng cách đọc body thô. Theo dõi ở `00-INDEX` §4.
- `params` của 422/409 không có type trong OpenAPI (giống các route website hiện có) (A6).
- Chưa có test chặn xoá mềm `NavigationConfig` (chỉ có comment).
- Phủ test thêm ở độ sâu 3 (reviewer T2):
  - tràn mục ở cấp 2;
  - nhãn/URL dài ở cấp 2–3;
  - `page-not-found` / `not-allowed` / `label-required` ở cấp 3;
  - `Parse` dữ liệu sâu > 3;
  - `Normalize` ở cấp 3.
- Schema snapshot chưa ràng buộc "`url` chỉ có ở `custom`". `Validate` đã chặn từ lúc lưu.
