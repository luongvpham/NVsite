# Docker test debt

> Nợ test cần Docker daemon (Testcontainers, hoặc `docker compose up` cho Postgres/Redis thật) mà
> session hiện tại KHÔNG chạy được — sandbox không có Docker. Máy có Docker đọc file này, chạy lại,
> rồi XOÁ đúng mục đã pass. Đây là nguồn sự thật DUY NHẤT cho loại nợ này — đừng ghi rải rác ở
> CLAUDE.md từng module hay báo miệng qua chat.

**Quy ước:**
- Thêm mục MỚI ở cuối danh sách "Pending" bên dưới, đừng sửa mục người/task khác đang chờ.
- Mỗi mục bắt buộc có: lệnh chạy chính xác (copy-paste được), lý do cần Docker, ngày thêm, task
  liên quan (nếu có), và tiêu chí "coi là xong".
- Chạy **thật** (không chỉ build) và **pass thật** → xoá nguyên mục khỏi file này. Lịch sử đã nằm ở
  git log của code/test liên quan, không cần giữ lại ở đây.
- Nếu FAIL → đừng xoá. Hoặc sửa bug rồi chạy lại tới khi pass, hoặc ghi rõ lỗi + để mục đó lại cho
  người tiếp theo (kèm ngày cập nhật gần nhất).
- File rỗng (chỉ còn "Pending" trống) là trạng thái mong muốn — không phải lỗi.

---

## Pending

_(rỗng)_
