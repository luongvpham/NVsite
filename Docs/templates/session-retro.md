# Session Retro — {TASK_ID}

> **Bản mẫu.** Chép sang `Docs/tasks/{TASK_ID}/session-retro.md` rồi điền, đừng sửa file này.
> **Viết SAU Gate 2**, không phải trước — lý do ở cuối file.

> **Session type:** `BE` | `FE` | `Full-stack (lane A/B)` | `Contract-sync` | `Integration`
> **Lane:** A / B / C
> **Date:** YYYY-MM-DD
> **Agent / model:**
> **Gate 2 kết luận:** merge thẳng / phải sửa rồi merge / bị trả về

---

## 0. Điều kiện kích hoạt — đọc trước, thường dừng ở đây

Retro đầy đủ chỉ viết khi có **≥ 1** trong:

- [ ] Lane B hoặc C
- [ ] Phải hỏi lại người giữa chừng
- [ ] Gate 1 hoặc Gate 2 trả về / bắt sửa
- [ ] Phải làm lại việc chính mình đã viết xong
- [ ] `change-reviewer` báo Critical

**Không thoả điều kiện nào** → xoá hết mục 1–6, để lại đúng một dòng rồi đóng file:

> Lane A · không có ma sát · không đề xuất gì.

Đây là kết quả hợp lệ và là kết quả **đúng** với phần lớn session lane A. Retro cho mỗi lần sửa CSS
là thuế, không phải học.

---

## 1. Bằng chứng đã đọc

Retro viết từ **artifact**, không viết từ trí nhớ hội thoại — trí nhớ cuối session là thứ kém tin nhất
trong cả quy trình.

- [ ] `Docs/tasks/{ID}/review.md` — `change-reviewer` bắt gì, Gate 2 quyết gì
- [ ] `Docs/tasks/{ID}/contract-diff.md` mục "Người duyệt đã quyết" — chỗ agent đoán sai đã lộ ra ở đây
- [ ] `Docs/tasks/{ID}/changelog.md` — mục "chưa làm xong"
- [ ] `git diff` của task
- [ ] 3 `session-retro.md` gần nhất **cùng loại session** — chỉ để khỏi viết trùng (xem §4)
  - `Docs/tasks/{ID-1}/session-retro.md`
  - `Docs/tasks/{ID-2}/session-retro.md`
  - `Docs/tasks/{ID-3}/session-retro.md`

## 2. Số đếm quan sát được

**Không ước lượng thời gian.** Agent không có đồng hồ; con số đó luôn là bịa và luôn bị meta-review
dùng sai.

| Chỉ số | Số | Chỗ đắt nhất |
|---|---|---|
| Lần phải hỏi lại người | | |
| Lần phải sửa lại code chính mình vừa viết | | |
| File phải mở ngoài dự kiến | | |
| Critical từ `change-reviewer` | | |
| Lần Gate trả về | | |

"File phải mở ngoài dự kiến" cao nghĩa là `DesignIdeal/00-INDEX.md` hoặc một `CLAUDE.md` đang chỉ sai
chỗ. Đó là tín hiệu rẻ nhất mà retro thu được — đừng bỏ trống dòng này.

## 3. Ma sát — gốc rễ, không phải triệu chứng

| Ma sát | Nguyên nhân gốc | Mức | Bằng chứng (`file:dòng` hoặc commit) |
|---|---|---|---|
| | | A/B/C | |

**Mức** — dùng để xếp ưu tiên ở meta-review. Tần suất một mình là trục sai: một lần suýt để lọt vi
phạm tenant isolation nặng hơn năm lần "brief.md hơi dài".

- **A** — suýt hoặc đã để lọt vi phạm invariant (#21, #1), contract sai hình dạng, merge nhầm
- **B** — phải làm lại việc đã xong
- **C** — chỉ khó chịu, không mất gì

## 4. Đề xuất

Thứ tự các ô dưới là **cố ý**: bớt trước, thêm sau.

- [ ] **Không đề xuất gì** — session này không sinh bài học nào
- [ ] **Xoá / thu gọn** một rule đang gây nhiễu, hoặc chưa cứu được gì lần nào
- [ ] Thêm / sửa rule trong `CLAUDE.md` (ghi rõ file nào)
- [ ] Sửa skill (tên skill + thay đổi cụ thể)
- [ ] Thêm / sửa checklist Gate 1 hoặc Gate 2
- [ ] Thêm / sửa hook
- [ ] Cải tiến template (`brief.md` / `data-needs.md` / chính file này)
- [ ] Khác

| # | Đề xuất | Mới / `+1` | Mức | Tự tin | Đụng quyết định nào |
|---|---|---|---|---|---|
| 1 | | Mới | A/B/C | 1–5 | không / số hiệu |

**Đề xuất đã có trong một retro cũ ở §1** → ghi `+1` kèm task ID, **đừng viết lại nội dung**. Việc
cộng dồn là của meta-review; viết lại chỉ làm nó đếm sai thành hai đề xuất khác nhau.

> ⚠️ **Form có ô trống không phải lý do để bịa một đề xuất.** Một flow đẻ ra đề xuất mỗi lần chạy sẽ
> làm phình tài liệu nhanh hơn là cải thiện quy trình — và tài liệu phình đúng là thứ `00-INDEX.md`
> sinh ra để chống. Mỗi rule mới bắt mọi session sau trả giá bằng token, mãi mãi.

## 5. Tự tin (1–5) — nói về **bằng chứng**, không về số lần lặp

`1` cảm giác · `3` trích được `file:dòng` cụ thể · `5` có artifact chứng minh (test đỏ, diff phải làm
lại, Critical của reviewer)

Việc **đếm lặp lại** thuộc về meta-review. Đừng tự hạ tự tin xuống chỉ vì "mới thấy một lần" — một
session đơn gần như không bao giờ tái hiện được lỗi hai lần, thang đo mà tính theo đó thì mọi đề xuất
đều rơi vào sọt chờ.

## 6. Ghi chú cho meta-review

(Điều quan trọng mà các bảng trên không bắt được. Không có thì ghi "Không có".)

---

## Vì sao viết SAU Gate 2, ở session riêng

Bằng chứng đáng học nhất đến **muộn**: `change-reviewer` bắt được gì, người duyệt bác gì ở Gate 2.
Retro viết trước Gate 2 bỏ lỡ đúng cái loại lỗi mà session **tự nó không nhìn thấy được**.

Và vì cùng một lý do `ai-agent-development-workflow.md` §9 nêu cho code review — *cùng một mạch suy
nghĩ đã sinh ra lỗi sẽ đọc lướt qua lỗi đó* — retro nên chạy ở **session riêng, ngắn, context sạch**,
không dán vào đuôi session vừa code xong, lúc context đã bẩn và cạn nhất.

**Quy tắc điền:** mục không có nội dung thì ghi "Không có". §4 đã có ô "Không đề xuất gì" chính là để
bạn khỏi phải bịa.
