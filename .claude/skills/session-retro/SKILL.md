---
name: session-retro
description: Viết Docs/tasks/{ID}/session-retro.md sau Gate 2 — ghi lại ma sát quy trình của task vừa xong. Chạy ở session riêng, context sạch, không dán vào đuôi session vừa code.
---

# session-retro

Bước **học** của quy trình, không phải bước phụ. Đây là nguồn dữ liệu duy nhất cho agent
`meta-reviewer`, và cho câu hỏi còn treo ở `DesignIdeal/ai-agent-development-workflow.md` §14 mục 6
(ngưỡng phân lane A/B/C — "chạy 3–4 task rồi rút ra").

## Không được làm

- KHÔNG sửa bất kỳ file workflow / `CLAUDE.md` / skill / hook / agent nào. Ghi **đúng một file**:
  `Docs/tasks/{ID}/session-retro.md`.
- KHÔNG viết từ trí nhớ hội thoại. Đọc artifact trước (bước 3).
- KHÔNG bịa đề xuất để lấp chỗ trống trong form. "Không đề xuất gì" là kết quả hợp lệ.
- KHÔNG ước lượng thời gian — bạn không có đồng hồ.

## Khi nào chạy

**Sau Gate 2.** Bằng chứng đáng học nhất — `change-reviewer` bắt gì, người duyệt bác gì — chỉ tồn tại
sau Gate 2. Retro viết trước đó bỏ lỡ đúng loại lỗi mà session tự nó không thấy được.

**Ở session riêng.** Cuối session vừa code là lúc context bẩn và cạn nhất, lại đúng là cùng mạch suy
nghĩ đã sinh ra lỗi — workflow §9 nói cùng điều đó về code review.

## Các bước

1. Đọc template `Docs/templates/session-retro.md`.
2. Chép sang `Docs/tasks/{TASK-ID}/session-retro.md`. Thay `{TASK_ID}` bằng ID thật.
3. Kiểm **điều kiện kích hoạt** ở §0 của template.
   - Không thoả điều kiện nào → viết bản một dòng, dừng ở đó, nhảy tới bước 7. Đây là đường ra hợp lệ
     và là kết quả đúng với phần lớn session lane A.
   - Thoả → đi tiếp.
4. Đọc bằng chứng theo **đúng thứ tự** §1 của template: `review.md` → `contract-diff.md` mục "Người
   duyệt đã quyết" → `changelog.md` → `git diff` của task → 3 `session-retro.md` gần nhất cùng loại
   session. Liệt kê rõ đã đọc file nào — không đọc được thì ghi lý do, đừng im lặng bỏ qua.
5. Điền §2–§6. Mỗi đề xuất bắt buộc có: **mức A/B/C**, **tự tin 1–5**, và **bằng chứng trỏ được tới
   `file:dòng` hoặc commit**.
6. Đề xuất đã xuất hiện trong một retro cũ ở bước 4 → ghi `+1` kèm task ID, **không viết lại nội
   dung**. Meta-review đếm lặp lại; viết lại làm nó đếm thành hai đề xuất khác nhau.
7. Tự kiểm trước khi đóng:
   - Đề xuất nào chung chung → sửa thành hành động cụ thể, hoặc xoá.
   - Đề xuất nào không trỏ được bằng chứng → xoá.
   - Toàn bộ đề xuất đều là "thêm một rule nữa"? Rule mới bắt **mọi session sau** trả giá bằng token,
     mãi mãi. Cân nhắc ô "xoá / thu gọn" trước khi chốt.
8. Báo lại một dòng: đã viết bản đầy đủ hay bản một dòng, số đề xuất **Mới** và số `+1`.
