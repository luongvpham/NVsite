# MEDIA-001 — Guide bàn giao cho session làm Bước 4

> Viết 2026-09-26, cuối session thiết kế lại MediaAsset. Session đó chỉ sửa tài liệu, **không** chạm
> code. File này là điểm xuất phát cho session lập plan và code Bước 4 — đọc hết trước khi mở file khác.

---

## 1. Nhiệm vụ

**Bước 4 của [`step.md`](../../../DesignIdeal/step.md):** `MediaAsset` + pipeline ảnh (sinh file lúc
đặt/upload, **không** image proxy) + Media Library + 9 preset + phục vụ `/media/*` trên mọi domain.

Việc đầu tiên của session mới: **lập plan** (lane, `brief.md`, `plan.md`), **chưa code**. Lane gần như
chắc chắn là **C** — module mới (`Media`), có migration, chạm cả ba loại contract (§5).

---

## 2. Nguồn sự thật — đọc theo thứ tự này

1. [`DesignIdeal/08-media-asset-design.md`](../../../DesignIdeal/08-media-asset-design.md) — **toàn bộ
   thiết kế**. §9 là ranh giới Bước 4 (có / không làm, tiêu chí dừng, 12 test bắt buộc). §10 là điểm
   còn trống.
2. [`DesignIdeal/DECISIONS.md`](../../../DesignIdeal/DECISIONS.md) — dòng #53, #55–#58, #64, #69–#81.
3. `07` §7.1 (kind `image`) và §7.4 (kind `binding`) — chỉ khi đụng Component Registry.
4. `backend/CLAUDE.md` — layout backend, Definition of Done.

**Không cần đọc** `05` §9, §20 hay `04` §4 để hiểu ảnh — chúng chỉ còn trỏ về `08`.

---

## 3. Đã chốt với người dùng — ĐỪNG mở lại

Mỗi điểm dưới đây là kết quả tranh luận có chủ đích. Thấy "lạ" thì đọc lý do ở `08`, đừng đề xuất lại.

