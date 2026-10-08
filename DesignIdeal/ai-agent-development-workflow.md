# AI Agent Development Workflow — vsite

> **STATUS:** `PROCESS` · **Tasks:** `—` · **Changelog:** `—` · **Stale:** `—`
> **Cửa vào:** [`00-INDEX.md`](00-INDEX.md)
>
> **Nguyên tắc của chính file này:** không chép nội dung của file khác vào đây. Skill, agent, hook,
> dependency map đều có file thật — file này **trỏ** tới chúng. Bản chép luôn rot trước bản thật.

Một người điều khiển Claude Code, làm **tuần tự BE → sync contract → FE**, đồng bộ bằng contract
commit trong repo. Hai session không nói chuyện trực tiếp; mọi context quan trọng nằm trong **file**,
không nằm trong chat.

---

## 1. Ba lane — chọn đúng lane trước khi làm gì khác

| Lane | Điều kiện | Cổng | Cách làm |
|---|---|---|---|
| **A — Fast** *(mặc định)* | Không chạm contract: sửa bug, đổi UI, refactor trong một module, thêm test | Gate 2 | Một session, làm cả hai phía |
| **B — Contract, một tay** | Chạm contract nhưng nhỏ: thêm 1–2 endpoint, thêm field, sửa validation | Gate 1 + Gate 2 | Một session: BE → sync → FE. Bước 0 tuỳ chọn |
| **C — Tuần tự đầy đủ** | Module mới, màn hình lớn, hoặc chạm nhiều module | Gate 1 + Gate 2 | Bước 0 + `plan.md` bắt buộc, `brief.md`, hai session |

Chỉ lên B khi thật sự chạm boundary contract; chỉ lên C khi khối lượng đủ lớn để mở session thứ hai có
lãi. **Chọn nhầm lane thì dừng và báo** — task phình giữa chừng mà vẫn chạy fast lane là cách quen
thuộc nhất để một thay đổi lớn lọt qua mà không ai duyệt contract.

⚠️ **Cạm bẫy của lane A và B:** cùng một session viết cả hai phía thì FE dễ bám vào hành vi BE **không có
trong contract** — cùng trí nhớ nên nó "biết" BE trả gì. Chống bằng: **FE vẫn code với MSW trước**, chỉ
bật API thật ở bước integration.

---

## 2. Luồng (lane C đầy đủ; A/B là tập con)

```text
Business requirement
   ↓
BƯỚC 0 · data-needs.md (10 phút, người viết) → plan.md (chia T1…Tn)
   ↓
SESSION BE · implement → test → export runtime OpenAPI
   ↓
skill contract-sync → Docs/tasks/{ID}/contract-diff.md
   ↓
🧑 GATE 1 (gọn, #89) → promote staging + contract.lock → BE viết brief.md
   ↓
SESSION FE · pnpm gen:api → UI với MSW mock ──── thiếu field/endpoint? ──→ vòng D{n} (§6.1) ─┐
   ↓                                                                                        │
   ↓ ←──────────────────────────────────────────────────────────────────────────────────────┘
integration (tắt MSW, API thật) → change-reviewer → review.md (kèm "Ma sát quy trình")
   ↓
🧑 GATE 2 · duyệt merge
   ⋮  (mỗi 3–5 task)
meta-review → Docs/process/improvement-proposals.md → 🧑 duyệt → sửa quy trình
```

Cả hai session **mở tại root monorepo** — `contracts/`, `config/`, `packages/api-sdk` nằm ở root. Giới
hạn phạm vi bằng prompt + `CLAUDE.md` từng thư mục. Session FE mở **sau khi** sync xong, để nó khỏi
bắt đầu đoán.

**Nhánh và Task ID:** mỗi task một nhánh `feature/{ID}` → một PR. Task chạm nhiều module vẫn một ID
(module chính); vòng mở lại contract dùng hậu tố `-D{n}` (§6.1).

---

## 3. Bước 0 — phác nhu cầu dữ liệu

**Cách chạy:** skill [`plan-task`](../.claude/skills/plan-task/SKILL.md) — cùng người đi từ business
requirement → khảo sát code (có link file:dòng) → flow đã có/cần làm → `data-needs.md` + `plan.md`.

