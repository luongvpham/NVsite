# MEDIA-001 — FE brief (session S2)

## Contract

```
contracts/openapi/media.v1.json   (sha256: 4ac4d8be8bec410b3e51cd7275c6f0ba31425abb7f675369d108274bca9fad02)
contracts/openapi/shop.v1.json    (sha256: 26e390e0d15943bfc987d099287be1c5ffa5c6ce28835f7bae66dae3833c1fcc)  ← ShopDto + logoId
```

Lịch sử duyệt contract:
- **2026-09-27** Gate 1 (`contract-diff.md` §"Người duyệt đã quyết").
- **2026-09-28** `media` promote lại sau khi gộp schema trùng `MediaAssetDto2` (`Docs/tasks/MEDIA-001-D2/contract-diff.md`; Quyết định #87 — property object nullable giờ luôn dạng `{ allOf: [$ref], nullable: true }`, JSON runtime không đổi).
- **2026-09-29** `media` thêm `GET …/library/{assetId}/derivatives` (`Docs/tasks/MEDIA-001-D3/contract-diff.md`).

**Kiểm sha256 với `contracts/contract.lock` trước khi chạy `pnpm gen:api`. Lệch thì dừng lại, không đoán.**

### Module `media`: 10 endpoint, tất cả dưới `/shops/{shopId}/…`, JWT Portal + membership

```
POST   /shops/{shopId}/media/slot-uploads                 multipart: file, preset, focalX, focalY, saveToLibrary, altText?  → SlotUploadResultDto
POST   /shops/{shopId}/media/library                      multipart: file, altText?, folder?                                → MediaAssetDto
GET    /shops/{shopId}/media/library?page&pageSize        (pageSize mặc định 24, tối đa 100)                                → PagedResult<MediaAssetDto>
POST   /shops/{shopId}/media/library/{assetId}/clones     JSON { preset, focalX?, focalY? }                                  → MediaAssetDto
GET    /shops/{shopId}/media/library/{assetId}/references                                                                   → { references: [{ kind: "ShopLogo" }] }
GET    /shops/{shopId}/media/library/{assetId}/derivatives?preset=   (preset tuỳ chọn, một giá trị, vd. "320x96,inside" — URL-encode) → MediaAssetDto[]  (phái sinh của bản Library; id lạ/shop khác → [] chứ không 404)
DELETE /shops/{shopId}/media/library/{assetId}            chỉ Owner                                                          → 204
GET    /shops/{shopId}/media/assets?ids=a&ids=b           (lặp tham số, ≤ 200 id, KHÔNG phải "a,b")                          → MediaAssetDto[]
GET    /shops/{shopId}/media/usage                                                                                          → { usedBytes }
PUT    /shops/{shopId}/logo                               multipart: file; chỉ Owner                                         → ShopLogoDto { libraryAsset, derivatives[] }
```

- `SlotUploadResultDto = { asset, libraryAsset? }`. **`asset` là record đặt vào tree.** Khi có tick "Lưu vào thư viện", `asset` là clone và `libraryAsset` là bản gốc trong thư viện.
- `MediaAssetDto = { id, storageKey, mimeType, width, height, sizeBytes, altText, focalPointX, focalPointY, originalFileName, folder, isInLibrary, preset, sourceAssetId, createdAt }`
- `ShopDto` có thêm `logoId: string | null` và (D4, #88) `logoStorageKey: string | null` — storage key **tương đối** của phái sinh `320x96,inside`, `null` khi chưa có logo hoặc chưa có phái sinh; KHÔNG phải URL. Response luôn có cả hai field, giá trị có thể là `null`. `ShopSummaryDto` (`GET /shops`) không đổi.

**Ảnh không đi qua API.** Mọi ảnh phục vụ tại `/media/{storageKey}`, trên mọi domain. Chỉ `resolveImage()` được nối chuỗi `/media/`.

## Việc FE cần làm

Theo plan §8, các mục F1–F5 ([plan.md](plan.md)):

1. `pnpm gen:api`. **Kiểm ngay hai điểm lạ của OpenAPI** (contract-diff §"quirks"):
   - Schema `MediaAssetDto2` là bản trùng của `MediaAssetDto`. Nếu Orval sinh ra 2 type TS trùng lặp, **dừng lại và báo** (quyết định Gate 1: sửa ở BE bằng schema transformer trước khi FE bám vào). Không tự viết type alias để lấp.
   - Form multipart không có `required`, nên `file` và `preset` sinh ra là optional. FE tự validate (Zod) cho hai field đó.
2. **F1 · `resolveImage`:** thay thân hàm, **giữ chữ ký** `(imageId, preset)` (#74). Thêm `mediaMap` vào `RenderContextValue`. Preset lệch thì `console.warn` nhưng vẫn render. Không được đụng `window`/`document` (#23). Thêm proxy `/media` vào `apps/portal` và `apps/web` (dev), giữ `changeOrigin: false`.
3. **F2 · dialog upload vào slot:**
   - `accept="image/jpeg,image/png,image/webp"`.
   - Client kiểm file ≤ 10 MB trước khi gửi.
   - Chọn focal point, ẩn khi preset là `inside`.
   - Checkbox "Lưu vào thư viện".
   - Hướng dẫn riêng khi gặp `MEDIA_HEIC_UNSUPPORTED`.
4. **F3 · picker Media Library:**
   - Danh sách có phân trang, upload vào thư viện, clone vào slot.
   - Xoá: gọi `references` trước, có tham chiếu thì **cảnh báo nhưng không chặn**.
   - Hiện dung lượng đã dùng.
5. **F4 · logo** trong trang sửa shop: nút upload chỉ hiện với Owner. Hiện logo bằng `mediaUrl(shop.logoStorageKey)` (phái sinh `320x96,inside`, BE trả sẵn trong `ShopDto`, #88); ngay sau khi upload có thể dùng `ShopLogoDto.derivatives`.
6. **F5 · `dev-registry`:** Hero/Gallery dùng dialog và picker thật. Tải lại trang thì điền `mediaMap` qua `GET …/media/assets?ids=`.

## Mã lỗi FE phải xử lý (ProblemDetails `error_code`)

| Mã | HTTP | Khi nào |
|---|---|---|
| `MEDIA_FILE_TOO_LARGE` | 413 | File vượt 10 MB (thực tế Kestrel chặn ở 11 MB) |
| `MEDIA_MULTIPART_REQUIRED` | 415 | Gửi sai content-type |
| `MEDIA_HEIC_UNSUPPORTED` | 422 | Ảnh HEIC. Hiện hướng dẫn chuyển sang JPEG |
| `MEDIA_UNSUPPORTED_FORMAT` · `MEDIA_TOO_MANY_PIXELS` · `MEDIA_CORRUPT_IMAGE` · `MEDIA_FILE_MISSING` | 422 | File không hợp lệ |
| `MEDIA_UNKNOWN_PRESET` | 422 | Preset không nằm trong 9 preset |
| `VALIDATION_ERROR` | 422 | Focal point ngoài [0,1], alt text quá dài… |
| `MEDIA_OWNER_REQUIRED` | 403 | Người gọi không phải Owner khi xoá khỏi thư viện hoặc upload logo |
| `SHOP_ACCESS_DENIED` | 403 | Không phải thành viên của shop |

## Ràng buộc

- **FE code với MSW trước**, chỉ bật API thật ở bước integration (workflow §4).
- **Không sửa tay** `packages/api-sdk/src/generated/**` và `packages/builder-components/generated/**`.
- **TS dùng `const object + union`, không dùng `enum`** (#19).
- **Tree lưu `{ imageId, alt? }`**, không bao giờ lưu `storageKey` hay URL. **Không bao giờ đặt id của bản Library vào tree**: lấy từ thư viện thì luôn gọi `clones` (#71).
- **Không có thao tác "lưu vào thư viện" sau khi đã upload** (#70). Muốn đổi focal point của ảnh upload thẳng thì upload lại. Ảnh có `sourceAssetId == null && !isInLibrary` thì ẩn điều khiển focal point.
- **Thiết lập Host header:** giống SHOP-001 brief (`admin.vsite.local`, proxy `changeOrigin: false`).

## Không thuộc phạm vi

- **Không sửa `backend/`.** Gặp bug BE hay contract sai thì dừng lại và báo (Rule 9).
- **Không đổi chữ ký `resolveImage`**, không đổi manifest hay `registry.lock.json`.
- Media Library đầy đủ (folder, search, bulk), `srcset`, ảnh Listing/Product, job dọn file: đều ngoài phạm vi (`08` §9).
- Chưa có Operations Engine / PageDraft (Bước 5), nên `dev-registry` chỉ giữ tree trong state local.

## Acceptance

- sha256 khớp `contract.lock`; `pnpm gen:api` không tay chỉnh; `pnpm test` xanh; UI chạy được **hoàn toàn bằng MSW**.
- Mỗi màn hình có đủ ba trạng thái loading, error và empty.
- **Tiêu chí dừng `08` §9, chạy tay một lần trên API thật:**
  - upload vào Hero theo **cả hai chế độ**, ảnh hiện đúng kích thước preset;
  - chọn lại ảnh Library cho một ô Gallery thì sinh clone mới;
  - upload logo thì sinh đủ phái sinh;
  - đọc được `/media/…` qua cả `admin.vsite.local` lẫn `{slug}.vsite.local:3000`.
