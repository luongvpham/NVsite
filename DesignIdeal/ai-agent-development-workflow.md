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
| **B — Contract, một tay** | Chạm contract nhưng nhỏ: thêm 1–2 endpoint, thêm field, sửa validation | Gate 1 + Gate 2 | Một session: BE → sync → FE |
| **C — Tuần tự đầy đủ** | Module mới, màn hình lớn, hoặc chạm nhiều module | Gate 1 + Gate 2 | Hai session, có `brief.md` |

**Lane A là mặc định.** Chỉ lên B khi thật sự chạm boundary contract; chỉ lên C khi khối lượng đủ
lớn để mở session thứ hai có lãi.

**Chọn nhầm lane thì dừng và báo**, không âm thầm đi tiếp. Task phình ra giữa chừng mà vẫn chạy fast
lane là cách quen thuộc nhất để một thay đổi lớn lọt qua mà không ai duyệt contract.

⚠️ **Cạm bẫy riêng của lane A và B.** Khi cùng một session viết cả hai phía, FE rất dễ bám vào hành
vi BE **không có trong contract** — cùng một trí nhớ nên nó "biết" BE trả gì. Đến lúc BE đổi, FE vỡ
mà contract không hề cảnh báo. Chống bằng đúng một quy tắc: **FE vẫn code với MSW trước**, chỉ bật
API thật ở bước integration.

---

## 2. Luồng (lane C đầy đủ; A/B là tập con)

```text
Business requirement
   ↓
BƯỚC 0 · phác nhu cầu dữ liệu        (10 phút, người viết)
   ↓
SESSION BE · implement → test → export runtime OpenAPI
   ↓
skill contract-sync → diff + phân loại → Docs/tasks/{ID}/contract-diff.md
   ↓
🧑 GATE 1 · đọc diff, 2 phút, ký      → promote staging + ghi contract.lock
   ↓
session BE viết brief.md
   ↓
SESSION FE · pnpm gen:api → UI với MSW mock
   ↓
integration (tắt MSW, API thật)
   ↓
change-reviewer (subagent read-only)
   ↓
🧑 GATE 2 · duyệt merge → Merge
   ↓
session-retro (session riêng, SAU Gate 2)  → Docs/tasks/{ID}/session-retro.md
   ⋮  (mỗi 3–5 task)
meta-review  → Docs/process/improvement-proposals.md → 🧑 duyệt → sửa quy trình
```

Cả hai session **đều mở tại root monorepo** — `contracts/`, `config/`, `packages/api-sdk` nằm ở root;
mở tại `apps/` thì session FE không đọc được contract và không chạy được Orval. Giới hạn phạm vi bằng
prompt + `CLAUDE.md` từng thư mục, không bằng thư mục mở. Session FE mở **sau khi** sync xong — không
mở sẵn để nó khỏi bắt đầu đoán.

---

## 3. Bước 0 — phác nhu cầu dữ liệu

Cái giá cố hữu của tuần tự BE-first: FE không có tiếng nói lúc thiết kế API. Những thứ chỉ lộ ra khi
dựng màn hình — *"màn này cần thêm một field nữa nếu không phải gọi hai lần"*, *"filter cần trả kèm
facet count"* — sẽ phát hiện muộn, lúc BE đã xong.

Chống bằng 10 phút trước khi prompt BE. **Không viết contract**, chỉ liệt kê vào
`Docs/tasks/{ID}/data-needs.md`:

```markdown
## Màn: Danh sách tin đăng (apps/web)
Mỗi item: tên shop, ảnh, category, quận/huyện, rating, số đánh giá, ListedSince
Toàn trang: tổng số kết quả, facet count theo category

## Câu hỏi mở
- rating null khi chưa có đánh giá, hay 0?
- facet count trả kèm search response hay endpoint riêng?
```

Chi phí gần bằng không, bắt được phần lớn loại thiếu sót nói trên.

---

## 4. Session BE

**Đọc:** `data-needs.md` + `backend/CLAUDE.md` + tài liệu thiết kế (qua [`00-INDEX.md`](00-INDEX.md), đừng mở bừa).
**Ghi:** chỉ trong `backend/`. **Không ghi:** `contracts/`, `config/`, `apps/`, `packages/`.