| Chốt | Đã cân nhắc và **bỏ** |
|---|---|
| Upload thẳng vào slot **không giữ bản gốc** (#70). Focal point chọn trong dialog trước khi crop | Luôn giữ bản gốc cho mọi upload |
| Không có "lưu vào thư viện" sau khi đã upload — chỉ tick lúc upload | |
| Ảnh nghiệp vụ (logo, Service, OG…) = bản Library + bộ phái sinh sinh sẵn, preset do **codegen** suy ra (#73) | Một preset cố định cho mỗi nguồn; render bản 1600px |
| FK ghép `(ImageId, ShopId) → media_assets (Id, ShopId)` cho tham chiếu vô hướng (#76) | Chỉ lưu uuid, không FK — **trái #1** (một `AppDbContext` để FK xuyên module được) |
| Ảnh **Listing/Product không dùng `MediaAsset`**: `ImageUrls text[]` đường dẫn file full, `thumb_` / `fthumb_` (#79) | `uuid[]` trỏ `MediaAsset`; bảng `product_images` |
| `fthumb_` chỉ sinh khi ảnh thành ảnh đại diện, không sinh sẵn hai cỡ | |
| Listing 640×480 / 160×120 (4:3). Product rộng 600 / 160, tỉ lệ danh mục, crop file full **lúc upload**; đổi tỉ lệ thì upload lại (#57) | 320×240 / 120×90 (mờ trên DPR ≥ 2); job sinh lại |
| Variant: `ProductVariant.ImageUrls text[]`, dùng chung đường dẫn (#80) | Gắn ảnh vào option màu |
| URL mọi ảnh = `{domain bất kỳ}/media/{relativePath}`; đọc **không** kiểm tenant; **ghi** thì kiểm prefix thư mục + file tồn tại (#53, #81) | Domain CDN riêng `cdn.vsite.vn` |
| 9 preset (#78) | 12 preset có họ `R` |

**Quy tắc sửa tài liệu người dùng yêu cầu:** khi một quyết định đổi, **xoá nội dung cũ và viết đè**,
không để cả cũ lẫn mới cùng file. Giữ số `#N` nếu quyết định còn sống một phần; số mới cấp tại
`DECISIONS.md` trước (số kế tiếp = số lớn nhất hiện có + 1; hiện lớn nhất là #81).

---

## 4. Trạng thái code hiện tại (đã kiểm 2026-09-26)

| Thứ | Hiện trạng | Bước 4 phải làm |
|---|---|---|
| `config/image-presets.json` | 6 preset cũ, có `600xR,cover` | Thêm `fit: "inside"`, thêm 4 preset, bỏ `600xR` → 9 preset (`08` §7). `tests/config-files.test.ts` kiểm file này |
| `config/binding-sources.json` | `Product, ProductCategory, Service, ServiceGroup, MediaAsset` — **chưa có `Shop`** | Thêm `Shop` (bind logo) |
| `config/reserved-routes.json` | Có `cdn`, `static`, `assets`… — **chưa có `media`** | Thêm `media` |
| `packages/builder-components/src/context.tsx` | `resolveImage` stub `(imageId, preset) => placeholder` | Thay thân, **giữ chữ ký** (`08` §6) |
| `Hero01/02`, `Gallery01/02` | Gọi `ctx.resolveImage(id, '1600x900,cover' \| '800x800,cover')` | Không đổi — hai preset này giữ nguyên, `registry.lock.json` không đổi |
| `apps/portal/vite.config.ts:12` | Comment còn nhắc "image proxy thật" | Sửa comment |
| Backend | Chưa có module `Media`, chưa có Hangfire | Tạo module theo `backend/CLAUDE.md` |
| `Docs/architecture/dependency-map.json` | `Media` dependsOn `[Shop]`, phase 2. `Marketplace` **không** phụ thuộc `Media` | Xem §6 mục 1 |

Git: tài liệu của session thiết kế **có thể chưa commit** — chạy `git status` trước. Nếu thấy
`DesignIdeal/*` đang modified, đó là thiết kế đã chốt, không phải rác.

---

## 5. Contract bị chạm — cả ba loại, đều phải người duyệt

| Contract | Thay đổi | Cơ chế |
|---|---|---|
| API boundary | Endpoint mới: upload (2 chế độ), list library, chọn-từ-library (clone), soft delete, upload logo | Sync vào `contracts/openapi/.staging/`, **không** ghi thẳng `contracts/openapi/*.json` |
| Component Registry | **Field additive** trên prop `binding` để khai preset ảnh của dữ liệu bind + artifact codegen "bộ phái sinh theo nguồn" (`08` §3.6) | `registry.lock.json` + `check-additive.ts` |
| Reserved routes | Thêm `media` | Test FE/BE đọc cùng file |

Hook `.claude/hooks/guard-bash.mjs` chặn mọi lệnh Bash **nhắc tới** đường dẫn contract, kể cả chỉ
đọc — dùng Read/Grep thay vì `cat`/`head` cho các file đó.

---

## 6. Việc phải chốt trong plan — hỏi người dùng, đừng tự quyết

| # | Vấn đề | Ghi chú |
|---|---|---|
| 1 | **Pipeline ảnh dùng chung đặt ở đâu.** Listing (module `Marketplace`, phase 1) và Product (`Catalog`) cần pipeline + hàm quy ước tên `thumb_`/`fthumb_`, nhưng `Marketplace` không phụ thuộc `Media`. Và `Shop.LogoId` (module `Shop`) cần sinh phái sinh logo, trong khi `Media` đã dependsOn `Shop` → cho `Shop` phụ thuộc `Media` là **vòng tròn** | Phát hiện cuối session thiết kế, **chưa bàn**. Hướng có thể: pipeline ở namespace dùng chung (`SharedSegments`), hoặc luồng upload logo nằm trong `Media` và chỉ ghi `Shop.LogoId` qua Public Contract |
| 2 | Giới hạn dung lượng file + số pixel tối đa | `08` §10 #1 |
| 3 | HEIC từ iPhone: từ chối kèm hướng dẫn, hay thêm decoder | `08` §10 #2 |
| 4 | Tên field preset trên prop `binding` + tên/vị trí artifact bộ phái sinh | `08` §10 #3 |
| 5 | Object storage thật là gì (local disk / S3 / MinIO) ở dev và prod; `/media/*` do .NET hay Caddy phục vụ | Chưa tài liệu nào chốt |
| 6 | Thư viện xử lý ảnh .NET (ImageSharp có license riêng cho thương mại — kiểm trước khi chọn) | |
| 7 | `Media` đang ghi phase 2 trong `dependency-map.json` và `02`, nhưng pipeline dùng cho Listing ở phase 1 | Sửa nhãn nếu người dùng đồng ý |

---

## 7. Nhắc quy trình (dễ quên)

- **Tuần tự BE → sync contract → FE.** Không chạy song song.
- Test cần Testcontainers (test 7, 8, 11 ở `08` §9) — máy không có Docker thì ghi vào
  `Docs/DOCKER-TEST-DEBT.md` theo quy ước, **không** coi là "bỏ qua". Đọc file đó trước: đang có nợ
  Identity chờ chạy.
- Task chưa có `Docs/tasks/MEDIA-001/changelog.md` là **chưa xong**.
- Tenant isolation (#21): `ShopId` chỉ từ route / `TenantContext`, không bao giờ từ body; ownership
  kiểm **trong câu query**.
- Enum serialize string ở BE; TS dùng `const object + union`, không `enum` (#19).
