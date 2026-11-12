# TOOLING-003 — changelog

Vá hook chặn ghi contract cho tool PowerShell + bớt chặn nhầm lệnh chỉ đọc. Lane A: chỉ tooling —
không chạm contract, không đổi runtime của app. Nhánh tách từ `master`. Nguồn: review workflow
2026-11-12 và mục "Ma sát lộ ra" của [`TOOLING-002`](../TOOLING-002/changelog.md).

## Đã làm

1. **`.claude/settings.json`** — matcher hook `Bash` → `Bash|PowerShell`. Trước đó `Set-Content
   contracts/contract.lock` qua tool PowerShell (shell chính của máy) không bị chặn.
2. **`.claude/hooks/guard-bash.mjs`** — nhận thêm lệnh ghi của PowerShell (`Set-Content`, `Add-Content`,
   `Out-File`, `Tee-Object`, `Copy/Move/Rename/New-Item` + alias, `[IO.File]::Write*`) và `sed -i`;
   so khớp không phân biệt hoa thường, đường dẫn `\` được chuẩn hoá. Toán tử `>`/`>>` chỉ chặn khi
   **đích ghi** là đường dẫn bảo vệ → `2>/dev/null`, `2>&1`, `git diff … > /tmp/x` hết bị chặn nhầm
   (ma sát đã ghi ở TOOLING-002). Export `shouldBlock()` để test.
3. **`.claude/hooks/guard-bash.test.mjs`** — 25 ca (14 chặn, 10 cho qua, 1 chạy hook thật qua stdin):
   `node --test .claude/hooks/guard-bash.test.mjs`.

## Giới hạn đã biết

- Hook không phân biệt nguồn/đích của `cp`/`Copy-Item`: chép **từ** đường dẫn bảo vệ ra ngoài vẫn bị
  chặn (dùng tool Read thay vào đó).
- Không bắt được ghi qua `node -e`, `python -c`. Hook là lưới thô; lớp chặn thật là `contract.lock`
  + drift check trong CI.

## Đã kiểm

- `node --test` 25/25 pass.
- Thử trực tiếp trong session: lệnh PowerShell chứa `Set-Content contracts/contract.lock` bị `BLOCKED`.
