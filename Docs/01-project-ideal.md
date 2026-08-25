# vsite — Ý Tưởng Dự Án

> Nền tảng tìm kiếm dịch vụ địa phương theo vị trí, kèm công cụ giúp chủ shop tự tạo website riêng bằng kéo-thả và AI Chat.
>
> **Tài liệu liên quan:** `02-tech-stack-and-decision.md` (quyết định kỹ thuật) · `03-identity-entity-design.md` (Identity) · `04-listing-and-review-design.md` (tin đăng & đánh giá)
>
> **Quy ước Phase:** lộ trình ở mục 8 là nguồn sự thật duy nhất; mọi bảng tính năng trong tài liệu này ghi Phase khớp với nó. "MVP" = **Phase 1** (Quyết định #39).

---

## 1. Vấn đề & Cơ hội

**Với người dùng:** Khi cần một dịch vụ gần nhà (spa, salon, sửa xe, phòng khám, chăm sóc thú cưng...), người dùng phải tìm rải rác qua Google Maps, Facebook group, hội nhóm địa phương. Thông tin giá cả không rõ ràng, đánh giá không đáng tin, không biết shop đó thực sự đã phục vụ bao nhiêu người.

**Với chủ shop nhỏ:** Muốn có website riêng nhưng không đủ ngân sách thuê thiết kế, không biết kỹ thuật, và tự làm trên các nền tảng có sẵn thì vẫn quá phức tạp. Kết quả là phần lớn shop nhỏ chỉ có một trang Facebook.

**Cơ hội:** Kết hợp hai nhu cầu này vào một nền tảng — người dùng tìm được dịch vụ đáng tin gần mình, còn shop vừa có website riêng vừa được tiếp cận lưu lượng từ nền tảng tổng.

---

## 2. Định vị sản phẩm

vsite là sự kết hợp của ba thứ:

| Thành phần | Tham chiếu |
|---|---|
| Tìm kiếm dịch vụ theo vị trí, hiển thị trên bản đồ | Google Maps |
| Danh bạ dịch vụ có đánh giá từ người dùng thật | Google Business Profile |
| Công cụ tạo website cho shop | Haravan / Wix, nhưng đơn giản hơn nhiều nhờ AI Chat |

**vsite không xử lý giao dịch.** Khách tìm được dịch vụ trên vsite rồi liên hệ và giao dịch **trực tiếp với shop** — vsite không nhận đặt cọc, không bán hàng hộ. vsite là kênh khám phá và dẫn khách, không phải sàn thương mại điện tử.

Đây là lựa chọn có ý thức: phần lớn dịch vụ địa phương (sửa xe, spa, phòng khám) không bán trọn gói online được, và việc chen vào giữa giao dịch tạo ra trách nhiệm vận hành lớn mà không mang thêm giá trị cho cả hai bên.

**Khác biệt cốt lõi:** Chủ shop mô tả bằng lời nói thường (*"tạo website spa màu tím, có phần khách hàng nói gì về tôi"*) và website được dựng ngay trước mắt. Đây là rào cản thấp nhất có thể có cho đối tượng không rành kỹ thuật.

---

## 2.1 Hai luồng doanh thu

vsite có hai sản phẩm bán được **độc lập với nhau**. Đây là điều chi phối toàn bộ kiến trúc, không chỉ là chuyện kinh doanh.

### Luồng 1 — Phí duy trì website

Shop dùng vsite để tạo và vận hành website riêng. Website đó **độc lập hoàn toàn với vsite về mặt nội dung**: khách vào `spa-abc.com` thấy đó là website của tiệm spa, không thấy dấu vết vsite.

Với tên miền dạng đường dẫn (`vsite.vn/spa-abc`) hoặc tên miền phụ (`spa-abc.vsite.vn`) thì địa chỉ vẫn để lộ vsite — đây là đánh đổi của gói rẻ, và cũng là động lực tự nhiên để shop nâng cấp lên tên miền riêng.

### Luồng 2 — Phí đăng tin trên vsite.vn

Shop đăng tin quảng cáo dịch vụ lên trang tìm kiếm chung, để khách trong khu vực tìm thấy. Luồng này **không đòi hỏi shop phải dùng website của vsite** — shop đã có website riêng bên ngoài vẫn đăng tin được, tin sẽ dẫn thẳng về website đó.

Luồng này chỉ có giá trị khi vsite đủ lớn và người dùng thật sự tìm dịch vụ qua vsite. Vì vậy nó **miễn phí ở giai đoạn đầu**, và chỉ bắt đầu thu khi lưu lượng đủ chứng minh giá trị cho shop.

### Hai loại shop

| | Shop `Hosted` | Shop `ExternalOnly` |
|---|---|---|
| Website | Do vsite dựng | Đã có website riêng bên ngoài |
| Mua gì | Luồng 1 (+ Luồng 2 nếu muốn đăng tin) | Chỉ Luồng 2 |
| Cổng quản trị | Đầy đủ (dịch vụ, builder, tin đăng, thống kê, đánh giá) | Rút gọn (tin đăng, thống kê, đánh giá) |
| Trang hồ sơ trên vsite.vn | Có | Có (bản đơn giản) |

`ExternalOnly` là cửa vào rẻ nhất: shop chỉ đăng tin, thấy hiệu quả, rồi nâng cấp lên website mà **không mất** tài khoản, đánh giá hay lịch sử. Đây là phễu cho Luồng 1.

### Nguyên tắc: vsite KHÔNG tự động đăng dịch vụ của shop

Shop có website trên vsite **không** đồng nghĩa dịch vụ của shop tự động xuất hiện trên trang tìm kiếm. Shop phải **chủ động tạo tin đăng**, tự chọn ngành, tự viết mô tả, tự chọn ảnh, tự đặt vị trí và tự chọn trang đích. Dữ liệu **không** map tự động từ website shop sang tin đăng.

Ba lý do:

1. Đây là hai sản phẩm bán riêng — không thể tặng kèm cái này khi bán cái kia.
2. Nội dung hợp với website riêng của shop chưa chắc hợp với trang tìm kiếm chung, nơi tin của shop nằm cạnh tin của đối thủ.
3. Nhiều shop chỉ có một trang quảng cáo đơn giản, không có "danh sách dịch vụ" nào để map.

---

## 3. Đối tượng người dùng

### 3.1 Khách hàng (Customer)
Người tìm dịch vụ trong khu vực mình sống. Ưu tiên: gần, giá rõ ràng, có người dùng thật đánh giá.

### 3.2 Chủ shop (Merchant)

Chủ shop được phân loại theo **hai chiều độc lập nhau**. Một cá nhân kinh doanh vẫn có thể là `Hosted`, và một shop lớn vẫn có thể là `ExternalOnly`.

**Theo quy mô:**

| Nhóm | Đặc điểm | Nhu cầu chính |
|---|---|---|
| Cá nhân kinh doanh | 1 người, 1–3 dịch vụ | Có mặt trên nền tảng, được tìm thấy |
| Shop nhỏ | 1 cơ sở, dưới 20 dịch vụ | Website riêng + quản lý dịch vụ |
| Shop lớn | Nhiều cơ sở, nhiều dịch vụ + hàng hóa | Quản lý phức tạp, phân quyền nhân viên, thương hiệu riêng (custom domain) |

**Theo quan hệ với vsite (phân khúc doanh thu — xem mục 2.1):**

| Loại | Đặc điểm | Doanh thu |
|---|---|---|
| `Hosted` | Chưa có website, cần vsite dựng hộ | Luồng 1 (+ 2) |
| `ExternalOnly` | Đã có website riêng, chỉ cần thêm kênh để được tìm thấy | Luồng 2 |

---

## 4. Hai ứng dụng chính

> Trước đây tài liệu này mô tả **ba** ứng dụng (`customer-web`, `shop-admin`, `website-builder`). Đã đổi thành **hai**, chia theo *chiến lược render* thay vì theo nhóm chức năng — xem Quyết định #22 trong `02-tech-stack-and-decision.md`.

```
┌─────────────────────────────────────────────────────────────┐
│                    BACKEND DÙNG CHUNG                        │
│              (.NET Modular Monolith + PostgreSQL)            │
└──────────────────┬──────────────────────┬───────────────────┘
                   │                      │
              apps/web (SSR)         apps/portal (CSR)
       Tìm kiếm · Hồ sơ shop ·      Quản trị shop +
       Website công khai của shop   Website Builder
```

### 4.1 `apps/web` — Nội dung công khai (SSR, hướng SEO)

Gồm **ba** loại nội dung khác nhau, cần phân biệt rõ vì rất dễ nhầm:

| Nội dung | URL | Ai kiểm soát nội dung | Có đánh giá |
|---|---|---|---|
| Trang chung vsite | `vsite.vn` — tìm kiếm, bản đồ, landing page khu vực | vsite | — |
| **Trang hồ sơ shop** | `vsite.vn/shop/{slug}` | **vsite** (mẫu thống nhất, không phải builder) | ✅ |
| **Website của shop** | `spa-abc.com` · `spa-abc.vsite.vn` · `vsite.vn/{slug}` | **shop** (output của builder) | ❌ |

Hai dòng cuối là hai thứ hoàn toàn khác nhau. Trang hồ sơ là nơi đánh giá tồn tại và là trang duy nhất mà shop `ExternalOnly` có trên vsite. Website shop là sản phẩm shop trả tiền để sở hữu.

### 4.2 `apps/portal` — Cổng quản trị (CSR, sau đăng nhập)

Tại `admin.vsite.vn`. Nơi chủ shop quản lý dịch vụ, hàng hóa, **tin đăng**, đánh giá, thống kê — và dựng website riêng qua route `/website` (kéo-thả trực quan hoặc chat với AI).

Với shop `ExternalOnly`, Portal ở chế độ **rút gọn**: chỉ tin đăng, thống kê lượt liên hệ, phản hồi đánh giá và hồ sơ shop. Không có builder, không có quản lý dịch vụ.

---

## 5. Danh sách tính năng

### 5.1 Tìm kiếm & Khám phá (`apps/web`)

> **Lưu ý chủ thể:** thứ được tìm thấy là **tin đăng (Listing)** do shop chủ động đăng, **không phải** mọi shop có mặt trên nền tảng. Shop không đăng tin thì không xuất hiện trong kết quả tìm kiếm (mục 2.1).

| Tính năng | Mô tả | Phase |
|---|---|---|
| Tìm dịch vụ theo vị trí | Lấy vị trí hiện tại → hiện tin đăng gần nhất trên bản đồ, bán kính tự nới theo mật độ | Phase 1 |
| Chọn ngành dịch vụ | Duyệt theo danh mục chuẩn của vsite: spa, salon, sửa xe, y tế, thú cưng... | Phase 1 |
| Lọc & sắp xếp | Theo khoảng cách, khoảng giá (nếu shop có ghi), điểm đánh giá | Phase 1 |
| Tìm theo từ khóa | Full-text search tiêu đề, mô tả tin đăng, tên shop | Phase 1 |
| Trang hồ sơ shop (`vsite.vn/shop/{slug}`) | Thông tin shop, các tin đăng, vị trí bản đồ, đánh giá, nút dẫn tới website shop | Phase 1 |
| Chỉ số tin cậy | Điểm **theo từng tin đăng** (không gộp chung cả shop — `04` §6.5), số lượt đánh giá, **thời gian đăng tin** (*"Đăng tin từ 03/2026"*), nhãn *"đã liên hệ shop"* trên từng đánh giá | Phase 1 |
| Đánh giá & phản hồi | Khách đã đăng nhập để lại đánh giá (chữ), shop được phản hồi | Phase 1 |
| Lưu yêu thích | Danh sách tin đăng / shop đã lưu | Phase 2 |
| Trang SEO theo khu vực | *"Spa quận 7"*, *"Sửa xe Bình Thạnh"* — landing page theo (ngành × khu vực) | Phase 2 |
| Ảnh trong đánh giá | Cần hạ tầng kiểm duyệt nội dung trước khi bật | Phase 2 |

**Đã bỏ khỏi kế hoạch:**

| Tính năng cũ | Lý do bỏ |
|---|---|
| "Số người đã sử dụng dịch vụ" | vsite không xử lý giao dịch và không xác thực việc đã dùng dịch vụ → **không có nguồn dữ liệu**. Thay bằng số lượt đánh giá + số lượt liên hệ qua vsite |
| "Sắp xếp theo số lượt sử dụng" | Cùng lý do trên |
| "So sánh shop" (2–3 shop cạnh nhau) | Không còn dữ liệu chuẩn hoá để so — tin đăng do shop tự viết, thuộc tính không chuẩn hoá, giá là tuỳ chọn |

### 5.2 Quản trị shop (`apps/portal`)

| Tính năng | Mô tả | Phase | `ExternalOnly` có? |
|---|---|---|---|
| Đăng ký & xác minh shop | Tạo shop, xác minh thông tin, chọn slug | Phase 1 | ✅ |
| **Quản lý tin đăng** | Tạo/sửa/gỡ tin đăng lên vsite: chọn ngành, viết mô tả, ảnh, vị trí, giá (tuỳ chọn), trang đích, thời hạn hiển thị | Phase 1 | ✅ |
| Quản lý hồ sơ shop | Địa chỉ, tọa độ, giờ mở cửa, liên hệ, ảnh đại diện | Phase 1 | ✅ |
| Quản lý đánh giá | Xem và phản hồi đánh giá; báo cáo đánh giá vi phạm (không được tự gỡ) | Phase 1 | ✅ |
| Thống kê cơ bản | Lượt xem tin đăng, lượt bấm gọi, lượt bấm sang website, lượt chỉ đường (nguồn: `Lead` — `04` §7) | Phase 1 | ✅ |
| Quản lý dịch vụ | CRUD dịch vụ: tên, mô tả, giá, thời lượng, ảnh — dùng cho website shop | Phase 2 | ❌ |
| Quản lý hàng hóa | CRUD sản phẩm vật lý (cho shop bán kèm hàng) | Phase 2 | ❌ |
| Đặt lịch / Booking | Khách đặt lịch trực tiếp, shop xác nhận | Phase 3 | ❌ |
| Phân quyền nhân viên | Chủ shop mời nhân viên, giới hạn quyền | Phase 4 | ✅ |
| Nhiều chi nhánh | Một thương hiệu, nhiều địa điểm | Phase 4 | ❌ |
| Thanh toán / Gói dịch vụ | Nâng cấp gói, thanh toán phí nền tảng | Phase 4 | ✅ |

> Cột `ExternalOnly` áp cho **mọi** dòng, kể cả các dòng Phase sau — shop chỉ đăng tin thì không bao giờ có builder, dịch vụ, hàng hoá hay booking.

### 5.3 Website Builder

| Tính năng | Mô tả | Phase |
|---|---|---|
| Chọn template theo ngành | Spa, Salon, Nhà hàng, Phòng khám, Khách sạn, Thú cưng | Phase 2 |
| Kéo-thả component | Thêm/xóa/sắp xếp các khối giao diện (chỉ trang `Composable` — Quyết định #33) | Phase 2 |
| Property Panel | Chỉnh sửa nội dung, ảnh, text của từng component | Phase 2 |
| Theme Engine | Đổi màu, font, bo góc, đổ bóng — áp dụng toàn site | Phase 2 |
| Data Binding | Component tự lấy dữ liệu thật từ shop (danh sách dịch vụ) — **không bao giờ** lấy `Review` của vsite | Phase 2 |
| Preview & Publish | Xem trước rồi mới xuất bản | Phase 2 |
| Undo / Redo | Hoàn tác nhiều bước | Phase 2 |
| Tên miền phụ (`spa-abc.vsite.vn`) | Địa chỉ gọn hơn dạng đường dẫn | Phase 2 |
| **AI Chat Builder** | Tạo và chỉnh sửa website bằng câu lệnh tiếng Việt | Phase 3 |
| Tên miền riêng | Kết nối domain của shop, tự động cấp SSL | Phase 3 |
| Marketplace Template | Kho template mở rộng | Phase 4 |

### 5.4 AI Chat Builder — Chi tiết

Đây là tính năng khác biệt hóa lớn nhất của sản phẩm.

**Cách hoạt động dưới góc nhìn người dùng:**

```
Chủ shop gõ:  "Tạo website cho tiệm spa của tôi, tông màu tím,
               có phần khách hàng nói gì về tôi"
        ↓
AI vừa nói vừa dựng, người dùng thấy website mọc lên từng phần:
   "Đang thêm phần đầu trang..."      → Hero xuất hiện
   "Đang thêm danh sách dịch vụ..."   → ServiceGrid xuất hiện
   "Đang thêm phần đánh giá..."       → Testimonials xuất hiện
        ↓
Hiện 2 nút:  [✓ Giữ thay đổi]   [✗ Hoàn tác]
```

**Chỉnh sửa từng phần:** Chủ shop click chọn một khối bất kỳ trên bản xem trước, rồi gõ *"đổi ảnh nền tối hơn"* — AI biết chính xác đang sửa khối nào, không cần đoán.

**Nguyên tắc trải nghiệm:**
- AI chỉ lắp ghép từ thư viện component có sẵn, không tự bịa giao diện lạ
- Mọi thay đổi đều ở dạng nháp, xem trước rồi mới lưu
- Khi không chắc chắn, AI hỏi lại thay vì đoán bừa
- Một câu lệnh = một bước hoàn tác

### 5.5 Mini-website của shop (đầu ra của Builder)

Website được dựng ra có thể truy cập qua ba dạng địa chỉ:

| Dạng | Ví dụ | Dành cho |
|---|---|---|
| Đường dẫn | `vsite.vn/spa-abc` | Mặc định, đã gồm trong gói website (Luồng 1) |
| Tên miền phụ | `spa-abc.vsite.vn` | Shop muốn địa chỉ gọn hơn |
| Tên miền riêng | `spa-abc.com` | Shop có thương hiệu riêng |

Tính năng trên mini-website: giới thiệu shop, danh sách dịch vụ kèm giá, thư viện ảnh, **lời chứng thực khách hàng (shop tự nhập)**, thông tin liên hệ và bản đồ, form liên hệ / đặt lịch.

⚠️ **Đánh giá từ vsite KHÔNG hiển thị trên website shop.** Đây là ranh giới có chủ đích: website shop là sản phẩm shop trả tiền để sở hữu (Luồng 1), còn đánh giá là tài sản tin cậy của vsite và chỉ sống ở `vsite.vn`. Khối "lời chứng thực" trên website shop là nội dung **shop tự nhập tay**, hoàn toàn khác với `Review` — xem `04-listing-and-review-design.md` §6.4.

---

## 6. Nguyên tắc thiết kế nền tảng

Ba nguyên tắc này chi phối toàn bộ kiến trúc sản phẩm:

### 6.1 Tách biệt Dữ liệu — Giao diện — Trình soạn thảo

```
Business Data        Presentation          Editor
─────────────        ────────────          ──────
Dịch vụ              Component Tree        Kéo-thả
Hàng hóa             Theme                 AI Chat
Đánh giá             Layout                Property Panel
Đặt lịch
```

**Hệ quả thực tế:**
- Shop thêm dịch vụ mới → website tự cập nhật, không cần sửa giao diện
- Shop đổi template → toàn bộ dữ liệu giữ nguyên
- AI chỉ thao tác trên Component Tree, không bao giờ đụng vào dữ liệu kinh doanh

### 6.2 Website lưu dưới dạng cấu trúc, không lưu HTML

Website được lưu dưới dạng cây component (JSON), mỗi node gồm `type`, `variant`, `props`, `children`. Bộ Renderer chuyển cây này thành giao diện thật.

**Lợi ích:** đổi theme áp dụng tức thì toàn site, undo/redo dễ, AI thao tác an toàn, và cùng một bộ Renderer dùng chung cho cả trình soạn thảo lẫn website thật — đảm bảo "thấy sao được vậy".

### 6.3 Kéo-thả và AI Chat là hai cách nhập liệu cho cùng một thứ

Cả hai đều tạo ra cùng một loại thao tác lên cùng một cây component. Không có "chế độ AI" tách biệt — người dùng có thể chat một câu, rồi kéo-thả chỉnh lại, rồi chat tiếp, liền mạch.

---

## 7. Thư viện Component

Website được lắp từ các khối có sẵn, mỗi khối phục vụ một mục đích giao diện cụ thể:

| Nhóm | Component |
|---|---|
| Khung trang | Header, Footer, Menu điều hướng |
| Mở đầu | Hero (banner tĩnh), Hero Slider (banner trượt) |
| Nội dung dịch vụ | Service List, Service Grid, Bảng giá |
| Hình ảnh | Gallery, Slide ảnh, Before/After |
| Tin cậy | Testimonials (lời chứng thực — **shop tự nhập**, không phải đánh giá từ vsite), Chỉ số (con số shop tự khai) |
| Thông tin | Giới thiệu, FAQ, Giờ mở cửa |
| Hành động | Liên hệ, Bản đồ chỉ đường, Đặt lịch (chỉ có từ Phase 3) |

Mỗi component có nhiều biến thể giao diện (`Hero01`, `Hero02`, `Hero03`, `HeroSlider01`...) để website các shop không bị trông giống hệt nhau.

⚠️ **Ràng buộc bắt buộc:** không component nào được phép bind tới nguồn dữ liệu `Review` của vsite. Enforce ở Component Registry manifest (Quyết định #17), không chỉ ghi trong tài liệu — đây là loại nhầm lẫn AI agent sẽ mắc khi thấy hai khái niệm cùng tên "đánh giá".

---

## 8. Lộ trình phát triển

Rủi ro lớn nhất của dự án không nằm ở kỹ thuật mà ở **phạm vi MVP**. Website Builder + AI Chat là phần khác biệt hóa lớn nhất nhưng cũng tốn công nhất. Lộ trình dưới đây ưu tiên kiểm chứng thị trường trước.

### Phase 1 — Danh bạ dịch vụ (kiểm chứng nhu cầu)
Tìm kiếm theo vị trí trên bản đồ, **tin đăng (Listing)**, trang hồ sơ shop, đánh giá + phản hồi, **kiểm duyệt nội dung**, thống kê lượt liên hệ cho shop.
**Mục tiêu:** Chứng minh có người dùng thật tìm dịch vụ qua nền tảng, và shop chịu đăng tin.

> Ở phase này cả hai loại shop đều tham gia được — `ExternalOnly` chỉ cần đăng tin, không cần website. Đây là cách hạ rào cản để có đủ tin đăng kiểm chứng nhu cầu, tránh tình trạng bản đồ trống ở lần dùng đầu tiên.
>
> **Kiểm duyệt là hạng mục vận hành có chi phí người thật**, không phải tính năng làm xong là xong — tin đăng xuất hiện trên landing page SEO của chính vsite, nên nội dung rác làm hỏng uy tín nền tảng chứ không riêng shop.

### Phase 2 — Website Builder (kéo-thả)
Quản lý dịch vụ & hàng hoá, template theo ngành, kéo-thả, theme, publish, tên miền phụ. Kèm: lưu yêu thích, landing page SEO theo khu vực, ảnh trong đánh giá (khi đủ điều kiện ở `04` §6.7).
**Mục tiêu:** Chứng minh chủ shop tự dựng được website mà không cần hỗ trợ.

### Phase 3 — AI Chat + Tên miền riêng
AI Chat Builder, custom domain + SSL tự động, đặt lịch/booking.
**Mục tiêu:** Hạ rào cản xuống mức thấp nhất, mở rộng sang nhóm shop lớn.

### Phase 4 — Mở rộng
Marketplace template, nhiều chi nhánh, phân quyền nhân viên, thanh toán, gói dịch vụ trả phí.

---

## 9. Giá trị mang lại

| Với người dùng | Với chủ shop |
|---|---|
| Tìm dịch vụ gần mình nhanh chóng, xem ngay trên bản đồ | Có website riêng mà không cần biết kỹ thuật (Luồng 1) |
| Biết trước khoảng giá khi shop có ghi | Được nền tảng đưa khách tới, đo được bằng số lượt liên hệ (Luồng 2) |
| Đánh giá từ người dùng đã đăng nhập, shop không được tự gỡ từng cái | Bị đánh giá xấu vẫn có đường làm lại — gỡ tin và bắt đầu lại từ đầu, đổi lại mất hết uy tín đã xây |
| Liên hệ thẳng shop, không qua trung gian | Đã có website riêng vẫn tham gia được, nâng cấp sau khi thấy hiệu quả |
