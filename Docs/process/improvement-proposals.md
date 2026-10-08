# Improvement Proposals

> **Nguồn:** mục "Ma sát quy trình" cuối các `Docs/tasks/*/review.md`, tổng hợp bởi agent `meta-reviewer`.
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
| P1 | **Gate 1 gọn trước production (#89):** bỏ phân tích nhãn `BREAKING`; chỉ thêm + không câu hỏi → promote ngay. Bằng chứng: MEDIA-001 có 4 lần Gate 1, mỗi lần chờ người 4.7–6.2 giờ, phần diff không bắt được lỗi nào | 2026-10-08 | workflow §5 §6 §10 (Rule 3, 5) · skill `contract-sync` · `CLAUDE.md` · `backend/CLAUDE.md` DoD 8 | Sau 2 task lane B/C — đo: số lần dừng Gate 1, lỗi contract lọt sang FE | ⏳ chưa tới hạn |
| P2 | **Checklist Gate 1:** chốt mọi "Câu hỏi mở" của `data-needs.md` + chạy thử Orval trên staging. Bằng chứng: MEDIA-001 phải mở lại contract 3 lần (D2 `MediaAssetDto2`, D3/D4 logo) vì hai việc này | 2026-10-08 | workflow §6 Checklist · skill `contract-sync` bước 4 | Sau 2 task lane B/C — đo: số lần promote lại contract trong cùng task | ⏳ chưa tới hạn |
| P3 | **Lane C cần Docker** trước session BE. Bằng chứng: MEDIA-001 phải viết hai bộ test; 13 test fail chỉ lộ khi Docker mới có | 2026-10-08 | workflow §4 | Task lane C kế tiếp | ⏳ chưa tới hạn |
| P4 | **Smoke chạy app thật sau task FE đầu tiên.** Bằng chứng: hai lỗi FE của MEDIA-001 (proxy `apps/web`, không có đường tới `/dev-registry`) chỉ lộ khi soạn hướng dẫn nghiệm thu | 2026-10-08 | workflow §8 | Task có FE kế tiếp — đo: lỗi chỉ lộ ở nghiệm thu tay | ⏳ chưa tới hạn |
| P5 | **Subagent chạy lệnh foreground; review task theo rủi ro.** Bằng chứng: D1 treo 4 lần, D2 treo tới lúc được đánh thức | 2026-10-08 | workflow Rule 14 · `CLAUDE.md` §Quy trình | Sau 2 task dùng subagent — đo: số lần agent treo | ⏳ chưa tới hạn |
| P6 | **Cho phép trước các lệnh an toàn** (`pnpm` script, `node tools/*`, `git` chỉ đọc, `dotnet ef migrations list`). Bằng chứng: ~57 giờ lệnh nằm chờ duyệt quyền qua đêm (6 lần) trong MEDIA-001 | 2026-10-08 | `.claude/settings.json` | Sau 2 task — đo: khoảng chờ > 1 giờ giữa lúc phát lệnh và lúc có kết quả | ⏳ chưa tới hạn — sửa lại 2026-10-08 (P10) vì bản đầu không có tác dụng trên máy dùng PowerShell |
| P7 | **Gỡ `session-retro`** (skill + template ~9 KB) → mục "Ma sát quy trình" cuối `review.md`. Bằng chứng: 0 retro sau 8 task; P1–P6 đều rút trực tiếp, không qua retro | 2026-10-08 | workflow §2 §9 · `meta-review` · `meta-reviewer` · `00-INDEX` §5 | Kỳ `meta-review` đầu tiên — đo: số `review.md` có mục ma sát điền thật | ⏳ chưa tới hạn |
| P8 | **Vòng mở lại contract `-D{n}`** (workflow §6.1) + chốt Task ID/nhánh (§14 mục 5, 7). Bằng chứng: MEDIA-001 mở lại 3 lần (D2–D4), mỗi lần một folder riêng, workflow không mô tả đường quay lại | 2026-10-08 | workflow §2 §6.1 §10 Rule 1 §14 · `00-INDEX` §5 | Task lane C kế tiếp — đo: số vòng D{n}, artifact có nằm trong folder task cha không | ⏳ chưa tới hạn |
| P9 | **Lane C: Bước 0 + `plan.md` bắt buộc.** Bằng chứng: IDENTITY-001, SHOP-001 không có `data-needs.md`; MEDIA-001/SHOP-001 thực tế đã chạy theo `plan.md` mà workflow không nhắc | 2026-10-08 | workflow §1 §3 §4 | Task lane C kế tiếp | ⏳ chưa tới hạn |
| P10 | **Quyền lệnh cho tool PowerShell** + sửa pattern `pnpm gen:*` (`:*` = ` *`, không khớp `gen:api`) + bỏ tự cho phép `git push`. Bằng chứng: `Bash(...)` không áp dụng cho tool PowerShell (docs permissions §PowerShell) | 2026-10-08 | `.claude/settings.json` | Sau 2 task — đo như P6 | ⏳ chưa tới hạn |

Ký hiệu cột **Kết quả**: `⏳ chưa tới hạn` · `✅ có tác dụng — giữ` · `❌ không cứu được gì — đã gỡ`

---

## Kỳ gần nhất — *(chưa chạy lần nào)*

Lần chạy `meta-review` đầu tiên sẽ thay toàn bộ phần dưới đây. Giữ nguyên cấu trúc bốn nhóm.

### Tóm tắt nhanh

- Số task đã đọc: —
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

| File | Trần | Hiện tại (2026-10-08) | Còn trống |
|---|---|---|---|
| `DesignIdeal/ai-agent-development-workflow.md` | 22 KB | 18.1 KB | 3.9 KB |
| `CLAUDE.md` (root) | 8 KB | 7.5 KB | 0.5 KB |
| `backend/CLAUDE.md` | 16 KB | 12.1 KB | 3.9 KB |
| `CLAUDE.md` mỗi thư mục khác | 6 KB | ≤ 4.5 KB | ≥ 1.5 KB |

**Luật:** đề xuất làm vượt trần phải **kèm một đề xuất gỡ bớt tương ứng**, hoặc tự xuống nhóm ưu tiên
thấp. Nâng trần là một quyết định riêng, phải ghi lý do ở đây kèm ngày.

> 2026-10-08: workflow từ 21.3 xuống 18.1 KB nhờ bỏ phần tự chép (§10 Rules, #89 lặp 5 chỗ, §12, §14
> mục đã xong) dù thêm §6.1. Muốn nâng trần thì ghi lý do ở đây kèm ngày, đừng nâng im lặng.

Đo bằng `wc -c`. Con số "hiện tại" cập nhật mỗi kỳ meta-review.
