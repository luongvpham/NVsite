# DesignIdeal — Cửa vào

> **Đọc file này trước, rồi mở đúng một file.** Thư mục `DesignIdeal/` nặng ~343 KB — mở nhầm một
> file là mất 25k token cho một câu trả lời đáng lẽ 2 dòng.
>
> **Hai bảng bắt buộc đọc:** §2 nói tài liệu nào **còn tin được**, §3 nói **đang làm tới đâu**.
> Tài liệu ở đây mô tả **ý định lúc thiết kế**, không phải bản đã chạy. Bỏ qua §2 là đi sửa code
> đang đúng cho khớp một bản vẽ đã lỗi thời.

---

## 1. Tra nhanh — câu hỏi thuộc về đâu

| Câu hỏi | Mở |
|---|---|
| **Một quyết định `#N` nói gì / đã code chưa** | [`DECISIONS.md`](DECISIONS.md) ← **luôn vào đây trước**, đừng quét `02` |
| Sản phẩm, tính năng, Phase nào làm gì | `01-project-ideal.md` §8 |
| Tech stack, ràng buộc dependency | `02-tech-stack-and-decision.md` §1, §2, §4 |
| `User` `ExternalLogin` `UserShop` `Role` `PendingRegistration` | `03-identity-entity-design.md` |
| `Shop` (đầy đủ) `ServiceCategory` `Listing` `Review` `Lead` | `04-listing-and-review-design.md` |
| Builder: `Website` `Page` `PageDraft` Component Tree | `05-website-builder-and-product-design.md` PHẦN I |
| `MediaAsset`, Media Library, pipeline ảnh, preset, `resolveImage()`, URL `/media/*`, ảnh `Listing`/`Product`/variant | `08-media-asset-design.md` |
| `Product` `ShopProductCategory` `ShopAttribute` variant | `05-website-builder-and-product-design.md` PHẦN II |
| `Service` `ShopServiceGroup` | `06-service-design.md` |
| Manifest schema, prop `kind`, codegen artifact | `07-component-manifest-schema.md` |
| Quy trình, lane, Gate, contract, `brief.md` | `ai-agent-development-workflow.md` |
| Ma sát quy trình sau Gate 2, cải tiến quy trình, trần ngân sách tài liệu | `ai-agent-development-workflow.md` §9 → [`../Docs/process/improvement-proposals.md`](../Docs/process/improvement-proposals.md) |
| Layout code backend (Domain/Application/Infrastructure, CQRS) | [`../backend/CLAUDE.md`](../backend/CLAUDE.md) ← **không** phải `architecture-guide.md` |
| Thứ tự triển khai (Bước 1–11, C, AI A0–A4, Marketplace M0–M5; số bước là tên, thứ tự làm theo `step.md`) | [`step.md`](step.md) |
| AI trong builder: tạo website từ form câu hỏi + template, chat sửa, AI được ghi gì | `01-project-ideal.md` §5.4 (#105–#107) · pipeline chat: `02` #14–#16 |
| Đa ngôn ngữ website shop | `05-website-builder-and-product-design.md` §26 |
| Nợ test cần Docker | [`../Docs/DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md) |

**Quyết định `#N` nằm ở đâu:** `#1–#39`, `#68`, `#104` → `02`. `#40–#58`, `#92`, `#94`, `#95`, `#97`, `#98` → `05` §0. `#93` → `04` §4.5. `#96` → `step.md`. `#59–#67` → `07` §0. `#69–#86` và `#88` → `08` §0. `#87`, `#91` → `backend/CLAUDE.md` §OpenAPI. `#89`, `#102` → `ai-agent-development-workflow.md` §6, §6.1. `#90`, `#101`, `#114` → `backend/docs/modules/identity.md`. `#115` → `backend/docs/modules/platform.md`. `#116` → `backend/CLAUDE.md` §Job nền. `#99`, `#100` → `backend/docs/modules/website.md`. `#103` → `Docs/tasks/WEBSITE-003/plan.md` §4. `#108`, `#109` → `Docs/tasks/WEBSITE-004/plan.md` §4. `#110` → `Docs/tasks/WEBSITE-004/contract-diff.md`. `#111`, `#112` → `backend/docs/modules/website.md`. `#113` → `Docs/tasks/WEBSITE-007/plan.md` §4. `#105–#107` → `01` §5.4.
Chi tiết và lý do vì sao không gộp về `02`: xem đầu [`DECISIONS.md`](DECISIONS.md).

---

## 2. Trạng thái từng tài liệu — tài liệu nào còn tin được

<!-- GEN:START doc-status -->
<!-- Bảng này SINH TỰ ĐỘNG từ banner STATUS của từng file. Đừng sửa tay —
     sửa banner ở đầu file tương ứng rồi chạy `pnpm gen:doc-index`. -->

| File | Trạng thái | Task đã chạm | Changelog | Đừng tin ở |
|---|---|---|---|---|
| `01-project-ideal.md` | ✅ Ổn định | DESIGN-ROADMAP-002 | — | — |
| `02-tech-stack-and-decision.md` | ⚠️ Tài liệu thiếu phần | REFACTOR-API-001, DESIGN-ROADMAP-001, DESIGN-ROADMAP-002 | [`REFACTOR-API-001/changelog.md`](../Docs/tasks/REFACTOR-API-001/changelog.md) | Không chứa #40–#67, #69–#86 và #88 — xem DECISIONS.md · đường dẫn API (vd. api.vsite.vn/auth/...) thực tế có prefix /api: /api/auth/... (Quyết định #91) |
| `03-identity-entity-design.md` | ⚠️ Đã code, có lệch có chủ đích | IDENTITY-001, REFACTOR-DB-001, REFACTOR-AUTHZ-001, REFACTOR-API-001, IDENTITY-002, JOBS-001 | [`IDENTITY-001/changelog.md`](../Docs/tasks/IDENTITY-001/changelog.md) | §3.1/§3.3/§5 cột PasswordSalt · §3.3 câu upsert UserShop · §4 tên ràng buộc · §4 tên bảng/cột PascalCase có quote (thực tế snake_case, bảng User = app_user, REFACTOR-DB-001 — xem Docs/tasks/REFACTOR-DB-001/changelog.md) · mọi bảng auditable có thêm cột deleted_at · §6.1 đăng ký lại khi UserShop đã xoá mềm = khôi phục dòng cũ, đang Suspended/Invited = 409; login/refresh/reset chỉ chấp nhận UserShop chưa xoá + Active và User Active (Quyết định #90, Docs/tasks/REFACTOR-AUTHZ-001/changelog.md) · đường dẫn API (vd. api.vsite.vn/auth/verify-email) thực tế có prefix /api: /api/auth/... (Quyết định #91) · mọi chỗ nói refresh token đi trong body/response (§6.4 và quanh đó): thực tế là cookie httpOnly |
| `04-listing-and-review-design.md` | ⚠️ Đã code, có lệch có chủ đích | SHOP-001, MEDIA-001, DESIGN-STEP5-PREP | [`MEDIA-001/changelog.md`](../Docs/tasks/MEDIA-001/changelog.md) | Changelog trước MEDIA-001: Docs/tasks/SHOP-001/changelog.md (banner chỉ nhận một đường dẫn) · Chỉ §2.1/§2.2 (Shop) đã code — §3 trở đi (ServiceCategory/Listing/Review/Lead) vẫn là spec, chưa có dòng code nào · §2.1 Shop chưa liệt kê LogoId (FK ghép, MEDIA-001) và logoUrl trên ShopDto/ShopSummaryDto (xem Docs/tasks/MEDIA-001/changelog.md mục 13) |
| `05-website-builder-and-product-design.md` | ⚠️ Đã code, có lệch có chủ đích | WEBSITE-008, WEBSITE-007, WEBSITE-005, WEBSITE-004, WEBSITE-003, WEBSITE-002, WEBSITE-001, MEDIA-001, REFACTOR-DB-001, DESIGN-STEP5-PREP, DESIGN-ROADMAP-001 | [`WEBSITE-008/changelog.md`](../Docs/tasks/WEBSITE-008/changelog.md) | Changelog trước WEBSITE-008: Docs/tasks/WEBSITE-007/changelog.md · 8d đã code ở WEBSITE-008 (#117, #118): prune SitePublication giữ 20 bản gồm current · listPublications trần 20 · draft + OG của trang xoá mềm > 7 ngày bị purge · mục menu mồ côi được dọn (menu không sửa < 24h, không IsDirty) · câu "chưa có prune/dọn mồ côi" bên dưới lỗi thời · Changelog trước WEBSITE-007: Docs/tasks/WEBSITE-005/changelog.md · §8 NavigationConfig đã code BE + FE ở WEBSITE-007 (Bước 7, #113; nghiệm thu 4/4, Gate 2 đóng 2026-11-22) nhưng lệch: thứ tự = vị trí mảng, không có field order · một trang chỉ một lần trong menu (NAV_PAGE_DUPLICATE) · menu lồng tối đa 3 cấp · nguồn chỉ page:{id} và custom (serviceGroup/productCategory chưa) · không có labels đa ngôn ngữ · snapshot.navigation.items lưu khoá + label/url/children, không hidden; FE ghép nhãn/URL từ pages (§7 shape navigation đừng tin) · Changelog trước WEBSITE-005: Docs/tasks/WEBSITE-004/changelog.md · Render site công khai ở apps/web (Bước 8c) đã code ở WEBSITE-005: đọc snapshot đã publish qua GET /api/site theo Host, LRU trong process theo host + ETag/304, HTML Cache-Control private, no-cache (§7 'Runtime đọc gì' đã cập nhật) · Changelog trước WEBSITE-004: Docs/tasks/WEBSITE-003/changelog.md · SitePublication + publish/rollback + GET /api/site đã code ở WEBSITE-004 (Bước 8b, BE): §7 shape snapshot đừng tin — thực tế thêm pages[].id, media (imageId → {storageKey, preset}) thay mediaMap, tracking, theme.presetKey, seo.faviconAssetId, navigation.items có menu thật từ WEBSITE-007; Website.CurrentPublicationId + FK ghép (current_publication_id, id) → site_publication (id, website_id); endpoint đọc công khai là một GET /api/site theo header Host (#108), không đọc theo số version · §25 mục 4 (cache/TTL) đã chốt #109: cache dữ liệu (con trỏ Redis site:pointer:{shopId}, TTL 5'/1', xoá khi publish/rollback; ETag/304), không cache HTML · shop Draft không chặn site (chỉ Suspended/Closed) · requiresProps đã enforce lúc publish (RequiredPropsChecker) · anchor #nodeId và năm © đã code ở WEBSITE-005 · Changelog trước WEBSITE-003: Docs/tasks/WEBSITE-002/changelog.md · Renderer (WEBSITE-003, Bước 6) đã code: RenderTree nhận basePath/locale/defaultLocale/pages, resolveUrl thật theo #103, SiteLayout Header/Footer ngoài Component Tree (§8), snapshot SSR↔CSR, preview toàn trang trong editor; §26.4 phần renderer đã code, overlay i18n chưa · Changelog trước WEBSITE-002: Docs/tasks/WEBSITE-001/changelog.md · Changelog trước WEBSITE-001: Docs/tasks/MEDIA-001/changelog.md (banner chỉ nhận một đường dẫn) · Editor Portal (Bước 5b) đã code ở WEBSITE-002 (F1–F11, đã nghiệm thu tay + qua Gate 2 2026-11-11): undo/redo dùng zundo (không phải invert-op), theme là state ngoài lịch sử undo của cây ( |
| `06-service-design.md` | 📐 Thiết kế, chưa code | DESIGN-STEP5-PREP, DESIGN-ROADMAP-001 | — | — |
| `07-component-manifest-schema.md` | ⚠️ Đã code, có lệch có chủ đích | WEBSITE-003, WEBSITE-002, BOOTSTRAP-002, MEDIA-001, TOOLING-001 | [`WEBSITE-003/changelog.md`](../Docs/tasks/WEBSITE-003/changelog.md) | Changelog trước WEBSITE-003: Docs/tasks/WEBSITE-002/changelog.md · §3/§7.3 |
| `08-media-asset-design.md` | ⚠️ Đã code, có lệch có chủ đích | WEBSITE-008, WEBSITE-004, WEBSITE-001, MEDIA-001, REFACTOR-DB-001, REFACTOR-API-001, SEC-IMAGESHARP-001 | [`WEBSITE-008/changelog.md`](../Docs/tasks/WEBSITE-008/changelog.md) | Changelog trước WEBSITE-008: Docs/tasks/WEBSITE-004/changelog.md · job dọn ảnh mồ côi đã code ở WEBSITE-008 (#117): cột MediaAsset.OrphanedAt, hai bước đánh dấu → xoá sau grace 7 ngày, chạy theo shop 04:00; §4/§10 mục 5–6 "Bước 8 sẽ làm" nay đã làm · Changelog trước WEBSITE-004: Docs/tasks/REFACTOR-DB-001/changelog.md · references đã quét thêm snapshot publish + ảnh OG/favicon (WEBSITE-004, T8): MediaReferenceKind có SitePublication (một mục gộp mọi bản publish, kèm isCurrent) / PageOgImage / WebsiteOgImage / WebsiteFavicon (#110); đọc bảng snapshot.media, không validate lại tree; xoá ảnh Library không kéo theo clone nên ảnh trong site đã publish không biến mất (R11) — câu "§4 quét tham chiếu mới chỉ Shop.LogoId" bên dưới lỗi thời hơn nữa · Changelog trước REFACTOR-DB-001: Docs/tasks/MEDIA-001/changelog.md (banner chỉ nhận một đường dẫn) · Từ WEBSITE-001: IMediaReferenceValidator đã nối vào lưu draft/page (Docs/tasks/WEBSITE-001/changelog.md) và references quét thêm PageDraft; mọi route Media chỉ Owner (#100, đổi hành vi) — các câu 'chưa nối handler' / 'chỉ Shop.LogoId' bên dưới đã lỗi thời · §2 tên bảng media_assets số nhiều (thực tế media_asset số ít, snake_case) và tên index ux_* của §3.5 (thực tế ix_media_asset_library, ux_media_asset_derivative, ix_media_asset_source) · §2 không có cột Kind (thực tế có Kind = Library/Direct/Clone/Derivative, REFACTOR-DB-001) · §2 công thức quota SourceAssetId IS NULL (thực tế Kind IN (Library, Direct)) · §3.6 ví dụ Shop có 1200x630,cover (thực tế chỉ 320x96,inside + 96x96,cover) · §5 và §10 mục 7 Cache-Control immutable (thực tế max-age=3600) · §3.5 và §9 test 6/7 chưa được chặn ở tầng request — IMediaReferenceValidator chưa nối handler nào (Bước 5) · §4 quét tham chiếu mới chỉ Shop.LogoId · §9 tiêu chí dừng chưa được chạy tay trên API + UI thật · đường dẫn API (vd. GET /shops, /shops/{shopId}/media/...) thực tế có prefix /api; /media/* (file ảnh) thì không (Quyết định #91) |
| `ai-agent-development-workflow.md` | ✅ Quy trình đang hiệu lực | — | — | — |
| `architecture-guide.md` | ⛔ Tham chiếu ngoại lai | — | — | 45 chỗ {Entity}, 10 chỗ {Project} chưa thay |
| `step.md` | ✅ Ổn định | DESIGN-ROADMAP-002, DESIGN-ROADMAP-001 | — | — |
<!-- GEN:END doc-status -->

**Luật:** trước khi sửa/mở rộng code dựa trên một file ở trên, đọc dòng của nó trong bảng này.
Nếu có changelog → đọc changelog. **Đừng "sửa lại cho đúng tài liệu" một chỗ mà changelog đã ghi là
lệch có chủ đích.**

---

## 3. Tiến độ theo [`step.md`](step.md)

| Bước | Nội dung | Trạng thái |
|---|---|---|
| 1 | Framework FE + BE, Orval, contracts, reserved-routes, module boundary | ✅ BOOTSTRAP-001 |
| 2 | Component Manifest + codegen + 5 component mẫu | ✅ BOOTSTRAP-002 |
| 3 | Identity + Role + UserShop + Shop + PendingRegistration + token scope | ✅ IDENTITY-001 · test tích hợp đã chạy thật trên Docker (2026-09-27) |
| 4 | `MediaAsset` + pipeline ảnh + Media Library + 9 preset + `/media/*` (`08`) | ✅ MEDIA-001 (validator đã nối ở 5a — WEBSITE-001) |
| 4.x | Dọn nền: nghiệm thu tay `08` §9 (phần ImageSharp CVE đã xong — SEC-IMAGESHARP-001) | ✅ MEDIA-001-ACCEPT (2026-10-13) — còn L2/L3 chờ quyết định, xem `Docs/tasks/MEDIA-001-ACCEPT/changelog.md` |
| 5a | `Website` → `Theme` → `Page` → `PageDraft` — BE, validate toàn tree khi `PUT` | ✅ WEBSITE-001 — đã merge `master`; 5b nghiệm thu tay trên chính BE này. Gate 2 đóng 2026-11-12 khi rà roadmap. Quy ước `ShopId`: #99 |
| 5b | Operations Engine + editor Portal | ✅ WEBSITE-002 (2026-11-11: nghiệm thu tay với BE thật + Gate 2 + `review.md`) — Operations Engine (`builder-core`), editor, autosave/409, Theme panel (CSS vars), Lexical RichText, khung crop preset. Việc theo dõi sau review: §4 dòng WEBSITE-002 |
| 6 | `builder-renderer` hoàn thiện (`basePath`, `locale`, SSR↔CSR) + preview toàn trang | ✅ WEBSITE-003 (đã đóng 2026-11-12) |
| 8b | `SitePublication` + publish/rollback + endpoint đọc công khai (BE) | ✅ WEBSITE-004 — đã merge `master` (PR #25), Gate 2 đóng. Quyết định #108–#112. UI publish ở Portal → 8e |
| 8c | `apps/web` render công khai + cache (FE) | ✅ WEBSITE-005 — đã merge `master` (PR #26), Gate 2 đóng 2026-11-19; nghiệm thu tay 7/7 ĐẠT với BE thật ([`changelog.md`](../Docs/tasks/WEBSITE-005/changelog.md)). Còn nợ ⛔ trước production (§4). Mốc review #96–#98: người duyệt giữ nguyên (2026-11-19) |
| 8e | UI publish/rollback ở Portal + nạp lại draft sau publish (FE) | ✅ WEBSITE-006 — Gate 2 đóng 2026-11-20 (nhánh `feature/WEBSITE-006`). Nghiệm thu 6/6 ĐẠT với BE thật bằng trình duyệt ([`changelog.md`](../Docs/tasks/WEBSITE-006/changelog.md)). Lệch G6: sau publish chỉ nhận version mới khi cây server trùng cây đã lưu |
| 7 | `NavigationConfig` | ✅ WEBSITE-007 — đã merge `master`. S1 (BE) xong, Gate 1 đã qua 2026-11-21 (promote #89, [`contract-diff.md`](../Docs/tasks/WEBSITE-007/contract-diff.md)); menu lồng **tối đa 3 cấp** (#113). S2 (FE) F1–F5 xong 2026-11-21. Nghiệm thu F6 4/4 ĐẠT với BE thật, **Gate 2 đóng 2026-11-22** ([`changelog.md`](../Docs/tasks/WEBSITE-007/changelog.md)); 5 mục feedback sau merge ở changelog mục "F6" |
| 8a | Hangfire (hạ tầng job dùng chung) + quyền `PlatformAdmin` (#114–#116) | ✅ JOBS-001 — Gate 2 đóng 2026-11-25, đã merge `master` (`2c0f864`). S1 (BE) xong, Gate 1 đã qua 2026-11-24 (promote `platform.v1`, #89, [`contract-diff.md`](../Docs/tasks/JOBS-001/contract-diff.md)); 3 job Identity + dashboard `/hangfire` + khung fan-out theo shop; `dotnet test` xanh ([`changelog.md`](../Docs/tasks/JOBS-001/changelog.md)). **S2 (FE)** F1–F2 xong 2026-11-25 (nút "Job nền", proxy `/hangfire`, nghiệm thu tay 5/5 người báo); [`review.md`](../Docs/tasks/JOBS-001/review.md) |
| 8d | Job của builder: prune publication, dọn media/menu/draft mồ côi | ✅ WEBSITE-008 (BE, Lane B; #117, #118). Hai job theo shop: `website.retention` 03:30 + `media.orphan-cleanup` 04:00. **Gate 2 đóng 2026-11-26** — [`review.md`](../Docs/tasks/WEBSITE-008/review.md) |
| 9 | `Service` + Binding Resolver | ⬜ ⚠️ chốt vị trí + chữ ký Binding Resolver trước |
| C | Thư viện component đợt 2 (khoảng 15 loại) + quy ước mô tả cho AI | ⬜ ⚠️ chốt danh sách component, quy ước `description`, cách nhúng video |
| 11 | `WebsiteTemplate` — AI sinh, developer duyệt (#107) | ⬜ ⚠️ chốt nguồn kho ảnh mẫu trước |
| A0 | Hạ tầng AI (module BE, công cụ sinh từ Registry, giới hạn lượt) | ⬜ ⚠️ chốt model, giới hạn chi phí, thao tác AI áp ở client hay BE |
| A1 | Form câu hỏi theo lĩnh vực (#107) | ⬜ |
| A2 | AI tạo website dịch vụ từ template + câu trả lời (#105, #106) | ⬜ |
| A3 | Chat sửa trong editor (#14–#16) | ⬜ |
| M0–M5 | Marketplace (Phase 1) — sau lõi builder và AI dịch vụ (#96). Outbox ở M2, Elasticsearch ở M4 | ⬜ ⚠️ M0: mô hình ghi không tenant + đọc xuyên shop |
| 10a/b/c | `Product` — sau Marketplace (#96): danh mục/attribute → product/variant → index ES + System page | ⬜ ⚠️ 10b: chốt UX variant (`05` §25 #6) |
| A4 | AI tạo shop có sản phẩm từ file mẫu | ⬜ ⚠️ chốt định dạng file mẫu |

Bảng xếp theo **thứ tự làm**, không theo số bước. Việc chưa xếp bước (email thật, deploy production,
đăng nhập social): `step.md` mục "Trước ra mắt".

---

## 4. Việc đang mở — cần người quyết hoặc còn nợ

### Cần bạn chốt

| Nguồn | Vấn đề | Chặn |
|---|---|---|
| `02` §6 #5 | Zalo user ID app-scoped: một Zalo app duy nhất hay mỗi app là một Provider | Tích hợp Zalo Login |
| `02` §6 #6 | Xoá tài khoản: giải phóng `EmailNormalized` thế nào | Trước launch |
| `02` §6 #8 | Mô hình giá hai luồng doanh thu | Trước khi bật thu phí |
| `02` §6 #9 | Ngưỡng & quy trình kiểm duyệt listing | Trước launch |
| `02` §6 #10 | SLA khiếu nại đánh giá — **rủi ro pháp lý** | Trước launch |
| `step.md` "Trước ra mắt" | Xếp chỗ cho: gửi email thật, deploy production, đăng nhập social | Trước M5 |
| `step.md` 9 | Binding Resolver chạy ở đâu (SSR loader gọi API hay BE resolve sẵn) + chữ ký | Bước 9 |
| `step.md` C | Danh sách component + variant của đợt 2 · quy ước `description` cho AI (có cần field manifest mới không) · video nhúng bằng gì | Bước C |
| `step.md` 11 | Kho ảnh mẫu theo lĩnh vực lấy từ đâu (bản quyền) | Bước 11 |
| `step.md` A0 | Model và nhà cung cấp · giới hạn lượt và chi phí theo shop · thao tác AI áp ở Portal (một engine) hay port engine sang BE · vị trí module AI | Bước A0 |
| `step.md` A4 | Định dạng file mẫu sản phẩm (sheet danh mục, thuộc tính, sản phẩm, biến thể) | Bước A4 |
| `05` §25 #7 | URL `System` page ở ngôn ngữ khác | Bật đa ngôn ngữ |
| `step.md` "Đa ngôn ngữ" | Thời điểm bật tính năng đa ngôn ngữ (bước L) | — |

### Nợ kỹ thuật đã ghi nhận

| Nguồn | Nợ |
|---|---|
| [`DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md) | Đang rỗng — REFACTOR-BE-001 + REFACTOR-DB-001 đã chạy pass thật trên Docker (2026-10-09, 296/297, 1 skip có sẵn) |
| `MEDIA-001-ACCEPT/changelog.md` L2–L3 | Chờ quyết định sửa: **L3** (8c: `/media` trên `node .output` nay trả 404 chung, prod phải route qua Caddy — xem dòng WEBSITE-005) mở thẳng `{slug}.vsite.local:3000/media/…` trên thanh địa chỉ ra 404 HTML (SSR chặn request dạng document; `<img>` vẫn 200; chưa rõ prod); **L2** upload >11 MB qua proxy portal ra 502 thay vì 413. Cũng ghi ở đó: 5.6 Gallery chưa xác nhận preview tự cập nhật (người nghiệm thu chấp nhận) |
| `MEDIA-001/changelog.md` "Chưa làm xong" #6 | Khung crop preset (WEBSITE-002 F11) chưa thử bằng chuột/cảm ứng thật |
| `MEDIA-001/changelog.md` "Chưa làm xong" #4 | Bản TypeScript của hàm quy ước tên `thumb_`/`fthumb_` (C# đã có ở `ImagePaths`) — làm ở M2 (`Listing`) |
| `MEDIA-001/changelog.md` "Chưa làm xong" #10 | `UploadShopLogoHandler` để lại file mồ côi khi shop biến mất giữa kiểm tra và ghi (race NotFound) — **job 8d không dọn được**: file không có record nên job quét theo record không thấy. Cần dọn theo storage hoặc sửa handler (#3 đã xong ở WEBSITE-008) |
| `WEBSITE-008/changelog.md` "Chưa làm xong" | Cửa sổ race còn lại khi xoá ảnh (ảnh được dùng lại đúng trong vòng lặp xoá) · một draft hỏng validate chặn job ảnh của shop đó (ném ⇒ Hangfire retry, an toàn) · `OrphanGrace=0` đánh dấu ngay clone picker mới tạo · #5 truy vấn ban đầu liệt kê media của shop không phân trang · #6 `IMediaReferenceSource` chưa hợp nhất với `IMediaUsageSource` |
| `WEBSITE-008/changelog.md` "Chưa làm xong" #7 | **Quy tắc:** entity/JSON nào lưu id `MediaAsset` (vd. `Service`/`ServiceGroup`, bước 9) phải đăng ký `IMediaUsageSource` trong cùng task, nếu không `MediaOrphanCleanupJob` xoá ảnh đó. Tuỳ chọn: test liệt kê FK tới `media_asset` từ `information_schema` so với danh sách đã phủ |
| `MEDIA-001/changelog.md` "Chưa làm xong" #9 | Lỗ hổng #19 có từ trước: 400 do model-binding và 415 của endpoint JSON không có `error_code` |
| `REFACTOR-AUTHZ-001/changelog.md` "Chưa làm" #3 | **Đã chấp nhận** (người duyệt, 2026-10-09 — rủi ro biết trước, không làm): access token `vsite-main` còn dùng được tới khi hết hạn (15 phút) sau khi `User` bị đình chỉ, và `vsite-portal` cũng vậy với endpoint KHÔNG có `{shopId}` (`GET /api/shops`, `/api/auth/me`). Token shop và mọi route `{shopId}` đã bị chặn ngay. Chặn nốt thì cần kiểm `User.Status` mỗi request (một query) |
| `REFACTOR-API-001/changelog.md` "Chưa làm" #1–#4 | Chưa bấm tay đăng nhập → refresh trên trình duyệt với DB dev · SSR loader thật đã có ở WEBSITE-005 (`createApiClient` mỗi request); còn cân nhắc cảnh báo khi SSR lỡ dùng instance mặc định · link verify/reset trong email trỏ thẳng API JSON, link reset còn GET vào endpoint POST — nên trỏ về trang FE (xem dòng IDENTITY-001 §C) |
| `IDENTITY-002/changelog.md` "Chưa làm xong" | **Đã chấp nhận** (người duyệt, 2026-10-18): hai tab cùng refresh một lúc dùng chung cookie → bị coi là reuse → đăng xuất cả hai (chữa bằng grace window ~10s, nới #3) · CSRF chỉ dựa `SameSite=Lax` (`{slug}.vsite.vn` cùng *site* với `admin.vsite.vn`: trang shop độc hại ép được logout/xoay token, không đọc được response) · `/auth/refresh-token` chưa đối chiếu audience của token với Host · đăng nhập lại không thu hồi token cũ trong cookie · logout chưa xoá cache React Query (người đăng nhập sau trên cùng tab thấy dữ liệu cũ tới khi refetch) · chưa smoke trình duyệt thật (cookie xuyên proxy Vite) |
| `REFACTOR-AUTHZ-001/changelog.md` "Chưa làm" #1, #2 | `ownerShopIds` chưa loại shop đã xoá mềm (Identity không được tham chiếu Shop); chưa có nghiệp vụ đình chỉ/khôi phục membership hay User (`Suspend`/`Reactivate` + thu hồi refresh token) |
| `REFACTOR-DB-001/changelog.md` "Chưa làm" #4 | Guid v7 cho khoá chính · bảng `shop_slug_history` (redirect slug cũ) — xem dòng WEBSITE-005 · outbox cho integration event — M2 |
| Review kiến trúc 2026-10-08 (P1) | **Nửa "đọc công khai một shop" đã chốt và code ở 8b (#108 — `GET /api/site` suy tenant từ Host, đọc qua Global Query Filter, không `IgnoreQueryFilters()`); phần còn lại chốt ở M0:** `Review`/`Lead` do khách ghi khi không có tenant context, và đọc xuyên shop ở marketplace — filter tenant fail-closed sẽ trả rỗng. Cần mô hình "public read/write" tường minh (read model riêng, hoặc truy vấn qua helper giống `UserShopQueries` #90) thay vì rải `IgnoreQueryFilters()` |
| `REFACTOR-BE-001/changelog.md` "Chưa làm xong" #3 | Quy ước đã chốt (#99) và áp dụng cho Website. **Còn nợ:** handler Media (và Shop) cũ vẫn lấy `ShopId` từ route/command trong khi `MediaReferenceValidator` đọc `ITenantContext` — dọn theo #99 |
| `TOOLING-001/changelog.md` "Chưa làm" #1, #2 | Phần P1/P2 còn lại của review FE: tách `builder-components` thành package model không React · `oneOf` theo type trong AI tool schema · hằng preset sinh ra thay hardcode · `@vsite/config` · gom error code về `@vsite/shared` · dọn file commit nhầm (`.tanstack/tmp`, `.staging`) · job CI cho script tooling. Classifier contract chưa phân biệt chiều request/response (thêm field required vào DTO **response** bị báo BREAKING) |
| `WEBSITE-002/review.md` (việc theo dõi) | m1 `isSafeLinkUrl` ném `RangeError` với entity > U+10FFFF · m2 siết `isKnownPropPath`/index của op `update` trước khi AI dùng `dispatch` · m3 `useMediaMap` chia lô 200 id (contract thiếu `maxItems`) · m4 chặn phím tắt editor khi có modal · m5/m6 RichText khi op bị từ chối + hai lớp undo · BE chưa có test đọc `tests/fixtures/richtext/be-canonical.json` (FE↔BE cùng dạng chuẩn) · nợ ghi ở changelog "Lệch": `LexicalComposer` deprecated, Việt hoá lỗi con 422, theme mất khi đổi trang, `requiresProps` chờ 8b |
| ⛔ **Trước production** — nợ SHOP-001 (WEBSITE-004 plan phát hiện 4, #111) | Owner tự đặt được `ShopStatus` bất kỳ qua `PATCH /api/shops/{id}` — kể cả tự gỡ `Suspended` (đã tái hiện bằng HTTP 2026-11-18) ⇒ đình chỉ vô hiệu, `GET /api/site` chạy lại. Cần task SHOP riêng: Owner chỉ đi lại `Draft`↔`Active`, không thoát `Suspended`/`Closed`; **giữ** đường kích hoạt shop mới (chưa có luồng riêng). Đổi contract Shop → Gate 1 |
| `WEBSITE-004/changelog.md` "Chưa làm xong / theo dõi" | **8c đã làm ở WEBSITE-005:** Host tự đặt, anchor, năm ©, trang chủ tắt, OG/favicon; nạp lại draft sau publish đã làm ở WEBSITE-006. **M4:** BE vẫn suy tenant theo **nhãn đầu** của Host (`evil.com` → slug `evil`); FE đã chặn host ngoài `{label}.{ROOT_DOMAIN}` (không gọi BE), BE còn mở — đối chiếu Host đầy đủ cùng `ShopDomain`. Còn mở ở BE: race con trỏ Redis ≤ 5' · Redis lỗi ⇒ `GET /api/site` 500 (M3) · ETag phải gồm `schemaVersion` khi có upcaster Snapshot đầu tiên (M1) · thiếu theme/draft ⇒ 500 (M2) · snapshot hỏng ⇒ `references` 500 · prune 20 bản (8d) · custom domain (G2) |
| `WEBSITE-005/changelog.md` "Chưa làm xong / nợ" ⛔ | **Trước production:** `shop_slug_history` (slug đổi → 404, chưa 301; BE + Gate 1, task riêng) · **CSRF `SameSite=Lax`** (FE không sửa; đề xuất task IDENTITY) · canonical/og:image/sitemap ra `http://` sau TLS proxy vì A3 bỏ qua `X-Forwarded-Proto` (quyết `PUBLIC_PROTOCOL` hoặc trusted proxy) · **Caddy phải route `/media/*` và `/api/*` sang BE** (`node .output` không có proxy; 404 `/assets/*` của Nitro mang `immutable` 1 năm — lưu ý CDN) |
| `WEBSITE-005/changelog.md` theo dõi | M4 phía BE (tenant theo nhãn đầu Host) · Orval zod `dateTimeOptions: { offset: true }` + regen (đang override cục bộ `publishedAt`) |
| `WEBSITE-006/changelog.md` "Chưa làm xong / nợ / theo dõi" | `PublishDialog`/nút tổng quan chưa tắt khi publish khác đang chạy · `Dialog` của `@vsite/ui` chưa có focus trap · label Inspector không gắn `htmlFor` · Portal thiếu `favicon.ico` · 4 shop smoke `w6smoke*` còn trong DB dev · dev server `apps/web :3000` trả 502 mọi shop (chưa điều tra) |
| `WEBSITE-007/changelog.md` "Chưa làm xong / theo dõi" | **Bind body có kiểu lỗi ⇒ 500 (Development) / 400 rỗng (Production), không `error_code`** — lỗi có sẵn, mọi route nhận body có kiểu (`saveNavigation` đã né bằng đọc body thô) · `params` 422/409 không có type trong OpenAPI · chưa có test chặn xoá mềm `NavigationConfig` · phủ test độ sâu 3 còn thiếu vài ca · FE (Minor review S2): màn Menu mở lúc GET lỗi mà có cache cũ thì dựng form từ cache cũ, tràn 10 mục/cha chỉ báo sau khi thả, `navigation-editor.tsx` ~750 dòng, `href` custom không trim, link custom tương đối trong preview báo "trang đã xoá", dấu "chưa publish" với website chưa publish lần nào, gom chính sách query editor nháp · `backend/docs/modules/website.md:141` chưa ghi 409 sau mọi publish/rollback · **feedback sau merge (changelog mục "F6"):** lỗi publish không chỉ rõ khối (có từ WEBSITE-006), khối rỗng cao 0px không chọn được trên canvas, ServiceGrid bắt buộc `source` mà Inspector chưa chọn được (chặn publish), CSS menu, và 3 câu Gate 2 người chấp nhận như hiện tại (mục ẩn tính vào giới hạn 12 — BE, "Giữ bản của tôi" khi 409, xoá cha không hỏi lại) |
| `WEBSITE-001/changelog.md` "Chưa làm xong" #6 | `GET /api/shops/{shopId}` (Shop) còn mở cho `Customer` có token global — chưa Owner-only (#100 mới áp cho Website + Media) |
| `WEBSITE-001/changelog.md` "Chưa làm xong" #8, #9 | **Brief 5b phải ghi:** `Theme` không có concurrency (G7, last-write-wins) · `PUT draft` có thể trả `sanitized: true` (FE nạp lại tree sạch) · lỗi có cấu trúc ở `params.errors[]` (`{path,code,message}`), không phải `errors` · mã con dùng đúng tên ở `tests/fixtures/tree/README.md` |
| `WEBSITE-001/changelog.md` "Chưa làm xong" #4, #10 | `props-schemas.json` thiếu `additionalProperties: false` ở `image`/`binding`/`cta` · `tree`/`tokens` là `JsonElement` → schema rỗng, Orval ra `unknown` — FE dùng Zod của `builder-components` |
| `WEBSITE-001/changelog.md` "Chưa làm xong" #11–#13 (#3 đã xong) | Website chưa có `SeedTemplateId`/translation (`requiresProps` + `CurrentPublicationId` + publish đã xong ở WEBSITE-004, `NavigationConfig` ở WEBSITE-007) · `references` validate lại draft mỗi lần. #13 (draft trang xoá mềm không được dọn) đã xong ở WEBSITE-008 |
| `WEBSITE-001/changelog.md` "Chưa làm xong" #5, #15, minor hoãn | Comment `IsInLibrary` lệch code · minor hoãn nhóm link-url/S0 (vector XSS còn thiếu: `href` khoảng trắng đầu, `img data:`) · draft cũ không qua validate → `references` rỗng im lặng · thiếu test `has-pending-model-changes` · race `23505` CreatePage/CreateWebsite/UpdatePage chưa có test tất định (kiểu `BumpXminInterceptor`) |
| `WEBSITE-001/changelog.md` "Chưa làm xong" #16, #17 | Handler 409/23505 toàn cục đổi 500→409 ở Identity/Shop (có thể route chưa khai 409) · `check:docs` không bắt backtick lẻ ở banner STATUS (làm mất Stale ở §2) |
| `BOOTSTRAP-002/changelog.md` | Chưa có cách enforce #11/#53 (chặn hardcode URL ảnh) |
| `BOOTSTRAP-002/changelog.md` | Thiếu test `data:` URL scheme trong `href` khi sanitize |
| `IDENTITY-001/changelog.md` §A | Một số giá trị `UserShopSource` chưa bao giờ được ghi — mới có `RegisteredOnShop` (verify-email) và `ShopCreator` (`POST /api/shops`, SHOP-001) |
| `JOBS-001/changelog.md` "Chưa làm xong / nợ" | W1 CSRF dashboard đã sửa (JOBS-001-D1, same-origin) · Minor còn: Host không port nhận Origin cùng host khác port, thiếu 2 test biên · nợ S2/FE nhỏ (onSessionExpired sau refresh stale, popupBlocked, Safari chưa thử) · ⛔ **trước ra mắt:** Caddy route `/hangfire` trên host `admin.*` · Data Protection key ring bền · tzdata trong image Linux · Postgres chết lúc boot khi `ServerEnabled=true` (~14 s rồi có thể crash). |
| `IDENTITY-001/changelog.md` §C | Verify-email từ domain shop chưa có bước handoff code (`03` §5) |
| `IDENTITY-001/changelog.md` §F | Social Login chưa làm — schema `ExternalLogin` đã sẵn, luồng OAuth chưa có |
| `IDENTITY-001/changelog.md` §D | `ShopCustomerDto` chưa có — khi làm màn "Khách hàng" ở Portal phải đọc `03` §3.3 trước, đừng serialize thẳng `User` |

---

## 5. Quy ước

- **§2 ở trên SINH TỰ ĐỘNG.** Nguồn là dòng `> **STATUS:**` ở đầu mỗi file `DesignIdeal/*.md`. Sửa
  banner rồi chạy `pnpm gen:doc-index` — **đừng sửa tay bảng đó**, CI sẽ bắt.
  Grammar banner: `> **STATUS:** \`ENUM\` · **Tasks:** \`A,B\` · **Changelog:** \`path\` · **Stale:** \`text\``
  ENUM ∈ `STABLE` `SPEC` `IMPLEMENTED` `INCOMPLETE_DOC` `EXTERNAL_REF` `PROCESS`.
- **§1, §3, §4 viết tay** — không suy ra được từ filesystem, nên không sinh. Cập nhật khi đóng một
  bước hoặc trả xong một món nợ.
- **Cấp số quyết định mới:** tại [`DECISIONS.md`](DECISIONS.md) trước, rồi mới viết nội dung.
- **Thư mục task:** `Docs/tasks/<TASK-ID>/` — `data-needs.md` · `plan.md` · `contract-diff.md` (+ `contract-diff-D{n}.md` khi mở lại contract, workflow §6.1) · `brief.md` · `changelog.md` · `review.md`.
  Task đã qua Gate 1 (có `brief.md` hoặc `contract-diff.md`) **bắt buộc** có `changelog.md`; task
  mẫu/không chạm code thì đặt file `.no-changelog` ghi lý do ở dòng đầu.
  Ma sát quy trình ghi ở mục cuối `review.md`, sau Gate 2 (workflow §9).
- **Chữ hoa đường dẫn:** thư mục là **`Docs/`**, không phải `docs/`. Windows tha, CI Linux thì không.
- **Ký hiệu:** dấu `#` kèm số **chỉ dành cho Quyết định**. Muốn trỏ một tiểu mục thì viết đủ dạng
  `03 §6.4` — đừng gắn `#` vào số tiết, nó đụng với số hiệu quyết định và `check:docs` báo lỗi.
  (Ngoại lệ đã hợp lệ: `#21.1`–`#21.5` và `#39.1`–`#39.5` là mục con **của chính quyết định** đó.)

### Lệnh

| Lệnh | Làm gì |
|---|---|
| `pnpm gen:doc-index` | Sinh lại §2 từ banner |
| `pnpm check:docs` | 5 khẳng định + kiểm §2 đã khớp banner chưa |

Cả hai chạy trong `pre-commit` và trong CI job `Docs check` (ubuntu — để bắt lỗi chữ hoa mà Windows
giấu đi).
