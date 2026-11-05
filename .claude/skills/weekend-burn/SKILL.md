---
name: weekend-burn
description: Use when the user wants to run a queue of already-planned task steps unattended — "weekend burn", "chạy đêm", "chạy hết credit", "chạy batch" — or when tools/burn/run.mjs invokes it with a batch file and --one.
---

# weekend-burn

Chạy tuần tự các bước **đã có plan** mà không người trông. Burn chỉ tăng tốc *thực thi*: không thêm
quyết định nào mà bình thường người phải quyết. Nguồn gốc + lý do: `Docs/process/weekend-burn-plan.md`.

**Một file duy nhất giữ trạng thái:** batch file `Docs/process/burn/{YYYY-MM-DD}.md` (định dạng:
`Docs/process/burn/README.md`). Mọi thứ bạn biết về batch nằm ở đó, không nằm trong chat (Rule 8).
File này **gitignored** — nó phải sống sót qua các lần `git checkout` giữa nhánh task. Không `git add` nó;
thứ cần lưu vào git là `review.md` + `changelog.md` trên nhánh của từng task.

## Hai chế độ

| Gọi | Ai lặp | Bạn làm |
|---|---|---|
| `/weekend-burn <batch>` | bạn (session tương tác) | Pre-flight → lặp "Một bước" tới hết hàng đợi hoặc gặp `HALT` |
| `/weekend-burn <batch> --one` | `tools/burn/run.mjs` (`claude -p`) | Đúng **một** bước — dòng chưa tick đầu tiên không thuộc task đã `BLOCKED` — rồi kết thúc. Không pre-flight, không hỏi gì, không sửa banner hay `## Tóm tắt` (runner giữ hai chỗ đó) |

## Pre-flight (chỉ chế độ tương tác, trước bước đầu tiên)

1. `git status --porcelain` rỗng. Mỗi `feature/{ID}` trong hàng đợi tồn tại. Không ai ở `master`.
2. Mỗi task: có `Docs/tasks/{ID}/plan.md`, **hoặc** có mục Acceptance trong batch file.
3. `docker info` → ghi `Docker: có/không` vào banner batch file.
4. Hỏi người **một lần**: "Session này khởi động bằng lệnh trong `Docs/process/burn/README.md` chưa
   (`--permission-mode auto`, chặn `git push`)?" — người còn ngồi đó lúc này, sau đó thì không. Chưa có
   câu trả lời "rồi" → không bắt đầu.
5. Banner batch file → `RUNNING`.

Fail mục 1–2 → dừng, báo, không chạy bước nào. Batch đã `RUNNING` (lần trước bị cắt) → chạy tiếp từ
`## Log`; bước dở dang không có dòng Log thì chạy lại từ đầu bước đó.

## Một bước

1. **Chọn** dòng `- [ ] {ID} {Fn}` đầu tiên của `## Hàng đợi` mà `{ID}` chưa có dòng `BLOCKED` trong `## Log`.
   Có `HALT` trong Log → dừng hẳn.
2. `git checkout feature/{ID}` · ghi `BASE=$(git rev-parse HEAD)`.
3. **Spawn implementer** — `general-purpose`, **foreground** (Rule 14), prompt theo mẫu ở dưới.
4. **Kiểm, đừng tin lời** (Rule 14): `git status --porcelain` rỗng — bẩn → `HALT`. Implementer báo
   `PASS*` mà `git log BASE..HEAD` không có commit → `BLOCKED`. Implementer báo `BLOCKED` → bỏ qua bước 5–6.
5. **Spawn `change-reviewer`**, foreground: "Review `git diff BASE..HEAD` của {ID} bước {Fn}. Plan:
   `Docs/tasks/{ID}/plan.md` mục {Fn}."
