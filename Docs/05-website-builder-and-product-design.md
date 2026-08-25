# vsite — Thiết Kế Entity: Website Builder & Product (Phase 2)

> **Tài liệu liên quan:** `01-project-ideal.md` (§5.3, §6, §7) · `02-tech-stack-and-decision.md` (Quyết định #11–#17, #33–#36) · `03-identity-entity-design.md` · `04-listing-and-review-design.md`
>
> **Phạm vi:** hai module Phase 2 — `Website` (builder + renderer) và `Product` (hàng hoá). Module `Service` thiết kế riêng, tài liệu này chỉ tham chiếu tới nó ở phần Data Binding.
>
> **Quy ước:** PostgreSQL, tên bảng snake_case số nhiều, tên entity C# PascalCase số ít. Mọi bảng thuộc shop đều có `ShopId` và tuân Quyết định #21 (tenant isolation).

---

## 0. Tóm tắt quyết định mới chốt trong phiên này

Các quyết định dưới đây cần được chép sang `02-tech-stack-and-decision.md` với số hiệu tương ứng.

| # | Nội dung |
|---|---|
| **#40** | `Product` là module **riêng biệt**, không gộp với `Service`. Không dùng chung enum trạng thái, không dùng chung bảng |
| **#41** | Đơn vị publish là **toàn site** (`SitePublication` snapshot bất biến), không publish từng trang |
| **#42** | Không persist op-log. Undo/redo chỉ tồn tại client-side (zundo) trong một phiên soạn thảo |
| **#43** | Component manifest **additive-only**: cấm đổi kiểu hoặc xoá prop đã phát hành. Thay đổi phá vỡ tương thích → tạo `variant` mới. Hệ quả: node trong tree **không cần** `schemaVersion` |
| **#44** | Draft **persist server-side** (`PageDraft`), không chỉ ở Zustand |
| **#45** | Template là **seed copy một lần**, không phải reference. Shop sửa template không ảnh hưởng template gốc và ngược lại |
| **#46** | `System` page **có Component Tree thật**, khoá ở tầng Operations Engine — không tách thành cấu trúc lưu trữ khác |
| **#47** | Form trên website shop **POST tới module `Lead`**. Builder không sở hữu submission |
| **#48** | Hàng hoá (`Product`) **không lên marketplace**. `Listing` chỉ neo `Service`. Không có giỏ hàng / đơn hàng / thanh toán ở Phase 2 |
| **#49** | Tồn kho là **tuỳ chọn** (`Product.TrackInventory`), mặc định tắt |
| **#50** | Attribute `Text` **không filter, không thống kê**. Chỉ hiển thị |
| **#51** | Xoá attribute/option là **xoá mềm** (`IsArchived`), không xoá cứng |
| **#52** | Tối đa **3 trục variant** mỗi sản phẩm |
| **#53** | Ảnh **resize lúc upload** về cạnh dài ≤1600px, **giữ nguyên tỉ lệ gốc**, chuyển webp, strip EXIF. Không giữ file người dùng upload. Phái sinh sinh **lúc render** qua image proxy trên **một domain CDN dùng chung cho mọi shop**. Kích thước giới hạn trong whitelist preset. Component không tự dựng URL ảnh, luôn qua `resolveImage()` |
| **#55** | **Không có bảng `MediaVariant`.** Key phái sinh là xác định (deterministic) nên object storage đã đóng vai trò index. Dọn dẹp bằng lifecycle policy của storage, không bằng Hangfire job quét DB. Quota tính trên **bản gốc**, không tính phái sinh |
| **#56** | Ảnh sản phẩm và thư viện media là **hai hệ thống độc lập** (`product_images` vs `media_assets`). Ảnh sản phẩm chết theo sản phẩm. Muốn dùng ảnh sản phẩm trong builder, shop phải bấm **"Thêm vào thư viện"** → clone sang `MediaAsset` mới → hai bản độc lập từ đó. **Builder picker không bao giờ thấy `product_images`** |
| **#57** | Tỉ lệ ảnh sản phẩm do **danh mục** quyết định (`ShopProductCategory.ImageRatio`), và **cắt lúc render** chứ không cắt lúc upload. Đổi tỉ lệ danh mục = đổi một dòng config, toàn bộ ảnh tự cắt lại, không phải upload lại |
| **#58** | **Không hỗ trợ `srcset` responsive ở Phase 2.** Mỗi vị trí ảnh dùng một preset cố định, chọn ở mức ~2× độ rộng CSS lớn nhất để đủ nét trên màn retina. Hoãn được vì URL sinh từ `resolveImage()` — thêm `srcset` sau chỉ cần sửa hàm đó, không migrate dữ liệu |
| **#54** | `Service` **không có `System` page riêng** (không `ServiceListing`/`ServiceDetail`). Số dịch vụ mỗi shop nhỏ (thường 2–3), không có filter/facet/variant — hiển thị hoàn toàn qua component bind trên trang `Composable`, cùng khuôn với `Timeline`/`Gallery`. Nếu shop cần trang riêng cho một dịch vụ, họ tạo `Page` thường và lắp component — không cần route hệ thống cố định. Phù hợp với cách `Listing.TargetPageId` (`04` §4) chỉ trỏ tới `ShopHome`/`ShopPage`/`ExternalUrl`, không có đích kiểu `ServiceDetail` |

---

# PHẦN I — MODULE `Website` (Builder)

## 1. Sơ đồ quan hệ

```
Shop (Kind = Hosted)
  └─1:1─ Website
           ├─1:1── Theme
           ├─1:1── NavigationConfig
           ├─1:n── Page ──1:1── PageDraft        (bản đang sửa)
           └─1:n── SitePublication               (bản đã xuất bản, bất biến)

Shop
  └─1:n─ MediaAsset        (dùng chung: builder, listing, product)

WebsiteTemplate            (design-time, không thuộc shop nào)
```

`MediaAsset` cố ý neo vào `Shop` chứ **không** neo `Website`: shop `ExternalOnly` không có website nhưng vẫn cần ảnh cho `Listing`.

---

## 2. `Website`

Gốc của module. Một shop `Hosted` có đúng một website.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ShopId` | uuid FK → `shops` | **UNIQUE** — enforce 1:1 |
| `Status` | enum | `Draft` · `Published` · `Suspended` |
| `SeedTemplateId` | uuid? FK → `website_templates` | Chỉ để thống kê/hỗ trợ. **Không** phải quan hệ sống (#45) |
| `CurrentPublicationId` | uuid? FK → `site_publications` | Bản đang phục vụ công chúng. NULL = chưa publish lần nào |
| `DefaultSeoTitle` | varchar(160) | Fallback khi `Page.SeoTitle` rỗng |
| `DefaultSeoDescription` | varchar(320) | |
| `DefaultOgImageId` | uuid? FK → `media_assets` | |
| `FaviconAssetId` | uuid? FK → `media_assets` | |
| `TrackingSnippets` | jsonb | `{ ga4?: string, metaPixel?: string }` — whitelist ID, **không** cho nhập HTML thô |
| `CreatedAt` / `UpdatedAt` | timestamptz | |

**Ràng buộc bảo mật:** `TrackingSnippets` chỉ nhận **mã ID** đã validate regex (`^G-[A-Z0-9]{6,}$`), không bao giờ nhận `<script>`. Cho nhập HTML tự do vào site nhiều tenant là mở XSS trên chính domain của vsite ở chế độ path-based (`vsite.vn/{slug}`) — nghĩa là cookie của vsite.vn bị lộ. Đây là ràng buộc cứng.

**Vì sao SEO mặc định nằm ở `Website` chứ không thành bảng `SiteSettings` riêng:** chỉ có 5 trường và luôn 1:1. Bảng riêng chỉ thêm một join mà không thêm khả năng nào.

---

## 3. `Theme`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `WebsiteId` | uuid PK, FK | 1:1 |
| `PresetKey` | varchar(50)? | Preset gốc đã chọn (`spa-lavender`...), để hiển thị trong UI |
| `Tokens` | jsonb NOT NULL | Xem shape bên dưới |
| `UpdatedAt` | timestamptz | |

```json
{
  "colors": { "primary": "#7c3aed", "secondary": "#a78bfa",
              "background": "#ffffff", "surface": "#f9fafb",
              "text": "#111827", "textMuted": "#6b7280", "border": "#e5e7eb" },
  "typography": { "headingFont": "Be Vietnam Pro", "bodyFont": "Inter",
                  "scale": "comfortable" },
  "shape": { "radius": "md", "shadow": "soft" },
  "spacing": { "sectionGap": "lg" }
}
```

**Vì sao jsonb chứ không phải cột riêng cho từng token:** tập token sẽ phình theo thời gian (thêm `accent`, thêm `radiusButton`...), và **không có truy vấn nào lọc theo màu**. Đây khác hẳn `Product.Price` — cái đó bắt buộc phải là cột vì có filter/sort.

**Ràng buộc:** shape của `Tokens` do một Zod schema duy nhất định nghĩa, dùng chung FE/BE, validate khi ghi. Đây **không** phải "JSON blob tự do" kiểu `SiteConfig` của hệ 4.8 (#36) — khác biệt nằm ở chỗ có schema bắt buộc và phạm vi hẹp cố định.

`changeTheme` (operation thứ 5, #14) ghi vào đúng bảng này.

---

## 4. `Page`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `WebsiteId` | uuid FK | |
| `Kind` | enum | `Composable` · `System` (#33) |
| `SystemType` | enum? | NOT NULL khi `Kind = System`, NULL khi `Composable` |
| `Slug` | varchar(120) | `""` cho trang chủ. UNIQUE `(WebsiteId, Slug)` |
| `Title` | varchar(160) | Nhãn hiển thị trong portal + nguồn mặc định cho menu |
| `SeoTitle` / `SeoDescription` | varchar(160) / (320) | Rỗng → fallback về `Website` |
| `OgImageId` | uuid? FK | |
| `NoIndex` | bool | Mặc định `false` |
| `IsEnabled` | bool | Tắt = không render, không lên menu, giữ nguyên dữ liệu |
| `SortOrder` | int | Thứ tự trong danh sách quản lý (không phải thứ tự menu) |
| `CreatedAt` / `UpdatedAt` | | |

**`SystemType`** — tập đóng do vsite định nghĩa, shop không tạo/xoá được:

| Giá trị | Route | Có ở Phase |
|---|---|---|
| `ProductListing` | `/san-pham` · `/danh-muc/{categorySlug}` | 2 |
| `ProductDetail` | `/san-pham/{slug}` | 2 |
| `ProductSearch` | `/tim-kiem` | 2 |
| `Contact` | `/lien-he` | 2 |
| `NotFound` | — | 2 |
| `Booking` | `/dat-lich` | 3 |

**Ràng buộc:** UNIQUE `(WebsiteId, SystemType)` — mỗi loại trang hệ thống chỉ có một. `IsHomePage` **không** là cột: trang chủ là trang `Composable` có `Slug = ""`, đã được UNIQUE `(WebsiteId, Slug)` đảm bảo duy nhất. Bớt một cột phải giữ đồng bộ.

**Ràng buộc:** một `Website` phải luôn có ít nhất một `Page` với `Slug = ""` và các `System` page bắt buộc. Chúng được tạo tự động khi khởi tạo website từ template, và **không cho xoá** (`DELETE` bị chặn ở tầng handler, không dựa vào FK).

---

## 5. `PageDraft` — bản đang sửa

Tách khỏi `Page` vì hai lý do: tree là cột nặng (có thể vài trăm KB) không nên load khi chỉ cần metadata trang; và draft có vòng đời riêng (có thể discard mà không đụng `Page`).

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `PageId` | uuid PK, FK | 1:1 |
| `Tree` | jsonb NOT NULL | Component Tree, xem §6 |
| `IsDirty` | bool | `true` khi khác bản đã publish gần nhất |
| `UpdatedAt` | timestamptz | |
| `UpdatedByUserId` | uuid FK | Phục vụ Phase 4 (nhiều nhân viên cùng sửa) |

**Autosave:** FE debounce ~2s, ghi đè toàn bộ `Tree`. Không patch từng phần — tree của một trang đủ nhỏ để ghi cả, và patch từng phần mở ra cả lớp lỗi đồng bộ không đáng.

**Chống ghi đè lẫn nhau (Phase 4):** thêm `RowVersion` (xid/bigint) kiểm tra optimistic concurrency. Ở Phase 2 một shop chỉ có một người sửa nên chưa bật, nhưng cột nên có sẵn.

---

## 6. Component Tree — shape JSON

Không phải bảng, nhưng là cấu trúc dữ liệu quan trọng nhất module. Định nghĩa một lần bằng Zod, dùng chung FE/BE (#19).

```json
{
  "id": "root",
  "type": "Page",
  "children": [
    {
      "id": "c_a3f21",
      "type": "Hero",
      "variant": "Hero02",
      "props": { "title": "Thời trang công sở", "imageId": "media_8891" }
    },
    {
      "id": "c_b8e02",
      "type": "Timeline",
      "variant": "TimelineCircleTabs",
      "props": {
        "heading": "Chặng đường phát triển",
        "items": [
          { "year": "2018", "title": "Mở cửa hàng đầu tiên", "description": "<p>…</p>" },
          { "year": "2021", "title": "Mở chi nhánh quận 7",  "description": "<p>…</p>" }
        ]
      }
    },
    {
      "id": "c_c1d44",
      "type": "ProductGrid",
      "variant": "ProductGrid01",
      "props": {
        "heading": "Áo sơ mi mới về",
        "binding": {
          "source": "Product",
          "categoryId": 12,
          "includeSubCategories": true,
          "sort": "Latest",
          "take": 8,
          "showSubCategoryTabs": true
        }
      }
    }
  ]
}
```

**Quy tắc bất biến:**

1. `id` duy nhất **trong phạm vi một trang**, sinh client-side (`c_` + nanoid 5 ký tự). Là target của mọi operation (#14) và của Selection Context (#16).
2. **Không** có `schemaVersion` trên node — hệ quả trực tiếp của #43 (additive-only).
3. `props` **không bao giờ** chứa dữ liệu nghiệp vụ đã materialize, chỉ chứa nội dung tĩnh hoặc `binding` mô tả cách lấy.
4. Ảnh lưu bằng **id** (`imageId: "media_8891"`), không lưu URL. URL sinh lúc render — cho phép đổi CDN, đổi kích thước, xoá ảnh mà phát hiện được tham chiếu.
5. `children` chỉ có ở component container. Manifest khai `acceptsChildren: true/false`.

---

## 7. `SitePublication` — snapshot bất biến

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `WebsiteId` | uuid FK | |
| `Version` | int | Tăng dần theo website. UNIQUE `(WebsiteId, Version)` |
| `Snapshot` | jsonb NOT NULL | Toàn bộ site, xem shape bên dưới |
| `Note` | varchar(200)? | Shop tự ghi ("Đổi banner tết") |
| `PublishedAt` | timestamptz | |
| `PublishedByUserId` | uuid FK | |

```json
{
  "version": 7,
  "theme": { "...tokens..." },
  "navigation": { "items": [ ... ] },
  "seo": { "defaultTitle": "...", "defaultDescription": "...", "ogImageId": "..." },
  "pages": [
    { "slug": "", "kind": "Composable", "title": "Trang chủ",
      "seo": {...}, "tree": { ... } },
    { "slug": "gioi-thieu", "kind": "Composable", "tree": { ... } },
    { "slug": "san-pham", "kind": "System", "systemType": "ProductListing",
      "tree": { ... } }
  ]
}
```

**Vì sao snapshot toàn site chứ không tham chiếu ngược về `Page`:** đây là điều làm cho publish có ý nghĩa. Shop sửa 5 trang trong portal, publish một lần → công chúng thấy 5 trang thay đổi **đồng thời**, không có trạng thái nửa vời. Rollback = đổi `CurrentPublicationId`, một lệnh UPDATE, không cần replay gì. Cache invalidation theo `version`, cực đơn giản.

**Đánh đổi đã chấp nhận:** dữ liệu trùng lặp giữa các phiên bản. Với site 3–7 trang của shop nhỏ, mỗi snapshot cỡ vài chục tới vài trăm KB — không đáng lo. Giữ tối đa **20 bản gần nhất** mỗi website, Hangfire job dọn định kỳ.

**Runtime đọc gì:** `apps/web` chỉ đọc `site_publications` (qua cache), **không bao giờ** đọc `page_drafts`. Preview trong portal thì ngược lại. Đây là ranh giới cứng — vi phạm nghĩa là bản nháp rò ra công chúng.

---

## 8. `NavigationConfig`

Theo Quyết định #34: derived + manual overlay, một màn hình kéo-thả duy nhất.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `WebsiteId` | uuid PK, FK | 1:1 |
| `Items` | jsonb NOT NULL | Danh sách phẳng có `children` một cấp |
| `UpdatedAt` | | |

```json
{
  "items": [
    { "key": "page:{pageId}",  "label": null,           "order": 0, "hidden": false },
    { "key": "page:{pageId}",  "label": "Về chúng tôi", "order": 1, "hidden": false },
    { "key": "sysProductListing", "label": null, "order": 2, "hidden": false,
      "children": [
        { "key": "productCategory:12", "label": null, "order": 0 },
        { "key": "productCategory:15", "label": "Hàng mới", "order": 1 }
      ] },
    { "key": "custom", "label": "Fanpage", "url": "https://facebook.com/...", "order": 3 }
  ]
}
```

**`label: null` nghĩa là dùng nhãn gốc từ nguồn** — shop đổi tên trang thì menu tự đổi theo. Chỉ khi shop cố ý ghi đè thì `label` mới có giá trị. Đây là điểm khác hệ 4.8, nơi nhãn menu bị copy cứng.

**Đồng bộ khi nguồn thay đổi:**
- Thêm trang / thêm category mới → **append vào cuối** với `hidden: true`, shop chủ động bật và sắp. Không tự chen vào giữa.
- Xoá nguồn → item trỏ tới nó bị **bỏ qua lúc render** (không xoá khỏi `Items` ngay), Hangfire job dọn item mồ côi hàng đêm.

**`Header` component nhận gì:** `NavigationConfig` được resolve thành danh sách phẳng đã gộp nhãn + URL thật, truyền vào `props` của `Header` lúc render — đúng ánh xạ 1-1 với `MenuUI` của hệ cũ (#34), nhưng thứ tự đến từ một màn hình kéo-thả thay vì bốn số `Index` rải rác.

**Menu không nằm trong Component Tree**, nhưng **có nằm trong `SitePublication.Snapshot`** — vì nó là một phần của "trạng thái site tại thời điểm publish".

---

## 9. `MediaAsset` — thư viện media

Neo vào `Shop`. Dùng cho **builder** và **listing**. **Không** dùng cho ảnh sản phẩm (#56 — xem §20).

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ShopId` | uuid FK | |
| `StorageKey` | varchar(300) | Đường dẫn tương đối trên object storage. **Không phải URL** |
| `MimeType` | varchar(80) | Sau xử lý luôn là `image/webp` |
| `Width` / `Height` | int | Kích thước **sau resize**. Có sẵn miễn phí từ bước xử lý |
| `SizeBytes` | bigint | Tính quota theo gói dịch vụ |
| `AltText` | varchar(200)? | Mặc định; node trong tree có thể override |
| `FocalPointX` / `FocalPointY` | real | 0..1, mặc định `0.5`/`0.5`. Điểm giữ lại khi cắt |
| `OriginalFileName` | varchar(200)? | Để shop nhận ra ảnh trong picker |
| `Purpose` | enum? | `Website` · `Listing` · `Shop`. **Chỉ để lọc mặc định trong picker**, không phải ràng buộc |
| `Folder` | varchar(100)? | Thư mục phẳng do shop tự đặt |
| `CreatedAt` | timestamptz | |
| `DeletedAt` | timestamptz? | Xoá mềm |

### 9.1 Xử lý lúc upload (#53)

```
File người dùng gửi lên
  → validate MIME thật (magic bytes, KHÔNG tin phần mở rộng)
  → strip toàn bộ EXIF
  → resize: cạnh dài ≤ 1600px, GIỮ NGUYÊN tỉ lệ gốc
  → encode webp (quality ~82)
  → lưu 1 file duy nhất + ghi Width/Height/SizeBytes
```

**Không giữ file người dùng upload.** Ảnh điện thoại đời mới là 4000×3000 / ~8MB; giữ nguyên nghĩa là mỗi lần cache miss phải đọc 8MB từ storage, và shop 500 sản phẩm chiếm 4GB cho thứ không ai xem ở kích thước đó.

**Vì sao giữ tỉ lệ gốc thay vì ép về một khung cố định:** ảnh dọc (thời trang) và ảnh ngang (nội thất) khác nhau về bản chất. Ép chung một khung thì hoặc cắt mất, hoặc thêm viền. Việc cắt để lại cho tầng render, nơi biết ngữ cảnh hiển thị.

**Strip EXIF là bắt buộc, không phải tuỳ chọn:** ảnh chụp bằng điện thoại chứa toạ độ GPS. Shop chụp sản phẩm tại nhà riêng rồi đăng lên là lộ địa chỉ nhà. Đây là rủi ro quyền riêng tư có thật.

### 9.2 URL & phái sinh (#53, #55)

Một domain CDN dùng chung cho **mọi** shop, kể cả shop có custom domain:

```
https://cdn.vsite.vn/i/{preset}/{storageKey}
https://cdn.vsite.vn/i/600x900,cover,fp0.5-0.35/shops/77/2026/03/a3f21.webp
```

| Nguyên tắc | Lý do |
|---|---|
| DB lưu `StorageKey`, **không** lưu URL | Đổi CDN không phải rewrite `SitePublication.Snapshot` — mà snapshot theo #41 là bất biến, sửa không được |
| Tree lưu `imageId`, **không** lưu `StorageKey` | Thêm một lớp gián tiếp, đổi được cả cấu trúc thư mục storage |
| Component **không bao giờ** tự nối chuỗi URL | Luôn gọi `resolveImage(assetId, preset)` — cùng tinh thần `resolveUrl()` của #11 |
| `{preset}` phải nằm trong **whitelist** | Cho `{w}x{h}` tự do là mở cửa cho việc gọi 10.000 kích thước ngẫu nhiên và đốt sạch CPU xử lý ảnh |

**Sinh lúc render, không sinh trước:** request tới → CDN có thì trả; không có thì proxy đọc bản gốc, cắt/resize theo preset, ghi vào storage, trả về, CDN cache. File nào không ai xem thì không tồn tại.

**Không có bảng `MediaVariant`** (#55). Key là xác định — cùng tham số luôn ra cùng đường dẫn — nên object storage **đã là** index. Thêm bảng DB chỉ nhét một `SELECT` hoặc `INSERT` vào hot path của render mà không đổi lại được gì. Dọn dẹp bằng lifecycle policy của storage (phái sinh không được truy cập 90 ngày → tự xoá), rẻ hơn Hangfire job quét DB.

**Quota tính trên bản gốc**, không tính phái sinh: phái sinh là chi phí vận hành của vsite, shop không kiểm soát được nên tính vào quota của họ là vô lý.

### 9.3 Preset (whitelist)

Khoảng 12 giá trị phủ toàn bộ thư viện component. Mỗi vị trí ảnh dùng **một** preset cố định (#58 — chưa hỗ trợ `srcset` ở Phase 2), chọn ở mức **~2× độ rộng CSS lớn nhất** để đủ nét trên màn retina.

| Preset | Dùng cho |
|---|---|
| `1600x900,cover` | Hero full-width |
| `1200x630,cover` | OG image (chuẩn Facebook) |
| `800x800,cover` | Ảnh chính trang chi tiết sản phẩm |
| `600xR,cover` | Thẻ sản phẩm trong lưới (`R` = tỉ lệ danh mục) |
| `160x160,cover` | Thumbnail dải ảnh |
| `96x96,cover` | Avatar / logo nhỏ |
| … | … |

Chọn theo độ rộng CSS (ví dụ 300px cho thẻ sản phẩm) là ảnh sẽ mờ trên **mọi** điện thoại đời mới, vì DPR ≥ 2.

### 9.4 Xoá ảnh

Xoá mềm (`DeletedAt`), **chỉ chủ shop xoá được**. Trước khi xoá, quét tham chiếu trong `page_drafts.Tree`, `site_publications.Snapshot`, `listings` → **cảnh báo, không chặn**.

Snapshot đã publish là bất biến (#41), nên file thật phải giữ cho tới khi snapshot cuối cùng tham chiếu tới nó bị dọn (giữ 20 bản gần nhất, §7). Hangfire job dọn file mồ côi chạy sau khi dọn snapshot.

---

## 10. `WebsiteTemplate` — design-time

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `Code` | varchar(50) UNIQUE | `spa-lavender`, `salon-minimal`… |
| `Name` / `Description` | | |
| `Industry` | enum | Spa · Salon · Restaurant · Clinic · Hotel · Pet · Retail · Generic |
| `PreviewImageUrl` | varchar(300) | |
| `Snapshot` | jsonb | Cùng shape `SitePublication.Snapshot` |
| `IsActive` | bool | |
| `SortOrder` | int | |

**Khởi tạo website từ template** (#45): deep copy `Snapshot` → tạo `Theme`, `NavigationConfig`, các `Page` + `PageDraft`. Sinh **id node mới** cho toàn bộ tree (tránh trùng id giữa các shop, dù về kỹ thuật không bắt buộc vì id chỉ duy nhất trong trang). Ảnh trong template là ảnh placeholder dùng chung, được copy thành `MediaAsset` của shop để shop thay được.

Sau bước này template và website **không còn quan hệ nào**.

---

## 11. Operations Engine — ràng buộc thực thi

Không phải entity, nhưng quyết định tính đúng đắn của cả module. Mọi thay đổi tree đi qua đây (#14), bất kể đến từ Drag&Drop, Property Panel hay AI Chat.

```
Operation → [1] Kiểm Page.Kind cho phép op này không     (#33)
          → [2] Kiểm component.allowedInPageKinds        (manifest)
          → [3] Kiểm targetId tồn tại trong tree
          → [4] Zod validate props theo manifest
          → [5] Kiểm binding.source nằm trong whitelist  (#7 §1 — CẤM `Review`)
          → [6] Apply (immer) → draft
```

**Bảng phép/cấm theo `PageKind`:**

| Operation | `Composable` | `System` |
|---|---|---|
| `add` | ✅ | ❌ |
| `remove` | ✅ | ❌ |
| `move` | ✅ | ❌ |
| `update` | ✅ | ✅ (chỉ props được manifest đánh dấu `editableInSystemPage`) |
| `changeTheme` | ✅ | ✅ |

**Kiểm tra ở bước [5] là ranh giới cứng của `01` §7:** không component nào được bind tới `Review`. Enforce bằng whitelist trong codegen, không bằng tài liệu.

---

# PHẦN II — MODULE `Product`

## 12. Sơ đồ quan hệ

```
Shop
 ├─1:n─ ShopProductCategory  (cây, per shop)
 │         └─n:m─ ShopAttribute   (qua category_attributes, kế thừa theo nhánh)
 ├─1:n─ ShopAttribute
 │         └─1:n─ ShopAttributeOption
 └─1:n─ Product
           ├─1:n─ ProductImage            → MediaAsset
           ├─1:n─ ProductAttributeValue   → ShopAttribute (+ Option)
           └─1:n─ ProductVariant
                     └─1:n─ ProductVariantOption → ShopAttribute (+ Option)
```

**Ranh giới:** hai tầng phân loại tách bạch tuyệt đối —

| | Ai định nghĩa | Dùng cho |
|---|---|---|
| `ServiceCategory` (module `Category`) | vsite, toàn cục (#35) | Tìm kiếm marketplace, landing SEO |
| `ShopProductCategory` | **shop tự vẽ cây** | Điều hướng + trình bày trên website riêng |

Hàng hoá không lên marketplace (#48), nên cây danh mục sản phẩm là chuyện riêng của shop và được phép tự do — đây **không** mâu thuẫn với #35, vì #35 nói về thứ được tìm kiếm chung.

---

## 13. `ShopProductCategory`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | int PK (identity) | int chứ không uuid — xuất hiện trong URL và trong `binding.categoryId` |
| `ShopId` | uuid FK | |
| `ParentId` | int? FK self | NULL = gốc |
| `Name` | varchar(120) | |
| `Slug` | varchar(140) | UNIQUE `(ShopId, Slug)` — **phẳng toàn shop**, không lồng theo cha |
| `Path` | varchar(300) | Materialized path: `/1/5/12/`. Index để query cả nhánh |
| `Depth` | smallint | Tối đa **3** (0,1,2) |
| `Description` | text? | Hiển thị đầu trang danh mục, tốt cho SEO |
| `ImageId` | uuid? FK → `media_assets` | Ảnh đại diện danh mục (thuộc thư viện, không phải `product_images`) |
| `ImageRatio` | enum | `R1x1` · `R2x3` · `R3x2` · `R3x4` · `R4x3` · `R16x9`. Mặc định `R1x1` (#57) |
| `SortOrder` | int | |
| `IsVisible` | bool | Ẩn khỏi menu/listing, không xoá dữ liệu |
| `ProductCount` | int | Denormalized, cập nhật bằng job. Chỉ để hiển thị trong portal |

**Vì sao materialized path chứ không đệ quy:** query "lấy sản phẩm thuộc category 5 và mọi con cháu" là truy vấn nóng nhất module. `WHERE path LIKE '/1/5/%'` với index btree giải quyết trong một lần quét, không cần CTE đệ quy hay load cả cây vào RAM như hệ cũ.

**Vì sao slug phẳng toàn shop:** URL `/danh-muc/ao-so-mi` ngắn và ổn định khi shop di chuyển category trong cây. Slug lồng (`/danh-muc/ao/ao-so-mi`) đẹp hơn nhưng vỡ URL mỗi lần đổi cha — với shop nhỏ hay sắp lại danh mục, đây là đổi rủi ro SEO lấy vẻ đẹp.

**`ImageRatio` — tỉ lệ ảnh theo danh mục (#57):** shop bán quần áo chọn `R2x3` (ảnh dọc), shop nội thất chọn `R3x2` (ảnh ngang), shop phụ kiện chọn `R1x1`. Lưới sản phẩm nhờ đó đều tăm tắp thay vì cao thấp lộn xộn.

**Cắt lúc render, không cắt lúc upload.** Ảnh lưu nguyên tỉ lệ gốc (§9.1); proxy cắt theo `ImageRatio` của danh mục + `FocalPoint` của ảnh lúc dựng URL. Kết quả hiển thị giống hệt phương án cắt-lúc-upload, nhưng:

| Tình huống | Cắt lúc upload | Cắt lúc render |
|---|---|---|
| Shop đổi ý về tỉ lệ sau 3 tháng | 200 sản phẩm phải upload lại — phần ảnh bị cắt **không còn tồn tại** | Đổi một dòng config, ảnh tự cắt lại |
| Chuyển sản phẩm sang danh mục khác tỉ lệ | Ảnh sai tỉ lệ, phải upload lại | Tự động đúng |
| OG image share Facebook cần 1.9:1 | Đã cắt 2:3 rồi thì không dựng lại được ảnh ngang | Cắt từ bản gốc, bình thường |

**`FocalPoint` là thứ khiến việc này chạy được:** cắt tự động ở giữa thường cắt mất đầu người mẫu. Shop chỉ một điểm trên ảnh, mọi tỉ lệ cắt đúng chỗ.

**Xoá category:** chặn nếu còn sản phẩm hoặc còn category con. Bắt shop chuyển sản phẩm đi trước. Cảnh báo (không chặn) nếu có `binding.categoryId` trong tree trỏ tới.

---

## 14. `ShopAttribute`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | int PK (identity) | **BẤT BIẾN** — là toàn bộ cơ chế rename an toàn |
| `ShopId` | uuid FK | |
| `Code` | varchar(50) | Slug dùng trong URL filter (`?mau-sac=41`). UNIQUE `(ShopId, Code)` |
| `Name` | varchar(100) | **Nhãn hiển thị — đổi tự do, không ảnh hưởng dữ liệu** |
| `Kind` | enum | `Enum` · `Boolean` · `Number` · `Text` |
| `Unit` | varchar(20)? | Chỉ `Number`: `kg`, `cm` |
| `IsFilterable` | bool | Lên facet sidebar hay không |
| `IsVariantAxis` | bool | Là trục sinh biến thể hay không (#52) |
| `ShowInSpecTable` | bool | Hiện trong bảng thông số trang chi tiết |
| `SortOrder` | int | |
| `IsArchived` | bool | Xoá mềm (#51) |

**Ràng buộc bắt buộc (CHECK constraint, không chỉ ở tầng ứng dụng):**

| Ràng buộc | Lý do |
|---|---|
| `Kind = Text` ⟹ `IsFilterable = false` | Quyết định #50 |
| `Kind = Text` ⟹ `IsVariantAxis = false` | Không thể sinh tổ hợp từ chuỗi tự do |
| `IsVariantAxis = true` ⟹ `Kind = Enum` | Trục variant phải là tập hữu hạn có id |
| `Unit IS NOT NULL` ⟹ `Kind = Number` | |

**`Text` không được thống kê** (#50): không đếm số sản phẩm theo giá trị, không lên facet, không index vào `attrs` của Elasticsearch. Nó chỉ đi vào `attrsText` để full-text search bắt được, và hiện trong bảng thông số.

---

## 15. `ShopAttributeOption`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | int PK (identity) | **BẤT BIẾN** |
| `AttributeId` | int FK | |
| `Value` | varchar(100) | **Đổi tự do** |
| `Code` | varchar(50) | Dùng trong URL nếu muốn `?mau-sac=do` thay vì `=41`. UNIQUE `(AttributeId, Code)` |
| `ColorHex` | char(7)? | Cho swatch màu |
| `ImageId` | uuid? FK | Cho swatch ảnh (vân vải) |
| `SortOrder` | int | Quan trọng với size: S < M < L, không phải alphabet |
| `IsArchived` | bool | Xoá mềm (#51) |

**Xoá mềm nghĩa là gì cụ thể:**

| | Option `IsArchived = true` |
|---|---|
| Sản phẩm đang dùng | Vẫn hiển thị bình thường, vẫn đúng text |
| Form thêm/sửa sản phẩm | Không xuất hiện trong dropdown |
| Facet sidebar | Không hiện, kể cả còn sản phẩm |
| Elasticsearch | Vẫn index (để không vỡ khi có link cũ) |
| Portal | Có bộ lọc "Hiện mục đã lưu trữ" + nút khôi phục |

**Vì sao không xoá cứng:** xoá cứng buộc phải chọn giữa (a) cascade xoá `ProductAttributeValue` — mất dữ liệu âm thầm, hoặc (b) chặn xoá — shop bị kẹt với option gõ sai. Xoá mềm né cả hai.

---

## 16. `CategoryAttribute` — schema theo nhánh cây

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `CategoryId` | int FK | `0` = áp cho toàn shop (giữ sentinel của hệ cũ, nhưng ghi rõ ý nghĩa) |
| `AttributeId` | int FK | |
| `IsRequired` | bool | |
| `SortOrder` | int | |
| PK | `(CategoryId, AttributeId)` | |

**Kế thừa theo nhánh** — điểm hệ 4.8 làm đúng, giữ nguyên logic:

```
Schema hiệu lực cho sản phẩm ở category 12
  = CategoryAttribute WHERE CategoryId IN (12, 5, 1, 0)
                                          └─ tách từ Path '/1/5/12/'
```

Khai "Thương hiệu" một lần ở `CategoryId = 0`, mọi sản phẩm đều có. Khai "Kiểu cổ" ở "Áo sơ mi", chỉ áo sơ mi có.

---

## 17. `Product`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ShopId` | uuid FK | |
| `CategoryId` | int FK | NOT NULL — mỗi sản phẩm thuộc đúng một node lá |
| `Name` | varchar(200) | |
| `Slug` | varchar(220) | UNIQUE `(ShopId, Slug)` |
| `ShortDescription` | varchar(500)? | Cho thẻ sản phẩm |
| `Description` | text? | HTML từ Lexical, sanitize server-side |
| `Status` | enum | `Draft` · `Active` · `Hidden` · `Archived` — **enum riêng, không dùng chung với `Service`** (#40) |
| `BasePrice` | numeric(14,2)? | Hiển thị khi không có variant, hoặc làm giá "từ" |
| `CompareAtPrice` | numeric(14,2)? | Giá gạch ngang (#11 trong khối B) |
| `HasVariants` | bool | Denormalized, tránh join khi render thẻ |
| `TrackInventory` | bool | Mặc định `false` (#49) |
| `Stock` | int? | Chỉ có nghĩa khi `TrackInventory = true` **và** `HasVariants = false` |
| `PrimaryImageId` | uuid? FK → `product_images` | Denormalized, tránh join khi render thẻ |
| `ViewCount` | int | |
| `PublishedAt` | timestamptz? | |
| `CreatedAt` / `UpdatedAt` | | |

**Ngữ nghĩa tồn kho (#49) — cần rõ để không mơ hồ:**

| `TrackInventory` | `HasVariants` | Tồn kho ở đâu | UI hiển thị |
|---|---|---|---|
| `false` | — | Không có | Không nhắc tồn kho |
| `true` | `false` | `Product.Stock` | "Còn N sản phẩm" / "Hết hàng" |
| `true` | `true` | `ProductVariant.Stock` | Theo variant đang chọn |

Tắt `TrackInventory` (mặc định) thì shop không thấy ô nhập số lượng ở đâu cả — không phải nhập 0 rồi bị hiểu là hết hàng. Đây là điểm phải cẩn thận: `Stock = NULL` và `Stock = 0` mang ý nghĩa hoàn toàn khác nhau.

**Không có** `Promotions` bitmask của hệ cũ (#36). Giảm giá thể hiện bằng `CompareAtPrice > Price`. Nếu sau này cần chương trình khuyến mãi thật (theo thời gian, theo nhóm sản phẩm), đó là entity riêng ở Phase sau, không phải bitmask.

---

## 18. `ProductAttributeValue`

Trái tim của cơ chế rename an toàn. **Không lưu text, chỉ lưu id.**

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `ProductId` | uuid FK | |
| `AttributeId` | int FK | |
| `OptionId` | int? FK | Chỉ `Kind = Enum`. NULL với các kiểu khác |
| `NumberValue` | numeric(18,4)? | Chỉ `Kind = Number` |
| `BoolValue` | bool? | Chỉ `Kind = Boolean` |
| `TextValue` | varchar(500)? | Chỉ `Kind = Text` |
| PK | `(ProductId, AttributeId, COALESCE(OptionId, 0))` | Cho phép multi-select |

**Multi-select tự nhiên:** một áo có cả Đỏ và Xanh → hai dòng cùng `(ProductId, AttributeId=3)`, khác `OptionId`. Không giới hạn số lượng — khác hệ cũ dùng bitmask `Int64` (tối đa 63 giá trị, không đọc được bằng mắt trong DB).

**Validate server-side khi save** (điểm hệ cũ thiếu, `SProduct-Architecture` §3):

1. `AttributeId` phải nằm trong schema hiệu lực của `Product.CategoryId` (§16)
2. Đúng một trong bốn cột giá trị được điền, khớp `Attribute.Kind`
3. `OptionId` phải thuộc đúng `AttributeId` đó và `IsArchived = false` (khi tạo mới; sửa sản phẩm cũ vẫn giữ được option đã archived)
4. Attribute có `IsRequired = true` thì bắt buộc có giá trị
5. Attribute có `IsVariantAxis = true` thì **không** được xuất hiện ở đây — giá trị của nó nằm ở `ProductVariantOption`

Quy tắc 5 là ranh giới rõ ràng thay cho cơ chế "variant override `SData` của cha" mơ hồ ở hệ cũ. Một attribute chỉ sống ở **một** trong hai nơi, không bao giờ cả hai.

---

## 19. `ProductVariant` & `ProductVariantOption`

**`ProductVariant`** — đơn vị bán thực sự. Thiết kế sẵn để `OrderItem` của Phase sau trỏ vào mà không phải migrate (#48).

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ProductId` | uuid FK | |
| `Sku` | varchar(60)? | UNIQUE `(ShopId, Sku)` khi khác NULL |
| `Price` | numeric(14,2) | NOT NULL — variant luôn có giá riêng |
| `CompareAtPrice` | numeric(14,2)? | |
| `Stock` | int? | Chỉ có nghĩa khi `Product.TrackInventory = true` |
| `ImageId` | uuid? FK | Ảnh riêng cho variant (áo màu đỏ) |
| `IsDefault` | bool | Variant hiển thị mặc định. Đúng một per product |
| `IsAvailable` | bool | Tắt tạm không xoá |
| `SortOrder` | int | |

**`ProductVariantOption`** — tổ hợp xác định variant:

| Cột | Kiểu |
|---|---|
| `VariantId` | uuid FK |
| `AttributeId` | int FK (phải có `IsVariantAxis = true`) |
| `OptionId` | int FK |
| PK | `(VariantId, AttributeId)` |

**Ràng buộc:**
- Tối đa **3** `AttributeId` phân biệt trong một `Product` (#52). Kiểm ở tầng handler
- Mọi variant của cùng một product phải có **cùng tập** `AttributeId` — không có chuyện variant A có Màu+Size còn variant B chỉ có Màu
- Tổ hợp `(AttributeId, OptionId)` là duy nhất trong một product — không hai variant cùng Đỏ/M

**Vì sao giới hạn 3 trục:** 3 trục × 6 option mỗi trục = 216 tổ hợp. UI nhập liệu dạng bảng đã khó dùng ở mức đó. Quá 3 là dấu hiệu shop đang mô hình hoá sai (nên tách thành nhiều sản phẩm).

---

## 20. `ProductImage` — hệ thống ảnh độc lập

Theo #56, ảnh sản phẩm **không** nằm trong `media_assets`. Bảng riêng, lifecycle gắn chặt sản phẩm.

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ProductId` | uuid FK | Cascade delete |
| `StorageKey` | varchar(300) | Đường dẫn tương đối, thư mục riêng `shops/{id}/products/…` |
| `Width` / `Height` | int | Sau resize |
| `SizeBytes` | bigint | |
| `AltText` | varchar(200)? | **Quan trọng cho SEO** — Google Image Search là nguồn traffic thật với shop bán hàng |
| `FocalPointX` / `FocalPointY` | real | Mặc định `0.5`/`0.5` |
| `SortOrder` | int | `0` = ảnh chính |
| `CreatedAt` | | |

**Xử lý upload giống hệt §9.1**: resize cạnh dài ≤1600, giữ tỉ lệ gốc, webp, strip EXIF. Cắt theo `ImageRatio` của danh mục xảy ra lúc render (#57).

**`SortOrder = 0` là ảnh chính** — tường minh thay vì ngầm định "phần tử đầu mảng". `Product.PrimaryImageId` denormalize sẵn để render thẻ sản phẩm không cần join.

### 20.1 Vì sao tách khỏi `media_assets`

| | `product_images` | `media_assets` |
|---|---|---|
| Vòng đời | Chết theo sản phẩm (cascade) | Độc lập, chỉ chủ shop xoá |
| Reuse | Không | Có, nhiều nơi dùng chung |
| Xuất hiện trong builder picker | **Không bao giờ** | Có |
| Nơi upload | Form sản phẩm | Media Library |

Ảnh sản phẩm được upload hàng loạt, dùng đúng một chỗ, và nên biến mất khi sản phẩm biến mất. Đưa chúng vào thư viện chung làm shop rối (ảnh sản phẩm lẫn ảnh banner) và làm việc xoá sản phẩm trở nên mơ hồ.

### 20.2 Dùng ảnh sản phẩm trong builder — phải clone trước (#56)

```
Shop bấm "Thêm vào thư viện" trên một ảnh sản phẩm
  → copy file sang thư mục library
  → tạo MediaAsset MỚI (Id mới, StorageKey mới)
  → từ đây hai bản HOÀN TOÀN độc lập
  → builder picker chỉ thấy và chỉ lưu Id của bản library
```

**Thứ tự thao tác là bắt buộc: clone trước, chọn sau.** Builder picker **không bao giờ** hiển thị `product_images`.

**Vì sao không cho chọn trực tiếp rồi clone ngầm:** kịch bản vỡ cụ thể — shop lắp ảnh sản phẩm vào Hero → publish (snapshot v5 lưu id của ảnh **product**) → sau đó xoá sản phẩm → ảnh chết theo cascade → snapshot v5 là bất biến (#41), không sửa được → banner trang chủ vỡ vĩnh viễn. Ép clone trước nghĩa là tree **không bao giờ** chứa id thuộc `product_images`, nên không tồn tại đường nào để vỡ.

Chi phí: file bị nhân đôi. Đổi lại là ranh giới sạch và không có trường hợp biên nào.

---

## 21. Elasticsearch — index & facet

Thay hoàn toàn mô hình in-memory LINQ của hệ cũ (#36).

**Document:**

```json
{
  "productId": "uuid", "shopId": "uuid",
  "name": "Áo sơ mi cotton dài tay",
  "shortDescription": "…",
  "categoryId": 12,
  "categoryPath": [1, 5, 12],
  "attrs":      [ {"a": 3, "o": 41}, {"a": 3, "o": 45}, {"a": 7, "o": 88} ],
  "attrsNum":   [ {"a": 9, "v": 0.35} ],
  "attrsBool":  [ {"a": 11, "v": true} ],
  "attrsText":  "Đỏ Xanh navy Cotton dài tay",
  "priceMin": 350000, "priceMax": 420000,
  "hasDiscount": true,
  "inStock": true,
  "status": "Active",
  "createdAt": "…", "viewCount": 128
}
```

**Nguyên tắc quan trọng nhất:** filter chạy trên **id** (`attrs.a` / `attrs.o`), không chạy trên text. Hệ quả:

- Rename attribute hoặc option → **link lọc cũ vẫn hoạt động**, chỉ nhãn hiển thị đổi
- Reindex sau rename là để cập nhật `attrsText` (phục vụ full-text), **không** ảnh hưởng filter
- `attrsText` là nơi duy nhất text xuất hiện, và nó chỉ phục vụ tìm kiếm mờ

`Kind = Text` **không** vào `attrs`, chỉ vào `attrsText` (#50).

**Facet:** ES `nested` aggregation trên `attrs`, đếm theo tập kết quả đã áp các filter khác — giữ đúng UX tốt của hệ cũ (`SProduct-Architecture` §4.3): option `count = 0` bị loại khỏi sidebar.

**Đồng bộ:** Hangfire job, trigger khi `Product` / `ProductVariant` / `ProductAttributeValue` thay đổi, hoặc khi `ShopAttributeOption.Value` đổi (reindex sản phẩm liên quan).

---

## 22. URL & trang `ProductListing`

```
/danh-muc/ao-so-mi                          ← index, canonical
/danh-muc/ao-so-mi?mau-sac=41&size=7&sort=moi-nhat   ← noindex, follow
/tim-kiem?q=ao+so+mi                        ← noindex
/san-pham/ao-so-mi-cotton-dai-tay           ← index, canonical
```

**Khác hệ cũ có chủ đích:** VSite 4.8 nhét toàn bộ filter vào path và tự parse bằng `VParser`. Ta dùng **path cho category, querystring cho filter**. Lý do: chỉ trang category cần được index; tổ hợp filter sinh ra hàng nghìn URL gần trùng nội dung — để chúng được index là tự tạo duplicate content. Querystring + `noindex` giải quyết sạch, và code parse đơn giản hơn nhiều.

**Component nhận gì** (trang `System`, chỉ đổi variant + bật/tắt slot theo #33):

```json
{ "variant": "ProductListing02",
  "showFilterSidebar": true, "showSortBar": true,
  "showCategoryDescription": true,
  "defaultSort": "Latest", "pageSize": 24,
  "showPriceFilter": true, "cardVariant": "ProductCard01" }
```

---

## 23. Bảng đối chiếu với hệ cũ

| Khía cạnh | VSite 4.8 | vsite mới | Vì sao |
|---|---|---|---|
| Attribute id ngầm | ✅ có | ✅ giữ | Rename an toàn — họ làm đúng |
| Option id ngầm | ✅ có | ✅ giữ | Như trên |
| Product lưu id không lưu text | ✅ có | ✅ giữ | Như trên |
| Kế thừa attribute theo nhánh | ✅ có | ✅ giữ | Tránh khai lặp |
| Nơi lưu value | JSON `SData` trong cột | Bảng quan hệ có FK | Validate được, index được, join được |
| Nơi lưu schema | JSON blob `SiteConfig` | Bảng có kiểu | #36 |
| Validate lúc save | ❌ gán thẳng | ✅ 5 quy tắc §18 | Data không trôi khỏi schema |
| Multi-value | bitmask ≤63 | nhiều dòng, không giới hạn | Đọc được, không giới hạn |
| Xoá option đang dùng | ❌ id mồ côi | ✅ xoá mềm | #51 |
| Filter | LINQ in-memory toàn tenant | Elasticsearch | #26, #36 |
| Facet count | ✅ có, ẩn count=0 | ✅ giữ | UX tốt — họ làm đúng |
| URL filter | toàn bộ trong path | path + querystring | Tránh duplicate content |
| Variant override attribute cha | mơ hồ | rõ: `IsVariantAxis` quyết định nơi lưu | Bớt một lớp nhập nhằng |
| Khuyến mãi | bitmask `Promotions` | `CompareAtPrice` | Đủ cho Phase 2 |
| Ảnh sản phẩm | chuỗi nối bằng `\|`, parse mỗi lần build cache | bảng `product_images` có kiểu | Có width/height/alt, không parse chuỗi |
| Thumbnail | `ImageHelper.ReplaceImg2Thumb` — đoán URL bằng nối chuỗi | image proxy + preset whitelist | Nhiều kích thước, không nhân bản quy ước tên |
| Tỉ lệ ảnh lưới | không có, ảnh cao thấp lộn xộn | `Category.ImageRatio`, cắt lúc render | Lưới đều, đổi tỉ lệ không phải upload lại |
| Trang chi tiết / listing | code cứng theo action | `System` page có tree, khoá op | #33, #46 |
| Trang chủ / giới thiệu | widget stack `UIH_Area` | `Composable` page, tree có `children` | Nâng từ phẳng lên cây |
| Menu | derived, sort bằng `Index` rải rác | derived + overlay, một màn kéo-thả | #34 |
| Trang tĩnh | mượn module tin tức | `Composable` page | Đã có khái niệm trang, không cần mượn |
| ViewModel | `object Item` / `Item2` | TS strict + Orval | #19 |

---

## 24. Thứ tự triển khai đề xuất

```
1. MediaAsset + image proxy + preset whitelist   (không phụ thuộc gì, Builder & Listing cần)
2. ShopProductCategory → ShopAttribute → ShopAttributeOption → CategoryAttribute
3. Product → ProductImage → ProductAttributeValue → ProductVariant → ProductVariantOption
4. Elasticsearch index + facet + Hangfire sync
5. Component Registry manifest + codegen         (chặn tất cả phần Builder phía sau)
6. Website → Theme → Page → PageDraft
7. Operations Engine + Zod validate
8. builder-renderer (dùng chung portal + web)
9. NavigationConfig
10. SitePublication + publish/rollback + cache
11. WebsiteTemplate + khởi tạo từ template
```

Bước **5 là nút thắt** — mọi thứ từ 6 trở đi phụ thuộc vào nó. Nên làm manifest cho 3–4 component trước (Hero, Timeline, ProductGrid, Header) để chạy được codegen end-to-end, rồi mới mở rộng thư viện.

---

## 25. Điểm còn trống của hai module này

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | Giới hạn số trang / số ảnh / dung lượng media theo gói dịch vụ | ⚠️ Cần chốt trước khi bật thu phí (liên quan #8 ở `02` §6) |
| 2 | Đa ngôn ngữ cho website shop (site song ngữ Việt–Anh) | ⏳ Chưa cần Phase 2, nhưng `Page`/`Theme` nên biết trước để không phải migrate |
| 3 | Sanitize HTML từ Lexical — whitelist thẻ/thuộc tính cụ thể | ⚠️ Cần chốt trước khi cho nhập richtext |
| 4 | Chính sách cache & TTL cho `SitePublication` ở tầng CDN/Caddy | ⚠️ Cần chốt cùng lúc với publish |
| 5 | Nhập sản phẩm hàng loạt (CSV/Excel) cho shop có nhiều SKU | ⏳ Phase 3 |
| 6 | Sinh variant tự động từ tổ hợp trục (matrix generator) — UI nhập liệu | ⚠️ Cần thiết kế UX trước khi code, ảnh hưởng API |

---

*Tài liệu này chưa bao gồm: module `Service` (thiết kế riêng), AI Chat Builder (Phase 3), Booking (Phase 3), Order/Cart (chưa lên lộ trình).*
