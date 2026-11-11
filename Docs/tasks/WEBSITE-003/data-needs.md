# WEBSITE-003 — Nhu cầu dữ liệu (preview toàn trang)

> Không có endpoint/field mới: mọi dữ liệu lấy từ API đã có (đối chiếu `packages/api-sdk/src/generated/**`, 2026-11-11).

## Màn: Preview toàn trang (lớp phủ trong editor Portal)

**Toàn trang (một lần mỗi lần mở preview):**
| Dữ liệu | Nguồn | Field dùng |
|---|---|---|
| Ngôn ngữ | `useGetWebsite` | `defaultLocale`, `locales`, `homePageId` |
| Danh sách trang (để dựng URL + tra ngược URL → trang) | `useListPages` | `id`, `kind`, `systemType`, `slug`, `title`, `isEnabled`, `isHomePage` |
| Header/Footer | `useGetShop` | `name`, `logoUrl` (đã có tiền tố `/media/`) |
| Theme | store editor (`themeDraft`, đang chỉnh) | — |

**Mỗi trang hiển thị:**
| Dữ liệu | Nguồn | Ghi chú |
|---|---|---|
| Cây trang đang sửa | store editor (`tree`, kể cả phần chưa autosave) | không gọi API |
| Cây trang khác | `useGetPageDraft(shopId, pageId)` | draft đã lưu trên server |
| Ảnh | `useGetAssetsByIds` (qua `use-media-map`) | như canvas |
| Trang NotFound (link hỏng) | `useGetPageDraft` của trang `systemType = NotFound` | |

**Liên hệ ở Footer (địa chỉ, điện thoại):** Shop chưa có field nào ([Shop.cs:47-66](../../../backend/src/Vsite.Domain/Shop/Entities/Shop.cs#L47-L66)).
Footer nhận props tuỳ chọn, Bước 6 để trống. Thuộc task Shop/Marketplace sau (#93).

## Câu hỏi mở
| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | Preview đọc bản nào của trang đang sửa? | Cây + theme trong store (đúng cái đang thấy); trang khác đọc draft server — người chốt 2026-11-11 |
| Q2 | Link tới trang tắt / đã xoá / systemPage chưa có trang? | Hiện như khách thấy: trang NotFound + dải báo lý do — người chốt 2026-11-11 |
| Q3 | Footer có liên hệ? | Khung sẵn vùng liên hệ, để trống; không chạm BE — người chốt 2026-11-11 |