6. Có **Critical** → spawn implementer **một lần** nữa với nguyên văn Critical → review lại. Vẫn còn →
   `BLOCKED`; riêng Critical về tenant (#21) vẫn còn → `HALT`.
7. **Ghi file — thứ tự này; commit riêng `review.md` với message `docs({ID}): burn {Fn} — {STATUS}`:**
   - `Docs/tasks/{ID}/review.md`: thêm mục `## Burn {ngày trong tên batch file} — {Fn}` = Critical/Warning của reviewer
     (nguyên văn, rút gọn) + `STATUS`. Chưa có file → tạo theo mẫu workflow §9, để trống các mục người điền.
   - Batch file: tick dòng hàng đợi của bước này (**mọi** STATUS — tick = "đã có dòng Log"; các bước sau
     của task `BLOCKED` để nguyên) · thêm 1 dòng `## Log` · thêm vào `Giả định tôi đã tự đặt` /
     `Cần người quyết` / `Ma sát — nháp từ burn` những gì implementer + reviewer báo.

## STATUS

| STATUS | Khi nào | Tiếp theo |
|---|---|---|
| `PASS` | DoD đủ, không còn Critical | Bước kế |
| `PASS-WITH-DEBT` | Có Warning, hoặc test Docker đã ghi `Docs/DOCKER-TEST-DEBT.md` | Bước kế |
| `BLOCKED` | Implementer báo `BLOCKED` · Critical sau một lần sửa · Gate 1 có câu hỏi/`REMOVED` | Bỏ các bước còn lại **của task này**, sang task kế |
| `HALT` | Tree bẩn sau bước · hook `BLOCKED` mà agent tìm đường vòng · tenant Critical không sửa được · build nhánh hỏng | Dừng cả batch, banner → `HALTED` |

Hết hàng đợi → banner `DONE`, viết `## Tóm tắt` (bảng: task · số bước PASS/DEBT/BLOCKED · link `review.md`).

## Mẫu prompt implementer

```
Bạn làm đúng một bước: {ID} {Fn}, nhánh feature/{ID} (đã checkout). Không ai trả lời câu hỏi
cho tới khi bước kết thúc — làm theo mục "Khi không chắc" thay vì hỏi.

Đọc: Docs/tasks/{ID}/plan.md mục {Fn} · brief.md nếu có · CLAUDE.md của thư mục bạn chạm.
DoD: BE → backend/CLAUDE.md "Definition of done"; FE → DesignIdeal/ai-agent-development-workflow.md §8.
Gate 1 — chỉ khi plan.md mục {Fn} GHI RÕ là bước BE đổi contract: skill contract-sync. Chỉ thêm +
  không câu hỏi → promote với approvedBy = burn-auto. Có câu hỏi hoặc REMOVED → STATUS BLOCKED.
  Bước FE (hoặc bước BE mà plan không nói đổi contract) phát hiện contract thiếu field → BLOCKED,
  kể cả khi phần BE chỉ vài dòng: đó là vòng D{n}, cần người.
Docker không có → viết test đủ, ghi Docs/DOCKER-TEST-DEBT.md theo quy ước trong file.
changelog: thêm mục "## {Fn}" vào Docs/tasks/{ID}/changelog.md. Tick checkbox của {Fn} trong plan.md.
Commit code + changelog trên feature/{ID}. Để working tree sạch.
BLOCKED giữa chừng: không commit phần dở; `git stash push -u -m "burn {ID} {Fn} BLOCKED"`, ghi tên stash
vào "Câu hỏi cho người".

Khi không chắc:
- Hình dạng API, tên field, nullable, ranh giới module, tenant, contract thiếu/sai → STATUS BLOCKED,
  ghi câu hỏi. Không viết tạm, không sửa contract, không mở vòng D{n}.
- Mọi thứ khác → tự quyết, ghi vào "Giả định".

Trả về đúng khung này, ≤ 15 dòng:
STATUS: PASS | PASS-WITH-DEBT | BLOCKED
Commit: <sha ngắn, cách nhau dấu phẩy>
Test: <lệnh> → <x/y>
Giả định: <mỗi dòng một ý, hoặc "không">
Câu hỏi cho người: <hoặc "không">
Ma sát: <prompt quyền bị chặn, việc làm lại, tài liệu sai — hoặc "không">
```

## Ranh giới của burn

Burn không push, không merge, không chạy Gate 2, không sửa `DECISIONS.md` (quyết định mới → `Cần người
quyết`), không mở D{n}, không tạo nhánh hay Task ID, không sửa `.claude/**`. Hook `BLOCKED` là đúng —
ghi vào Ma sát rồi `BLOCKED`, không thử lối khác.