Cái giá của BE-first: FE không có tiếng nói lúc thiết kế API — *"màn này cần thêm field nữa nếu không
phải gọi hai lần"* lộ ra muộn, lúc BE đã xong. Chống bằng 10 phút trước khi prompt BE. **Không viết
contract**, chỉ liệt kê vào `Docs/tasks/{ID}/data-needs.md`:

```markdown
## Màn: Danh sách tin đăng (apps/web)
Mỗi item: tên shop, ảnh, category, quận/huyện, rating, số đánh giá, ListedSince
Toàn trang: tổng số kết quả, facet count theo category

## Câu hỏi mở
- rating null khi chưa có đánh giá, hay 0?
```

**Lane C:** sau `data-needs.md`, viết `Docs/tasks/{ID}/plan.md` chia task T1…Tn (mẫu:
[`Docs/tasks/MEDIA-001/plan.md`](../Docs/tasks/MEDIA-001/plan.md)), thực thi bằng subagent từng task +
review theo rủi ro (Rule 14).

---

## 4. Session BE

**Đọc:** `data-needs.md` · `plan.md` (lane C) · `backend/CLAUDE.md` · tài liệu thiết kế qua
[`00-INDEX.md`](00-INDEX.md).
**Ghi code:** chỉ `backend/`. **Ghi tài liệu:** `Docs/tasks/{ID}/`, banner `STATUS` + `DECISIONS.md`
(DoD 9), `contracts/openapi/.staging/` (qua script). **Không ghi:** `apps/`, `packages/`, `config/`,
contract đã chốt.

**Definition of done:** [`backend/CLAUDE.md`](../backend/CLAUDE.md) §Definition of done — nguồn duy
nhất. Thiếu `changelog.md` là task chưa xong; `change-reviewer` báo Critical.

**Lane C cần máy có Docker** trước khi mở session BE — không có thì phải viết hai bộ test và lỗi thật
chỉ lộ ở cuối.

---

## 5. ⚠️ Bước sync — diff và phân loại, KHÔNG overwrite

```
❌  runtime swagger  ──overwrite──→  contracts/openapi/listing.v1.json
✅  runtime swagger + committed contract → normalize → diff → 🧑 duyệt → promote + lock
```

