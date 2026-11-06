# Burn batch — định dạng + cách chạy

Skill: [`.claude/skills/weekend-burn/SKILL.md`](../../../.claude/skills/weekend-burn/SKILL.md) ·
Lý do: [`../weekend-burn-plan.md`](../weekend-burn-plan.md). File batch ở thư mục này **gitignored**.

## Chạy

Cần: working tree sạch, mỗi task có nhánh `feature/{ID}`, task đã qua Bước 0.

```powershell
# A — tương tác, máy để mở (prompt sót vẫn hiện để duyệt khi quay lại)
claude --permission-mode auto --disallowedTools "Bash(git push *)" "PowerShell(git push *)"
#   rồi gõ:  /weekend-burn Docs/process/burn/2026-11-08.md

# B — runner headless, mỗi bước một `claude -p` mới (context sạch, crash không giết cả batch)
node tools/burn/run.mjs Docs/process/burn/2026-11-08.md --budget-per-step 15 --max-steps 30
node tools/burn/run.mjs Docs/process/burn/2026-11-08.md --dry-run   # chỉ in lệnh, không chạy
```

Dừng runner an toàn (sau bước đang chạy): tạo file `.superpowers/burn/STOP`. Chạy lại cùng lệnh để tiếp
tục — bước đã tick được bỏ qua. Log JSON của từng bước: `.superpowers/burn/{batch}/`.

## Mẫu file batch

```markdown
# Burn 2026-11-08

> **STATUS:** `QUEUED` · **Docker:** — · **Bắt đầu:** —

## Hàng đợi
- [ ] WEBSITE-002 F3
- [ ] WEBSITE-002 F4
- [ ] SHOP-003 ALL

## Acceptance
### SHOP-003
(chỉ cho task không có plan.md — lane A/B; `ALL` = cả task là một bước)

## Log
| Thời điểm | Task | Bước | STATUS | Commit | Ghi chú |
|---|---|---|---|---|---|

## Giả định tôi đã tự đặt
## Cần người quyết
## Ma sát — nháp từ burn
## Tóm tắt
```

Thứ tự trong hàng đợi = thứ tự chạy. Các bước của một task phải liền nhau, đúng thứ tự trong `plan.md`.
STATUS banner: `QUEUED` → `RUNNING` → `DONE` | `HALTED` | `STOPPED` (runner: file STOP, `--max-steps`,
hoặc một bước `ABORTED` — session chết giữa chừng vì rate-limit/budget; chạy lại để làm tiếp bước đó).

## Thứ Hai

Đọc `## Tóm tắt` + `## Cần người quyết` → Gate 2 từng task như thường lệ (đọc `Docs/tasks/{ID}/review.md`)
→ chép những dòng ma sát đáng giữ vào mục "Ma sát quy trình" chính thức của `review.md`.
