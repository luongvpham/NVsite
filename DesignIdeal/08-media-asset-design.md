# vsite — MediaAsset, Media Library, ảnh Listing/Product & pipeline ảnh (Bước 4)

> **STATUS:** `IMPLEMENTED` · **Tasks:** `MEDIA-001` · **Changelog:** `Docs/tasks/MEDIA-001/changelog.md` · **Stale:** `§2 và §3.5 tên bảng media_assets, cột DeletedAt, tên index ux_* (thực tế: bảng MediaAsset, IsDeleted, index tên EF) · §3.6 ví dụ Shop có 1200x630,cover (thực tế chỉ 320x96,inside + 96x96,cover) · §5 và §10 mục 7 Cache-Control immutable (thực tế max-age=3600) · §3.5 và §9 test 6/7 chưa được chặn ở tầng request — IMediaReferenceValidator chưa nối handler nào (Bước 5) · §4 quét tham chiếu mới chỉ Shop.LogoId · §9 tiêu chí dừng chưa được chạy tay trên API + UI thật`
> **Cửa vào:** [`00-INDEX.md`](00-INDEX.md)
>
> 📌 **§0 là nơi định nghĩa Quyết định `#69–#86` và `#88`.** Các quyết định cũ còn hiệu lực về ảnh (`#53`
> `#55` `#56` `#57` `#58`) định nghĩa ở `05` §0, `#64` ở `07` §0 — tài liệu này là **chi tiết** của
> chúng. Tra số ở [`DECISIONS.md`](DECISIONS.md).
>
> **Tài liệu này là nơi DUY NHẤT mô tả `MediaAsset` và mô hình file ảnh của `Listing`/`Product`.**
> `05` §9, §20 và `04` §4 chỉ còn trỏ về đây (#39.5).
>
> **Tài liệu liên quan:** `05` (§0, §7 `SitePublication`, §13 `ImageRatio`, §17 `Product`, §19 variant, §20 ảnh sản phẩm) ·
> `07` (§7.1 kind `image`, §7.4 kind `binding`) · `06` (§4, §5 ảnh dịch vụ) · `04` (§4 `Listing`) ·
> `02` (#1 — một `AppDbContext`, FK xuyên module được phép) · `config/reserved-routes.json` (#24)
>
> **Phạm vi:** entity `MediaAsset`, hai chế độ upload, Media Library + clone, ảnh dữ liệu nghiệp vụ,
> ảnh `Listing`/`Product` (không qua `MediaAsset`), vòng đời file, URL, danh sách preset, ranh giới Bước 4.
>
> **Vị trí trong lộ trình:** input trực tiếp của **Bước 4**. Bước 4 **có** migration.

---

## 0. Quyết định chốt trong tài liệu này

| # | Nội dung |
|---|---|
| **#69** | **Một bảng `MediaAsset` duy nhất.** Bản Library và bản đã crop phân biệt bằng `IsInLibrary` (bool) + `Preset`: **`Preset IS NULL` ⟺ `IsInLibrary = true`**, enforce bằng CHECK hai chiều. Không tách hai bảng, không có cột `Purpose` |
| **#70** | **Hai chế độ upload ảnh vào slot.** Shop tick *"Lưu vào thư viện"* → sinh **hai** record: bản Library (≤1600px, chưa crop) + clone crop theo preset của slot. Không tick → sinh **một** record crop đúng preset của slot, **không giữ bản gốc**. **Không có** thao tác "lưu vào thư viện" sau khi đã upload. Focal point chọn **trong dialog upload, trước khi server crop** |
| **#71** | **Lấy ảnh từ Library luôn clone.** Component Tree **không bao giờ** chứa id của record `IsInLibrary = true`. Clone chỉ sinh từ bản Library, **không bao giờ** từ một clone khác |
| **#72** | **Clone không cascade delete theo node.** Xoá component chỉ gỡ tham chiếu trong tree; record + file giữ lại, dọn theo retention của `SitePublication` (Bước 8). Điều kiện để #41 không vỡ |
| **#73** | **Ảnh dữ liệu nghiệp vụ = bản Library + bộ phái sinh sinh sẵn lúc upload.** Áp cho ảnh **không** nằm trong tree mà render qua `binding` hoặc bề mặt của vsite: `Shop.LogoId`, `Service.ImageId`, `ShopServiceGroup.ImageId`, `ShopProductCategory.ImageId`, `Website.DefaultOgImageId`, `Website.FaviconAssetId`, `Page.OgImageId`. Danh sách preset của bộ phái sinh **sinh bằng codegen** từ manifest bind nguồn đó, cộng tập cố định của bề mặt vsite. Resolve lúc render theo `(bản Library, preset)`. **Không** áp cho ảnh nội dung trong tree — ảnh đó theo #70/#71. **Không** áp cho ảnh `Listing`/`Product` — ảnh đó theo #79 |
| **#74** | **`resolveImage(imageId, preset)` giữ nguyên chữ ký** (`07` §7.1). `preset` là **assertion**: lệch với preset thật của asset thì **log cảnh báo và vẫn render**, không hard-fail |
| **#75** | **File ảnh bất biến — cấm ghi đè.** Áp cho mọi ảnh: `MediaAsset.StorageKey` và file của `Listing`/`Product`. Mọi thay đổi (re-crop, đổi focal point, thay ảnh) sinh **đường dẫn mới**. Cache nhờ đó để `immutable` mà không bao giờ stale |
| **#76** | **Tham chiếu ảnh vô hướng từ module khác dùng FK ghép `(ImageId, ShopId) → media_assets (Id, ShopId)`.** Chặn tenant ngay ở tầng DB, cùng khuôn `Listing.(TargetPageId, ShopId)` (`04` §4.1). Code module khác vẫn đọc/kiểm ảnh qua Public Contract interface của module Media (#1) |
| **#77** | **Tham chiếu ảnh không diễn đạt được bằng FK thì kiểm ở handler, trong MỘT câu query.** Gồm: `imageId` trong `page_drafts.Tree` và `Page.OgImageId` (bảng `pages` chưa có `ShopId`). Mọi id phải thuộc đúng `ShopId` hiện tại, chưa xoá mềm; id trong tree phải là clone (`IsInLibrary = false`), id nghiệp vụ phải là bản Library (`IsInLibrary = true`). Sai bất kỳ điều kiện nào → fail request, không silent-fix (#21.3) |
| **#78** | **Danh sách preset chốt 9 giá trị** (§7), chỉ dùng cho ảnh `MediaAsset`. Ảnh `Listing`/`Product` **không** đi qua whitelist preset — kích thước của chúng cố định ở #79 |
| **#79** | **Ảnh `Listing` và `Product` không dùng `MediaAsset`.** Lưu `ImageUrls text[]` = **đường dẫn tương đối của file full**; thứ tự trong mảng là thứ tự hiển thị, `[0]` là ảnh đại diện. Mỗi entity một thư mục `shops/{shopId}/listings/{id}/` hoặc `shops/{shopId}/products/{id}/`, tên file là uuid. Thumb cùng thư mục, suy ra bằng prefix tên file: **`thumb_`** cho mọi ảnh, **`fthumb_`** (lớn hơn) chỉ sinh cho ảnh đang là ảnh đại diện, sinh khi cần. Kích thước và luật tỉ lệ ở §8 |
| **#80** | **Ảnh variant là field `ProductVariant.ImageUrls text[]`** trên bảng variant — rỗng = dùng `Product.ImageUrls`. File nằm chung thư mục sản phẩm; nhiều variant được dùng chung một đường dẫn (upload một lần, "áp cho mọi variant cùng màu" chỉ copy đường dẫn) |
| **#81** | **Kiểm quyền lúc GHI đường dẫn ảnh, không kiểm lúc ĐỌC.** Mọi phần tử của `Listing.ImageUrls`, `Product.ImageUrls`, `ProductVariant.ImageUrls`, `ShopAttributeOption.SwatchImageUrl` phải bắt đầu bằng thư mục của đúng entity đó dưới `shops/{shopId}/` — `shopId` từ `TenantContext` (#21.4) — **và** file phải tồn tại. Sai → fail request. Đọc ảnh là công khai, không kiểm tenant |
| **#82** | **Pipeline ảnh và object storage ở namespace dùng chung `Imaging`** — `Vsite.Application.Common.Imaging` (interface, quy ước tên `thumb_`/`fthumb_`) + `Vsite.Infrastructure.Imaging` (implementation), `Imaging` thêm vào `ModuleBoundaryTests.SharedSegments`. Lý do: `Marketplace` (phase 1) và `Catalog` cần pipeline mà không phụ thuộc `Media`. **Upload logo là endpoint của `Media`**, ghi `Shop.LogoId` qua Public Contract `IShopLogoWriter` của `Shop` — chiều `Media → Shop` đã hợp lệ; cho `Shop` gọi `Media` là vòng tròn. `Media` giữ phase 2 trong `dependency-map.json` |
| **#83** | **Object storage sau interface `IObjectStorage`, hai implementation `LocalDisk` và `S3`**, chọn bằng setting `Storage:Provider` (mặc định `LocalDisk`). `S3` dùng được với MinIO. Ghi file dùng chế độ **không ghi đè** (#75). `/media/*` do .NET phục vụ bằng cùng interface — đổi provider không đổi URL |
| **#84** | **Thư viện xử lý ảnh là ImageSharp** — thuần managed, có sẵn auto-orient, xoá metadata, encoder webp. License Six Labors Split: miễn phí khi doanh thu năm < 1M USD; vượt ngưỡng phải mua license thương mại |
| **#85** | **Giới hạn upload: file ≤ 10 MB, ảnh ≤ 25 megapixel** — số pixel đọc từ header **trước khi decode** (chặn decompression bomb). Chỉ nhận JPEG / PNG / WebP (nhận diện bằng magic bytes). **HEIC bị từ chối** với `error_code` riêng; FE đặt `accept` là JPEG/PNG/WebP để iOS tự chuyển và hiện hướng dẫn khi gặp lỗi này |
| **#86** | **Prop `binding` thêm field additive `imagePresets`** — preset ảnh theo từng source (`Partial<Record<source, preset[]>>`), mỗi preset phải có trong whitelist (#64). Codegen gom thành **bộ phái sinh theo nguồn** ghi vào `packages/builder-components/generated/`, hợp với **tập cố định của bề mặt vsite** khai trong `config/image-presets.json`. BE đọc artifact đó lúc startup |
| **#88** | `ShopDto` **và `ShopSummaryDto`** (`GET /shops`) trả sẵn **`logoUrl`** = `"/media/" + storageKey` của phái sinh `320x96,inside` của logo (đường dẫn tương đối theo domain, dùng được trên mọi host; `null` nếu chưa có logo hoặc chưa có phái sinh). **Field DTO tên `*Url` mà Portal nhận do BE trả sẵn có tiền tố `/media/`**; DB vẫn chỉ lưu key tương đối; nội dung ảnh trong builder vẫn đi qua `resolveImage()`. Prefix + hàm dựng URL nằm ở MỘT chỗ BE (`ImagePaths.MediaUrl`, cùng hằng với route mount `/media`). `Shop` đọc qua port do chính `Shop` khai báo (`IShopLogoReader`, có bản tra theo lô một câu SQL cho `GET /shops`), adapter nằm ở `Media` (Shop không reference Media). Endpoint `GET …/library/{assetId}/derivatives` vẫn giữ cho các nguồn ảnh nghiệp vụ khác |

**Quyết định cũ đã viết đè theo mô hình này** (giữ số, nội dung mới — xem `05` §0, `07` §0):
`#53` (pipeline + URL `{domain}/media/{path}`, không image proxy runtime), `#55` (không `MediaVariant`,
quota, cách dọn), `#56` (ảnh sản phẩm là `ImageUrls`, vào builder qua Library), `#57` (`ImageRatio`
crop lúc upload), `#58` (`srcset` hoãn), `#64` (whitelist preset là lint build-time + bảng kích thước).

---

## 1. Vì sao không có image proxy runtime

Pipeline ảnh **sinh file đúng kích thước lúc đặt vào slot hoặc lúc upload**, không sinh lúc render
(#53). CDN trỏ thẳng object storage, phục vụ file tĩnh.

Một image proxy sinh phái sinh lúc render kéo theo bốn thứ MVP không cần trả giá:

| Thứ phải nuôi nếu có proxy | Vì sao sinh lúc đặt thì không cần |
|---|---|
| Một service xử lý ảnh **đang sống**, chịu tải public | Resize xảy ra trong request lưu draft / upload — đã có auth, đã rate-limit sẵn |
| Whitelist preset như **biên giới bảo mật** | Không có URL công khai nhận tham số kích thước nào để brute-force (#64) |
| Race condition khi nhiều request cùng cache-miss một ảnh | Mỗi file sinh đúng một lần, trong một request có chủ |
| Lifecycle policy dọn phái sinh không ai xem | Mỗi file gắn với một record DB — dọn bằng job quét tham chiếu (#55) |

**Cái giá đã chấp nhận:**

- **Ảnh upload thẳng (không tick thư viện) không đổi được focal point sau này** — phần bị cắt đã mất.
  Focal point chọn trong dialog upload. Inspector **ẩn** điều khiển focal point với record có
  `SourceAssetId IS NULL AND IsInLibrary = false`; muốn đổi thì upload lại. Ảnh lấy từ Library thì
  đổi được (clone lại từ bản Library).
- **Ảnh upload thẳng không sinh lại được** khi manifest đổi preset của slot. Ảnh đó chạy ở trạng thái
  preset drift (#74) — hiển thị hơi mờ hoặc hơi thừa, không vỡ. Ảnh từ Library và ảnh nghiệp vụ thì
  job sinh lại được vì còn bản Library.
- **Muốn dùng lại ảnh đã upload thẳng thì upload lại**, lần này tick "Lưu vào thư viện".
- **Thêm `srcset` sau này (#58) đắt hơn**: mỗi slot cần sinh nhiều file. Vẫn hoãn được vì component
  không tự dựng URL (#74).

---

## 2. Entity `MediaAsset`

Neo vào `Shop`, **không** neo `Website` — shop `ExternalOnly` không có website nhưng vẫn cần ảnh cho
ảnh website. **Không** dùng cho ảnh `Listing` và ảnh sản phẩm — hai loại đó là đường dẫn trong `ImageUrls` (#79, §8).

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ShopId` | uuid FK → `shops` | UNIQUE `(Id, ShopId)` — đích của FK ghép #76 |
| `StorageKey` | varchar(300) | Đường dẫn tương đối trên object storage. **Không phải URL.** **Bất biến** (#75) |
| `MimeType` | varchar(80) | Sau xử lý luôn là `image/webp` |
| `Width` / `Height` | int | Kích thước thật của **file này** |
| `SizeBytes` | bigint | |
| `AltText` | varchar(200)? | Mặc định; node trong tree có thể override |
| `FocalPointX` / `FocalPointY` | real | 0..1, mặc định `0.5`. Dùng **lúc sinh clone/phái sinh**, không dùng lúc render |
| `OriginalFileName` | varchar(200)? | Để shop nhận ra ảnh trong picker |
| `Folder` | varchar(100)? | Thư mục phẳng do shop đặt. Chỉ có nghĩa khi `IsInLibrary = true` |
| `IsInLibrary` | bool NOT NULL, default `false` | `true` = bản Library (≤1600px, giữ tỉ lệ gốc, chưa crop), hiện trong picker (#69) |
| `Preset` | varchar(40)? | Preset đã áp cho file này. `NULL` ⟺ `IsInLibrary = true` |
| `SourceAssetId` | uuid? FK → `media_assets` | Clone/phái sinh sinh từ bản Library nào. `NULL` = bản gốc (bản Library, hoặc ảnh upload thẳng). Không phải quan hệ sống — `ON DELETE SET NULL`, cùng khuôn `SeedTemplateId` (#45) |
| `CreatedAt` | timestamptz | |
| `DeletedAt` | timestamptz? | Xoá mềm |

**Ba loại record** — suy ra từ cột, không cần enum:

| Loại | `IsInLibrary` | `Preset` | `SourceAssetId` | Tính quota |
|---|---|---|---|---|
| Bản Library | `true` | `NULL` | `NULL` | ✅ |
| Ảnh upload thẳng vào slot | `false` | preset của slot | `NULL` | ✅ |
| Clone / phái sinh | `false` | preset | id bản Library | ❌ |

**Quota** = tổng `SizeBytes` của record `SourceAssetId IS NULL AND DeletedAt IS NULL` (#55).

### 2.1 Ràng buộc DB

Theo #17 — invariant nằm ở tầng mà migration tay, seed data và use-case mới đều không lách được.

```sql
-- [1] Bản Library chưa crop; mọi record khác đã crop theo đúng một preset (#69)
ALTER TABLE media_assets ADD CONSTRAINT ck_media_library_preset CHECK (
  (IsInLibrary = true  AND Preset IS NULL) OR
  (IsInLibrary = false AND Preset IS NOT NULL)
);

-- [2] Clone chỉ sinh từ bản Library, không sinh từ clone (#71)
--     Không diễn đạt được bằng CHECK -> enforce ở handler + test.
--     Cố ý KHÔNG dùng trigger — đây là quy tắc một chiều trên dữ liệu đang ghi, thuộc về handler.

-- [3] StorageKey duy nhất toàn hệ thống (#75)
CREATE UNIQUE INDEX ux_media_storage_key ON media_assets (StorageKey);

-- [4] Đích của FK ghép từ module khác (#76)
CREATE UNIQUE INDEX ux_media_id_shop ON media_assets (Id, ShopId);

-- [5] Picker — query nóng nhất
CREATE INDEX ix_media_library ON media_assets (ShopId, CreatedAt DESC)
  WHERE IsInLibrary = true AND DeletedAt IS NULL;

-- [6] Resolve phái sinh của ảnh nghiệp vụ theo (bản Library, preset) (#73)
CREATE INDEX ix_media_derivative ON media_assets (SourceAssetId, Preset)
  WHERE SourceAssetId IS NOT NULL;
```

### 2.2 Tham chiếu từ module khác (#76, #77)

| Nơi tham chiếu | Cách chặn tenant |
|---|---|
| `Shop.LogoId` · `Service.ImageId` · `ShopServiceGroup.ImageId` · `ShopProductCategory.ImageId` · `Website.DefaultOgImageId` · `Website.FaviconAssetId` | FK ghép `(ImageId, ShopId) → media_assets (Id, ShopId)` |
| `imageId` trong `page_drafts.Tree` · `Page.OgImageId` | Handler lúc lưu draft / lưu page, một câu query (`pages` chưa có `ShopId` trong `05` §4) |
| `seo.ogImageId` trong `site_publications.Snapshot` | Không kiểm lại — sao chép từ `Website`/`Page` lúc publish, đã kiểm lúc ghi nguồn |

Tất cả trỏ tới **bản Library** (ảnh nghiệp vụ) hoặc **clone** (ảnh trong tree) — không bao giờ trỏ vào
phái sinh của ảnh nghiệp vụ. Đường dẫn ảnh `Listing`/`Product` không nằm trong bảng này — kiểm theo #81.

---

## 3. Các luồng

### 3.1 Pipeline chung (#53)

```
File người dùng gửi lên
  → giới hạn kích thước file + số pixel (chặn decompression bomb) — 10 MB / 25 MP (#85)
  → validate MIME thật bằng magic bytes (KHÔNG tin extension)
  → strip toàn bộ EXIF (ảnh điện thoại chứa GPS — lộ địa chỉ nhà)
  → sinh file theo luồng bên dưới, encode webp (quality ~82)
```

**Không giữ file người dùng upload.** Bản lớn nhất được giữ có cạnh dài ≤1600px (bản Library, hoặc file
full của `Listing`/`Product`). Pipeline này **dùng chung** cho mọi ảnh — `MediaAsset` (§3.2–§3.6) và
`Listing`/`Product` (§8).

### 3.2 Upload thẳng vào slot — không tick thư viện

```
Shop kéo file vào một slot ảnh, chọn focal point trong dialog
  → pipeline chung
  → resize + crop THEO ĐÚNG preset slot khai trong manifest
  → INSERT MediaAsset { IsInLibrary: false, Preset: '<preset slot>', SourceAssetId: null }
  → tree lưu { imageId, alt? }
```

Không qua Library, không clone, một file duy nhất.

### 3.3 Upload vào slot — có tick "Lưu vào thư viện"

```
  → pipeline chung
  → INSERT bản Library { IsInLibrary: true, Preset: null }  (≤1600px, giữ tỉ lệ gốc)
  → crop từ bản Library theo preset slot
  → INSERT clone { IsInLibrary: false, Preset: '<preset slot>', SourceAssetId: <bản Library> }
  → tree lưu id của CLONE
```

Cả hai record sinh trong **một** request. Đây là cách duy nhất để một ảnh vào Library từ builder —
không có "lưu vào thư viện" sau khi đã upload (#70).

### 3.4 Chọn từ Library — luôn clone (#71)

```
Shop mở picker (chỉ thấy IsInLibrary = true), chọn ảnh cho slot
  → đọc bản Library
  → crop theo preset slot, FocalPoint của bản Library làm mặc định (shop chỉnh được)
  → INSERT clone { IsInLibrary: false, Preset: '<preset slot>', SourceAssetId: <bản Library> }
  → tree lưu id của CLONE
```

**Vì sao ép clone:** shop lắp ảnh Library vào Hero → publish (snapshot v5 lưu id bản Library) → xoá
ảnh khỏi thư viện → snapshot v5 bất biến (#41) → banner vỡ vĩnh viễn. Ép clone nghĩa là không tồn tại
đường nào để vỡ. Đây là cùng lý do với #56, áp cho mọi nguồn ảnh.

### 3.5 Kiểm tree lúc lưu draft (#77)

Một câu query cho toàn bộ id cần kiểm — `imageId` trong tree (phải là clone) và id nghiệp vụ không
có FK như `Page.OgImageId` (phải là bản Library). Cùng một khuôn query dùng cho handler lưu draft và
lưu page:

```sql
SELECT count(*) FROM media_assets
WHERE ShopId = @currentShopId AND DeletedAt IS NULL
  AND (   (Id = ANY(@treeImageIds)     AND IsInLibrary = false)
       OR (Id = ANY(@businessImageIds) AND IsInLibrary = true) )
-- count ≠ số id khác nhau → fail request
```

`ShopId` lấy từ `TenantContext`, không bao giờ từ body (#21.4). Tham chiếu có FK ghép (§2.2) thì DB
đã chặn tenant; handler chỉ còn kiểm `IsInLibrary = true` cho chúng.

### 3.6 Ảnh dữ liệu nghiệp vụ (#73)

```
Shop upload logo / ảnh dịch vụ / ảnh danh mục / ảnh OG
  → pipeline chung
  → INSERT bản Library { IsInLibrary: true, Preset: null }
  → với MỖI preset trong bộ phái sinh của nguồn đó:
      INSERT phái sinh { IsInLibrary: false, Preset: p, SourceAssetId: <bản Library> }
  → entity nghiệp vụ lưu id BẢN LIBRARY (Shop.LogoId, Service.ImageId, Website.DefaultOgImageId…)

Render
  → binding resolver đọc id bản Library + preset slot cần
  → tra (SourceAssetId, Preset)  — index [6]
  → trả StorageKey của phái sinh
```

Portal (không đi qua resolver) đọc logo shop qua **`ShopDto.logoUrl`** và **`ShopSummaryDto.logoUrl`** (#88) —
`/media/{storageKey}` của phái sinh `320x96,inside`, `null` nếu chưa có logo/phái sinh; FE dùng thẳng
`<img src={shop.logoUrl}>`. `Shop` lấy giá trị qua port `IShopLogoReader` do chính `Shop` khai báo (một bản tra
đơn cho `ShopDto`, một bản tra theo lô một câu SQL cho `GET /shops`), adapter nằm ở `Media` (Shop không
reference Media). Với các nguồn ảnh nghiệp vụ khác, Portal tra cùng cặp `(SourceAssetId, Preset)` bằng
`GET /shops/{shopId}/media/library/{assetId}/derivatives?preset=` — vẫn trả phái sinh khi bản Library đã soft
delete (A11); id lạ/của shop khác → `[]`, không 404.

**Bộ phái sinh của một nguồn** = hợp của hai tập:

1. **Codegen từ manifest:** mọi preset mà component bind nguồn đó dùng để hiển thị ảnh. Hiện prop
   `binding` chỉ khai `sources` — **cần thêm một field additive** để manifest khai preset ảnh của
   dữ liệu bind (#43). Đây là thay đổi Component Registry, qua duyệt contract ở Bước 4.
2. **Tập cố định của bề mặt vsite** không đi qua manifest: Shop Profile (`vsite.vn/shop/{slug}`),
   OG image.

Ví dụ artifact sinh ra: `{ "Service": ["800x600,cover"], "Shop": ["320x96,inside", "1200x630,cover"], … }`.
Artifact nằm ở `packages/builder-components/generated/`, tập cố định của bề mặt vsite khai trong `config/image-presets.json` (#86).

**Nguồn `Shop` cần thêm vào `config/binding-sources.json`** để component bind được logo — hiện file
chưa có `Shop`.

**Component mới dùng preset chưa có trong bộ** → job sinh bù phái sinh từ bản Library. Luôn làm được,
vì ảnh nghiệp vụ luôn có bản Library.

**Đổi logo** = update `Shop.LogoId`; mọi nơi bind tự đúng. Đây là lý do ảnh nghiệp vụ không clone vào
tree như ảnh nội dung.

**Ranh giới:** chỉ ảnh **thuộc về entity nghiệp vụ** đi đường này. Không mở cơ chế binding cho ảnh nội
dung của trang — mở ra là quay lại đúng bài toán snapshot vỡ ở §3.4.

---

## 4. Vòng đời file

Ảnh `Listing`/`Product` chết theo entity — xoá entity là xoá cả thư mục của nó (§8.4). Clone của
`MediaAsset` **không** cascade theo node (#72).

| Hành động | Record | File trên storage |
|---|---|---|
| Xoá component khỏi draft | Giữ nguyên | Giữ nguyên |
| Đổi ảnh khác cho slot | Record cũ giữ, tạo record mới | Cả hai cùng tồn tại |
| Xoá ảnh khỏi Library | Soft delete, ẩn khỏi picker. Clone và phái sinh **không bị ảnh hưởng**, vẫn render | Giữ tới khi hết tham chiếu |
| `SitePublication` cũ bị prune (ngoài 20 bản) | Đủ điều kiện dọn | Đủ điều kiện dọn |

**Xoá khỏi Library:** chỉ chủ shop xoá được. Trước khi xoá, quét tham chiếu (tree, snapshot, và các
entity nghiệp vụ ở §2.2 qua Public Contract) → **cảnh báo, không chặn**.

**Ai thật sự dọn file:** Hangfire job định kỳ quét record không còn được tham chiếu bởi bất kỳ
`page_drafts.Tree`, `site_publications.Snapshot`, hay entity nghiệp vụ nào (§2.2) → soft delete → sau
grace period mới xoá file thật và record. Xoá cứng bản Library → `SourceAssetId` của clone về `NULL`.
**Việc của Bước 8**, không phải Bước 4. Bước 4 chỉ đảm bảo **không xoá gì cả** ngoài thao tác shop
chủ động xoá khỏi Library.

> ⚠️ Cảnh báo cho agent: cám dỗ tự nhiên khi code Operations Engine (Bước 5) là "xoá node thì xoá
> luôn ảnh cho sạch". Làm thế là vỡ #41. Node delete **chỉ** đụng tree.

---

## 5. URL

```
https://{domain bất kỳ}/media/{relativePath}
https://vsite.vn/media/shops/77/website/2026/03/a3f21.webp
https://spa-abc.com/media/shops/77/products/9c1e…/thumb_5b7d….webp
```

**Áp cho mọi ảnh** (#53): `MediaAsset` (`relativePath` = `StorageKey`) và `Listing`/`Product`
(`relativePath` = phần tử `ImageUrls`, hoặc bản `thumb_`/`fthumb_` của nó). Domain nào cũng truy cập
được — site chính, `{slug}.vsite.vn`, custom domain của shop. Đọc ảnh **không** kiểm tenant (#81).

| Nguyên tắc | Lý do |
|---|---|
| **`media` là reserved route** trong `config/reserved-routes.json` (#8, #24) | Chế độ path-based `vsite.vn/{slug}`: không shop nào được lấy slug `media`, và route trang không bao giờ đè lên ảnh |
| Tầng phía trước (.NET / Caddy) chuyển `/media/*` thẳng về object storage, **không** qua xử lý ảnh | Không có image proxy runtime (#53) — chỉ phục vụ file tĩnh. Đặt CDN phía trước sau này không đổi URL |
| DB lưu **đường dẫn tương đối**, **không** lưu URL có domain | Cùng một ảnh dùng được trên mọi domain của shop; đổi hạ tầng không phải rewrite `SitePublication.Snapshot` (#41) |
| Tree lưu `imageId`, **không** lưu `StorageKey` | Một lớp gián tiếp: đổi được cấu trúc thư mục của ảnh website |
| Component **không bao giờ** tự nối chuỗi URL | Ảnh trong nội dung builder luôn đi qua `resolveImage(imageId, preset)` — cùng tinh thần `resolveUrl()` của #11; chính `resolveImage` thêm tiền tố `/media/`. Field DTO tên `*Url` mà Portal nhận (vd. `ShopDto.logoUrl`, #88) do BE trả sẵn với tiền tố `/media/` (đường dẫn tương đối theo domain, chạy trên mọi host); prefix và hàm dựng nằm ở MỘT chỗ BE (`ImagePaths.MediaUrl`) |
| File bất biến (#75) | Cache-Control `immutable` được — chốt cùng cache publish ở Bước 8 |

---

## 6. `resolveImage()` (#74)

`07` §7.1 chốt: *"Bước 4 thay thân hàm, KHÔNG đổi chữ ký"*.

```ts
// Bước 2 (stub):
resolveImage: (id, preset) => `/_dev/placeholder/${preset}.svg`

// Bước 4: preset là ASSERTION, không còn là tham số dựng URL
resolveImage: (id, preset) => {
  const asset = ctx.mediaMap[id];
  if (!asset) return PLACEHOLDER;
  if (asset.preset !== preset) {
    console.warn(`[media] preset drift: asset=${id} có '${asset.preset}', slot cần '${preset}'`);
    // VẪN render — lệch metadata là lỗi hiển thị, không được biến thành trang trắng
  }
  return `/media/${asset.storageKey}`;   // tương đối — chạy đúng trên mọi domain
}
```

**Preset drift** xảy ra khi manifest đổi preset của một slot mà ảnh cũ chưa được sinh lại (ảnh upload
thẳng thì không bao giờ sinh lại được — §1). Đó là vấn đề chất lượng hiển thị, không phải đúng/sai.

**Vẫn giữ tham số dù hiện tại "thừa":** khi làm `srcset` (#58), nó lại có nghĩa thật. Bỏ bây giờ là
phải sửa mọi component sau này.

Ảnh nghiệp vụ đi qua binding resolver hoặc bề mặt vsite (§3.6), cũng trả về qua `resolveImage` — resolver điền
`mediaMap` bằng phái sinh đúng preset.

---

## 7. Danh sách preset (#78)

Nguồn duy nhất: `config/image-presets.json`, dùng chung FE/BE. **Chỉ cho ảnh `MediaAsset`** — ảnh
`Listing`/`Product` có kích thước cố định riêng (§8). Vai trò (#64): **lint build-time** cho manifest,
và **bảng kích thước** pipeline BE dùng để crop. Kích thước chọn ~2× độ rộng CSS lớn nhất
(#58). `cover` = crop lấp đầy khung theo focal point; `inside` = thu vừa khung, **không crop**.

| # | Preset | Dùng cho |
|---|---|---|
| 1 | `1600x900,cover` | Hero full-width (16:9) |
| 2 | `1600x600,cover` | Banner mỏng, nền section |
| 3 | `1200x630,cover` | OG image (chuẩn Facebook) |
| 4 | `1200x1200,inside` | Ảnh trong nội dung (RichText, bài viết), giữ tỉ lệ gốc |
| 5 | `800x800,cover` | Gallery, ảnh vuông |
| 6 | `800x600,cover` | Thẻ dịch vụ / thẻ nội dung (4:3) |
| 7 | `320x96,inside` | Logo trên Header — không crop (logo thường nằm ngang) |
| 8 | `160x160,cover` | Thumbnail dải ảnh |
| 9 | `96x96,cover` | Avatar |

**Không có preset cho ảnh sản phẩm.** Component như `ProductGrid` hiển thị ảnh sản phẩm qua binding
bằng `fthumb_`/`thumb_`/file full (§8) — không qua `image-presets.json`.

Chọn theo độ rộng CSS (ví dụ 300px cho thẻ sản phẩm) là ảnh mờ trên **mọi** điện thoại đời mới (DPR ≥ 2).

**Việc Bước 4 phải làm với config:** thêm `fit: "inside"`, thêm 4 preset mới (`1600x600,cover`,
`1200x1200,inside`, `800x600,cover`, `320x96,inside`), **bỏ** `600xR,cover` (không manifest nào dùng).
Hai preset manifest đang dùng (`1600x900,cover`, `800x800,cover`) giữ nguyên — `registry.lock.json`
không đổi.

---

## 8. Ảnh `Listing` & `Product` (#79, #80, #81)

Ảnh của một listing hoặc một sản phẩm **chỉ thuộc về đúng entity đó**, không dùng lại ở nơi khác.
Nên không cần Library, clone hay bộ phái sinh — chỉ **file full + thumb**, quản lý bằng thư mục.

### 8.1 Lưu trữ

```
Listing.ImageUrls                   text[] NOT NULL              -- ≥ 1 ảnh
Product.ImageUrls                   text[] NOT NULL DEFAULT '{}'
ProductVariant.ImageUrls            text[] NOT NULL DEFAULT '{}' -- rỗng = dùng Product.ImageUrls (#80)
ShopAttributeOption.SwatchImageUrl  text?                        -- swatch vân vải, chỉ cần thumb_

-- mỗi phần tử = đường dẫn tương đối của FILE FULL, ví dụ:
--   shops/77/products/9c1e…/5b7d….webp
```

```
shops/{shopId}/listings/{listingId}/{uuid}.webp           ← full
shops/{shopId}/listings/{listingId}/thumb_{uuid}.webp     ← mọi ảnh
shops/{shopId}/listings/{listingId}/fthumb_{uuid}.webp    ← chỉ ảnh đã/đang là ảnh đại diện
shops/{shopId}/products/{productId}/…                     ← cùng khuôn, gồm cả ảnh của variant
shops/{shopId}/attributes/{attributeId}/…                 ← swatch
```

- **`[0]` là ảnh đại diện.** Thứ tự trong mảng là thứ tự hiển thị. Không có `PrimaryImageId`.
- **Tên file là uuid**, sinh mới mỗi lần upload — thay ảnh không bao giờ ghi đè file cũ (#75).
- **Thumb suy ra bằng prefix**, không lưu trong DB. Quy ước tên nằm ở **một** hàm dùng chung FE/BE —
  không nối chuỗi `thumb_` rải rác (#17).

### 8.2 Kích thước

Kích thước chọn ~2× độ rộng CSS (#58) — màn hình điện thoại có DPR ≥ 2.

| | `Listing` | `Product` (kể cả variant) |
|---|---|---|
| File full | cạnh dài ≤1600px, **giữ tỉ lệ gốc** | cạnh dài ≤1600px, **crop theo `ImageRatio` của danh mục lúc upload** (#57) |
| `fthumb_` (ảnh đại diện) | **640×480**, crop 4:3 | rộng **600**, cao theo tỉ lệ danh mục (vd `R2x3` → 600×900) |
| `thumb_` (mọi ảnh) | **160×120**, crop 4:3 | rộng **160**, cao theo tỉ lệ danh mục |

| Vị trí hiển thị | Độ rộng CSS | Dùng file |
|---|---|---|
| Thẻ listing trên marketplace / bản đồ | ~340px (mobile 1 cột) | `fthumb_` 640 |
| Thẻ sản phẩm trong lưới | 170–280px (2 cột mobile – 4 cột desktop) | `fthumb_` 600 |
| Dải thumbnail trang chi tiết | 60–80px | `thumb_` 160 |
| Ảnh chính trang chi tiết, OG | — | file full |

**Listing dùng 4:3 cố định:** `ServiceCategory` không có tỉ lệ, và thẻ trên marketplace phải đều nhau
giữa mọi shop.

**Product giữ tỉ lệ danh mục:** thời trang chụp dọc (2:3); crop về 4:3 ngang là cắt mất người mẫu.
Shop chọn tỉ lệ theo danh mục chính là để lưới sản phẩm đều. Lưới trộn nhiều danh mục thì UI đặt ảnh
trong khung cố định với `object-fit: cover`.

**Crop file full của Product diễn ra lúc upload**, shop chọn vùng crop trong dialog. Hệ quả đã chấp
nhận: đổi `ImageRatio` của danh mục hoặc chuyển sản phẩm sang danh mục khác thì **ảnh cũ giữ tỉ lệ cũ**
— vẫn hiển thị được nhờ `object-fit: cover`; muốn đúng tỉ lệ mới thì upload lại.

### 8.3 Luồng

```
Upload ảnh cho listing/product
  → pipeline chung (§3.1)
  → Product: crop theo ImageRatio danh mục (vùng crop shop chọn) · Listing: giữ tỉ lệ gốc
  → ghi {uuid}.webp + thumb_{uuid}.webp
  → nếu ảnh này thành [0] → ghi thêm fthumb_{uuid}.webp

Đổi ảnh đại diện (đổi thứ tự mảng, ảnh mới lên [0])
  → fthumb_ của ảnh mới chưa có → sinh từ file full
  → fthumb_ của ảnh đại diện cũ để nguyên (không ghi đè, dọn cùng thư mục)
```

**Không vi phạm #75:** `fthumb_{uuid}` luôn sinh từ cùng một file full bất biến, nên sinh lại vẫn ra
cùng nội dung. Chỉ sinh `fthumb_` khi cần — không sinh sẵn hai cỡ cho mọi ảnh.

**Ảnh variant (#80):** chỉ cần file full + `thumb_` — lưới sản phẩm luôn hiện `Product.ImageUrls[0]`,
nên `fthumb_` chỉ sinh cho ảnh đó. Chọn variant ở trang chi tiết thì ảnh chính là file full của
`ProductVariant.ImageUrls[0]`. Đỏ/S, Đỏ/M, Đỏ/L thường cùng một bộ ảnh: shop upload **một lần**, UI có
nút *"áp cho mọi variant cùng màu"* — copy **đường dẫn**, không copy file.

**Không gắn ảnh vào option màu** (kiểu Shopee): `ShopAttributeOption` "Đỏ" dùng chung cho mọi sản phẩm
của shop, muốn ảnh theo từng sản phẩm phải thêm bảng trung gian `(ProductId, OptionId, ImageUrls)`.
Dùng chung đường dẫn cho kết quả tương tự mà không thêm bảng.

### 8.4 Kiểm quyền lúc ghi & vòng đời (#81)

| Việc | Luật |
|---|---|
| Ghi `Listing.ImageUrls` | Mọi phần tử bắt đầu bằng `shops/{shopId}/listings/{listingId}/` và file tồn tại |
| Ghi `Product.ImageUrls` / `ProductVariant.ImageUrls` | Bắt đầu bằng `shops/{shopId}/products/{productId}/` và file tồn tại |
| Ghi `SwatchImageUrl` | Bắt đầu bằng `shops/{shopId}/attributes/{attributeId}/` và file tồn tại |
| Xoá một ảnh khỏi sản phẩm | Gỡ đường dẫn khỏi `Product.ImageUrls` **và mọi** `ProductVariant.ImageUrls` trong **cùng transaction**; xoá file khi không còn chỗ nào trong sản phẩm trỏ tới |
| Xoá listing / sản phẩm | Xoá cả thư mục của entity — không cần job quét tham chiếu |

`shopId` lấy từ `TenantContext`, không bao giờ từ body (#21.4). Sai bất kỳ điều kiện nào → fail
request. Đọc ảnh là công khai (§5).

**Dùng ảnh sản phẩm trong builder (#56):** *"Thêm vào thư viện"* copy file full sang một **bản
Library** `MediaAsset` mới → từ đó hai bản độc lập → lắp vào slot thì clone như mọi ảnh Library (#71).
Builder picker **không bao giờ** thấy ảnh sản phẩm trực tiếp.

---

## 9. Ranh giới Bước 4

| Có làm | Không làm |
|---|---|
| Entity `MediaAsset` + migration + ràng buộc §2.1 | Upload ảnh `Listing` (làm cùng module Listing) và `Product` (Bước 10) — nhưng dùng lại pipeline + hàm quy ước tên của Bước 4 |
| Pipeline **dùng chung**: giới hạn kích thước → magic bytes → strip EXIF → resize/crop → webp; hàm quy ước tên `thumb_`/`fthumb_` (§8.1) | Image proxy runtime — **không tồn tại** trong thiết kế |
| Hai chế độ upload vào slot (#70) + chọn từ Library → clone (#71) | `srcset` / responsive (#58) |
| API: upload, list library, chọn-từ-library (clone), soft delete, tra phái sinh theo (bản Library, preset) (`.../library/{assetId}/derivatives`) | Job dọn file mồ côi (Bước 8) |
| Thay thân `resolveImage()`, giữ chữ ký · phục vụ `/media/*` + reserved route `media` (§5) | Job re-crop khi đổi `ImageRatio` — **không có**, đổi tỉ lệ thì upload lại (#57) |
| `config/image-presets.json` đủ 9 preset (#78) | Media Library UI đầy đủ (folder, search, bulk) — MVP chỉ list + upload + xoá |
| `Shop.LogoId` + FK ghép + bộ phái sinh logo (#73, #76) | Bảng `MediaVariant` — **cố ý không có** (#55) |
| Field additive trên prop `binding` + artifact codegen bộ phái sinh + `Shop` trong `binding-sources.json` | Binding Resolver thật cho `Service`/`Product` (Bước 9, 10) |
| Tính quota trên bản gốc (#55) | Giới hạn quota theo gói (`05` §25 #1) |

**Tiêu chí dừng:** upload được ảnh vào slot của Hero/Gallery (Bước 2) theo cả hai chế độ, ảnh hiện đúng
kích thước preset; chọn lại ảnh Library cho slot khác sinh ra clone độc lập; upload logo sinh đủ bộ phái
sinh; test khẳng định tree không chứa id `IsInLibrary = true`.

**Test bắt buộc:**

1. File `.jpg` đổi tên thành `.png` → bị từ chối bởi magic bytes
2. Ảnh có EXIF GPS → sau upload không còn EXIF (đọc lại file thật, không mock)
3. Upload không tick thư viện → đúng một record, `Preset` = preset slot, `SourceAssetId IS NULL`
4. Upload có tick → hai record, clone có `SourceAssetId` = bản Library
5. Chọn từ Library → record mới, `StorageKey` khác, `SourceAssetId` đúng
6. Ghi id `IsInLibrary = true` vào tree → request fail
7. Ghi id ảnh của shop khác vào tree → request fail (#77)
8. Ghi `Shop.LogoId` trỏ ảnh của shop khác → DB từ chối (FK ghép #76)
9. Xoá ảnh khỏi Library → clone vẫn render
10. Quota không tăng khi clone / sinh phái sinh
11. Insert `IsInLibrary = false, Preset = NULL` → DB từ chối (CHECK [1])
12. `GET /media/{relativePath}` trả đúng file trên cả domain chính lẫn domain shop; shop không đăng ký được slug `media`

Test 7, 8, 11 cần Testcontainers — không có Docker thì ghi `Docs/DOCKER-TEST-DEBT.md`.

---

## 10. Điểm còn trống

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | Giới hạn dung lượng file + số pixel tối đa khi upload | ✅ #85 |
| 2 | HEIC từ iPhone | ✅ #85 — từ chối kèm hướng dẫn |
| 3 | Tên field additive trên prop `binding` + vị trí artifact bộ phái sinh | ✅ #86 — tên file cụ thể chốt ở Gate duyệt Component Registry của MEDIA-001 |
| 4 | Tái dùng clone có cùng `(SourceAssetId, Preset, FocalPoint)` thay vì sinh file mới | ⏳ Tối ưu, an toàn nhờ #75. Không làm ở MVP |
| 5 | Nhúng `storageKey` vào snapshot lúc publish để runtime khỏi tra DB | ⏳ Bước 8 |
| 6 | Grace period trước khi xoá file thật | ⏳ Bước 8 |
| 7 | Cache-Control `immutable` cho CDN | ⏳ Bước 8, cùng cache publish |
| 8 | Giới hạn dung lượng media theo gói dịch vụ (`05` §25 #1) — gồm cả cách tính quota ảnh `Listing`/`Product` (không có `SizeBytes` trong DB) | ⚠️ Vẫn mở, chặn việc bật thu phí |
| 9 | Favicon (cần PNG, không phải webp) | ⏳ Ngoài phạm vi Bước 4 |
| 10 | Upload ảnh trước khi entity có `Id` (form tạo mới cần thư mục `{id}`): tạo entity `Draft` trước, hay upload vào thư mục tạm rồi chuyển | ⚠️ Chốt ở task Listing / Bước 10 |
| 11 | Số ảnh tối đa mỗi listing / sản phẩm / variant | ⚠️ Chốt ở task Listing / Bước 10 |
