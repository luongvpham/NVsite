# Plan: Weekend-burn mode & giảm permission prompt

> **STATUS:** `ĐÃ APPLY — Giai đoạn 1 + 2, chờ smoke thật` · **Ngày:** 2026-11-06 · **Task:** TOOLING-002 ([changelog](../tasks/TOOLING-002/changelog.md)) · **Theo dõi:** P11 ở [`improvement-proposals.md`](improvement-proposals.md)
> **Loại:** đề xuất quy trình (Rule 13) — người đã duyệt §6 ngày 2026-11-06.
> **Khảo sát:** `.claude/` (settings, hooks, 2 agent, 3 skill), workflow §1–§14, `improvement-proposals.md`
> (P1–P10, trần ngân sách), plan.md của WEBSITE-002 (nhánh `feature/WEBSITE-002`), `.superpowers/sdd/`, Claude Code 2.1.202.
> Khả năng của Claude Code được xác minh với docs chính thức ngày 2026-11-06 (link ở §8).

---

## 1. Kết luận review — một đoạn

Bản nháp đúng về mục tiêu và ràng buộc. Sai ở **ba chỗ** (khiến nó không chạy được như đã viết) và
**thừa ở một chỗ** (hai agent mới mà repo đã có thứ làm được việc đó):

1. **Chỉ `permissionMode` thì không chặn được reviewer ghi file.** Session cha ở `auto`/`acceptEdits`/`bypassPermissions` thì `permissionMode` trong
   frontmatter của subagent **bị bỏ qua**. Reviewer phải bị chặn bằng **danh sách `tools`**. `change-reviewer` đã làm đúng như vậy.
2. **`defaultMode: "auto"` trong `.claude/settings.json` bị bỏ qua.** `auto` và `bypassPermissions` chỉ có tác dụng ở user/managed settings hoặc
   cờ `--permission-mode`. Chỗ đặt mode là lệnh khởi động, không phải file của project.
3. **"Prompt sót của subagent surface lên main session"** chỉ hữu ích khi có người ngồi đó. Burn chạy **không người
   trông** → mỗi prompt sót là một lần **treo tới sáng**. Đúng ma sát mà P6 đã đo: khoảng 57 giờ trong MEDIA-001.
   Vì vậy phải dựa vào `auto` + deny-list, cộng với điểm dừng rõ ràng.
4. **Đơn vị burn đúng là bước `Fn` trong `plan.md`, không phải Task ID.** Lane C đã chạy kiểu "mỗi bước một subagent + review"
   qua `superpowers:subagent-driven-development` (`.superpowers/sdd/plan-WEBSITE-00{1,2}`). Burn nên là **lớp vỏ
   không-người-trông** quanh cơ chế đó. Không nên xây một hệ 4 agent song song với nó.

---

## 2. Review chi tiết — bản nháp vs thực tế project

