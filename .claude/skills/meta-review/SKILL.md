---
name: meta-review
description: Tổng hợp các session-retro thành Docs/process/improvement-proposals.md cho người duyệt, và rà lại hiệu quả các đề xuất đã promote. Chạy session riêng, sau mỗi 3–5 task hoặc 1–2 tuần.
---

# meta-review

Chạy ở **session riêng**, không lẫn với task feature.

## Không được làm

- KHÔNG sửa `DesignIdeal/ai-agent-development-workflow.md`, `CLAUDE.md` bất kỳ, skill, hook, agent,
  hay `DECISIONS.md`. Session này **chỉ được ghi đúng một file**:
  `Docs/process/improvement-proposals.md`.
- KHÔNG promote bất cứ đề xuất nào. Promote là hành động riêng, sau khi người duyệt — cùng luật với
  contract ở Gate 1.
- KHÔNG tự phân tích thay cho agent. Việc đọc và tổng hợp thuộc về `meta-reviewer`, vốn không có
  quyền ghi. Bạn đọc các retro ở session này nghĩa là mất đúng lớp cách ly đó.

## Các bước

1. Spawn agent `meta-reviewer` (read-only). Nêu rõ trong prompt: phạm vi retro cần đọc, và ngày hôm nay.
2. Nhận báo cáo. **Không sửa nội dung phân tích** — chỉ kiểm hình thức: mọi đề xuất có task ID thật
   không, có mức A/B/C không, có trỏ file sẽ phải sửa không. Thiếu thì hỏi lại agent, đừng tự điền.
3. Ghi vào `Docs/process/improvement-proposals.md`:
   - Giữ nguyên bảng **"Đã promote — theo dõi hiệu quả"**, cập nhật cột `Kết quả` theo kết luận rà lại.
   - Thay phần đề xuất bằng bản mới, có ngày.
   - Đề xuất kỳ trước chưa được quyết → giữ lại, đừng đánh rơi.
4. Dừng. Báo người: số retro đã đọc, số đề xuất mới, số dòng `⏳` đã tới hạn rà và kết luận của chúng.

## Sau khi người duyệt

Việc này **không thuộc session meta-review** — mở session khác:

1. Người đọc `Docs/process/improvement-proposals.md`, quyết cái nào nhận.
2. Nhận → sửa thẳng `ai-agent-development-workflow.md` / `CLAUDE.md` / skill / hook, và cấp số ở
   `DesignIdeal/DECISIONS.md` nếu đó là một quyết định thật.
3. Đánh dấu trạng thái trong bảng đề xuất: `Đã promote` / `Từ chối`.
4. **Thêm một dòng vào bảng "Đã promote — theo dõi hiệu quả"**, có ngày và mốc rà lại. Bỏ bước này
   thì rule mới không bao giờ bị hỏi lại, và quy trình chỉ còn biết phình.