Overwrite nghĩa là hình dạng API do EF query và handler quyết định — không còn gì để duyệt, và đảo
ngược chiều nguồn sự thật (#18). Script chỉ ghi `contracts/openapi/.staging/`; promote là lệnh riêng
(`tools/contract-sync/promote.mjs`), cùng lúc cập nhật `contract.lock`.

**Cách chạy:** skill [`contract-sync`](../.claude/skills/contract-sync/SKILL.md) — đọc file đó, đừng
đoán các bước (gồm cả giới hạn của tool với response dạng mảng).

---

## 6. Gate 1 — duyệt contract

> ### ⚠️ Gate 1 gọn trước production (#89)
>
> **Hiện tại (chưa deploy production):** BREAKING là **bình thường** — sửa BE, chạy lại `pnpm gen:api`,
> sửa lỗi compile FE, **không tạo `v2`**. `contract-diff.md` chỉ gồm: endpoint/field mới · auth policy ·
> giả định tự đặt · câu hỏi · bảng "Người duyệt đã quyết". Nhãn của tool chỉ để tham khảo.
> **Không có câu hỏi và chỉ thêm endpoint/field → promote luôn, không dừng chờ người**; người đọc lại
> `contract-diff.md` sau. Có câu hỏi hoặc `REMOVED` (gần như luôn là bug) → dừng.
>
> **Từ lần deploy production đầu tiên:** BREAKING mặc định là bug; phân loại đầy đủ và Gate 1 luôn dừng.
> Người bật lại phải sửa **Rule 5** ở §10 và dòng #89 ở `DECISIONS.md` cùng lúc.

**Mục tiêu: 2 phút.** Người đọc `contract-diff.md`, không đọc JSON. Mẫu:
[`Docs/tasks/MEDIA-001-D3/contract-diff.md`](../Docs/tasks/MEDIA-001-D3/contract-diff.md).

**"Giả định tôi đã tự đặt" quan trọng hơn mục câu hỏi** — thứ agent tự quyết mà không nghĩ đến việc hỏi
mới là chỗ hay sai.

⚠️ **"Người duyệt đã quyết" là chỗ hay mất nhất.** Mọi quyết định phải rơi vào **một `#N` đã có** (ghi
số) hoặc **một dòng mới ở [`DECISIONS.md`](DECISIONS.md)**. Không có ô thứ ba tên là "nhớ trong đầu".

### Checklist

- [ ] Endpoint đủ cho Bước 0, không thừa endpoint "để dành"
- [ ] Quy ước #19 (enum string · `error_code` · pagination chung · nested REST) · **không** `shopId` trong body (#21.4)
- [ ] Auth policy ghi rõ audience + role từng endpoint
- [ ] Các giả định tự đặt đều chấp nhận được
- [ ] `Docs/architecture/dependency-map.json` đã cập nhật nếu chạm module khác
- [ ] **Mọi "Câu hỏi mở" trong `data-needs.md` đã có quyết định**
- [ ] Đã chạy thử Orval trên bản **staging** vào thư mục tạm: không type trùng (`…Dto2`)

Duyệt xong → promote → `contract.lock` → `brief.md`. `pnpm gen:api` tự kiểm sha256 với lock
(`packages/api-sdk/scripts/check-contract-lock.mjs`); lệch → dừng, không đoán.

### 6.1 Vòng mở lại contract (D{n}) — đường quay lại có chủ đích

FE phát hiện thiếu field/endpoint, hoặc type sinh ra sai → **dừng FE, không workaround** (Rule 9), mở
vòng `{ID}-D{n}` (n từ 2):

1. Session BE sửa → test → `contract-sync`.
2. Ghi vào **folder task cha**: `Docs/tasks/{ID}/contract-diff-D{n}.md`, thêm mục `## D{n}` vào
   `changelog.md` của task. Không tạo folder mới.
3. Gate 1 gọn như trên; promote với `taskId = {ID}-D{n}`. Cập nhật `brief.md` nếu việc FE đổi.
4. FE tiếp tục từ chỗ dừng.

Mỗi vòng tốn một lượt chờ người — nhiều vòng trong một task là tín hiệu Bước 0 đang hụt, ghi vào "Ma sát
quy trình" (§9). *(MEDIA-001 dùng folder `MEDIA-001-D2…D4` riêng, trước quy ước này.)*

---

## 7. `brief.md` — bàn giao BE → FE

Do **session BE** viết, sau Gate 1. **Trỏ** tới contract, không chép lại nội dung.

Năm mục bắt buộc: **Contract** (đường dẫn + sha256 + tóm tắt thay đổi) · **Việc FE cần làm** ·
**Ràng buộc** · **Không thuộc phạm vi** · **Acceptance**. Mẫu:
[`Docs/tasks/IDENTITY-001/brief.md`](../Docs/tasks/IDENTITY-001/brief.md).

**"Không thuộc phạm vi" quan trọng nhất** — thiếu nó, session FE thấy code BE ngay trong repo và dễ với
tay sang sửa, nhất là khi gặp bug BE.

---

## 8. Session FE

**Đọc:** `brief.md` → contract → `apps/*/CLAUDE.md`, `packages/*/CLAUDE.md`.
**Ghi:** `apps/`, `packages/` (trừ generated).

**Definition of done:** `pnpm gen:api` chạy được (lock khớp) · pages/components/forms · **loading ·
error · empty đủ cả ba** · validation bằng Zod sinh từ contract · test Vitest + MSW · state đúng #20 ·
dùng API model, không mirror Domain Entity.

**Smoke chạy app thật ngay sau task FE đầu tiên**, không đợi integration: dev server lên, host
`{slug}.vsite.local` không bị chặn, proxy tới BE chạy, route mới mở được khi đã đăng nhập.

---

## 9. Integration, Gate 2, và bước học

**Integration.** Tắt MSW, chạy API thật. Đây mới là chỗ bắt lỗi ngữ nghĩa — drift check chỉ so hình
dạng; sai ownership scoping, sai phân trang vẫn PASS drift. **Đừng để màu xanh của drift check tạo cảm
giác an tâm sai chỗ.**

**`change-reviewer`.** Cùng mạch suy nghĩ đã sinh ra lỗi sẽ đọc lướt qua lỗi đó — spawn subagent
read-only, context sạch → [`.claude/agents/change-reviewer.md`](../.claude/agents/change-reviewer.md)
(nguồn duy nhất cho danh sách kiểm).

**Gate 2.** Người đọc `Docs/tasks/{ID}/review.md`:

```markdown
## Tự động
- [ ] BE test: x/y          - [ ] FE test: x/y
- [ ] Integration (MSW off): PASS / FAIL
- [ ] contract-sync sau cùng: không còn diff ngoài contract đã duyệt
- [ ] check:docs + gen:doc-index --check: PASS / FAIL
- [ ] check-additive (nếu chạm registry): PASS / FAIL

## change-reviewer — Critical / Warning
## Nợ kỹ thuật cố ý + task theo dõi
## Điểm cần anh quyết trước khi merge

## Ma sát quy trình
| Ma sát | Nguyên cớ gốc | Mức A/B/C | Bằng chứng (file:dòng / commit) | Đề xuất (bớt trước, thêm sau) |
```

Có mục FAIL hoặc Critical chưa xử lý → không đưa lên Gate 2.

### Bước học — "Ma sát quy trình" + meta-review

`changelog.md` ghi code lệch **thiết kế**; mục **"Ma sát quy trình"** ở cuối `review.md` ghi quy trình
lệch **thực tế**. Điền **sau khi người quyết ở Gate 2** — bằng chứng đáng học nhất (reviewer bắt gì,
người bác gì) chỉ có lúc đó. Mức: **A** suýt/đã lọt vi phạm invariant, contract sai, merge nhầm ·
**B** phải làm lại việc đã xong · **C** chỉ khó chịu. Không ước lượng thời gian. Không có ma sát → một
dòng "Không có". Đây cũng là dữ liệu cho §14 mục 6.

Mỗi 3–5 task: skill [`meta-review`](../.claude/skills/meta-review/SKILL.md) + agent read-only
[`meta-reviewer`](../.claude/agents/meta-reviewer.md) → `Docs/process/improvement-proposals.md`.
`meta-reviewer` **không có quyền ghi** — đề xuất quy trình mà agent tự promote thì không còn cổng duyệt.

⚠️ **Vòng lặp phải khép.** `improvement-proposals.md` có bảng *"Đã promote — theo dõi hiệu quả"* (mỗi
rule có mốc rà lại và có thể bị **gỡ**) và **trần ngân sách tài liệu**. Thiếu hai thứ này thì flow chỉ
là máy sinh rule.

---

## 10. Rules

| # | Rule |
|---|---|
| 1 | **Tuần tự BE → sync → FE.** Không chạy song song; quay lại BE chỉ qua vòng D{n} (§6.1) |
| 2 | **Swagger đề xuất, người duyệt, contract chốt.** Không bao giờ overwrite contract bằng runtime |
| 3 | **Script chỉ ghi `.staging/`.** Promote là hành động riêng (§6, #89) |
| 4 | **Contract đã duyệt phải lock.** Sha256 lệch thì dừng |
| 5 | **BREAKING: chưa production thì cứ sửa**, Gate 1 gọn (#89). **Từ production** mặc định là bug, Gate 1 luôn dừng |
| 6 | **Generated code không sửa tay** — `api-sdk`, `builder-components/generated` (hook chặn) |
| 7 | **FE code với MSW trước**, kể cả lane A/B |
| 8 | **Context quan trọng nằm trong file**, không nằm trong chat |
| 9 | **Session không sửa phạm vi của session kia.** Contract sai → dừng và báo |
| 10 | **Tenant invariants (#21) là điều kiện merge**, không phải gợi ý |
| 11 | **Chọn nhầm lane thì dừng và báo.** Lane A là mặc định |
| 12 | **Task chạm code phải có `changelog.md`** và banner `STATUS` đã cập nhật |
| 13 | **Đề xuất quy trình phải qua người duyệt**; mọi rule đã promote có mốc rà lại để còn gỡ được |
| 14 | **Subagent chạy lệnh foreground.** Chạy nền rồi kết thúc lượt = treo. Báo "đang chạy" phải kiểm bằng `git`/danh sách agent, không tin lời agent |

---

## 11. Hai contract còn lại

**Component Registry** (`packages/builder-components/registry/*.manifest.ts`) — pipeline codegen
riêng, phục vụ 5 consumer: builder-renderer · Operations Engine · Zod FE · JSON Schema BE · Property
Inspector.

```
*.manifest.ts → pnpm gen:registry → generated/ → check-additive.ts ← registry.lock.json
```

Sửa manifest = đổi contract; breaking phải cố ý cập nhật `registry.lock.json`, cần người duyệt. Luật
chi tiết: [`packages/builder-components/CLAUDE.md`](../packages/builder-components/CLAUDE.md).

⏳ **Khi tạo `builder-core`:** thêm operation type phải cập nhật đồng thời 6 chỗ — union type · Zod ·
`apply()` · `invert()` · AI tool schema · test apply + invert. Đưa checklist này vào `CLAUDE.md` của
package đó ngay khi tạo.

**Reserved routes** (`config/reserved-routes.json`) — một nguồn, FE import build-time, BE đọc lúc
startup, `ReservedRoutesTests` khẳng định hai bên đọc cùng file (#24).

---

## 12. Thứ tự module — hai loại, đừng lẫn

| | Ràng buộc bởi | Nguồn |
|---|---|---|
| **Phụ thuộc entity** | FK NOT NULL, global query filter | [`Docs/architecture/dependency-map.json`](../Docs/architecture/dependency-map.json) — `ModuleBoundaryTests` đọc chính file đó |
| **Thứ tự triển khai** (Bước 1 → 2 → …) | Rủi ro kỹ thuật, không phải FK | [`step.md`](step.md), tiến độ ở [`00-INDEX.md`](00-INDEX.md) §3 |

Hai thứ tự này **độc lập** — một bước có thể đi trước module nó không phụ thuộc FK (vd. Component
Registry trước Identity, vì build-time và rủi ro cao nhất).

---

## 13. Cơ chế chặn — đọc file, đừng chép

| Cơ chế | Nguồn duy nhất |
|---|---|
| Hook chặn ghi contract/generated | `.claude/hooks/guard-write.mjs` · `guard-bash.mjs`, khai ở `.claude/settings.json` |
| Quyền lệnh tự cho phép | `.claude/settings.json` `permissions.allow` |
| Skill lập plan từ requirement | `.claude/skills/plan-task/SKILL.md` + `references/plan-template.md` |
| Skill sync contract | `.claude/skills/contract-sync/SKILL.md` |
| Kiểm lock trước Orval | `packages/api-sdk/scripts/check-contract-lock.mjs` (trong `gen:api`) |
| Drift contract trong CI | `tools/contract-sync/check-no-drift.mjs` (job `contract-check`) |
| Agent review Gate 2 | `.claude/agents/change-reviewer.md` |
| Tổng hợp cải tiến | `.claude/skills/meta-review/SKILL.md` + `.claude/agents/meta-reviewer.md` |
| Trần ngân sách tài liệu | `Docs/process/improvement-proposals.md` §Trần ngân sách |
| Ranh giới module | `Docs/architecture/dependency-map.json` + `ModuleBoundaryTests` |
| Additive-only cho registry | `packages/builder-components/registry.lock.json` + `scripts/check-additive.ts` |
| Tài liệu không rot | `pnpm check:docs` · `pnpm gen:doc-index` (chi tiết ở `00-INDEX.md` §5) |

Nguyên tắc nền: **codegen > skill > CLAUDE.md > hy vọng agent nhớ** (#17).

---

## 14. Điểm còn trống cần chốt

Mục 1–5 và 7 đã chốt (OpenAPI lib, normalize, thư mục backend, thứ tự Bước 2/3, Task ID, nhánh — §2).

| # | Vấn đề | Trạng thái |
|---|---|---|
| 6 | Ngưỡng phân lane A/B/C — con số cụ thể hay để cảm tính | ⏳ Dữ liệu từ mục "Ma sát quy trình" của `review.md`; chốt ở kỳ `meta-review` đầu tiên có đủ dữ liệu |
| 8 | **Mốc bật lại luật BREAKING** (§6) — lần deploy production đầu, hay sớm hơn | ⚠️ Chốt trước khi có consumer ngoài đầu tiên |
