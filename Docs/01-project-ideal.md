# vsite — Ý Tưởng Dự Án
 
> Nền tảng tìm kiếm dịch vụ địa phương theo vị trí, kèm công cụ giúp chủ shop tự tạo website riêng bằng kéo-thả và AI Chat.
 
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
| Tìm kiếm theo vị trí | Google Maps |
| Marketplace có review & giá minh bạch | Yelp / ShopeeFood |
| Công cụ tạo website cho shop | Haravan / Wix, nhưng đơn giản hơn nhiều nhờ AI Chat |
 
**Khác biệt cốt lõi:** Chủ shop mô tả bằng lời nói thường (*"tạo website spa màu tím, có phần đánh giá khách hàng"*) và website được dựng ngay trước mắt. Đây là rào cản thấp nhất có thể có cho đối tượng không rành kỹ thuật.
 
---
 
## 3. Đối tượng người dùng
 
### 3.1 Khách hàng (Customer)
Người tìm dịch vụ trong khu vực mình sống. Ưu tiên: gần, giá rõ ràng, có người dùng thật đánh giá.
 
### 3.2 Chủ shop (Merchant)
Ba nhóm với nhu cầu khác nhau, nền tảng phải phục vụ được cả ba:
 
| Nhóm | Đặc điểm | Nhu cầu chính |
|---|---|---|
| Cá nhân kinh doanh | 1 người, 1–3 dịch vụ | Có mặt trên nền tảng, được tìm thấy |
| Shop nhỏ | 1 cơ sở, dưới 20 dịch vụ | Website riêng + quản lý dịch vụ |
| Shop lớn | Nhiều cơ sở, nhiều dịch vụ + hàng hóa | Quản lý phức tạp, phân quyền nhân viên, thương hiệu riêng (custom domain) |
 
---
 
## 4. Ba ứng dụng chính
 
```
┌─────────────────────────────────────────────────────────────┐
│                    BACKEND DÙNG CHUNG                        │
│              (.NET Modular Monolith + PostgreSQL)            │
└───────┬─────────────────┬─────────────────────┬─────────────┘
        │                 │                     │
   customer-web      shop-admin          website-builder
   Tìm kiếm/SEO      Quản lý shop        Kéo-thả + AI Chat
```
 
### 4.1 customer-web — Tìm kiếm & khám phá
Ứng dụng hướng SEO, là cửa ngõ thu hút lưu lượng cho toàn nền tảng.
 
### 4.2 shop-admin — Cổng quản trị của shop
Nơi chủ shop quản lý dịch vụ, hàng hóa, đơn đặt, đánh giá, và xem số liệu.
 
### 4.3 website-builder — Công cụ tạo website
Nơi chủ shop dựng mini-website của riêng mình. Hai cách sử dụng song song: kéo-thả trực quan, hoặc chat với AI.
 
---
 
## 5. Danh sách tính năng
 
### 5.1 Tìm kiếm & Khám phá (customer-web)
 
| Tính năng | Mô tả | Phase |
|---|---|---|
| Tìm dịch vụ theo vị trí | Nhập/lấy vị trí hiện tại → hiện shop gần nhất theo bán kính | MVP |
| Lọc & sắp xếp | Theo khoảng cách, giá, điểm đánh giá, số lượt sử dụng | MVP |
| Tìm theo từ khóa | Full-text search tên dịch vụ, tên shop, mô tả | MVP |
| Trang chi tiết shop | Danh sách dịch vụ, giá, ảnh, giờ mở cửa, bản đồ | MVP |
| Chỉ số tin cậy | Số người đã sử dụng dịch vụ, điểm trung bình, số lượt đánh giá | MVP |
| Đánh giá & phản hồi | Khách để lại review, shop được phản hồi lại | MVP |
| Danh mục dịch vụ | Duyệt theo ngành: spa, salon, sửa chữa, y tế, thú cưng... | MVP |
| So sánh shop | Đặt cạnh nhau 2–3 shop để so giá và đánh giá | Phase 2 |
| Lưu yêu thích | Danh sách shop đã lưu | Phase 2 |
| Trang SEO theo khu vực | *"Spa quận 7"*, *"Sửa xe Bình Thạnh"* — landing page tự sinh | Phase 2 |
 
### 5.2 Quản trị shop (shop-admin)
 
