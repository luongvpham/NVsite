# TOOLING-002 — changelog

Burn không người trông + giảm permission prompt. Lane A: chỉ tooling, quy trình, settings — không chạm
contract, không đổi runtime của app. Plan + review bản nháp gốc:
[`Docs/process/weekend-burn-plan.md`](../../process/weekend-burn-plan.md). Theo dõi hiệu quả: P11 ở
[`improvement-proposals.md`](../../process/improvement-proposals.md). Nhánh tách từ `master`.

---

## Đã làm

1. **Skill `weekend-burn`** — `.claude/skills/weekend-burn/SKILL.md`. Hai chế độ: tương tác (session tự
   lặp) và `--one` (runner gọi). Mỗi bước: implementer (`general-purpose`, foreground) →
   kiểm bằng git → `change-reviewer` → tối đa một vòng sửa Critical → ghi `review.md` + batch file.
   STATUS `PASS` / `PASS-WITH-DEBT` / `BLOCKED` (bỏ task) / `HALT` (dừng batch).
2. **Runner** — `tools/burn/run.mjs`. Mỗi bước một `claude -p --permission-mode auto --max-budget-usd N
   --disallowedTools "Bash(git push *)" "PowerShell(git push *)"`. Kiểm sau mỗi bước: tree sạch (không
   → `HALT`), dòng hàng đợi đã tick (không → `ABORTED` + dừng, chạy lại để tiếp). Có `--dry-run`, file
   dừng `.superpowers/burn/STOP`, log JSON từng bước ở `.superpowers/burn/{batch}/`, tự viết `## Tóm tắt`.
3. **Định dạng batch** — `Docs/process/burn/README.md`; batch file gitignored (`.gitignore`).
4. **`.claude/settings.json` `permissions.deny`**: `.env*`, `appsettings.Production*`, `git reset --hard`,
   `git clean`, `git branch -D`, `docker system prune`, `docker volume rm` (Bash + PowerShell).
   Thêm 22 pattern `allow` chỉ-đọc, rút từ 33 transcript gần nhất (`/fewer-permission-prompts`):
   `pnpm lint`, `pnpm tsc --noEmit`, `pnpm vitest run`, `docker info/version`, `dotnet list`,
   `git check-ignore`, `where` (Bash + PowerShell) và cmdlet chỉ-đọc `Get-Content`, `Select-String`,
   `findstr`, `Get-ChildItem`, `ConvertFrom-Json`, `Get-Process`, `Get-NetTCPConnection`.
5. **Workflow** — Rule 15 + 1 dòng §13 (18.1 → 18.9 KB, trần 22 KB).
6. **`improvement-proposals.md`** — dòng P11.

## Lệch có chủ đích (so với bản nháp gốc)

- Không tạo `weekend-supervisor` / `task-agent` / `implementation-subagent` / `reviewer-subagent`. Dùng
  main session (hoặc `claude -p`) làm supervisor + `change-reviewer` có sẵn. Lý do: plan §2 R1–R4.
- Không có batch branch, không có Task ID batch (R6). Không có `weekend-burn-implementation-notes.md` (R16).
- `git push` **không** bị deny ở session thường (người quyết Q3). Burn chặn bằng `--disallowedTools`.
- Gate 1 trong burn tự promote theo #89 với `approvedBy = burn-auto` (Q1, Q2).

## Chưa làm xong

- **Smoke test thật** (plan §7 "Smoke test") — chưa chạy. Cần commit TOOLING-002 trước, vì runner đòi
  tree sạch và skill phải có mặt trên nhánh smoke. Theo dõi: P11.
- Ngưỡng dừng theo `rate_limits.seven_day` (status line) — chưa làm, chờ dữ liệu 2 lần burn (plan §7).
- Dọn ~497 rule một-lần trong `.claude/settings.local.json` (`/fewer-permission-prompts`) — việc của
  người, file local không commit.
- `defaultMode: "auto"` ở `~/.claude/settings.json` — việc của người, settings cá nhân.