| # | Bản nháp nói | Thực tế | Sửa thành |
|---|---|---|---|
| R1 | Hierarchy 3 tầng: Supervisor → Task Agent → Impl/Reviewer | Lồng được (mặc định tối đa 3 tầng). Nhưng mỗi tầng là một lần bàn giao làm rơi chi tiết, còn Task Agent chỉ chuyển tiếp | **2 tầng:** skill chạy ở main session làm supervisor, spawn implementer + `change-reviewer` cho từng bước |
| R2 | Reviewer `permissionMode: plan` | Bị bỏ qua khi cha ở `auto` (docs sub-agents) | Dùng lại **`change-reviewer`** (tools: Read, Grep, Glob, Bash; không có Write/Edit/Agent) |
| R3 | Tạo `reviewer-subagent.md` mới | `change-reviewer` đã có checklist tenant/contract/docs và là nguồn duy nhất (workflow §9) | **Không tạo.** Agent chép lại checklist sẽ rot trước bản gốc |
| R4 | `implementation-subagent.md` có `permissionMode` | Cũng bị bỏ qua dưới `auto` | Không cần file agent. Prompt implementer do skill sinh, gồm DoD + lane + "không hỏi được thì làm gì" |
| R5 | `defaultMode: auto` cho session thường | Đặt ở project settings thì vô hiệu | Đặt ở `~/.claude/settings.json` (cá nhân) hoặc khởi động bằng `claude --permission-mode auto` |
| R6 | Batch branch `feature/BATCH-YYYY-WNN` | Workflow §2: **mỗi task một nhánh `feature/{ID}` → một PR** | Burn chạy trên **nhánh của task**. Batch chỉ là danh sách ở `Docs/process/burn/{YYYY-MM-DD}.md`, không tạo nhánh. Không tạo Task ID mới |
| R7 | Gate 1 "vẫn phải tồn tại", không rõ burn xử lý ra sao | #89: chỉ thêm + không câu hỏi → **promote luôn, không chờ người** | Burn **được** promote theo #89 với `approvedBy = burn-auto-#89`. Có câu hỏi hoặc `REMOVED` → bước đó `BLOCKED`, dừng task đó |
| R8 | Vòng D{n} "tự động" | Rule 9: FE thiếu field → dừng, mở D{n} có người | Burn **không tự mở D{n}**. FE thiếu field → `BLOCKED` |
| R9 | Reviewer điền "Ma sát quy trình" | §9: điền **sau khi người quyết ở Gate 2** — bằng chứng giá trị nhất chỉ có lúc đó | Burn ghi mục **"Ma sát — nháp từ burn"** (prompt treo, bước BLOCKED, giả định tự đặt). Người chốt mục thật ở Gate 2 |
| R10 | "Hỏi, đừng đoán" không được nhắc | Root `CLAUDE.md`: hình dạng API, field, nullable, module, tenant → **phải hỏi** | Không người để hỏi → nhóm câu hỏi đó = `BLOCKED`. Mọi thứ khác → ghi "Giả định tôi đã tự đặt" |
| R11 | Không nhắc Docker | P3: lane C BE cần Docker. `DOCKER-TEST-DEBT.md` có quy ước riêng | Pre-flight chạy `docker info`. Không có Docker → bỏ bước BE cần Testcontainers và ghi nợ theo quy ước |
| R12 | Deny `.env*`, ask `git push`/`commit`… | `git commit` **đã allow** (cần cho mỗi bước). `git push` đã bỏ allow (P10). **Chưa có `deny` nào.** `settings.local.json` có ~497 rule một-lần | Thêm `deny` cứng (§5.2). `git push` **không** deny ở session thường (Q3) — burn chặn bằng `--disallowedTools` lúc khởi động. Burn không cần `ask`: dưới không-người-trông, `ask` = treo |
| R13 | Credit: "nếu tool hỗ trợ" | Không có API hỏi quota. Status line JSON có `rate_limits.seven_day.used_percentage` + `resets_at` (Pro/Max) | Mặc định: dừng khi **hết danh sách** hoặc bị **rate-limit cắt**. An toàn nhờ commit sau mỗi bước + file trạng thái để chạy tiếp (§4.4). Ngưỡng % theo status line là tuỳ chọn, làm sau |
| R14 | Song song task lane A độc lập? | Một working tree + `dotnet build`/`pnpm` dùng chung → xung đột. Rule 1 | **Tuần tự tuyệt đối.** Không dùng worktree mỗi task (mỗi lần `pnpm install` + restore .NET trên Windows quá đắt) |
| R15 | Section mới trong workflow (sau §10/§14) | Workflow 18.1/22 KB. Nguyên tắc "trỏ, đừng chép" | Workflow chỉ thêm **1 dòng ở §13 + 1 Rule 15** (~0.4 KB). Chi tiết nằm trong `SKILL.md`. Root `CLAUDE.md` còn 0.5 KB → **không đụng** |
| R16 | `weekend-burn-implementation-notes.md` riêng | Thêm một file tài liệu cần giữ đồng bộ | Ghi quyết định cuối vào dòng **P11** ở `improvement-proposals.md` (có mốc rà lại). File plan này đổi STATUS thành `ĐÃ APPLY` |
| R17 | Không nói version | Máy đang chạy **2.1.202**: đã có `--permission-mode auto`, `--max-budget-usd`, `--disallowedTools` (kiểm `claude --help`). `--permission-prompts none` cần ≥ 2.1.259 — runner không dùng | Không bắt buộc `claude update` |
| R18 | (không có) | Batch file nằm trong working tree mà supervisor `git checkout` giữa nhánh task + đòi tree sạch → track thì mất khi đổi nhánh, không track thì tree luôn "bẩn" | `Docs/process/burn/*` **gitignored** (trừ README). Kết quả bền vững = `review.md` + `changelog.md` commit trên nhánh từng task |
| R19 | (baseline RED, 2026-11-06) | Agent không có skill giữ kỷ luật tốt (không đi vòng hook, ghi nợ Docker) nhưng **sai hình dạng**: tự đặt `D1.md`, tự đặt `burn-report.md` trong folder task, supervisor tự sửa Critical | Skill viết dạng **công thức + mẫu** (khung trả về của implementer, vị trí file cố định), không phải danh sách cấm dài |

