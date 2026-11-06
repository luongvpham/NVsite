---
name: weekend-burn
description: Use when the user wants to run a queue of already-planned task steps unattended — "weekend burn", "chạy đêm", "chạy hết credit", "chạy batch" — or when tools/burn/run.mjs invokes it with a batch file and --one.
---

# weekend-burn

Chạy tuần tự các bước **đã có plan** mà không người trông. Trước production, burn **tự duyệt contract**
(#102): bước thiếu field/endpoint thì tự mở vòng D{n}, tự promote, ghi lại mọi quyết định đã tự đặt.
Ngoài contract, burn không thêm quyết định nào mà bình thường người phải quyết. Lý do:
`Docs/process/weekend-burn-plan.md`.

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
3. **Spawn implementer** — `general-purpose`, **foreground** (Rule 14), prompt theo "Mẫu prompt implementer".
4. **Kiểm, đừng tin lời** (Rule 14): `git status --porcelain` rỗng — bẩn → `HALT`. Implementer báo
   `PASS*` mà `git log BASE..HEAD` không có commit → `BLOCKED`. Báo `BLOCKED` → sang bước 7.
   Báo `NEEDS-CONTRACT` → chạy **"Vòng D{n} tự động"** rồi quay lại bước 3 (implementer mới, kèm tên
   stash nếu có).
5. **Spawn `change-reviewer`**, foreground: "Review `git diff BASE..HEAD` của {ID} bước {Fn}. Plan:
   `Docs/tasks/{ID}/plan.md` mục {Fn}."
6. Có **Critical** → spawn implementer **một lần** nữa với nguyên văn Critical → review lại. Vẫn còn →
   `BLOCKED`; riêng Critical về tenant (#21) vẫn còn → `HALT`.
7. **Ghi file — thứ tự này; commit riêng `review.md` với message `docs({ID}): burn {Fn} — {STATUS}`:**
   - `Docs/tasks/{ID}/review.md`: thêm mục `## Burn {ngày trong tên batch file} — {Fn}` = Critical/Warning
     của reviewer (nguyên văn, rút gọn) + `STATUS` + vòng D{n} đã mở trong bước này (nếu có). Chưa có file
     → tạo đủ khung workflow §9 (`Tự động` · `change-reviewer` · `Nợ kỹ thuật` · `Điểm cần anh quyết` ·
     `Ma sát quy trình`), để trống các mục người điền.
   - Batch file: tick dòng hàng đợi của bước này (**mọi** STATUS — tick = "đã có dòng Log"; các bước sau
     của task `BLOCKED` để nguyên) · thêm 1 dòng `## Log` cho `{Fn}` (và 1 dòng `{Fn}-D{n}` cho mỗi vòng D)
     · thêm vào `Giả định tôi đã tự đặt` / `Cần người quyết` / `Ma sát — nháp từ burn` những gì
     implementer + reviewer báo. Mọi quyết định contract `burn-auto` → `Giả định tôi đã tự đặt`.

## Vòng D{n} tự động (#102 — chỉ trước production)

Implementer trả `NEEDS-CONTRACT` = bước cần field/endpoint mà contract chưa có.

1. `n` = số mục `## D…` đã có trong `Docs/tasks/{ID}/changelog.md` + 2 (D đánh số từ **2**, workflow §6.1).
   `## Log` của batch file đã có **2 dòng `…-D…`** cho task này → `BLOCKED` (Bước 0 đang hụt — để người xem).
2. Spawn implementer theo **"Mẫu prompt D{n}"**, foreground. Kiểm git như bước 4 của "Một bước".
3. Spawn `change-reviewer` trên diff của vòng D: "Review vòng {ID}-D{n}. Soi: tenant #21, auth policy,
   khớp contract ↔ handler, nullable."
4. Critical → như bước 6. `BLOCKED`/`HALT` của vòng D là STATUS của cả bước `{Fn}`.

## STATUS

| STATUS | Khi nào | Tiếp theo |
|---|---|---|
| `PASS` | DoD đủ, không còn Critical | Bước kế |
| `PASS-WITH-DEBT` | Có Warning, hoặc test Docker đã ghi `Docs/DOCKER-TEST-DEBT.md` | Bước kế |
| `BLOCKED` | Implementer báo `BLOCKED` · Critical sau một lần sửa · vòng D chạm hàng rào (mẫu D{n}) · quá 2 vòng D | Bỏ các bước còn lại **của task này**, sang task kế |
| `HALT` | Tree bẩn sau bước · hook `BLOCKED` mà agent tìm đường vòng · tenant Critical không sửa được · build nhánh hỏng | Dừng cả batch, banner → `HALTED` |

Hết hàng đợi → banner `DONE`, viết `## Tóm tắt` (bảng: task · số bước PASS/DEBT/BLOCKED · link `review.md`).

## Mẫu prompt implementer

```
Bạn làm đúng một bước: {ID} {Fn}, nhánh feature/{ID} (đã checkout). Không ai trả lời câu hỏi
cho tới khi bước kết thúc — làm theo mục "Khi không chắc" thay vì hỏi.
{nếu có: Trước khi làm: git stash pop "<tên stash>" — phần dở từ lần thử trước.}

Đọc: Docs/tasks/{ID}/plan.md mục {Fn} · brief.md nếu có · CLAUDE.md của thư mục bạn chạm.
DoD: BE → backend/CLAUDE.md "Definition of done"; FE → DesignIdeal/ai-agent-development-workflow.md §8.
Contract — trước production burn tự duyệt (#102):
  - Bước BE mà plan nói đổi contract: skill contract-sync rồi promote với approvedBy = burn-auto, kể cả
    khi có câu hỏi — tự quyết, ghi từng quyết định vào bảng "Người duyệt đã quyết" của contract-diff
    (`burn-auto (#102)`) và vào "Giả định". Gặp hàng rào ở "Mẫu prompt D{n}" → BLOCKED.
  - Bước cần field/endpoint mà contract chưa có → dừng ngay, STATUS NEEDS-CONTRACT, mô tả cần gì.
    Không tự sửa backend trong bước này, không viết type tạm.
Docker không có → viết test đủ, ghi Docs/DOCKER-TEST-DEBT.md theo quy ước trong file.
changelog: thêm mục "## {Fn}" vào Docs/tasks/{ID}/changelog.md. Tick checkbox của {Fn} trong plan.md.
Commit code + changelog trên feature/{ID}. Để working tree sạch.
Dừng giữa chừng (BLOCKED / NEEDS-CONTRACT): không commit phần dở;
  `git stash push -u -m "burn {ID} {Fn}"`, ghi tên stash vào khung trả về.

Khi không chắc:
- Ranh giới module, tenant (#21) → STATUS BLOCKED, ghi câu hỏi.
- Mọi thứ khác → tự quyết, ghi vào "Giả định".

Trả về đúng khung này, ≤ 15 dòng:
STATUS: PASS | PASS-WITH-DEBT | NEEDS-CONTRACT | BLOCKED
Commit: <sha ngắn, cách nhau dấu phẩy>
Test: <lệnh> → <x/y>
Contract cần: <module, endpoint, field + kiểu + nullable đề xuất — hoặc "không">
Stash: <tên — hoặc "không">
Giả định: <mỗi dòng một ý, hoặc "không">
Câu hỏi cho người: <hoặc "không">
Ma sát: <prompt quyền bị chặn, việc làm lại, tài liệu sai — hoặc "không">
```

## Mẫu prompt D{n}

```
Bạn làm vòng mở lại contract {ID}-D{n} cho bước {Fn}, nhánh feature/{ID} (đã checkout).
Cần: {nguyên văn "Contract cần" của implementer}. Trước production burn tự duyệt contract (#102) —
không ai trả lời câu hỏi; tự quyết và ghi lại.

Đọc: backend/CLAUDE.md · DesignIdeal/ai-agent-development-workflow.md §6.1 · Docs/tasks/{ID}/brief.md nếu có.
1. Sửa backend tối thiểu cho đúng nhu cầu trên; test theo DoD BE (Docker không có → DOCKER-TEST-DEBT).
2. Skill contract-sync. contract-diff ghi vào Docs/tasks/{ID}/contract-diff-D{n}.md; bảng
   "Người duyệt đã quyết": mỗi quyết định bạn tự đặt (tên, kiểu, nullable, …) một dòng `burn-auto (#102)`.
3. node tools/contract-sync/promote.mjs {ID}-D{n} burn-auto <module>.v<n>.json → pnpm gen:api.
4. changelog: mục "## D{n}". brief.md: cập nhật "Việc FE cần làm" nếu đổi.
5. Commit. Working tree sạch.

Hàng rào — gặp một trong các điều sau thì KHÔNG promote, git stash, STATUS BLOCKED:
- contract-diff có REMOVED;
- đổi auth policy / role của endpoint đã có;
- phải nhận ShopId từ body hoặc nới bất kỳ invariant #21 nào;
- dự án đã deploy production (Rule 5).

Trả về khung như implementer, thêm dòng: Contract: <file> sha256 <8 ký tự đầu>
```

## Ranh giới của burn

Burn không push, không merge, không chạy Gate 2, không tạo nhánh hay Task ID, không sửa `.claude/**`.
Quyết định ngoài contract mà cần số `#N` mới → `Cần người quyết`, không tự sửa `DECISIONS.md`. Hook
`BLOCKED` là đúng — ghi vào Ma sát rồi `BLOCKED`, không thử lối khác.
