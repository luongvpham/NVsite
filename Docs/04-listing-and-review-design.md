# vsite — Thiết Kế: Listing (tin đăng marketplace) & Review

> Tài liệu này chốt mô hình `Shop.Kind` / `ServiceCategory` / `Listing` / `Lead` / `Review`, và mô hình doanh thu hai luồng chi phối chúng.
>
> **Tài liệu liên quan:** `01-project-ideal.md` (ý tưởng) · `02-tech-stack-and-decision.md` (quyết định kỹ thuật) · `03-identity-entity-design.md` (Identity/Shop membership)
>
> **Trạng thái đồng bộ:** các sửa đổi mà tài liệu này yêu cầu ở 01/02/03 **đã được áp dụng xong** — §10 giờ là nhật ký đối chiếu, không còn là danh sách việc phải làm. Mọi thay đổi so với tài liệu này phải được ghi nhận lại tại đây.

---

## 1. Nguyên tắc nền

Sáu nguyên tắc dưới đây chi phối toàn bộ phần còn lại. Nếu một thiết kế tương lai mâu thuẫn với chúng, thiết kế đó sai chứ không phải nguyên tắc sai.

| # | Nguyên tắc | Lý do |
|---|---|---|
| 1 | **Hai luồng doanh thu độc lập:** (a) phí duy trì website cho shop, (b) phí đăng tin trên marketplace | Shop có thể mua một trong hai, hoặc cả hai. Mọi entity phải hỗ trợ được cả bốn tổ hợp, không giả định shop nào cũng dùng builder |
| 2 | **Website shop độc lập hoàn toàn với vsite về mặt nội dung** | Shop trả tiền để sở hữu website đó (đúng tinh thần Quyết định #4). Khách vào `spa-abc.com` không cần biết vsite tồn tại |
| 3 | **Đánh giá là tài sản của vsite, không phải của shop** | Chỉ hiển thị trên `vsite.vn`, **không bao giờ** render trên website shop (§6.4) |
| 4 | **vsite KHÔNG tự động đăng dịch vụ của shop lên marketplace** | Shop chủ động tạo `Listing`, chủ động chọn nội dung. Không có auto-map dữ liệu từ website shop sang marketplace |
| 5 | **Mọi `Listing` đều thuộc một `Shop`** — kể cả shop chỉ có website bên ngoài | Cần chủ thể để billing, phản hồi đánh giá, chịu trách nhiệm nội dung, nhận thống kê lead (§2.1) |
| 6 | **vsite không xử lý giao dịch/thanh toán giữa khách và shop** ở giai đoạn này | Khách làm việc trực tiếp với shop. vsite là kênh khám phá + dẫn khách, không phải sàn giao dịch |

---

## 2. Hai luồng doanh thu và hệ quả lên `Shop`

### 2.1 `Shop.Kind` — hai loại shop

```
Shop
─────────────────────────────────────────────
Id                 UUID       PK
Name               string     NOT NULL
Slug               string     NOT NULL  UNIQUE
Kind               enum       NOT NULL  { Hosted, ExternalOnly }
ExternalUrl        string?              -- NOT NULL khi Kind = ExternalOnly
Status             enum       { Draft, Active, Suspended, Closed }
CreatedAt / UpdatedAt
```

| | `Hosted` | `ExternalOnly` |
|---|---|---|
| Website do vsite dựng | ✅ builder + `ShopDomain` | ❌ đã có website riêng bên ngoài |
| Doanh thu | Phí duy trì website (+ phí listing nếu đăng) | Chỉ phí listing |
| Portal | Đầy đủ (dịch vụ, builder, listing, lead, đánh giá) | **Rút gọn**: listing, lead, đánh giá, hồ sơ shop |
| `Listing.TargetKind` cho phép | `ShopHome` · `ShopPage` | **chỉ** `ExternalUrl` |
| Trang hồ sơ `vsite.vn/shop/{slug}` | ✅ (song song với website riêng) | ✅ **bản đơn giản** — xem §5 |

**Vì sao không cho `Listing.ShopId = NULL`:** một tin đăng không có chủ thì không có ai để xuất hoá đơn (doanh thu #2), không ai phản hồi được đánh giá, không ai chịu trách nhiệm khi nội dung giả mạo, và không ai nhận thống kê lead — mà thống kê lead chính là thứ chứng minh giá trị để shop tiếp tục trả tiền. `ExternalOnly` giải quyết đúng nhu cầu "shop có website riêng" mà không phá bốn thứ trên.

**`ExternalOnly` là phễu cho doanh thu #1.** Shop vào bằng cửa rẻ (chỉ đăng tin), thấy hiệu quả, rồi nâng cấp lên website. Nâng cấp = đổi `Kind` sang `Hosted` + tạo `ShopDomain`, **không mất** tài khoản, tin đăng, đánh giá hay lịch sử lead. Nếu để listing không chủ thì không tồn tại đường nâng cấp này.

### 2.2 Ràng buộc khi đổi `Kind`

| Chiều | Cho phép | Lưu ý |
|---|---|---|
| `ExternalOnly` → `Hosted` | ✅ | Sau khi tạo website, shop **được** đổi `TargetKind` sang `ShopHome`/`ShopPage`. Không tự động đổi — shop chủ động |
| `Hosted` → `ExternalOnly` | ✅ (ngừng trả phí website) | Mọi `Listing` đang trỏ `ShopHome`/`ShopPage` **phải** được chuyển sang `ExternalUrl` hoặc bị `Unpublished`. Không được để listing trỏ vào website đã tắt |

⚠️ Chiều thứ hai là chỗ dễ quên. Website tắt mà listing vẫn trỏ vào = 404 cho khách đến từ vsite, và vsite mang tiếng. Phải chặn ở tầng application khi đổi `Kind`, không phải phát hiện bằng job.

---

## 3. `ServiceCategory` & cơ chế đánh giá theo tin đăng

### 3.1 `ServiceCategory` — taxonomy toàn cục, platform quản trị

```
ServiceCategory
─────────────────────────────────────────────
Id             UUID      PK
ParentId       UUID?     → ServiceCategory
Code           string    NOT NULL  UNIQUE
Name           string    NOT NULL
Slug           string    NOT NULL  UNIQUE   -- phục vụ landing page SEO
Icon           string?
IsLeaf         bool      NOT NULL
Index          int       NOT NULL
Status         enum      { Active, Hidden }
```

- **Chỉ vsite được tạo/sửa.** Shop không tự đặt danh mục — nếu shop A ghi "Massage body" còn shop B ghi "Massage toàn thân" thì search không gom được và landing page khu vực rỗng nghĩa.
- `Listing` **chỉ được gắn vào node lá** (`IsLeaf = true`). Gắn vào node cha làm kết quả tìm kiếm nhiễu.
- Slug ổn định, không đổi theo tên — đổi slug là mất SEO của landing page.

### 3.2 Đánh giá neo vào `Listing` — shop được làm lại từ đầu ⭐

**Chốt:** `Review` neo vào **`Listing`**, không neo vào một entity bền vững ở tầng shop. Shop gỡ tin đăng thì đánh giá của tin đó biến mất cùng; đăng lại là **bắt đầu từ con số 0**.

**Vì sao chọn hướng này** (đã cân nhắc phương án ngược lại và loại):

Nếu đánh giá bám vĩnh viễn theo `(Shop, ngành)`, một shop bị đánh giá xấu nặng sẽ **rời nền tảng luôn** — không còn lý do gì để tiếp tục trả phí đăng tin khi điểm số đã hỏng không sửa được. vsite mất cả shop lẫn cơ hội để shop đó cải thiện.

Reset **không phải miễn phí**: shop mất toàn bộ uy tín đã xây, quay về trạng thái không ai biết mình là ai, phải bắt đầu lại từ đầu và cố gắng đạt đánh giá tốt hơn. Đó là một hình phạt thật, đủ để tạo động lực đúng, nhưng có lối ra — khác với án chung thân.

**⚠️ Rủi ro đã ghi nhận và các lớp giảm nhẹ bắt buộc:**

Shop có thể coi reset là **chiến thuật định kỳ**: gom đánh giá xấu → gỡ tin → đăng lại tuần sau → sạch sẽ. Với shop cố tình, chi phí chỉ là vài ngày không hiển thị, rẻ hơn nhiều so với cải thiện chất lượng thật. Ba lớp giảm nhẹ, bắt buộc triển khai cùng lúc với cơ chế này:

| # | Lớp | Chi tiết |
|---|---|---|
| 1 | **Hiển thị tuổi tin đăng** | *"Đăng tin từ 03/2026"* trên kết quả tìm kiếm và trang hồ sơ. Shop reset liên tục sẽ luôn hiện "đăng tin từ tuần trước" — khách tự đọc được tín hiệu, vsite không cần phán xét gì. **Rẻ nhất và hiệu quả nhất trong ba lớp** |
| 2 | **Cooldown khi đăng lại cùng ngành** | Gỡ rồi đăng lại cùng `CategoryId` trong vòng 30 ngày → phải chờ hết cooldown, hoặc bị hạ thứ hạng tạm thời. Làm cho reset đắt hơn |
| 3 | **Đếm số lần reset (nội bộ)** | Lưu ở `ShopCategoryHistory`, **không hiển thị công khai**. Shop reset lần thứ 3 trong 6 tháng → vào hàng đợi kiểm tra thủ công. Đây là tín hiệu chất lượng rõ ràng |

```
ShopCategoryHistory        -- chỉ để phát hiện lạm dụng, KHÔNG chứa Review
─────────────────────────────────────────────
Id, ShopId, CategoryId
ListingId          UUID      -- listing đã bị gỡ
ReviewCountAtDelete  int
RatingAvgAtDelete    decimal?
DeletedAt          timestamp
```

Bảng này **không** khôi phục đánh giá và **không** hiển thị cho khách. Nó chỉ trả lời câu hỏi vận hành: *"shop này đã reset mấy lần?"*

**Quan hệ:**

```
Shop  1 ── N  Listing  1 ── N  Review
                       (gỡ Listing → Review đi theo)
```

Không còn entity `ShopCategory` trung gian. `Listing` gắn thẳng `CategoryId`, và `UNIQUE (ShopId, CategoryId)` chuyển về `Listing` (chỉ tính các listing chưa bị gỡ).

---

## 4. `Listing`

```
Listing
─────────────────────────────────────────────
Id                 UUID       PK
ShopId             UUID       NOT NULL → Shop
CategoryId         UUID       NOT NULL → ServiceCategory (IsLeaf = true)

ListedSince        timestamp  NOT NULL   -- ngày đăng lần đầu, hiển thị công khai (§3.2 lớp 1)
                                         -- KHÔNG reset khi sửa nội dung; chỉ set lúc tạo
DeletedAt          timestamp?            -- xoá mềm: Review đi theo, không hiển thị nữa

RatingAvg          decimal?              -- derived, xem §6.5
ReviewCount        int        NOT NULL DEFAULT 0

-- nội dung, shop tự nhập cho vsite (KHÔNG map từ dữ liệu website shop)
Title              string     NOT NULL
Description        string     NOT NULL   -- TEXT THUẦN, không HTML (§4.3)
ImageUrls          text[]     NOT NULL   -- >= 1 ảnh

-- vị trí RIÊNG của listing
Location           geography(Point, 4326)  NOT NULL
Address            string     NOT NULL
WardCode / DistrictCode / ProvinceCode     -- phục vụ landing page SEO khu vực

-- giá: TUỲ CHỌN HOÀN TOÀN
PriceFrom          decimal?
PriceTo            decimal?
PriceNote          string?    -- "Tuỳ tình trạng xe", "Liên hệ để báo giá"

-- đích đến
TargetKind         enum       NOT NULL { ShopHome, ShopPage, ExternalUrl }
TargetPageId       UUID?      → Page
TargetUrl          string?

Phone              string?    -- nút gọi trực tiếp trên vsite

PublishFrom        timestamp  NOT NULL
PublishTo          timestamp?            -- null = không hết hạn
Status             enum       { Draft, Scheduled, Active, Expired, Unpublished }
ModerationStatus   enum       { Pending, Approved, Rejected }

CreatedAt / UpdatedAt / LastIndexedAt

-- 1 shop ↔ tối đa 1 listing đang sống trong mỗi ngành
UNIQUE (ShopId, CategoryId) WHERE DeletedAt IS NULL
```

⚠️ **Xoá listing là xoá mềm** (`DeletedAt`), không xoá cứng — cần giữ row để `Review` không mồ côi FK và để đối chiếu khi điều tra lạm dụng. Nhưng listing đã xoá **không** hiển thị ở bất kỳ đâu và đánh giá của nó **không** được tính vào bất kỳ con số nào. Về mặt người dùng, nó đã biến mất.

⚠️ **`ListedSince` không được reset khi shop sửa nội dung tin đăng.** Chỉ set một lần lúc tạo. Nếu reset theo mỗi lần sửa thì lớp giảm nhẹ #1 ở §3.2 vô hiệu — shop chỉ cần sửa một chữ là "trẻ lại". Đây đúng loại lỗi AI agent sẽ tạo ra khi viết handler update (thấy `UpdatedAt` thì gán luôn cả `ListedSince`).

### 4.1 Ràng buộc DB

```sql
-- [1] Target phải nhất quán với TargetKind
ALTER TABLE "Listing" ADD CONSTRAINT ck_listing_target CHECK (
  (TargetKind = 'ShopHome'    AND TargetPageId IS NULL AND TargetUrl IS NULL) OR
  (TargetKind = 'ShopPage'    AND TargetPageId IS NOT NULL) OR
  (TargetKind = 'ExternalUrl' AND TargetUrl    IS NOT NULL)
);

-- [2] Giá: nếu có cả hai thì From <= To
ALTER TABLE "Listing" ADD CONSTRAINT ck_listing_price CHECK (
  PriceFrom IS NULL OR PriceTo IS NULL OR PriceFrom <= PriceTo
);

-- [3] Thời hạn
ALTER TABLE "Listing" ADD CONSTRAINT ck_listing_period CHECK (
  PublishTo IS NULL OR PublishTo > PublishFrom
);

-- [4] Một shop chỉ có một listing đang sống trong mỗi ngành
CREATE UNIQUE INDEX ux_listing_shop_category
  ON "Listing" (ShopId, CategoryId)
  WHERE DeletedAt IS NULL;

-- [5] TargetPageId phải thuộc đúng Shop  → composite FK
ALTER TABLE "Page"    ADD CONSTRAINT uq_page_id_shop UNIQUE (Id, ShopId);
ALTER TABLE "Listing" ADD CONSTRAINT fk_listing_page
  FOREIGN KEY (TargetPageId, ShopId) REFERENCES "Page" (Id, ShopId);
```

> `Page` (và `PageKind`) là entity của module `Website`, chốt ở Quyết định #33 — thiết kế chi tiết ở **Phase 2**. Ràng buộc [5] chỉ áp khi module đó tồn tại; ở Phase 1 mọi shop `Hosted` chưa có website nên `TargetKind` thực dùng là `ExternalUrl`.

**Ràng buộc [5]** dùng đúng khuôn composite FK `(RoleId, RoleScope)` ở §4 của `03-identity-entity-design.md`. Nó chặn ở tầng DB việc shop A đăng listing trỏ vào trang của shop B. Đây là loại lỗi AI agent sẽ tạo ra khi viết use-case mới — FK thường không bắt được.

**Ràng buộc application-level (không CHECK xuyên bảng được):**

- `Shop.Kind = 'ExternalOnly'` ⇒ `TargetKind = 'ExternalUrl'`. Enforce ở domain + Hangfire integrity job (cùng khuôn với nhánh Zalo ở §4 tài liệu 03).
- **Cooldown đăng lại** (§3.2 lớp 2): tạo listing mới với `(ShopId, CategoryId)` đã có bản ghi `ShopCategoryHistory.DeletedAt` trong 30 ngày → chặn hoặc hạ thứ hạng tạm thời. Không CHECK được vì cần so sánh thời gian với bảng khác.

### 4.2 Đích đến: tham chiếu với shop `Hosted`, URL thô với `ExternalOnly`

| `TargetKind` | Lưu gì | Resolve URL thế nào |
|---|---|---|
| `ShopHome` | không lưu gì | `ShopDomain` primary → `/` |
| `ShopPage` | `TargetPageId` | `ShopDomain` primary + `resolveUrl(page.slug)` (Quyết định #11) |
| `ExternalUrl` | URL thô | dùng nguyên văn |

**Vì sao shop `Hosted` không được lưu URL thô:** shop đổi custom domain hoặc đổi slug trang là listing trỏ vào 404 mà không ai biết, vì đó chỉ là một chuỗi text. Lưu tham chiếu thì URL tự đúng, và luôn trỏ về **primary domain** nên không tạo duplicate content làm loãng SEO (Quyết định #10).

**Với `ExternalUrl` buộc phải chấp nhận URL thô** → cần **Hangfire job kiểm tra link chết định kỳ** (đề xuất hàng tuần), báo cho shop và gắn cờ nếu 404 nhiều lần liên tiếp. Đây là khác biệt vận hành thật giữa hai `Kind`, và cũng là điểm bán hàng tự nhiên cho doanh thu #1.

⚠️ **Link từ vsite sang shop phải là link thật** (`<a href>`, dofollow), **không** redirect qua `vsite.vn/go/{id}`. Redirect trung gian giết giá trị SEO mà `01-project-ideal.md` hứa cho shop ("được nền tảng đưa khách tới"). Đếm click bằng event tracking phía client (§7), không đổi cấu trúc link.

### 4.3 `Description` là text thuần

Shop nhập trực tiếp trên vsite, không qua builder. Cho HTML vào đây là mở XSS **trên chính domain `vsite.vn`** — khác hẳn website shop, nơi HTML nằm trong Component Tree đã kiểm soát bởi Component Registry (Quyết định #17).

Cho phép: xuống dòng. Không cho phép: thẻ HTML, markdown link, script. Sanitize ở BE, không tin FE.

### 4.4 Vòng đời

`Status` là **derived** — hàm của `PublishFrom`/`PublishTo` + `ModerationStatus` + shop có bấm gỡ hay không. Lưu cột để tiện query ở Portal, nhưng:

⚠️ **Query search PHẢI lọc `publishFrom <= now <= publishTo` trực tiếp**, không tin vào cột `Status` hay độ trễ của Hangfire. Job trễ 10 phút = tin hết hạn vẫn hiển thị, mà shop đã ngừng trả tiền. Job chỉ để dọn dẹp và nhắc gia hạn.

---

## 5. Trang hồ sơ shop trên `vsite.vn/shop/{slug}`

**Mọi shop đều có**, kể cả `ExternalOnly`. Đây là nơi đánh giá tồn tại — không có nó thì không có chỗ nào chứa đánh giá của shop ngoài.

Trang hồ sơ **gộp hiển thị** đánh giá của tất cả `Listing` đang sống của shop, nhưng điểm số vẫn tính và hiển thị **theo từng listing** (§6.5). Shop chưa đăng tin nào thì trang hồ sơ tồn tại nhưng không có đánh giá — đúng theo nguyên tắc #4 (không đăng tin thì không hiện diện trên marketplace).

| | `Hosted` | `ExternalOnly` |
|---|---|---|
| Nội dung | Hồ sơ + danh sách listing + đánh giá + nút "Xem website" | **Bản đơn giản**: tên, ảnh, mô tả ngắn, các listing, vị trí bản đồ, đánh giá, nút "Xem website" ra URL ngoài |
| Do ai dựng | vsite render (không phải builder) | vsite render |

⚠️ **Trang này KHÔNG phải output của builder.** Nó là trang do vsite thiết kế, thống nhất cho mọi shop — đừng nhầm với "Shop (website công khai)" trong `apps/web` ở Quyết định #22. Hai thứ khác nhau:

| | Trang hồ sơ vsite | Website shop |
|---|---|---|
| URL | `vsite.vn/shop/{slug}` | `spa-abc.com` · `spa-abc.vsite.vn` · `vsite.vn/{slug}` (nếu domain kind = Path) |
| Ai kiểm soát nội dung | vsite | shop (builder) |
| Có đánh giá | ✅ | ❌ |

⚠️ **Xung đột URL — đã chốt:** shop `Hosted` dùng `ShopDomain(kind = Path)` có website tại đúng `vsite.vn/{slug}`. Website shop thắng (đó là thứ shop trả tiền để sở hữu); **trang hồ sơ nằm ở `vsite.vn/shop/{slug}`**, và `shop` đã được đưa vào `reservedPaths` của `config/reserved-routes.json` (Quyết định #24 — là nguồn duy nhất, Quyết định #8 không còn giữ danh sách riêng).

---

## 6. `Review` — đánh giá

### 6.1 Chốt: không xác thực việc đã dùng dịch vụ

**Đã cân nhắc và loại bỏ** cơ chế `ServiceEncounter` (scan mã giữa shop và khách để chứng minh đã phục vụ).

**Lý do loại:** cơ chế đó đòi hỏi thay đổi hành vi của **cả hai bên** — shop phải nhớ đưa mã, khách phải chịu quét. Ở giai đoạn nền tảng chưa có người dùng, tỉ lệ áp dụng gần như bằng 0, trong khi chi phí xây dựng (mã xoay vòng, màn hình Portal, app nhân viên, kiểm tra vị trí, luồng xác nhận hai chiều) rất cao. Ngoài ra shop kiểm soát việc phát mã nên vẫn thiên lệch chọn mẫu — đổi lấy một tín hiệu không thật sự sạch.

**Chốt:** user đã đăng nhập được đánh giá tự do, không cần chứng minh. Shop được phản hồi.

**Nhưng "tự do" nghĩa là không cần chứng minh đã dùng dịch vụ — KHÔNG phải không có kiểm soát.** §6.6 là bắt buộc, không phải tuỳ chọn.

### 6.2 Entity

```
Review
─────────────────────────────────────────────
Id                 UUID       PK
ListingId          UUID       NOT NULL → Listing
ShopId             UUID       NOT NULL → Shop          -- denormalize
UserId             UUID       NOT NULL → User

Rating             smallint   NOT NULL  CHECK (Rating BETWEEN 1 AND 5)
Content            string     NOT NULL             -- text thuần
HasContacted       bool       NOT NULL DEFAULT false  -- xem §6.3

Status             enum       NOT NULL { Published, Hidden, UnderReview }
HiddenReason       string?

ShopReplyContent   string?
ShopRepliedAt      timestamp?
ShopReplyByUserId  UUID?

CreatedAt          timestamp
EditableUntil      timestamp  NOT NULL   -- CreatedAt + 24h

UNIQUE (UserId, ListingId)
```

**`UNIQUE (UserId, ListingId)`** — một người một đánh giá cho mỗi tin đăng. Khách quay lại lần hai thì sửa đánh giá cũ, không viết mới. Nếu không, shop sẽ khuyến khích khách ruột đánh giá mỗi lần ghé để đẩy điểm.

**Khi listing bị gỡ** (`DeletedAt`), toàn bộ `Review` của nó ngừng hiển thị và ngừng được tính vào mọi con số. Giữ row trong DB để không mồ côi FK và để đối chiếu khi điều tra lạm dụng (§3.2 lớp 3), nhưng về mặt người dùng chúng đã biến mất. Shop đăng lại là listing mới, `ReviewCount = 0`.

**Chưa có ảnh ở MVP** (§6.7).

### 6.3 Nhãn "đã liên hệ shop" — tín hiệu rẻ thay cho xác thực

Khi tạo đánh giá, nếu user có `Lead` (kind `ClickPhone` hoặc `ClickWebsite`) tới **chính listing đó** trong vòng 90 ngày trước → `HasContacted = true`, hiển thị nhãn nhẹ *"đã liên hệ shop"*.

Không chứng minh được gì chắc chắn, nhưng **phân biệt được người có tương tác thật với tài khoản rải đánh giá hàng loạt**, và **không tốn thêm thao tác nào của cả shop lẫn khách**. Đây là lý do nó đáng làm còn `ServiceEncounter` thì không.

Mặc định sắp đánh giá có `HasContacted = true` lên trên.

### 6.4 ⚠️ Đánh giá KHÔNG hiển thị trên website shop

Chỉ hiển thị trên `vsite.vn` (trang hồ sơ shop + kết quả tìm kiếm). Nhất quán với Quyết định #4 và nguyên tắc #3 ở §1: website shop là sản phẩm shop trả tiền để sở hữu; đánh giá là tài sản của vsite.

⚠️ **`builder-components` KHÔNG được có component nào render `Review` từ marketplace.** Thư viện component ở `01-project-ideal.md` mục 7 có "Testimonials (đánh giá khách)" — đó là **testimonial shop tự nhập vào props**, hoàn toàn khác `Review`. Ghi rõ vào `CLAUDE.md` của `builder-components`: đây đúng loại nhầm lẫn AI agent sẽ mắc khi thấy hai khái niệm cùng tên.

Hệ quả kỹ thuật: `builder-renderer` chạy trong `apps/web` cho cả hai loại trang, nên **data binding của component không được có nguồn `Reviews`** — chặn ở Component Registry manifest, không chỉ ở tài liệu.

### 6.5 `RatingAvg` là derived

Tính lại qua Hangfire khi có review mới/ẩn/sửa, ghi vào `Listing.RatingAvg` + `ReviewCount`, rồi đẩy vào ES. **Không** tính bằng `AVG()` lúc query.

**Hiển thị trên kết quả tìm kiếm:** điểm của **chính listing đó**. Không mượn điểm từ listing khác của cùng shop.

**Listing chưa có đánh giá** (`ReviewCount = 0`): hiện *"Chưa có đánh giá"* + `ListedSince`, **không** hiện 0 sao. Không mượn điểm chung của shop — điểm mượn sẽ vô hiệu hoá toàn bộ cơ chế reset ở §3.2 (shop gỡ tin xấu nhưng vẫn giữ điểm qua đường vòng).

**Xếp hạng listing mới:** không đẩy xuống đáy chỉ vì chưa có đánh giá — nếu không, shop mới không bao giờ có cơ hội nhận đánh giá đầu tiên, và cơ chế "làm lại từ đầu" trở thành án tử. Đề xuất: trộn một tỉ lệ nhỏ listing mới vào kết quả (có nhãn *"Tin mới"*), giới hạn theo khoảng cách để không giảm chất lượng tìm kiếm.

### 6.6 Bốn lớp kiểm soát bắt buộc

Không có bằng chứng phục vụ nghĩa là **mất lớp phòng thủ tự nhiên**. Bốn thứ dưới đây không phải tính năng chống gian lận — chúng là nghĩa vụ pháp lý và rủi ro sống còn của nền tảng.

| # | Lớp | Chi tiết |
|---|---|---|
| 1 | **Bắt buộc đăng nhập** | Không có tài khoản thì không rate-limit được và không chặn được khi lạm dụng. Đăng nhập tại `vsite.vn`, audience `vsite-main` (Quyết định #31) |
| 2 | **Rate-limit** | Theo user (N đánh giá/ngày) **và** theo IP. Chặn user rải đánh giá vào nhiều shop cùng ngành + cùng khu vực trong thời gian ngắn — dấu hiệu review farm |
| 3 | **Không xoá, không sửa sau 24h** | Shop được *phản hồi* và *báo cáo*, **không được gỡ**. Cho gỡ là toàn bộ hệ thống đánh giá thành vô nghĩa — và đó chính xác là thứ phân biệt vsite với Facebook page của shop. Không sửa sau 24h để chặn kiểu đổi 5★ thành 1★ khi tranh chấp |
| 4 | **Quy trình khiếu nại có thời hạn cam kết** | Shop báo cáo → `Status = UnderReview` → vsite xem xét trong N ngày làm việc → `Published` hoặc `Hidden` + `HiddenReason`, có audit log. Đánh giá sai sự thật là rủi ro pháp lý cho **vsite**, không chỉ cho shop |

**Phát hiện bất thường (thay cho xác thực):** vì không có bằng chứng phục vụ, tín hiệu duy nhất còn lại là hành vi. Vài quy tắc đơn giản + hàng đợi kiểm tra thủ công là đủ cho MVP:

- Tài khoản tạo dưới X giờ đã đánh giá ngay
- Một cụm tài khoản cùng đánh giá một shop trong vài giờ
- Một user rải 1★ khắp các shop cùng ngành cùng khu vực
- Một shop có tỉ lệ `ReviewCount / Lead` bất thường cao

→ Đưa vào hàng đợi, `Status = UnderReview`, **không** tự động ẩn (false positive làm mất đánh giá thật).

### 6.7 Ảnh trong đánh giá — hoãn sang Phase 2

**Chốt: MVP không có ảnh.** Đánh giá bằng chữ đủ để kiểm chứng nhu cầu.

**Vì sao hoãn:** cho người lạ upload ảnh lên domain vsite nghĩa là vsite có thể bị dùng để phát tán nội dung bất hợp pháp, và ở VN trách nhiệm gỡ bỏ thuộc về nền tảng. Bật tính năng này đòi hỏi **hạ tầng vận hành có người thật**, chưa có ở giai đoạn đầu.

**Khi bật ở Phase 2, bắt buộc có đủ:**

1. Quét nội dung tự động trước khi công khai (dịch vụ ngoài, không tự xây)
2. **Strip EXIF** — ảnh điện thoại có GPS; đăng ảnh chụp tại nhà là lộ địa chỉ nhà của chính người đánh giá
3. Giới hạn số ảnh/đánh giá và dung lượng
4. Hàng đợi gỡ bỏ có người xử lý trong 24h

---

## 7. `Lead` — thay thế cho "đơn hàng"

Không có giao dịch trên vsite, nên `Lead` là nguồn dữ liệu duy nhất chứng minh vsite mang khách tới cho shop. Đây là **cơ sở định giá cho doanh thu #2**.

```
Lead
─────────────────────────────────────────────
Id             UUID       PK
ListingId      UUID       NOT NULL → Listing
ShopId         UUID       NOT NULL → Shop        -- denormalize
UserId         UUID?                             -- null nếu khách chưa đăng nhập
SessionKey     string?                           -- hash, để dedupe khách ẩn danh
Kind           enum       NOT NULL
               { ViewDetail, ClickPhone, ClickWebsite, ClickDirections }
OccurredAt     timestamp  NOT NULL
```

- Đây chính là "Thống kê cơ bản" ở mục 5.2 của `01-project-ideal.md`, giờ có nguồn dữ liệu thật.
- **Ghi bất đồng bộ**, không chặn đường nóng. Gom qua queue → Hangfire ghi theo lô.
- Dedupe: cùng `(SessionKey, ListingId, Kind)` trong 30 phút tính là một lead.
- `SessionKey` phải là hash và có TTL — nó là dữ liệu cá nhân của khách ẩn danh.

---

## 8. Tìm kiếm & Elasticsearch

### 8.1 Mô hình tìm kiếm

Tìm theo bản đồ là luồng chính:

```
geo_distance (bán kính từ vị trí user)
+ CategoryId (chọn từ danh sách ngành)
+ tuỳ chọn: từ khoá, khoảng giá, rating tối thiểu
sort: khoảng cách | rating | mới nhất
```

**Không có facet theo thuộc tính** — đã bỏ `PlatformAttributes` cùng với việc bỏ map dữ liệu (nguyên tắc #4). Kết quả hiển thị: vị trí trên bản đồ, tên, mô tả ngắn, ảnh, điểm đánh giá, khoảng cách. Click → sang đích của listing.

⚠️ **Bán kính mặc định phải thích ứng theo mật độ**, không cố định 5km. Ở quận 1 thì 2km đã quá nhiều kết quả; ở ngoại thành 5km có thể trống trơn — và người dùng thấy bản đồ rỗng ở lần dùng đầu sẽ không quay lại. Cách rẻ: nới dần bán kính cho tới khi đủ ~20 kết quả, hiển thị rõ *"trong bán kính X km"*.

### 8.2 Document ES

Denormalize sẵn (đúng nguyên tắc "giải nén một lần lúc index"):

```
listingId, shopId, shopName, shopKind
categoryId, categoryPath[]        -- cả nhánh, để filter theo node cha
title, description
imageUrl (ảnh đầu)
location (geo_point), wardCode, districtCode, provinceCode
priceFrom, priceTo, hasPriceInfo
ratingAvg, reviewCount
targetUrl (đã resolve sẵn)
publishFrom, publishTo
```

**Điều kiện vào index:** `ModerationStatus = Approved` **và** `Shop.Status = Active` **và** `Listing.DeletedAt IS NULL`.

**Reindex trigger:** `ListingUpdated`, `ReviewCountChanged`, `ShopUpdated` (tên/kind), `ShopDomainChanged` (đổi domain → `targetUrl` thay đổi), `ShopSuspended` (theo `ShopId`, không phải từng listing).

⚠️ `ShopDomainChanged` rất dễ quên. Triệu chứng: shop đổi domain xong, kết quả tìm kiếm vẫn dẫn về domain cũ.

**Sắp xếp:** listing **không có thông tin giá** (`hasPriceInfo = false`) bị đẩy xuống dưới trong cùng mức liên quan. Nếu không, mọi shop sẽ để trống giá và "giá cả minh bạch" — giá trị cốt lõi #1 ở `01-project-ideal.md` mục 9 — chết ngay ở MVP.

### 8.3 Landing page SEO khu vực

`(CategoryId × ProvinceCode/DistrictCode)` → landing page tĩnh, có canonical, được index. Ví dụ `vsite.vn/spa/quan-7`.

⚠️ **Mọi URL có ≥2 điều kiện lọc phải `noindex`.** Tổ hợp filter sinh không gian URL vô hạn; không có quy tắc này thì crawl budget của toàn platform bị đốt sạch (mở rộng của Quyết định #10).

---

## 9. Kiểm duyệt & vận hành

`ModerationStatus` trên `Listing` và hàng đợi `UnderReview` trên `Review` **là hạng mục vận hành có chi phí người thật**, không phải tính năng làm xong là xong.

| Giai đoạn | Cách làm |
|---|---|
| MVP | Auto-approve listing + hàng đợi báo cáo. Auto-check cơ bản: từ khoá cấm, trùng lặp nội dung, URL đích hợp lệ |
| Khi có traffic | Duyệt trước với shop mới, auto-approve với shop đã có lịch sử sạch |

Listing xuất hiện trên landing page SEO **của chính vsite** → nội dung rác làm hỏng uy tín platform, không chỉ của shop. Đây là lý do không thể bỏ hoàn toàn kiểm duyệt kể cả ở MVP.

---

## 10. Nhật ký đối chiếu với tài liệu 01/02/03

Trước đây mục này là danh sách **việc cần sửa** ở các tài liệu khác. Toàn bộ đã được áp dụng; giữ lại dưới dạng nhật ký để hiểu vì sao các tài liệu kia có hình dạng hiện tại.

### 10.1 `01-project-ideal.md` — ✅ đã áp dụng

| Mục | Đã sửa thành |
|---|---|
| §2 Định vị | Bỏ tham chiếu Yelp/ShopeeFood; nói rõ vsite là kênh **khám phá + dẫn khách**, giao dịch diễn ra trực tiếp giữa khách và shop |
| §2.1 (mục mới) | Hai luồng doanh thu + `Hosted`/`ExternalOnly` + nguyên tắc không auto-đăng dịch vụ |
| §3.2 | Thêm chiều phân loại `Hosted` vs `ExternalOnly` bên cạnh phân loại theo quy mô |
| §4 | 3 app → **2 app** (`web`, `portal`) theo Quyết định #22; phân biệt rõ trang hồ sơ shop ≠ website shop |
| §5.1 | Chủ thể tìm kiếm là **Listing**; bỏ "số người đã sử dụng", bỏ "so sánh shop", bỏ sắp xếp theo lượt sử dụng |
| §5.2 | Thêm quản lý tin đăng, thống kê lead, phản hồi đánh giá |
| §5.5 · §7 | "Testimonials" ghi rõ là **shop tự nhập**, không phải `Review` |
| §8 | Phase 1 gồm Listing + kiểm duyệt |
| §9 | Bỏ "số liệu giao dịch thật"; dựa trên đánh giá + nhãn "đã liên hệ" |
| Toàn bộ bảng Phase | Đánh số lại theo Quyết định #39.1 ("MVP" = Phase 1) |

### 10.2 `02-tech-stack-and-decision.md` — ✅ đã áp dụng

| Mục | Đã sửa thành |
|---|---|
| §1.2 | Thêm `Category`, `Listing`, `Lead`; `Review` lên Phase 1; `Service`/`Website` xuống Phase 2 cho khớp lộ trình |
| Quyết định #8 / #24 | `shop` nằm trong `reservedPaths`; **xoá** danh sách viết tay ở #8, chỉ còn một nguồn JSON |
| Quyết định #22 | Ghi rõ `apps/web` gồm ba loại nội dung, trong đó trang hồ sơ ≠ website shop |
| Quyết định #26 | Phạm vi index: chỉ `Listing`; danh sách reindex trigger theo §8.2 |
| Quyết định #32 | Bỏ "review" khỏi ô ✅ của token `shop:*`; thêm dòng ❌ tường minh |
| Quyết định #34 | Nguồn menu là nhóm dịch vụ **của shop**, không phải `ServiceCategory` toàn cục |
| Mục 6 | Thêm các điểm trống: giá hai luồng, ngưỡng kiểm duyệt, SLA khiếu nại, `ShopServiceGroup` |

**Các quyết định do tài liệu này sinh ra — đã soạn xong:** #35 (taxonomy + đánh giá neo `Listing`), #36 (anti-pattern hệ cũ), #37 (`Shop.Kind`), #38 (Listing opt-in + đánh giá tự do có kiểm soát), #39 (chuẩn hoá Phase và các mâu thuẫn liên tài liệu). #33 (page model) và #34 (navigation) cũng đã soạn.

### 10.3 `03-identity-entity-design.md` — ✅ đã áp dụng

| Mục | Đã sửa thành |
|---|---|
| §3.3 | Định nghĩa "liên hệ" = `Lead.Kind = ClickPhone`; kèm bảng phân biệt với nhãn "đã liên hệ shop" ở §6.3 tài liệu này |
| §3.3 | Ghi rõ **viết đánh giá không tạo `UserShop`** |
| §3.4 | `Shop` trích dẫn có `Kind`/`ExternalUrl`, trỏ về §2.1 tài liệu này làm nguồn sự thật |
| §7.1 | `Review` yêu cầu audience `vsite-main`; phản hồi của shop đi qua `vsite-portal` + role Owner/Manager |
| §9 | Thêm rate-limit endpoint đánh giá, test từ chối token `shop:*`, ràng buộc `ExternalOnly` không có password shop |

### 10.4 Ranh giới sở hữu khái niệm

Khi hai tài liệu cùng mô tả một entity, tài liệu thiết kế chi tiết thắng (Quyết định #39.5):

| Entity | Nguồn sự thật |
|---|---|
| `Shop` (đầy đủ) · `ServiceCategory` · `Listing` · `Review` · `Lead` · `ShopCategoryHistory` | **tài liệu này** |
| `User` · `ExternalLogin` · `UserShop` · `Role` · `PendingRegistration` | `03-identity-entity-design.md` |
| `ShopDomain` · `Page`/`PageKind` · Component Tree | `02-tech-stack-and-decision.md` (Quyết định #7, #33, #12) |

---

## 11. Trạng thái rủi ro

### ✅ Đã đóng

| Rủi ro | Đóng bằng |
|---|---|
| Shop reset đánh giá định kỳ như chiến thuật | **Chưa đóng hoàn toàn** — giảm nhẹ bằng 3 lớp ở §3.2 (hiển thị `ListedSince`, cooldown 30 ngày, đếm reset nội bộ). Đây là đánh đổi có ý thức để shop có đường cải thiện |
| Listing không có chủ → không billing/không ai chịu trách nhiệm | `Shop.Kind = ExternalOnly`, `ShopId` luôn NOT NULL (§2.1) |
| Listing trỏ vào trang của shop khác | Composite FK (§4.1 [4][5]) |
| Shop đổi domain → listing 404 / loãng SEO | Lưu tham chiếu thay vì URL thô với shop `Hosted` (§4.2) |
| XSS trên `vsite.vn` qua nội dung shop nhập | `Description` text thuần, sanitize BE (§4.3) |
| Tin hết hạn vẫn hiển thị dù shop ngừng trả phí | Query ES lọc thời hạn trực tiếp, không tin job (§4.4) |
| Đánh giá rò sang website shop | Cấm ở Component Registry, không chỉ ở tài liệu (§6.4) |
| Shop tự tạo đánh giá bằng token shop | Policy `RequireGlobalScope` cho endpoint review — Quyết định #38 + `03` §7.1 |

### ⚠️ Cần xử lý khi implement

| # | Việc | Ghi chú |
|---|---|---|
| 1 | Job kiểm tra link chết cho `TargetKind = ExternalUrl` | Hàng tuần; báo shop, gắn cờ nếu 404 nhiều lần |
| 2 | Chặn `Hosted → ExternalOnly` khi còn listing trỏ nội bộ | Ở tầng application, không phát hiện bằng job (§2.2) |
| 3 | Bán kính tìm kiếm thích ứng theo mật độ | Không hardcode 5km (§8.1) |
| 4 | Reindex khi `ShopDomainChanged` | Rất dễ quên (§8.2) |
| 5 | Rate-limit endpoint đánh giá theo user **và** IP | §6.6 |
| 6 | Ghi `Lead` bất đồng bộ, dedupe 30 phút | Không chặn đường nóng (§7) |
| 7 | `noindex` cho URL có ≥2 filter | §8.3 |
| 8 | Quy tắc phát hiện bất thường + hàng đợi thủ công | Không auto-ẩn (§6.6) |
| 9 | `SessionKey` của `Lead` phải hash + TTL | Dữ liệu cá nhân khách ẩn danh |
| 10 | Test: token `shop:*` bị từ chối ở mọi endpoint review | Cùng nhóm với §7.1 tài liệu 03 |
| 11 | Xử lý shop bị `Suspended` | Gỡ toàn bộ listing khỏi ES theo `ShopId`, một job, không lặp từng listing |
| 12 | **`ListedSince` không được reset khi update listing** | Chỉ set lúc tạo. Reset theo mỗi lần sửa làm vô hiệu lớp giảm nhẹ #1 (§3.2). Cần test khẳng định điều này |
| 13 | Ghi `ShopCategoryHistory` khi gỡ listing | Nếu quên, lớp #2 (cooldown) và #3 (đếm reset) đều không hoạt động |
| 14 | Listing mới không bị đẩy xuống đáy xếp hạng | Nếu không, "làm lại từ đầu" thành án tử và cơ chế reset mất ý nghĩa (§6.5) |
| 15 | Không mượn điểm shop cho listing chưa có đánh giá | Điểm mượn vô hiệu hoá toàn bộ cơ chế reset (§6.5) |

### 📌 Cố tình để mở

- **Giá cho hai luồng doanh thu** — chưa chốt mô hình (thuê bao / theo listing / theo lead). Entity đã sẵn sàng cho cả ba.
- **Ảnh trong đánh giá** — Phase 2, điều kiện đủ ở §6.7.
- **Đặt lịch / booking** — **Phase 3** theo lộ trình `01-project-ideal.md` mục 8 (Quyết định #39.1). Khi có, `Lead` sẽ có thêm kind mới và **có thể** mở lại bài toán xác thực đánh giá bằng bằng chứng đặt lịch thật — lúc đó cơ chế xác thực mới đáng làm, vì bằng chứng đến miễn phí từ luồng nghiệp vụ.
