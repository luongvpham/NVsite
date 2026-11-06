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
- **#102 (người quyết sau smoke, Q6):** trước production burn tự mở vòng D{n} khi bước thiếu
  field/endpoint — tự quyết tên/kiểu/nullable, promote `burn-auto`, ghi vào "Người duyệt đã quyết". Hàng
  rào còn lại: `REMOVED`, đổi auth policy endpoint đã có, chạm #21, > 2 vòng/task. Sửa: skill (STATUS
  `NEEDS-CONTRACT`, mục "Vòng D{n} tự động", mẫu prompt D{n}), workflow §6.1 + Rule 15 + §6 ghi chú đảo
  lại, `DECISIONS.md` #102, root `CLAUDE.md` contract rule 4 (thêm ngoại lệ).

## Smoke test (2026-11-06, nhánh `feature/BURN-SMOKE`, runner headless)

| Kiểm | Kết quả |
|---|---|
| F1 lane A (`clampPercent` + test) | `PASS` — commit `6722279` + changelog + `review.md`, reviewer 0 Critical |
| F2 cần field không có trong `website.v1` | `BLOCKED` đúng lý do, không viết code, không sửa contract, không mở D{n} |
| Tree sạch sau mỗi bước · `contracts/`, `config/`, generated không đổi · `master` không có commit mới | ✅ |
| Permission denial trong `claude -p --permission-mode auto` | **0** (13 lượt/bước, 2–3 phút/bước) |
| Chạy lại cùng batch | Bỏ qua cả hai bước, 0 lần gọi `claude` |
| Chi phí | $1.45 + $0.89 |

**Sửa sau smoke:** runner ghi giờ máy thay vì UTC; gọi `claude` bằng một chuỗi lệnh (hết cảnh báo Node
DEP0190); tự gỡ `CLAUDECODE`/`CLAUDE_CODE_*` để chạy được cả khi gọi từ trong một session Claude Code.

**Ma sát lộ ra (chưa sửa, theo dõi ở P11):**
- `guard-bash.mjs` chặn nhầm lệnh **chỉ đọc** contract nếu cùng dòng có `>` ở bất kỳ đâu (`2>&1`, email
  trong `Co-Authored-By: <…>`). Burn chưa dính, nhưng một implementer viết lệnh ghép kiểu đó sẽ bị
  `BLOCKED` → skill coi là ma sát. Sửa: chỉ khớp khi đường dẫn bảo vệ đứng **sau** toán tử ghi.
- Agent đặt tên vòng mở lại là `BURN-SMOKE-D1`; workflow §6.1 đánh số D{n} từ 2. Chỉ là chữ trong
  `Cần người quyết`, không tạo file.
- `review.md` do burn tạo chỉ có 2 mục để trống thay vì đủ khung workflow §9 (`Tự động`, …).

## Chưa làm xong

- Xoá nhánh `feature/BURN-SMOKE` sau khi người xem xong (rule deny `git branch -D` chặn agent — đúng ý).
- Ngưỡng dừng theo `rate_limits.seven_day` (status line) — chưa làm, chờ dữ liệu 2 lần burn (plan §7).
- Dọn ~497 rule một-lần trong `.claude/settings.local.json` (`/fewer-permission-prompts`) — việc của
  người, file local không commit.
- `defaultMode: "auto"` ở `~/.claude/settings.json` — việc của người, settings cá nhân.
