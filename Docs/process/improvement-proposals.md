# Improvement Proposals

> **Nguồn:** các `Docs/tasks/*/session-retro.md`, tổng hợp bởi agent `meta-reviewer`.
> **Cách chạy:** skill `meta-review`. Xem `DesignIdeal/ai-agent-development-workflow.md` §9 mục
> "Sau Gate 2 — bước học".
>
> **File này là đề xuất, không phải quyết định.** Không đề xuất nào có hiệu lực cho tới khi người
> duyệt sửa thẳng file quy trình và đánh dấu `Đã promote` ở đây.

---

## Đã promote — theo dõi hiệu quả

Bảng **thường trực**, không xoá theo kỳ. Đây là bước đóng vòng: thiếu nó thì quy trình chỉ biết cộng
thêm rule mà không bao giờ biết bớt.

Mỗi lần chạy `meta-review`, agent phải rà mọi dòng `⏳` đã tới hạn và kết luận: giữ, gỡ, hay chờ tiếp.

| # | Đề xuất | Ngày promote | Sửa ở file nào | Rà lại sau | Kết quả |
|---|---|---|---|---|---|
| — | *(chưa có dòng nào — flow này vừa dựng)* | — | — | — | — |

Ký hiệu cột **Kết quả**: `⏳ chưa tới hạn` · `✅ có tác dụng — giữ` · `❌ không cứu được gì — đã gỡ`

---

## Kỳ gần nhất — *(chưa chạy lần nào)*

Lần chạy `meta-review` đầu tiên sẽ thay toàn bộ phần dưới đây. Giữ nguyên cấu trúc bốn nhóm.

### Tóm tắt nhanh

- Số `session-retro.md` đã đọc: —
- Số đề xuất thô: —
- Số đề xuất đưa lên nhóm ưu tiên cao: —
- Số dòng `⏳` đã rà: —

### Ưu tiên cao — nên làm ngay

| # | Đề xuất | Bằng chứng (Task ID) | Mức | Tần suất | Effort | File sẽ sửa | Đụng quyết định | Trạng thái |
|---|---|---|---|---|---|---|---|---|
| | | | A/B/C | | S/M/L | | không / số hiệu | Chờ duyệt |

### Ưu tiên trung bình

*(trống)*

### Tạm gác — cần thêm dữ liệu

Chỉ dành cho đề xuất **mức C** xuất hiện một lần. Đề xuất **mức A** dù chỉ một lần vẫn phải lên nhóm
ưu tiên cao — một lần suýt để lọt vi phạm tenant isolation không cần lặp lại mới đáng sửa.

*(trống)*

### Bị loại — và lý do

Ghi lại để kỳ sau không đề xuất lại cùng một thứ.

*(trống)*

---

## Trần ngân sách tài liệu

Mỗi rule mới bắt **mọi session sau** trả giá bằng token, mãi mãi. Trần này là cái phanh duy nhất.

| File | Trần | Hiện tại (2026-09-18) | Còn trống |
|---|---|---|---|
| `DesignIdeal/ai-agent-development-workflow.md` | 22 KB | **21.3 KB** | ⚠️ 0.7 KB |
| `CLAUDE.md` (root) | 8 KB | 6.5 KB | 1.5 KB |
| `backend/CLAUDE.md` | 16 KB | 14.5 KB | 1.5 KB |
| `CLAUDE.md` mỗi thư mục khác | 6 KB | ≤ 5 KB | ≥ 1 KB |
| `Docs/templates/session-retro.md` | 7 KB | 6.0 KB | 1 KB |

**Luật:** đề xuất làm vượt trần phải **kèm một đề xuất gỡ bớt tương ứng**, hoặc tự xuống nhóm ưu tiên
thấp. Nâng trần là một quyết định riêng, phải ghi lý do ở đây kèm ngày.

> ⚠️ **Workflow đã gần chạm trần ngay hôm dựng flow này** — chính mục §9 "Sau Gate 2 — bước học" tốn
> ~3 KB. Đề xuất kế tiếp nào chạm `ai-agent-development-workflow.md` **bắt buộc** kèm một đề xuất gỡ
> bớt. Đây không phải sự cố; đây là cái phanh chạy đúng ngay lần đầu. Muốn nâng trần thì ghi lý do ở
> đây kèm ngày, đừng nâng im lặng.

Đo bằng `wc -c`. Con số "hiện tại" cập nhật mỗi kỳ meta-review.