---

## 3. Thiết kế cuối

```
🧑 Người (thứ Sáu)
   · chọn task đã có plan.md với các bước Fn còn [ ], hoặc task lane A/B có acceptance rõ
   · viết danh sách → Docs/process/burn/{YYYY-MM-DD}.md
   · claude --permission-mode auto   →   /weekend-burn Docs/process/burn/{YYYY-MM-DD}.md
        │
        ▼
MAIN SESSION = supervisor (skill weekend-burn)
   pre-flight (§4.1) → với từng task trong danh sách, tuần tự:
       git checkout feature/{ID}  (phải sạch, phải tồn tại)
       với từng bước Fn chưa tick của plan.md:
           ├─ spawn IMPLEMENTER (general-purpose, foreground, context sạch)
           │     làm Fn theo plan + DoD → test → commit "{ID} Fn: …" → trả STATUS
           ├─ spawn change-reviewer (read-only, foreground) trên diff của Fn
           ├─ Critical? → 1 lần implementer sửa → review lại → vẫn Critical: BLOCKED
           └─ ghi state + tick Fn + dòng vào burn log   ← điểm chạy tiếp nếu bị cắt
       hết bước → ghi changelog/review.md nháp của task (KHÔNG Gate 2, KHÔNG push, KHÔNG merge)
   → report tổng ở Docs/process/burn/{YYYY-MM-DD}.md
        │
        ▼
🧑 Người (thứ Hai) · đọc report → Gate 2 từng task như thường lệ → chốt "Ma sát quy trình"
```

**Vì sao main session làm supervisor mà không là một agent riêng:** prompt sót và lỗi được surface ở đây,
và context ở đây chỉ chứa báo cáo ngắn của từng bước. Main session vẫn phình qua nhiều task, nên mỗi bước
phải trả **≤ 15 dòng** (mẫu ở `SKILL.md`). Cơ chế auto-compact lo phần còn lại, và mọi trạng thái nằm
trong file (Rule 8).