| Tính năng | Mô tả | Phase |
|---|---|---|
| Đăng ký & xác minh shop | Tạo shop, xác minh thông tin, chọn slug | MVP |
| Quản lý dịch vụ | CRUD dịch vụ: tên, mô tả, giá, thời lượng, ảnh | MVP |
| Quản lý hồ sơ shop | Địa chỉ, tọa độ, giờ mở cửa, liên hệ, ảnh đại diện | MVP |
| Quản lý hàng hóa | CRUD sản phẩm vật lý (cho shop bán kèm hàng) | MVP |
| Quản lý đánh giá | Xem và phản hồi review của khách | MVP |
| Thống kê cơ bản | Lượt xem, lượt liên hệ, dịch vụ được quan tâm nhất | MVP |
| Đặt lịch / Booking | Khách đặt lịch trực tiếp, shop xác nhận | Phase 2 |
| Phân quyền nhân viên | Chủ shop mời nhân viên, giới hạn quyền | Phase 2 |
| Nhiều chi nhánh | Một thương hiệu, nhiều địa điểm | Phase 3 |
| Thanh toán / Gói dịch vụ | Nâng cấp gói, thanh toán phí nền tảng | Phase 3 |
 
### 5.3 Website Builder
 
| Tính năng | Mô tả | Phase |
|---|---|---|
| Chọn template theo ngành | Spa, Salon, Nhà hàng, Phòng khám, Khách sạn, Thú cưng | MVP |
| Kéo-thả component | Thêm/xóa/sắp xếp các khối giao diện | MVP |
| Property Panel | Chỉnh sửa nội dung, ảnh, text của từng component | MVP |
| Theme Engine | Đổi màu, font, bo góc, đổ bóng — áp dụng toàn site | MVP |
| Data Binding | Component tự lấy dữ liệu thật từ shop (danh sách dịch vụ, đánh giá) | MVP |
| Preview & Publish | Xem trước rồi mới xuất bản | MVP |
| Undo / Redo | Hoàn tác nhiều bước | MVP |
| **AI Chat Builder** | Tạo và chỉnh sửa website bằng câu lệnh tiếng Việt | Phase 2 |
| Tên miền riêng | Kết nối domain của shop, tự động cấp SSL | Phase 2 |
| Marketplace Template | Kho template mở rộng | Phase 3 |
 
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
| Đường dẫn | `vsite.vn/spa-abc` | Mặc định, miễn phí cho mọi shop |
| Tên miền phụ | `spa-abc.vsite.vn` | Shop muốn địa chỉ gọn hơn |
| Tên miền riêng | `spa-abc.com` | Shop có thương hiệu riêng |
 
Tính năng trên mini-website: giới thiệu shop, danh sách dịch vụ kèm giá, thư viện ảnh, đánh giá khách hàng, thông tin liên hệ và bản đồ, form liên hệ / đặt lịch.
 
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
| Tin cậy | Testimonials (đánh giá khách), Chỉ số (số khách đã phục vụ) |
| Thông tin | Giới thiệu, FAQ, Giờ mở cửa |
| Hành động | Liên hệ, Đặt lịch, Bản đồ chỉ đường |
 
Mỗi component có nhiều biến thể giao diện (`Hero01`, `Hero02`, `Hero03`, `HeroSlider01`...) để website các shop không bị trông giống hệt nhau.
 
---
 
## 8. Lộ trình phát triển
 
Rủi ro lớn nhất của dự án không nằm ở kỹ thuật mà ở **phạm vi MVP**. Website Builder + AI Chat là phần khác biệt hóa lớn nhất nhưng cũng tốn công nhất. Lộ trình dưới đây ưu tiên kiểm chứng thị trường trước.
 
### Phase 1 — Marketplace (kiểm chứng nhu cầu)
Tìm kiếm theo vị trí, hồ sơ shop, quản lý dịch vụ, đánh giá.
**Mục tiêu:** Chứng minh có người dùng thật tìm dịch vụ qua nền tảng, và shop chịu lên nền tảng.
 
### Phase 2 — Website Builder (kéo-thả)
Template theo ngành, kéo-thả, theme, publish, tên miền phụ.
**Mục tiêu:** Chứng minh chủ shop tự dựng được website mà không cần hỗ trợ.
 
### Phase 3 — AI Chat + Tên miền riêng
AI Chat Builder, custom domain + SSL tự động, booking.
**Mục tiêu:** Hạ rào cản xuống mức thấp nhất, mở rộng sang nhóm shop lớn.
 
### Phase 4 — Mở rộng
Marketplace template, nhiều chi nhánh, thanh toán, gói dịch vụ trả phí.
 
---
 
## 9. Giá trị mang lại
 
| Với người dùng | Với chủ shop |
|---|---|
| Tìm dịch vụ gần mình nhanh chóng | Có website riêng mà không cần biết kỹ thuật |
| Giá cả minh bạch, so sánh được | Quản lý dịch vụ tập trung một nơi |
| Tin tưởng dựa trên số liệu thật | Được nền tảng đưa khách tới |
| Đánh giá từ người dùng thật | Nâng cấp lên thương hiệu riêng khi đủ lớn |