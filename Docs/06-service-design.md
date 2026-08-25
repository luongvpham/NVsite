# vsite — Thiết Kế Entity: Service (Phase 2)

> **Tài liệu liên quan:** `01-project-ideal.md` (§2.1, §5.2) · `02-tech-stack-and-decision.md` (#33, #34, #38) · `04-listing-and-review-design.md` (§4 — `Listing.TargetPageId`) · `05-website-builder-and-product-design.md` (#54)
>
> **Phạm vi:** module `Service` — danh mục dịch vụ của shop, hiển thị trên **website riêng của shop**.

---

## 1. `Service` là gì và KHÔNG là gì

Đây là chỗ dễ nhầm nhất trong toàn hệ, nên phát biểu dứt khoát ngay từ đầu.

| Module | Là gì | Sống ở đâu | `ExternalOnly` có |
|---|---|---|---|
| **`Listing`** | **Tin đăng marketplace** — xuất hiện ở `vsite.vn`, trên bản đồ, trong kết quả tìm kiếm | Trang chung của vsite | ✅ |
| **`Service`** | **Danh mục dịch vụ của shop** — rửa xe, sửa máy lạnh, cắt tóc, chăm sóc da | Website riêng của shop | ❌ |

**`Service` không phải trang root của vsite.** Nó là dữ liệu nghiệp vụ nội bộ của shop, ngang hàng với `Product`, tồn tại để component trên website shop có cái mà bind tới.

**Quan hệ giữa `Service` và `Listing`: không có.** Quyết định #38 chốt "Listing opt-in, không auto-map" — shop tạo tin đăng thì tự chọn ngành, tự viết mô tả, tự chọn ảnh. Dữ liệu **không** chảy từ `Service` sang `Listing`. Ba lý do ở `01` §2.1: hai sản phẩm bán riêng; nội dung hợp website chưa chắc hợp trang tìm kiếm; shop `ExternalOnly` không có `Service` nào để map.

```
Shop (Hosted)
 ├── Service       ─┐
 ├── Product       ─┼─→ bind vào component trên website shop
 └── Testimonial   ─┘

 └── Listing       ───→ marketplace vsite.vn   [KHÔNG liên quan 3 cái trên]
```

---

## 2. Vì sao `Service` đơn giản hơn `Product` rất nhiều

| | `Service` | `Product` |
|---|---|---|
| Số lượng điển hình mỗi shop | 2–5 | hàng chục đến hàng trăm |
| Phân loại | **nhóm phẳng** (`ShopServiceGroup`) | cây 3 cấp + attribute schema |
| Thuộc tính động | ❌ | ✅ (`Enum`/`Number`/`Bool`/`Text`) |
| Variant / SKU | ❌ | ✅ (tối đa 3 trục) |
| Tồn kho | ❌ | ✅ (tuỳ chọn) |
| Filter / facet | ❌ | ✅ (Elasticsearch) |
| Elasticsearch | ❌ — load thẳng Postgres theo `ShopId` là đủ | ✅ |
| `System` page riêng | ❌ (#54) | ✅ `ProductListing` / `ProductDetail` / `ProductSearch` |

**Bài học đã áp dụng:** tài liệu `NewsBDS-Architecture.md` của hệ 4.8 ghi rõ — *đừng generalize quá tay; nếu nội dung chỉ có vài trường cố định, không cần tổ chức như module có nhiều biến thể*. Bản thiết kế đầu tiên của tôi đã thêm `ServiceListing`/`ServiceDetail` vào `Page.SystemType` bằng cách sao chép cấu trúc của `Product` — đó chính là lỗi mà tài liệu trên cảnh báo, và đã được sửa ở #54.

**Bằng chứng nằm trong chính `04-listing-and-review-design.md`:** `Listing.TargetKind` chỉ có ba giá trị `ShopHome` · `ShopPage` · `ExternalUrl`, và `TargetPageId` trỏ tới **`Page` bất kỳ** — không có `TargetKind = ServiceDetail`. Thiết kế `Listing` từ đầu đã ngầm giả định `Service` không cần route hệ thống riêng.

---

## 3. Sơ đồ quan hệ

```
Shop (Kind = Hosted)
  ├─1:n─ ShopServiceGroup        (nhóm PHẲNG, không phải cây)
  │         └─1:n─ Service
  └─1:n─ Service ──0:1──▶ Page   (DetailPageId, tuỳ chọn)
```

Hai bảng. Hết.

---

## 4. `ShopServiceGroup`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | int PK (identity) | int vì xuất hiện trong `binding.groupId` |
| `ShopId` | uuid FK | |
| `Name` | varchar(120) | "Chăm sóc da", "Massage", "Sửa chữa tại nhà" |
| `Slug` | varchar(140) | UNIQUE `(ShopId, Slug)` |
| `Description` | varchar(500)? | |
| `ImageId` | uuid? FK → `media_assets` | |
| `SortOrder` | int | |
| `IsVisible` | bool | |
| `CreatedAt` / `UpdatedAt` | | |

**Phẳng, không phải cây** — khác hẳn `ShopProductCategory`. Shop có 2–5 dịch vụ thì cây 3 cấp là vô nghĩa; một tầng nhóm đã đủ để spa tách "Chăm sóc da" khỏi "Massage".

**Đây là nguồn menu theo #34** — menu shop derive từ `ShopServiceGroup` (nhóm dịch vụ **của shop**), **không** từ `ServiceCategory` (taxonomy toàn cục của vsite, #35).

**Nhóm là tuỳ chọn:** `Service.GroupId` nullable. Shop có 3 dịch vụ thì không cần nhóm nào cả.

---

## 5. `Service`

| Cột | Kiểu | Ghi chú |
|---|---|---|
| `Id` | uuid PK | |
| `ShopId` | uuid FK | |
| `GroupId` | int? FK → `shop_service_groups` | NULL = không thuộc nhóm nào |
| `Name` | varchar(200) | |
| `Slug` | varchar(220) | UNIQUE `(ShopId, Slug)` |
| `ShortDescription` | varchar(500)? | Hiển thị trong thẻ / danh sách |
| `Description` | text? | HTML từ Lexical, sanitize server-side |
| `PriceFrom` | numeric(14,2)? | |
| `PriceTo` | numeric(14,2)? | |
| `PriceNote` | varchar(200)? | "Tuỳ tình trạng xe", "Liên hệ báo giá" |
| `DurationMinutes` | int? | Chuẩn bị sẵn cho `Booking` (Phase 3) |
| `ImageId` | uuid? FK → `media_assets` | Dùng thư viện chung, khác `Product` (#56) |
| `DetailPageId` | uuid? FK → `pages` | **Tuỳ chọn** — xem §6 |
| `Status` | enum | `Draft` · `Active` · `Hidden` — **enum riêng, không dùng chung `Product.Status`** (#40) |
| `IsFeatured` | bool | Cho component `ServiceHighlight` |
| `SortOrder` | int | |
| `ViewCount` | int | |
| `CreatedAt` / `UpdatedAt` | | |

**Giá là tuỳ chọn hoàn toàn** — giống `Listing` ở `04` §4. Nhiều dịch vụ (sửa xe, sửa điện nước) không báo giá trước được; ép nhập giá là ép shop nói dối hoặc bỏ trống một trường bắt buộc.

**`DurationMinutes` có mặt từ Phase 2 dù chưa dùng:** `Booking` (Phase 3) cần nó để tính khung giờ. Thêm một cột nullable bây giờ tốn 0 đồng; thêm sau là migration trên bảng đang chạy.

**Ảnh dùng `media_assets`** (thư viện chung), không phải bảng riêng như `product_images`. Lý do khác biệt: mỗi shop chỉ có vài dịch vụ, ảnh dịch vụ hay được dùng lại làm banner/Hero, và không có nhu cầu upload hàng loạt.

---

## 6. `DetailPageId` — mô hình lai đã chốt

Đây là điểm thiết kế cốt lõi của module. Shop có **hai cách** trình bày dịch vụ, và cả hai đều hợp lệ:

### 6.1 Shop đơn giản — hiện thẳng trên trang chính

2–3 dịch vụ, không cần trang riêng. `DetailPageId = NULL`.

```json
{ "type": "ServiceGrid", "variant": "ServiceGrid02",
  "props": { "heading": "Dịch vụ của chúng tôi",
             "binding": { "source": "Service", "groupId": null, "sort": "SortOrder" } } }
```

Component hiện tên + mô tả ngắn + giá + ảnh ngay tại chỗ. Click không đi đâu cả (hoặc mở modal).

### 6.2 Shop làm kỹ — mỗi dịch vụ một trang riêng

Spa 15 dịch vụ, muốn mô tả sâu kèm gallery ảnh "những lần đã phục vụ khách".

```
1. Shop tạo Page thường (Kind = Composable, slug "/cham-soc-da-chuyen-sau")
2. Lắp component tuỳ ý:  ServiceHighlight + Gallery + Timeline + Testimonials + ContactForm
3. Gán Service.DetailPageId = <pageId>
4. ServiceGrid trên trang chủ tự động trỏ link tới trang đó
```

**Trang chi tiết dịch vụ là `Page` `Composable` bình thường** — shop tự thiết kế bằng đúng bộ component của builder, không có route cố định nào do vsite định nghĩa. Đây là toàn bộ nội dung của #54.

### 6.3 Vì sao giữ entity `Service` thay vì bỏ hẳn (chỉ dùng Page + component tĩnh)

Phương án "bỏ entity, dịch vụ chỉ là item trong component tĩnh" đã được cân nhắc và loại bỏ:

| | Có entity `Service` | Chỉ Page + component tĩnh |
|---|---|---|
| Đổi tên/giá dịch vụ | Sửa 1 chỗ, mọi component bind tự cập nhật | Sửa tay ở từng component đang nhắc tới |
| Cùng dịch vụ hiện ở 3 trang | Bind 3 lần, một nguồn | Nhập lại 3 lần, dễ lệch |
| Thống kê "dịch vụ nào được xem nhiều" | Có, vì có `Id` ổn định | Không có gì để đếm |
| `Booking` Phase 3 đặt lịch cho dịch vụ nào | Trỏ `ServiceId` rõ ràng | Phải suy ra từ tên trang — mong manh |
| Độ phức tạp | 2 bảng đơn giản | Gần như 0 |

Chi phí của việc giữ entity là hai bảng phẳng không có logic phức tạp. Lợi ích là một nguồn sự thật và một điểm neo ổn định cho `Booking`.

### 6.4 Ràng buộc

- `DetailPageId` phải trỏ tới `Page` thuộc **cùng `Website` của cùng shop** — kiểm ở tầng handler
- `Page.Kind` phải là `Composable` (không cho trỏ vào `System` page)
- Xoá `Page` đang được `Service` trỏ tới → **cảnh báo**, cho phép tiếp tục, `DetailPageId` set NULL
- Nhiều `Service` **được phép** trỏ chung một `Page` (shop gộp mô tả nhiều dịch vụ vào một trang)

---

## 7. Thư viện component cho `Service`

| Component | Binding | Dùng khi |
|---|---|---|
| `ServiceGrid` | `{ source: "Service", groupId?, take?, sort }` | Lưới có ảnh, 4–12 dịch vụ |
| `ServiceList` | như trên | Danh sách gọn, hợp shop 2–3 dịch vụ |
| `PriceTable` | như trên | Bảng giá, hợp salon/spa |
| `ServiceHighlight` | `{ source: "Service", serviceId }` | Spotlight **một** dịch vụ chủ lực, thường đặt ngay dưới Hero |
| `ServiceGroupTabs` | `{ source: "ServiceGroup" }` | Tab theo nhóm, cho shop có nhiều nhóm |

Mỗi component có 3–5 `variant` theo #13, đủ để website các shop không giống hệt nhau.

**Lưu ý `ServiceHighlight` dùng `serviceId` (số ít) chứ không phải list** — đây là lý do entity `Service` cần `Id` ổn định. Nếu dịch vụ chỉ là text tự do trong component thì không có gì để trỏ tới.

**Ràng buộc toàn vẹn:** xoá `Service` → quét tree tìm `binding.serviceId` trỏ tới nó → cảnh báo, không chặn, component tự ẩn ở runtime. Cùng chính sách với `binding.categoryId` của `Product`.

---

## 8. Bảng đối chiếu với hệ cũ

| Khía cạnh | VSite 4.8 | vsite mới | Vì sao |
|---|---|---|---|
| Dịch vụ tổ chức như sản phẩm | dùng chung `SProduct` + `ProdStatus` | module riêng, 2 bảng phẳng | #40 — gộp là nguồn gây rối |
| Trang chi tiết | route cố định, code cứng theo action | `Page` `Composable` shop tự dựng | Linh hoạt hơn, ít code hơn |
| Phân nhóm | cây danh mục dùng chung với sản phẩm | nhóm phẳng riêng | 2–5 dịch vụ không cần cây |
| Nguồn menu | `ProdCategory` lẫn lộn nhiều loại | `ShopServiceGroup` rõ ràng | #34 |
| Giá | bắt buộc | tuỳ chọn + `PriceNote` | Nhiều ngành không báo giá trước được |

---

## 9. Thứ tự triển khai

```
1. ShopServiceGroup
2. Service  (chưa có DetailPageId — Page chưa tồn tại)
3. [chờ module Website: Page xong]
4. Thêm cột Service.DetailPageId + FK
5. Component ServiceGrid / ServiceList / PriceTable / ServiceHighlight + manifest
6. Binding Resolver: source "Service" + "ServiceGroup"
```

Bước 2 và 4 tách nhau vì `Service` không phụ thuộc `Website` — shop có thể nhập dịch vụ trước khi dựng website. Nhưng cột nên được thêm trong cùng đợt migration nếu hai module làm song song.

---

## 10. Điểm còn trống

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | Giới hạn số `Service` theo gói dịch vụ | ⚠️ Chốt cùng lúc với quota media (`05` §25 #1) |
| 2 | `Service` có cần `Testimonial` neo riêng không (đánh giá theo từng dịch vụ) | ⏳ Phase 3, cân nhắc cùng `Booking` |
| 3 | Nhân viên phụ trách dịch vụ (`ServiceStaff`) | ⏳ Phase 3, bắt buộc nếu làm `Booking` |
| 4 | Sanitize HTML cho `Service.Description` | ⚠️ Dùng chung whitelist với `Product.Description` (`05` §25 #3) |