**Vì sao không dùng thẳng `subagent-driven-development`:** skill đó giả định có người trả lời giữa chừng
và không biết DoD của repo (changelog, Docker debt, #89, Rule 9). `weekend-burn` dùng lại **mô hình**
của nó (mỗi bước một subagent + review), nhưng tự định nghĩa luật dừng và luật ghi file.

---

## 4. Luật vận hành (đưa nguyên vào `SKILL.md`)

### 4.1 Pre-flight — fail bất kỳ mục nào thì không chạy

- Working tree sạch. Mỗi `feature/{ID}` trong danh sách tồn tại. Đang **không** ở `master`.
- Mỗi task có `plan.md` (lane C) hoặc mục Acceptance trong danh sách (lane A/B). Task chưa qua Bước 0 → loại khỏi batch.
- `docker info`: ghi Có/Không vào report. Không có Docker → bước BE lane C chỉ chạy nếu plan ghi rõ là không cần Testcontainers.
- Mode hiện tại là `auto`. Nếu không phải → cảnh báo và hỏi người **trước khi** bắt đầu (lúc này người vẫn còn ngồi đó).
- `pnpm build` + test baseline của nhánh đầu tiên xanh. Đỏ từ trước → dừng, không đổ lỗi cho burn.

### 4.2 Trạng thái của mỗi bước

| STATUS | Nghĩa | Supervisor làm gì |
|---|---|---|
| `PASS` | DoD đủ, review không còn Critical | Bước tiếp |
| `PASS-WITH-DEBT` | Xong, nhưng có test Docker ghi nợ hoặc Warning | Bước tiếp, liệt kê trong report |
| `BLOCKED` | Cần người: câu hỏi nhóm "hỏi, đừng đoán" (R10) · contract thiếu/sai (R8) · Gate 1 có câu hỏi/`REMOVED` (R7) · chọn nhầm lane (Rule 11) · Critical sau một lần sửa | **Dừng task này**, chuyển sang task kế tiếp trong danh sách |
| `HALT` | Invariant có thể đã bị phá (#21 tenant, hook `BLOCKED` mà agent cố đi vòng), build nhánh hỏng mà không tự cứu được | **Dừng cả batch** |

### 4.3 Không bao giờ làm, kể cả khi `auto` cho phép

`git push` · merge · `git reset --hard` / `git clean` / force checkout trên thay đổi chưa commit ·
ghi `contracts/openapi/*.json` ngoài `promote.mjs` · `config/reserved-routes.json` · sửa `DECISIONS.md`
(quyết định mới → ghi vào report để người chốt) · mở vòng D{n} · tạo nhánh/Task ID mới · sửa
`.claude/**` hoặc settings.

### 4.4 Chạy tiếp được sau khi bị cắt

Sau mỗi bước: commit code + tick `- [x]` trong `plan.md` + thêm một dòng vào bảng của burn log. Gọi lại
`/weekend-burn <cùng file>` thì skill đọc bảng đó và bỏ qua bước đã `PASS`. Nhờ vậy rate-limit, đóng máy
hay hết credit chỉ mất **phần đang dở của một bước**.

### 4.5 Artifact mỗi task

- `Docs/tasks/{ID}/changelog.md` — implementer cập nhật theo DoD hiện tại (không đổi luật).
- `Docs/tasks/{ID}/review.md` — supervisor ghi từ báo cáo của `change-reviewer`, theo mẫu §9, cộng mục
  **"Ma sát — nháp từ burn"**. Không ghi mục "Ma sát quy trình" chính thức.
- `Docs/DOCKER-TEST-DEBT.md` — theo đúng quy ước của file đó.

---

## 5. Permission baseline

### 5.1 Session thường (giải quyết permission fatigue — làm được ngay, không phụ thuộc burn)

- **Cá nhân:** `~/.claude/settings.json` → `"permissions": { "defaultMode": "auto" }`. Chỉ đặt được ở đây.
  Project settings bỏ qua giá trị này.
- **Dọn `settings.local.json`:** ~497 rule, phần lớn là lệnh ghép một lần không bao giờ khớp lại. Chạy
  skill `/fewer-permission-prompts` để rút pattern chung lên `settings.json`, rồi xoá phần rác. Việc này
  đo được bằng thước của P6/P10.

### 5.2 Thêm vào `.claude/settings.json` (commit, áp cho mọi người)

```jsonc
"deny": [
  "Read(./.env)", "Read(./.env.*)", "Read(**/appsettings.Production*.json)",
  // git push: KHÔNG deny (Q3) — burn chặn bằng --disallowedTools
  "Bash(git reset --hard*)", "PowerShell(git reset --hard*)",
  "Bash(git clean *)", "PowerShell(git clean *)",
  "Bash(git branch -D *)", "PowerShell(git branch -D *)",
  "Bash(docker system prune*)", "Bash(docker volume rm*)"
]
```

Deny thắng allow ở mọi scope, kể cả dưới `auto`. `git commit`, `pnpm`, `dotnet test`, `node tools/*`
giữ nguyên allow.

### 5.3 Không làm

- Không đặt `bypassPermissions` ở đâu cả. `auto` + deny + hook là đủ. Bypass bỏ luôn classifier, và
  hook chặn contract là lớp duy nhất còn lại.
- Không dựa vào `permissionMode` trong frontmatter agent cho bất cứ việc gì liên quan đến an toàn (R2).

---

## 6. Người đã quyết (2026-11-06)

| # | Câu hỏi | Quyết định |
|---|---|---|
| Q1 | `approvedBy` khi burn tự promote theo #89 | **`burn-auto`** |
| Q2 | Burn tự promote Gate 1 hay coi mọi Gate 1 là `BLOCKED`? | **Tự promote** theo #89 (chỉ thêm + không câu hỏi). Từ production thì Rule 5 tự biến thành `BLOCKED` |
| Q3 | Deny `git push` cho cả session thường? | **Không deny** — vẫn bị hỏi như hiện tại. Burn chặn push bằng `--disallowedTools` |
| Q4 | Làm runner headless (Giai đoạn 2)? | **Có** — `tools/burn/run.mjs` |
| Q5 | Batch đầu tiên | **Chạy sau**, người tự chọn |

---

## 7. Các bước apply (cho agent apply — tuần tự)

**Giai đoạn 1 — tối thiểu, đủ dùng cuối tuần này**

1. `.claude/skills/weekend-burn/SKILL.md`: §3–§4 của file này + mẫu prompt implementer + mẫu báo cáo ≤ 15
   dòng + mẫu burn log (bảng `Task · Fn · STATUS · commit · ghi chú`). **Viết bằng
   `superpowers:writing-skills`.**
2. `Docs/process/burn/README.md` (≤ 1 KB): định dạng file danh sách batch.
3. `.claude/settings.json`: thêm `deny` ở §5.2 (sau khi người trả lời Q3).
4. Workflow: thêm 1 dòng ở §13 (`Burn không người trông | .claude/skills/weekend-burn/SKILL.md`) và
   **Rule 15**: *"Burn: chỉ trên nhánh task, tuần tự, `auto` không bypass; không push/merge/D{n}; Gate 2
   như thường."* Giữ dưới trần 22 KB.
5. `improvement-proposals.md`: thêm dòng **P11** vào bảng "Đã promote". Mốc rà lại: sau 2 lần burn. Đo
   số prompt treo, số bước `BLOCKED` vì lý do đúng/sai, số Critical lọt sang Gate 2.
6. File này: STATUS → `ĐÃ APPLY — Giai đoạn 1`.

**Giai đoạn 2 (Q4 = có):** `tools/burn/run.mjs` lặp
`claude -p "/weekend-burn <batch> --one" --permission-mode auto --max-budget-usd <n> --disallowedTools "Bash(git push *)" …`
cho từng bước. Mỗi bước có context thật sự sạch, và một lần crash không giết cả batch. Runner tự kiểm
tree sạch + dòng hàng đợi đã tick sau mỗi bước. Không tick → `ABORTED` + dừng. Tree bẩn → `HALT`. Một
skill, hai chế độ, không có skill `-step` riêng. Ngưỡng dừng theo `rate_limits.seven_day` qua status
line: **chưa làm**, chờ dữ liệu từ 2 lần burn đầu.

### Smoke test (trước cuối tuần thật)

Dùng nhánh nháp `feature/BURN-SMOKE` từ `master`, với `plan.md` giả có 2 bước:
**F1** lane A (sửa một text UI + test) và **F2** cố ý cần đổi contract.

Kỳ vọng: F1 `PASS` + commit + `changelog.md`. F2 `BLOCKED` (R8). Report có cả hai. `git status` sạch.
`contracts/contract.lock` không đổi. Không có commit nào trên `master`. Gọi lại skill thì bỏ qua F1.
Xong thì xoá nhánh (người làm).

### Checklist trước khi merge thay đổi quy trình

- [ ] Reviewer là `change-reviewer`, không có Write/Edit/Agent trong `tools`
- [ ] Hook `guard-write`/`guard-bash` vẫn chặn (thử ghi `contracts/contract.lock` trong burn → `BLOCKED` → `HALT`)
- [ ] Mỗi task chạm code có `changelog.md` + `review.md` nháp, banner STATUS đúng
- [ ] Report có bảng STATUS + link tới từng task
- [ ] Workflow ≤ 22 KB, root `CLAUDE.md` không đổi
- [ ] P11 có mốc rà lại

---

## 8. Nguồn đã xác minh (2026-11-06)

- Subagent lồng tối đa 3 tầng; `permissionMode` bị bỏ qua khi cha ở `auto`/`acceptEdits`/`bypassPermissions` —
  https://code.claude.com/docs/en/sub-agents.md
- `auto` có trên mọi gói, chọn qua `--permission-mode auto` hoặc user settings; project settings bỏ qua `auto` —
  https://code.claude.com/docs/en/permission-modes.md · https://code.claude.com/docs/en/settings.md
- Thứ tự deny → ask → allow; cú pháp `Bash(git push *)`, `PowerShell(...)` —
  https://code.claude.com/docs/en/permissions.md
- Headless `-p`: `--permission-mode`, `--max-budget-usd`, gọi `/skill` trong prompt —
  https://code.claude.com/docs/en/headless.md
- `rate_limits.*.used_percentage` trong status line JSON — https://code.claude.com/docs/en/statusline.md
