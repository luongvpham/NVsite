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
| Retro sau Gate 2, cải tiến quy trình, trần ngân sách tài liệu | `ai-agent-development-workflow.md` §9 → [`../Docs/process/improvement-proposals.md`](../Docs/process/improvement-proposals.md) |
| Layout code backend (Domain/Application/Infrastructure, CQRS) | [`../backend/CLAUDE.md`](../backend/CLAUDE.md) ← **không** phải `architecture-guide.md` |
| Thứ tự triển khai 11 bước | [`step.md`](step.md) |
| Nợ test cần Docker | [`../Docs/DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md) |

**Quyết định `#N` nằm ở đâu:** `#1–#39` và `#68` → `02`. `#40–#58` → `05` §0. `#59–#67` → `07` §0. `#69–#86` và `#88` → `08` §0.
Chi tiết và lý do vì sao không gộp về `02`: xem đầu [`DECISIONS.md`](DECISIONS.md).

---

## 2. Trạng thái từng tài liệu — tài liệu nào còn tin được

<!-- GEN:START doc-status -->
<!-- Bảng này SINH TỰ ĐỘNG từ banner STATUS của từng file. Đừng sửa tay —
     sửa banner ở đầu file tương ứng rồi chạy `pnpm gen:doc-index`. -->

| File | Trạng thái | Task đã chạm | Changelog | Đừng tin ở |
|---|---|---|---|---|
| `01-project-ideal.md` | ✅ Ổn định | — | — | — |
| `02-tech-stack-and-decision.md` | ⚠️ Tài liệu thiếu phần | — | — | Không chứa #40–#67, #69–#86 và #88 — xem DECISIONS.md |
| `03-identity-entity-design.md` | ⚠️ Đã code, có lệch có chủ đích | IDENTITY-001 | [`IDENTITY-001/changelog.md`](../Docs/tasks/IDENTITY-001/changelog.md) | §3.1/§3.3/§5 cột PasswordSalt · §3.3 câu upsert UserShop · §4 tên ràng buộc |
| `04-listing-and-review-design.md` | ⚠️ Đã code, có lệch có chủ đích | SHOP-001, MEDIA-001 | [`SHOP-001/changelog.md`](../Docs/tasks/SHOP-001/changelog.md) | Chỉ §2.1/§2.2 (Shop) đã code — §3 trở đi (ServiceCategory/Listing/Review/Lead) vẫn là spec, chưa có dòng code nào · §2.1 Shop chưa liệt kê LogoId (FK ghép, MEDIA-001) và logoUrl trên ShopDto/ShopSummaryDto (xem Docs/tasks/MEDIA-001/changelog.md mục 13) |
| `05-website-builder-and-product-design.md` | 📐 Thiết kế, chưa code | MEDIA-001 | [`MEDIA-001/changelog.md`](../Docs/tasks/MEDIA-001/changelog.md) | §9 tên bảng media_assets (thực tế MediaAsset) — MediaAsset đã code (Bước 4), phần còn lại của tài liệu (Website/Page/Product) vẫn là spec |
| `06-service-design.md` | 📐 Thiết kế, chưa code | — | — | — |
| `07-component-manifest-schema.md` | ⚠️ Đã code, có lệch có chủ đích | BOOTSTRAP-002, MEDIA-001 | [`BOOTSTRAP-002/changelog.md`](../Docs/tasks/BOOTSTRAP-002/changelog.md) | §7.2 nhãn "#67 cần xác nhận" (đã chốt) · §3 vị trí context · §9 route dev harness · §3 và §7.1 config/image-presets.json nay là {presets, surfaces} và resolveImage đã là bản thật, không còn stub · §7.4 binding.imagePresets đã code (xem Docs/tasks/MEDIA-001/changelog.md mục 4, 15) |
| `08-media-asset-design.md` | ⚠️ Đã code, có lệch có chủ đích | MEDIA-001 | [`MEDIA-001/changelog.md`](../Docs/tasks/MEDIA-001/changelog.md) | §2 và §3.5 tên bảng media_assets, cột DeletedAt, tên index ux_* (thực tế: bảng MediaAsset, IsDeleted, index tên EF) · §3.6 ví dụ Shop có 1200x630,cover (thực tế chỉ 320x96,inside + 96x96,cover) · §5 và §10 mục 7 Cache-Control immutable (thực tế max-age=3600) · §3.5 và §9 test 6/7 chưa được chặn ở tầng request — IMediaReferenceValidator chưa nối handler nào (Bước 5) · §4 quét tham chiếu mới chỉ Shop.LogoId · §9 tiêu chí dừng chưa được chạy tay trên API + UI thật |
| `ai-agent-development-workflow.md` | ✅ Quy trình đang hiệu lực | — | — | — |
| `architecture-guide.md` | ⛔ Tham chiếu ngoại lai | — | — | 45 chỗ {Entity}, 10 chỗ {Project} chưa thay |
| `step.md` | ✅ Ổn định | — | — | — |
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
| 4 | `MediaAsset` + pipeline ảnh + Media Library + 9 preset + `/media/*` (`08`) | ✅ MEDIA-001 ⚠️ chưa nghiệm thu tay tiêu chí dừng `08` §9, `IMediaReferenceValidator` chưa nối handler — xem §4 |
| 5 | `Website` → `Theme` → `Page` → `PageDraft` + Operations Engine | ⬜ |
| 6 | `builder-renderer` | ⬜ |
| 7 | `NavigationConfig` | ⬜ |
| 8 | `SitePublication` + publish/rollback + cache | ⬜ ⚠️ chốt cache/TTL (`05` §25 #4) trước |
| 9 | `Service` + Binding Resolver | ⬜ |
| 10 | `Product` + Attribute/Variant + Elasticsearch | ⬜ |
| 11 | `WebsiteTemplate` | ⬜ |

`step.md` dòng 15 ghi "chốt sanitize whitelist trước Bước 5" — **việc đó đã xong ở Bước 2**
(`config/sanitize-profiles.json`). Không còn là điều kiện chặn.

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
| `05` §25 #4 | Cache/TTL cho `SitePublication` | Bước 8 |

### Nợ kỹ thuật đã ghi nhận

| Nguồn | Nợ |
|---|---|
| [`DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md) | Đang rỗng — mọi test cần Docker (Identity, Shop, Media) đã chạy pass thật |
| `MEDIA-001/changelog.md` "Chưa làm xong" #5, #7 | **Nghiệm thu tay tiêu chí dừng `08` §9 chưa ai chạy** trên API + UI thật: upload vào Hero ở cả hai chế độ đúng kích thước preset; chọn Library cho slot Gallery ra clone độc lập; logo sinh đủ phái sinh và còn sau reload; `/media/…` đọc được trên `admin.vsite.local` và `{slug}.vsite.local:3000`; giới hạn 413 (>11 MB) qua Kestrel thật |
| `MEDIA-001/changelog.md` "Chưa làm xong" #8 | `tools/contract-sync` mù với response dạng mảng (`GET /shops`, `…/derivatives`, `…/assets?ids=` hiện luôn "UNCHANGED"): cần **task riêng** sửa `extractSchemaRefs` (đi theo `items.$ref`/`allOf` + diff dự phòng `components.schemas`) và ghi chú vào skill `contract-sync` |
| `MEDIA-001/changelog.md` "Chưa làm xong" #6 | Khung crop theo tỉ lệ preset ở dialog upload/chọn ảnh — F2 mới có dấu chấm focal point |
| `MEDIA-001/changelog.md` "Chưa làm xong" #4 | Bản TypeScript của hàm quy ước tên `thumb_`/`fthumb_` (C# đã có ở `ImagePaths`) — làm cùng task Listing đầu tiên |
| `MEDIA-001/changelog.md` "Chưa làm xong" #1, #2, #3 | Nối `IMediaReferenceValidator` vào handler lưu draft/page (Bước 5) · endpoint `references` mới quét `Shop.LogoId` (tree → Bước 5, snapshot → Bước 8) · job dọn file mồ côi (Bước 8) |
| `MEDIA-001/changelog.md` "Chưa làm xong" #9 | Lỗ hổng #19 có từ trước: 400 do model-binding và 415 của endpoint JSON không có `error_code` |
| `BOOTSTRAP-002/changelog.md` | Chưa có cách enforce #11/#53 (chặn hardcode URL ảnh) |
| `BOOTSTRAP-002/changelog.md` | Thiếu snapshot SSR↔CSR cho `RenderTree` — bài test duy nhất chứng minh trực tiếp #23 |
| `BOOTSTRAP-002/changelog.md` | Thiếu test `data:` URL scheme trong `href` khi sanitize |
| `IDENTITY-001/changelog.md` §A | 3/4 giá trị `UserShopSource` chưa bao giờ được ghi — chưa có endpoint tạo `Shop` (`ShopCreator`) |
| `IDENTITY-001/changelog.md` §B | **Không có Hangfire** — 3 job `03` yêu cầu đều chưa tồn tại (dọn token hết hạn, integrity job nhánh Zalo) |
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
- **Thư mục task:** `Docs/tasks/<TASK-ID>/` — `brief.md` · `plan.md` · `contract-diff.md` · `changelog.md` · `review.md` · `session-retro.md`.
  Task đã qua Gate 1 (có `brief.md` hoặc `contract-diff.md`) **bắt buộc** có `changelog.md`; task
  mẫu/không chạm code thì đặt file `.no-changelog` ghi lý do ở dòng đầu.
  `session-retro.md` viết **sau Gate 2**, và chỉ bắt buộc đầy đủ khi thoả điều kiện kích hoạt ở
  `Docs/templates/session-retro.md` §0 — lane A trơn tru thì một dòng là đủ.
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
