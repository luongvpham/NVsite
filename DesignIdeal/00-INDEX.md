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
| Builder: `Website` `Page` `PageDraft` Component Tree `MediaAsset` | `05-website-builder-and-product-design.md` PHẦN I |
| `Product` `ShopProductCategory` `ShopAttribute` variant | `05-website-builder-and-product-design.md` PHẦN II |
| `Service` `ShopServiceGroup` | `06-service-design.md` |
| Manifest schema, prop `kind`, codegen artifact | `07-component-manifest-schema.md` |
| Quy trình, lane, Gate, contract, `brief.md` | `ai-agent-development-workflow.md` |
| Layout code backend (Domain/Application/Infrastructure, CQRS) | [`../backend/CLAUDE.md`](../backend/CLAUDE.md) ← **không** phải `architecture-guide.md` |
| Thứ tự triển khai 11 bước | [`step.md`](step.md) |
| Nợ test cần Docker | [`../Docs/DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md) |

**Quyết định `#N` nằm ở đâu:** `#1–#39` và `#68` → `02`. `#40–#58` → `05` §0. `#59–#67` → `07` §0.
Chi tiết và lý do vì sao không gộp về `02`: xem đầu [`DECISIONS.md`](DECISIONS.md).

---

## 2. Trạng thái từng tài liệu — tài liệu nào còn tin được

<!-- GEN:START doc-status -->
<!-- Bảng này SINH TỰ ĐỘNG từ banner STATUS của từng file. Đừng sửa tay —
     sửa banner ở đầu file tương ứng rồi chạy `pnpm gen:doc-index`. -->

| File | Trạng thái | Task đã chạm | Changelog | Đừng tin ở |
|---|---|---|---|---|
| `01-project-ideal.md` | ✅ Ổn định | — | — | — |
| `02-tech-stack-and-decision.md` | ⚠️ Tài liệu thiếu phần | — | — | Không chứa #40–#67 — xem DECISIONS.md |
| `03-identity-entity-design.md` | ⚠️ Đã code, có lệch có chủ đích | IDENTITY-001 | [`IDENTITY-001/changelog.md`](../Docs/tasks/IDENTITY-001/changelog.md) | §3.1/§3.3/§5 cột PasswordSalt · §3.3 câu upsert UserShop · §4 tên ràng buộc |
| `04-listing-and-review-design.md` | ⚠️ Đã code, có lệch có chủ đích | SHOP-001 | [`SHOP-001/changelog.md`](../Docs/tasks/SHOP-001/changelog.md) | Chỉ §2.1/§2.2 (Shop) đã code — §3 trở đi (ServiceCategory/Listing/Review/Lead) vẫn là spec, chưa có dòng code nào |
| `05-website-builder-and-product-design.md` | 📐 Thiết kế, chưa code | — | — | §0 câu "chép sang 02" đã lỗi thời |
| `06-service-design.md` | 📐 Thiết kế, chưa code | — | — | — |
| `07-component-manifest-schema.md` | ⚠️ Đã code, có lệch có chủ đích | BOOTSTRAP-002 | [`BOOTSTRAP-002/changelog.md`](../Docs/tasks/BOOTSTRAP-002/changelog.md) | §7.2 nhãn "#67 cần xác nhận" (đã chốt) · §3 vị trí context · §9 route dev harness |
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
| 3 | Identity + Role + UserShop + Shop + PendingRegistration + token scope | ✅ IDENTITY-001 ⚠️ test tích hợp **chưa chạy thật** (cần Docker) |
| 4 | `MediaAsset` + image proxy + preset whitelist | ⬜ kế tiếp |
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
| `07` §15 #2 | Preset ảnh: hiện 6, tài liệu tự ghi cần ~12 | Bước 4 |
| `05` §25 #4 | Cache/TTL cho `SitePublication` | Bước 8 |

### Nợ kỹ thuật đã ghi nhận

| Nguồn | Nợ |
|---|---|
| [`DOCKER-TEST-DEBT.md`](../Docs/DOCKER-TEST-DEBT.md) | 3 mục Identity chưa chạy thật — 8 integration test + middleware e2e + 6 `TokenScopeTests` |
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
- **Thư mục task:** `Docs/tasks/<TASK-ID>/` — `brief.md` · `plan.md` · `contract-diff.md` · `changelog.md` · `review.md`.
  Task đã qua Gate 1 (có `brief.md` hoặc `contract-diff.md`) **bắt buộc** có `changelog.md`; task
  mẫu/không chạm code thì đặt file `.no-changelog` ghi lý do ở dòng đầu.
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
