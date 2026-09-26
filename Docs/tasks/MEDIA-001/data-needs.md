# MEDIA-001 — Nhu cầu dữ liệu (Bước 0)

> Không phải contract. Liệt kê màn hình Portal cần gì, để session BE thiết kế API không thiếu.
> Nguồn: `DesignIdeal/08-media-asset-design.md` §3, §9.

## Màn: Dialog upload ảnh vào slot (apps/portal, builder)
Input: file, preset của slot (lấy từ manifest), focal point (x, y trong 0..1, chọn trên ảnh xem trước
ở client), checkbox "Lưu vào thư viện", alt text (không bắt buộc).
Kết quả cần: id + `storageKey` + `preset` + width/height của record **đặt vào tree**; nếu có tick
thì thêm id bản Library (để picker cập nhật ngay, không cần gọi lại list).

## Màn: Picker Media Library (apps/portal)
Mỗi item: id, `storageKey` (hiện ảnh), width/height, `originalFileName`, alt text, focal point mặc
định, `createdAt`.
Toàn trang: phân trang theo shape chung `{ items, total, page, pageSize }`, mới nhất trước.
Hành động: upload thẳng vào thư viện (không gắn slot), chọn ảnh cho slot → clone theo preset slot
(chỉnh focal point được), xoá.
Trước khi xoá: danh sách nơi đang tham chiếu ảnh đó để cảnh báo (không chặn).
Hiển thị dung lượng đã dùng (quota, #55) — chỉ con số, chưa có giới hạn theo gói.

## Màn: Sửa shop → logo (apps/portal)
Upload logo, thấy logo hiện tại. Cần `LogoId` và `storageKey` của phái sinh `320x96,inside` để hiện.

## Render trong builder (resolveImage)
Sau khi tải lại trang, builder có danh sách `imageId` trong tree → cần tra **theo lô** ra
`{ id, storageKey, preset }` để điền `mediaMap`.

## Câu hỏi mở
- Logo hiện tại lấy từ `GET /shops/{shopId}` (thêm field vào `ShopDto`) hay endpoint riêng của Media?
  → Đề xuất: `ShopDto` thêm `logoId` (Shop sở hữu cột), còn `storageKey` tra qua endpoint tra theo lô
  của Media. Chốt ở Gate 1.