**Definition of done** — nguồn đầy đủ ở [`backend/CLAUDE.md`](../backend/CLAUDE.md) §Definition of done. Tóm tắt 9 mục:
Clean Architecture đúng tầng · validation + authn + authz · test · quy ước codegen (#19) · tenant
invariants (#21) · export OpenAPI theo document module · chạy `contract-sync` · viết `brief.md` **sau**
Gate 1 · **đồng bộ tài liệu** (`changelog.md` + banner `STATUS` + cấp số ở `DECISIONS.md`).

**Task chạm code mà thiếu `changelog.md` là task chưa xong** — `change-reviewer` báo Critical ở Gate 2.

---

## 5. ⚠️ Bước sync — diff và phân loại, KHÔNG overwrite

Đây là điểm sống còn của cả quy trình.

```
❌  runtime swagger  ──overwrite──→  contracts/openapi/listing.v1.json
```

Làm vậy nghĩa là hình dạng API do EF query và handler quyết định — cái gì tiện lúc code thì thành
contract. Không còn gì để duyệt, và đảo ngược chiều nguồn sự thật (#18).

```
✅  runtime swagger  ──┐
                       ├─→ normalize → diff → phân loại → 🧑 duyệt → ghi contract + lock
    committed contract ┘
```

Script **không bao giờ** ghi thẳng vào `contracts/openapi/`. Nó ghi ra `contracts/openapi/.staging/`.
Promote là lệnh riêng, chạy sau Gate 1, cùng lúc cập nhật `contract.lock`.

| Loại | Xử lý |
|---|---|
| `NEW_ENDPOINT` | Contract "ra đời" ở đây. Duyệt shape — vẫn kịp vì chưa có FE code nào bám vào |
| `ADDITIVE` | Duyệt nhanh, giữ `v1` |
| `BREAKING` | **Xem §6 — luật khác nhau trước và sau production** |
| `REMOVED` | Endpoint biến mất khỏi runtime. Gần như luôn là bug → dừng, hỏi |
| `UNCHANGED` | Bỏ qua |

**Cách chạy:** skill [`contract-sync`](../.claude/skills/contract-sync/SKILL.md) — đọc file đó, đừng
đoán các bước. Quy tắc normalize (sort key đệ quy · bỏ `servers`/`info.version` · so theo operation
chứ không so text) đã hiện thực trong `tools/contract-sync/lib/normalize.mjs`.

---

## 6. Gate 1 — duyệt contract

> ### ⚠️ Luật BREAKING đổi theo giai đoạn — đọc kỹ mục này
>
> **Hiện tại (chưa deploy production, không có client nào đang chạy):**
> BREAKING là **bình thường**. Sửa BE → chạy lại `pnpm gen:api` → sửa lỗi compile ở FE.
> Trình biên dịch tìm giúp hết. **Không tạo `v2`.** API có hình dạng sai thì sửa ngay lúc còn rẻ.
>
> **Từ lần deploy production đầu tiên** (hoặc khi có consumer ngoài đầu tiên):
> BREAKING **mặc định là bug implementation**. Muốn `v2` thật thì phải có lý do ghi ra trong
> `contract-diff.md` và được duyệt riêng.
>
> Ai bật lại luật này cũng phải sửa **Rule 5** ở §10 cùng lúc. Ghi ngày bật vào `DECISIONS.md`.

**Mục tiêu: 2 phút.** Người đọc `contract-diff.md`, không đọc file JSON.

```markdown
# LISTING-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)   ← nếu có thì để đầu file

## NEW_ENDPOINT
POST /shops/{shopId}/listings          tạo tin đăng
GET  /listings                         tìm kiếm công khai

## ADDITIVE
ListingSummary: + rating (double?)     null khi chưa có đánh giá

## Auth policy
POST: audience vsite-portal + role Owner/Manager tại shopId
GET : anonymous

## Giả định tôi đã tự đặt (không hỏi)
- pageSize mặc định 20, tối đa 50
- rating làm tròn 1 chữ số thập phân ở tầng API

## Câu hỏi cần anh quyết
1. ListedSince trả về dạng date hay datetime?

## Người duyệt đã quyết   ← ĐIỀN LÚC GATE 1, không để trống
| Câu hỏi / giả định | Quyết định | Số hiệu |
|---|---|---|
| ListedSince date hay datetime | datetime, UTC | không cấp mới — Quyết định #19 đã phủ |
| Ảnh đánh giá lưu chung media_assets? | Không, bảng riêng | số mới → đã thêm dòng ở DECISIONS.md |
```

**"Giả định tôi đã tự đặt" quan trọng hơn mục câu hỏi.** Thứ agent tự quyết mà không nghĩ đến việc
hỏi mới là chỗ hay sai; thứ nó biết để hỏi thì đã an toàn rồi.

⚠️ **"Người duyệt đã quyết" là chỗ hay mất nhất.** Bạn trả lời ở Gate 1 rồi câu trả lời bay vào chat —
task sau không có cách nào biết. Mọi quyết định phải rơi vào một trong hai chỗ: **một `#N` đã có**
(ghi rõ số), hoặc **một dòng mới ở [`DECISIONS.md`](DECISIONS.md)**. Không có ô thứ ba tên là "nhớ
trong đầu".

### Checklist

- [ ] Endpoint đủ cho Bước 0, không thừa endpoint "để dành"
- [ ] Enum string · ProblemDetails có `error_code` · pagination đúng shape chung · nested REST cho child resource (#19)
- [ ] **Không** có `shopId` trong request body (#21.4)
- [ ] Auth policy ghi rõ audience + role từng endpoint
- [ ] Các giả định tự đặt đều chấp nhận được
- [ ] `Docs/architecture/dependency-map.json` đã cập nhật nếu chạm module khác

Duyệt xong → promote staging → ghi `contract.lock` (sha256 + `approvedAt` + `taskId`) → viết `brief.md`.
Session FE kiểm sha256 trước khi chạy Orval; lệch → dừng, không đoán.

---

## 7. `brief.md` — bàn giao BE → FE

Do **session BE** viết, sau Gate 1. **Trỏ** tới contract, không chép lại nội dung contract.

Bốn mục bắt buộc: **Contract** (đường dẫn + sha256 + tóm tắt thay đổi) · **Việc FE cần làm** ·
**Ràng buộc** · **Không thuộc phạm vi** · **Acceptance**.
Mẫu đang chạy: [`Docs/tasks/IDENTITY-001/brief.md`](../Docs/tasks/IDENTITY-001/brief.md).

Mục **"Không thuộc phạm vi"** quan trọng nhất — không có nó, session FE thấy code BE ngay trong repo
và rất dễ với tay sang sửa, đặc biệt khi gặp bug BE.

---

## 8. Session FE

**Đọc:** `brief.md` → contract → `apps/*/CLAUDE.md`, `packages/*/CLAUDE.md`.
**Ghi:** `apps/`, `packages/` (trừ `packages/api-sdk/` là generated).

`contracts/openapi/*.json` → `pnpm gen:api` (Orval) → `packages/api-sdk/`: types · client · TanStack
Query hooks · Zod schemas · MSW handlers.

**Definition of done:** sha256 khớp `contract.lock` (lệch → dừng) · chạy Orval, không sửa tay
`api-sdk` · pages/components/forms · **loading · error · empty đủ cả ba** · validation bằng Zod sinh
từ contract · test Vitest + MSW · server state ở Query, client state ở Zustand, không copy qua lại (#20).

FE dùng **API model**, không mirror BE Domain Entity.

---

## 9. Integration, Gate 2, và bước học sau Gate 2

**Integration.** Tắt MSW, chạy API thật. Đây mới là chỗ bắt lỗi ngữ nghĩa — drift check chỉ so hình
dạng. BE trả đúng schema nhưng sai ownership scoping, sai ngữ nghĩa phân trang, sai dữ liệu thì vẫn
PASS drift. **Đừng để màu xanh của drift check tạo cảm giác an tâm sai chỗ.**

**`change-reviewer`.** Người viết code là người tệ nhất để review nó, và với LLM thì tệ hơn: cùng một
mạch suy nghĩ đã sinh ra lỗi sẽ đọc lướt qua lỗi đó. Spawn subagent read-only, context sạch, không
thấy lý lẽ session đã tự thuyết phục mình.
→ [`.claude/agents/change-reviewer.md`](../.claude/agents/change-reviewer.md) — **đọc file đó**, nó là
nguồn duy nhất cho danh sách kiểm.

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
```

Có mục FAIL hoặc Critical chưa xử lý → không đưa lên Gate 2.

### Sau Gate 2 — bước học

`changelog.md` ghi code lệch **thiết kế**. Không có chỗ nào ghi quy trình lệch **thực tế** — retro là
chỗ đó, và là dữ liệu cho §14 mục 6 (ngưỡng phân lane).

```
mỗi task  → session-retro   → Docs/tasks/{ID}/session-retro.md
mỗi 3–5   → meta-review     → Docs/process/improvement-proposals.md → 🧑 duyệt → sửa quy trình
```

| Bước | Nguồn duy nhất | Ghi được vào |
|---|---|---|
| Viết retro | skill [`session-retro`](../.claude/skills/session-retro/SKILL.md) · template [`Docs/templates/session-retro.md`](../Docs/templates/session-retro.md) | `Docs/tasks/{ID}/session-retro.md` |
| Tổng hợp | skill [`meta-review`](../.claude/skills/meta-review/SKILL.md) + agent read-only [`meta-reviewer`](../.claude/agents/meta-reviewer.md) | `Docs/process/improvement-proposals.md` |

Ba ràng buộc, cả ba đều cố ý:

1. **Retro viết SAU Gate 2, ở session riêng.** Bằng chứng đáng học nhất — `change-reviewer` bắt gì,
   người duyệt bác gì — chỉ có sau Gate 2. Và cùng lý do nêu ở trên về code review: cùng một mạch suy
   nghĩ đã sinh ra lỗi sẽ đọc lướt qua lỗi đó.
2. **Retro không bắt buộc với mọi session.** Điều kiện kích hoạt ở §0 của template; lane A trơn tru
   thì một dòng "không có ma sát" là đủ. Retro cho mỗi lần sửa CSS là thuế, không phải học.
3. **`meta-reviewer` không có quyền ghi** — cùng khuôn với `change-reviewer`. Đề xuất quy trình mà
   agent tự promote thì không còn cổng duyệt nào; swagger đề xuất, người duyệt, contract chốt (§5)
   áp dụng nguyên vẹn ở đây.

⚠️ **Vòng lặp phải khép.** `improvement-proposals.md` có bảng thường trực *"Đã promote — theo dõi hiệu
quả"*: mỗi rule đã nhận đều có mốc rà lại và có thể bị **gỡ**. Kèm theo là **trần ngân sách tài liệu**
ghi ở cuối file đó. Thiếu hai thứ này thì flow chỉ là máy sinh rule, và tài liệu phình đúng cái mà
[`00-INDEX.md`](00-INDEX.md) sinh ra để chống.

---

## 10. Rules

| # | Rule |
|---|---|
| 1 | **Tuần tự BE → sync → FE.** Không chạy song song |
| 2 | **Swagger đề xuất, người duyệt, contract chốt.** Không bao giờ overwrite contract bằng runtime |
| 3 | **Script chỉ ghi `.staging/`.** Promote là hành động sau khi người duyệt |
| 4 | **Contract đã duyệt phải lock.** Sha256 lệch thì dừng |
| 5 | **BREAKING: chưa production thì cứ sửa** (regen FE, không tạo `v2`). **Từ production trở đi** thì mặc định là bug implementation — xem khung cảnh báo ở §6 |
| 6 | **Generated code không sửa tay** — `api-sdk`, `builder-components/generated` |
| 7 | **FE code với MSW trước**, kể cả ở lane A/B khi cùng một session viết cả hai phía |
| 8 | **Context quan trọng nằm trong file**, không nằm trong chat |
| 9 | **Session không sửa phạm vi của session kia.** Contract sai → dừng và báo |
| 10 | **Tenant invariants (#21) là điều kiện merge**, không phải gợi ý |
| 11 | **Chọn nhầm lane thì dừng và báo.** Lane A là mặc định |
| 12 | **Task chạm code phải có `changelog.md`** và banner `STATUS` đã cập nhật |
| 13 | **Đề xuất quy trình phải qua người duyệt.** `meta-reviewer` chỉ đọc; promote là hành động riêng, và mọi rule đã promote đều có mốc rà lại để còn gỡ được |

---

## 11. Hai contract còn lại

**Component Registry** (`packages/builder-components/registry/*.manifest.ts`) — không đi qua Orval mà
qua pipeline codegen riêng, phục vụ 5 consumer: builder-renderer · Operations Engine · Zod FE ·
JSON Schema BE · Property Inspector.

```
*.manifest.ts → pnpm gen:registry → generated/ → check-additive.ts ← registry.lock.json
```

Manifest là **build-time artifact**, không phải bảng database. Sửa manifest = đổi contract. Muốn
breaking → phải cố ý cập nhật `registry.lock.json`, và việc đó cần người duyệt.
Luật chi tiết: [`packages/builder-components/CLAUDE.md`](../packages/builder-components/CLAUDE.md).

⏳ **Khi `builder-core` ra đời (Bước 5, Operations Engine):** thêm một operation type mới phải cập
nhật **đồng thời 6 chỗ** — union type · Zod schema · `apply()` · `invert()` · AI tool schema · test
cho cả apply lẫn invert. Thiếu một = chưa xong. Đưa checklist này vào `CLAUDE.md` của package đó ngay
khi tạo. *(Package này chưa tồn tại; invariant "builder-renderer không import builder-core" ở root
`CLAUDE.md` là ràng buộc đặt trước.)*

**Reserved routes** (`config/reserved-routes.json`) — một nguồn duy nhất, FE import build-time, BE đọc
lúc startup, `ReservedRoutesTests` khẳng định hai bên đọc cùng file (#24).

---

## 12. Thứ tự module — hai loại, đừng lẫn

| | Ràng buộc bởi | Vi phạm thì sao |
|---|---|---|
| **Thứ tự phụ thuộc entity** | FK NOT NULL, global query filter theo `TenantContext` | Không migrate được, hoặc migrate xong phải sửa lại |
| **Thứ tự triển khai** (Bước 1 → 2 → 3 …) | Rủi ro kỹ thuật, không phải FK | Phát hiện sai lầm muộn, phải làm lại nhiều thứ |

Hai thứ tự này **độc lập**.
Loại thứ nhất: [`Docs/architecture/dependency-map.json`](../Docs/architecture/dependency-map.json) —
nguồn duy nhất, `ModuleBoundaryTests` đọc chính file đó. Thêm module mà quên khai → test FAIL.
Loại thứ hai: [`step.md`](step.md), tiến độ ở [`00-INDEX.md`](00-INDEX.md) §3.

**Vì sao Component Registry (Bước 2) chạy trước Identity (Bước 3) — cố ý:** manifest là build-time,
không FK, không cần `Shop`/`Identity` tồn tại; và nó là chỗ **rủi ro cao nhất** (schema sai làm hỏng
đồng thời 5 consumer). Bước 2 kiểm chứng pipeline codegen — thứ mọi module sau đều dựa vào, và là chỗ
Quyết định #17 đặt cược. Đổi lại, Bước 2 **không** được đụng database/entity/migration.

---

## 13. Cơ chế chặn — đọc file, đừng chép

| Cơ chế | Nguồn duy nhất |
|---|---|
| Hook chặn ghi contract/generated | `.claude/hooks/guard-write.mjs` · `guard-bash.mjs`, khai ở `.claude/settings.json` |
| Skill sync contract | `.claude/skills/contract-sync/SKILL.md` |
| Agent review Gate 2 | `.claude/agents/change-reviewer.md` |
| Skill viết retro sau Gate 2 | `.claude/skills/session-retro/SKILL.md` + `Docs/templates/session-retro.md` |
| Skill + agent tổng hợp cải tiến | `.claude/skills/meta-review/SKILL.md` + `.claude/agents/meta-reviewer.md` |
| Trần ngân sách tài liệu | `Docs/process/improvement-proposals.md` §Trần ngân sách |
| Ranh giới module | `Docs/architecture/dependency-map.json` + `ModuleBoundaryTests` |
| Additive-only cho registry | `packages/builder-components/registry.lock.json` + `scripts/check-additive.ts` |
| Tài liệu không rot | `pnpm check:docs` · `pnpm gen:doc-index` (chi tiết ở `00-INDEX.md` §5) |

Nguyên tắc nền: **codegen > skill > CLAUDE.md > hy vọng agent nhớ** (#17).

---

## 14. Điểm còn trống cần chốt

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | ~~Thư viện OpenAPI .NET 9 + đặt tên document theo module~~ | ✅ `Microsoft.AspNetCore.OpenApi` — `backend/CLAUDE.md` |
| 2 | ~~Quy tắc normalize trong `tools/contract-sync`~~ | ✅ `tools/contract-sync/lib/normalize.mjs` |
| 3 | ~~Vị trí thư mục backend~~ | ✅ `backend/` |
| 4 | ~~Bước 2 (manifest) trước Bước 3 (Identity)~~ | ✅ Cố ý — §12 |
| 5 | Task ID convention khi một task chạm nhiều module | ⏳ |
| 6 | Ngưỡng phân lane A/B/C — con số cụ thể hay để cảm tính | ⏳ Chạy 3–4 task rồi rút ra, đừng chốt sớm. **Dữ liệu đến từ `session-retro.md` §2** — chốt ở kỳ `meta-review` đầu tiên có đủ retro |
| 7 | Branch riêng mỗi task, hay commit thẳng vào nhánh dev | ⏳ |
| 8 | **Mốc bật lại luật BREAKING** (§6) — gắn vào lần deploy production đầu, hay sớm hơn | ⚠️ Chốt trước khi có consumer ngoài đầu tiên |
