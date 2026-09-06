# AI Agent Development Workflow — vsite

> **Phiên bản 3.** Thay thế v2 (flow song song). Flow chính thức là **tuần tự BE → sync → FE**.
> **Tài liệu liên quan:** `01-project-ideal.md` · `02-tech-stack-and-decision.md` (nguồn sự thật cho quyết định kiến trúc) · `03`–`07` (thiết kế entity/module).

## 0. Thay đổi so với v2

| # | Thay đổi | Lý do |
|---|---|---|
| 1 | **Bỏ chạy song song BE ∥ FE.** Flow là tuần tự: BE → sync contract → FE | Song song chỉ mua thời gian đồng hồ, đổi lại token gấp đôi và mọi thay đổi contract giữa chừng phải dừng cả hai. Phase 1 vốn BE-heavy nên gần như không lãi |
| 2 | **Bỏ subagent implementer.** Mỗi phía là một session Claude Code do người điều khiển trực tiếp | Không còn nhu cầu cách ly context giữa hai agent chạy cùng lúc |
| 3 | **Contract sinh ra ở bước sync, không viết trước từ tưởng tượng** | Với module mới, contract viết trước khi code gần như luôn sai; hình dạng đúng chỉ lộ ra khi implement |
| 4 | ⚠️ **Bước sync là diff + phân loại, KHÔNG overwrite** | Đây là điểm sống còn. Để swagger ghi đè contract là đảo ngược chiều nguồn sự thật (Quyết định #18) |
| 5 | Gate 1 chuyển vị trí: từ *trước khi code* sang *sau BE, trước FE* | Vẫn kịp — lúc đó chưa có dòng FE nào bám vào contract |
| 6 | Thêm **`brief.md`** — bàn giao BE → FE, do session BE viết | Người vừa implement xong biết chính xác cái gì đã đổi |
| 7 | Thêm **ba lane** (A/B/C) thay vì một quy trình duy nhất | Task nhỏ không được trả giá của task lớn, nếu không sẽ bỏ quy trình sau hai tuần |
| 8 | Thêm **`change-reviewer`** — subagent read-only ở Gate 2 | Người viết code là người tệ nhất để review nó |
| 9 | Cả hai session VS Code **đều mở tại root monorepo** | `contracts/`, `config/`, `packages/api-sdk` nằm ở root; mở tại `apps/` thì session FE không đọc được contract và không chạy được Orval |
| 10 | Thêm **Bước 0** — phác nhu cầu dữ liệu của màn hình trước khi prompt BE | Bù lại nhược điểm cố hữu của tuần tự BE-first: FE không có tiếng nói lúc thiết kế API |

---

## 1. Mục tiêu và nguyên tắc

Một người điều khiển hai session Claude Code, làm tuần tự, đồng bộ bằng contract commit trong repo.

- BE làm trước, FE làm sau. **Không chạy song song.**
- Contract là ngôn ngữ chung. Hai session **không** nói chuyện trực tiếp.
- Mọi context quan trọng nằm trong **file**, không nằm trong chat.
- Swagger runtime dùng để **đề xuất và kiểm chứng** contract, không bao giờ tự động ghi đè nó.
- Breaking change → version mới, không sửa contract cũ.
- Người giữ hai cổng: duyệt contract, duyệt merge.

---

## 2. Ba loại contract

OpenAPI không phải contract duy nhất. Cả ba đều là ranh giới nhiều bên cùng đọc, đều phải được người duyệt, đều có cơ chế chống sửa lén.

| Contract | File | Ai được sửa | Cơ chế bảo vệ |
|---|---|---|---|
| **API boundary** | `contracts/openapi/{module}.v{n}.json` | Chỉ sau khi người duyệt ở bước sync | `contracts/contract.lock` (sha256) |
| **Component Registry** (5 consumer: builder-renderer, Operations Engine, Zod FE, JSON Schema BE, Property Inspector) | `packages/builder-components/registry/*.manifest.ts` | Người duyệt | `registry.lock.json` + `check-additive.ts` (#59–#67) |
| **Reserved routes** | `config/reserved-routes.json` | Người duyệt | Test khẳng định FE và BE đọc cùng file (#24) |

> Nguyên tắc nền: **codegen > skill > CLAUDE.md > hy vọng agent nhớ** (Quyết định #17).

---

## 3. Setup — hai session, cùng một root

```
┌─────────────────────────────┐   ┌─────────────────────────────┐
│ VS Code #1  — SESSION BE    │   │ VS Code #2  — SESSION FE    │
│ mở tại: vsite/  (root)      │   │ mở tại: vsite/  (root)      │
│ ghi: backend/               │   │ ghi: apps/, packages/       │
└─────────────────────────────┘   └─────────────────────────────┘
              │                                 │
              └──────────┬──────────────────────┘
                         ▼
              Artifact dùng chung trên đĩa
        contracts/ · brief.md · contract.lock
```

**Cả hai đều mở tại root monorepo.** vsite không phải hai repo tách rời như project cũ: FE trải trên `apps/web`, `apps/portal` và 8 package; `contracts/`, `config/reserved-routes.json`, `packages/api-sdk` đều ở root. Mở session FE tại `apps/` thì nó không đọc được contract, không đọc được reserved routes, và không chạy được Orval.

Giới hạn phạm vi bằng **prompt + CLAUDE.md từng thư mục**, không bằng thư mục mở. Vì tuần tự nên hai session không bao giờ ghi cùng lúc — không có xung đột file.

Session FE mở **sau khi** bước sync xong. Không mở sẵn để tránh nó bắt đầu đoán.

---

## 4. Ba lane

Không phải task nào cũng đáng trả giá đầy đủ. Phần lớn task thực tế là task nhỏ.

| Lane | Điều kiện | Cổng | Cách làm |
|---|---|---|---|
| **A — Fast** | Không chạm contract nào: sửa bug, đổi UI, refactor trong một module, thêm test | Gate 2 | Một session ở root làm cả BE lẫn FE |
| **B — Contract, một tay** | Có chạm contract nhưng nhỏ: thêm 1–2 endpoint, thêm field, sửa validation | Gate 1 + Gate 2 | Một session, làm BE xong → sync → làm FE |
| **C — Tuần tự đầy đủ** | Module mới, hoặc màn hình lớn, hoặc chạm nhiều module | Gate 1 + Gate 2 | Hai session như §3, có `brief.md` |

**Lane A là mặc định.** Chỉ lên B khi thật sự chạm boundary contract; chỉ lên C khi khối lượng đủ lớn để việc mở session thứ hai có lãi.

**Chọn nhầm lane thì dừng, không âm thầm đi tiếp.** Đang ở A/B mà phát hiện task lớn hơn tưởng → báo, đề xuất lên lane cao hơn. Task phình ra giữa chừng mà vẫn chạy fast lane là cách quen thuộc nhất để một thay đổi lớn lọt qua mà không ai duyệt contract.

⚠️ **Cạm bẫy riêng của lane A và B:** khi cùng một session viết cả hai phía, FE rất dễ bám vào hành vi BE **không có trong contract** — vì cùng một trí nhớ nên "biết" BE trả gì. Đến lúc BE đổi, FE vỡ mà contract không hề cảnh báo. Chống bằng đúng một quy tắc: **FE vẫn code với MSW trước**, chỉ bật API thật ở bước integration. UI chạy được với mock sinh từ contract thì chắc chắn không bám vào thứ ngoài contract.

---

## 5. Core workflow (lane C)

```text
        Business requirement
                 ↓
   ┌─── BƯỚC 0 · Phác nhu cầu dữ liệu (10 phút, người làm)
   │             ↓
   │    ═══ SESSION BE ═══
   │      implement domain / application / api / test
   │             ↓
   │      export runtime OpenAPI
   │             ↓
   │      chạy skill contract-sync  ──→ diff + phân loại
   │             ↓
   │      viết docs/tasks/{ID}/contract-diff.md
   │             ↓
   ├─── 🧑 GATE 1 — duyệt contract
   │      ✅ → ghi contracts/openapi/*.json + contract.lock
   │      ❌ BREAKING không chính đáng → BE sửa code, chạy lại
   │             ↓
   │      session BE viết docs/tasks/{ID}/brief.md
   │             ↓
   │    ═══ SESSION FE ═══   (mở lúc này, không sớm hơn)
   │      pnpm gen:api (Orval) → api-sdk
   │      implement UI với MSW mock
   │             ↓
   │      integration: tắt MSW, chạy API thật
   │             ↓
   │      change-reviewer (subagent read-only)
   │             ↓
   └─── 🧑 GATE 2 — duyệt merge
                 ↓
               Merge
```

---

## 6. Bước 0 — phác nhu cầu dữ liệu

Cái giá cố hữu của tuần tự BE-first: FE không có tiếng nói lúc thiết kế API. Những thứ chỉ lộ ra khi dựng màn hình — "màn này cần thêm một field nữa nếu không phải gọi hai lần", "filter cần trả kèm facet count" — sẽ phát hiện muộn, lúc BE đã xong.

Chống bằng 10 phút trước khi prompt BE. **Không viết contract**, chỉ liệt kê:

```markdown
# LISTING-001 — nhu cầu dữ liệu

## Màn: Danh sách tin đăng (apps/web)
Cần mỗi item: tên shop, ảnh đại diện, category, quận/huyện, rating, số đánh giá, ListedSince
Cần cho toàn trang: tổng số kết quả, facet count theo category

## Màn: Tạo tin đăng (apps/portal)
Cần: cây category (chỉ node lá chọn được), danh sách MediaAsset của shop

## Câu hỏi mở
- rating null khi chưa có đánh giá, hay 0?
- facet count trả kèm search response hay endpoint riêng?
```

File này là input cho session BE. Nó bắt được phần lớn loại thiếu sót nói trên với chi phí gần bằng không.

---

## 7. Session BE

**Đọc:** Bước 0 + `backend/CLAUDE.md` + doc 02–07 phần liên quan.
**Ghi:** chỉ trong `backend/`.
**Không được ghi:** `contracts/`, `config/`, `apps/`, `packages/`.

### Definition of done

1. Domain / Application / Infrastructure / Api theo Clean Architecture + DDD Lite
2. Validation + authentication + authorization
3. Test (unit + integration)
4. Tuân thủ quy ước codegen (#19): enum serialize **string** · đủ `ProducesResponseType` · error trả **ProblemDetails** có `error_code` · pagination một shape `{ items, total, page, pageSize }` · nested REST cho child resource
5. Tuân thủ tenant invariants (#21) — xem §12
6. Export runtime OpenAPI theo **document tách theo module**
7. Chạy skill `contract-sync`, sinh `contract-diff.md`
8. Viết `brief.md` **sau khi** Gate 1 pass

Chưa qua Gate 1 thì chưa viết `brief.md` — brief mô tả contract, mà contract lúc đó chưa được duyệt.

---

## 8. ⚠️ Bước sync — diff và phân loại, không overwrite

Đây là điểm khác biệt quan trọng nhất giữa v3 và cách làm cũ.

### Sai lầm cần tránh

```
❌  runtime swagger  ──overwrite──→  contracts/openapi/listing.v1.json
```

Làm vậy nghĩa là hình dạng API do EF query và handler quyết định — cái gì tiện lúc code thì thành contract. Ba hệ quả: breaking change đi qua im lặng (BE đổi tên field → swagger đổi → contract đổi → FE regen → "chạy", nhưng client cũ đã vỡ và không ai được báo); không còn gì để duyệt trước; và vi phạm trực tiếp Quyết định #18.

### Đúng

```
runtime swagger  ──┐
                   ├──→ normalize ──→ diff ──→ phân loại ──→ 🧑 duyệt ──→ ghi contract + lock
committed contract ┘
```

Script **không bao giờ** ghi thẳng vào `contracts/openapi/`. Nó ghi ra staging:

```
contracts/openapi/.staging/listing.v1.json     ← script ghi
contracts/openapi/listing.v1.json              ← chỉ promote sau khi người duyệt
```

Promote là một lệnh riêng, chạy sau Gate 1, và cùng lúc cập nhật `contract.lock`.

### Phân loại bắt buộc

| Loại | Nghĩa | Xử lý |
|---|---|---|
| `NEW_ENDPOINT` | Contract chưa có endpoint này | Contract "ra đời" ở đây. Người duyệt shape. Vẫn kịp vì chưa có FE code nào bám vào |
| `ADDITIVE` | Thêm field optional, thêm response code, thêm endpoint mới trong module đã có | Duyệt nhanh, giữ `v1` |
| `BREAKING` | Rename field · xoá field · đổi type · đổi nullable · đổi enum value · đổi ngữ nghĩa | **DỪNG.** Hỏi: bug implementation hay thật sự cần `v2`? Mặc định là bug |
| `REMOVED` | Endpoint có trong contract nhưng runtime không còn | **DỪNG.** Gần như luôn là bug |
| `UNCHANGED` | — | Bỏ qua |

### Normalize trước khi diff

So chuỗi JSON thô sẽ báo khác biệt giả. `tools/contract-sync/` phải: sort key đệ quy · bỏ `servers` và `info.version` · chuẩn hoá whitespace trong `description` · so sánh có cấu trúc theo từng operation, không so text.

### Điều kiện kỹ thuật

Committed contract chia theo **module**, nên runtime cũng phải xuất được **từng document theo module**. Backend chỉ xuất một file `v1` gộp tất cả thì không diff được. Cách làm: đặt tên document theo module khi cấu hình OpenAPI, gắn nhóm cho từng endpoint, xuất từng document ra file lúc build.

> ⚠️ Cú pháp cụ thể phụ thuộc thư viện chọn ở Bước 1 (built-in `Microsoft.AspNetCore.OpenApi` của .NET 9, hay Swashbuckle). Chốt cùng Bước 1 và ghi vào `backend/CLAUDE.md` — đây là hạ tầng cho mọi task về sau.

### Skill

`.claude/skills/contract-sync/SKILL.md`

```markdown
---
name: contract-sync
description: So runtime OpenAPI với contract đã commit, phân loại khác biệt, sinh contract-diff.md cho người duyệt. Chạy ở cuối session BE, trước khi bàn giao FE.
---

# contract-sync

## Không được làm
- KHÔNG ghi vào contracts/openapi/*.json. Chỉ ghi contracts/openapi/.staging/.
- KHÔNG sửa contract.lock.
- KHÔNG tự quyết một khác biệt là "chấp nhận được".

## Các bước
1. Export runtime OpenAPI theo document từng module (xem backend/CLAUDE.md).
2. Ghi ra contracts/openapi/.staging/{module}.v{n}.json
3. Chạy tools/contract-sync — normalize cả hai bên, diff, phân loại theo
   NEW_ENDPOINT / ADDITIVE / BREAKING / REMOVED.
4. Sinh docs/tasks/{TASK-ID}/contract-diff.md theo template ở §9.
5. Có BREAKING hoặc REMOVED → nêu lên đầu file, ghi rõ nghi ngờ là bug
   implementation hay là thay đổi có chủ ý.
6. Dừng lại. Báo cáo và chờ người duyệt. Không đi tiếp sang brief.md.
```

---

## 9. Gate 1 — duyệt contract

**Người đọc `contract-diff.md`, không đọc file JSON.** Mục tiêu dưới 10 phút.

```markdown
# LISTING-001 — contract diff

## ⚠️ BREAKING / REMOVED
(không có)   ← nếu có thì để đầu file, mọi thứ khác xuống dưới

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
- facet count trả kèm search response, không tách endpoint
- rating làm tròn 1 chữ số thập phân ở tầng API

## Câu hỏi cần anh quyết
1. ListedSince trả về dạng date hay datetime?
```

Mục **"Giả định tôi đã tự đặt"** là bắt buộc, và nó quan trọng hơn mục câu hỏi. Thứ agent tự quyết mà không nghĩ đến việc hỏi mới là chỗ hay sai; thứ nó biết để hỏi thì đã an toàn rồi.

### Checklist

- [ ] Không có BREAKING/REMOVED, hoặc có và đã có lý do chính đáng + đã tạo `.v2`
- [ ] Endpoint đủ cho Bước 0, không thừa endpoint "để dành"
- [ ] Enum serialize **string** (#19)
- [ ] Error là **ProblemDetails** có `error_code` (#19)
- [ ] Pagination đúng shape chung (#19)
- [ ] Child resource dùng nested REST `/shops/{shopId}/listings/{id}` (#19)
- [ ] **Không** có `shopId` trong request body (#21.4)
- [ ] Auth policy ghi rõ audience + role từng endpoint
- [ ] Các giả định tự đặt đều chấp nhận được
- [ ] `dependency-map.json` đã cập nhật nếu chạm module khác

Duyệt xong → promote staging → ghi `contract.lock` → session BE viết `brief.md`.

### `contracts/contract.lock`

```json
{
  "listing.v1.json": {
    "sha256": "a3f21c…",
    "approvedAt": "2026-09-05T09:40:00Z",
    "approvedBy": "human",
    "taskId": "LISTING-001"
  }
}
```

Session FE kiểm sha256 trước khi chạy Orval. Lệch → dừng, không đoán.

---

## 10. `brief.md` — bàn giao BE → FE

Do **session BE** viết, sau Gate 1. Trỏ tới contract, không chép lại nội dung contract.

```markdown
# LISTING-001 — FE brief

## Contract
contracts/openapi/listing.v1.json   (sha256: a3f21c…)
Thay đổi: + POST /shops/{shopId}/listings
          + GET  /listings
          ~ ListingSummary thêm rating (nullable)

## Việc FE cần làm
- apps/portal: màn tạo tin đăng (form + chọn category node lá + chọn ảnh từ MediaAsset)
- apps/web: card listing hiển thị rating, ẩn hoàn toàn khi null (không hiện "0 sao")
- apps/web: trang kết quả tìm kiếm + facet theo category

## Ràng buộc
- Chạy `pnpm gen:api` trước. KHÔNG sửa tay packages/api-sdk
- Code với MSW mock trước; chỉ bật API thật ở bước integration
- Server state ở TanStack Query, client state ở Zustand.
  KHÔNG copy dữ liệu từ Query sang Zustand (#20)
- rating: null nghĩa là chưa có đánh giá, không phải 0

## Không thuộc phạm vi
- KHÔNG đụng backend/
- KHÔNG sửa contracts/ hay config/
- Contract sai hoặc thiếu → DỪNG, báo cáo. Không tự sửa, không tự làm tạm

## Acceptance
- [ ] Loading / error / empty state đủ cả ba
- [ ] Validation dùng Zod schema sinh từ contract
- [ ] Test Vitest + MSW pass
```

Mục **"Không thuộc phạm vi"** quan trọng nhất. Không có nó, session FE thấy code BE ngay trong repo và rất dễ với tay sang sửa — đặc biệt khi gặp bug BE.

---

## 11. Session FE

**Đọc:** `brief.md` → contract → `apps/*/CLAUDE.md`, `packages/*/CLAUDE.md`.
**Ghi:** `apps/`, `packages/` (trừ `packages/api-sdk/` là generated).

```text
contracts/openapi/listing.v1.json
              │  pnpm gen:api  (Orval)
              ▼
       packages/api-sdk/
              ├── TypeScript types
              ├── API client
              ├── TanStack Query hooks
              ├── Zod schemas
              └── MSW handlers
```

### Definition of done

1. Kiểm sha256 contract khớp `contract.lock`. Lệch → dừng
2. Chạy Orval. Không sửa tay bất cứ file nào trong `api-sdk`
3. Pages / components / forms / query-mutation flow
4. Loading · error · empty — đủ cả ba, không bỏ empty
5. Validation dùng Zod schema sinh từ contract
6. Test Vitest + MSW
7. Server state ở Query, client state ở Zustand, không copy qua lại (#20)

FE dùng **API model**, không mirror BE Domain Entity.

---

## 12. Integration và Gate 2

### Integration

Tắt MSW, chạy API thật. Đây mới là chỗ bắt lỗi ngữ nghĩa — drift check chỉ so hình dạng. BE trả đúng schema nhưng sai ownership scoping, sai ngữ nghĩa phân trang, sai dữ liệu thì vẫn PASS drift. **Đừng để màu xanh của drift check tạo cảm giác an tâm sai chỗ.**

### `change-reviewer`

Người viết code là người tệ nhất để review nó, và với LLM thì tệ hơn: cùng một mạch suy nghĩ đã sinh ra lỗi sẽ đọc lướt qua lỗi đó. Spawn một subagent read-only, context sạch, không thấy lý lẽ session đã tự thuyết phục mình.

`.claude/agents/change-reviewer.md`

```markdown
---
name: change-reviewer
description: Review độc lập diff của một task trước Gate 2. Chỉ đọc, không sửa.
tools: Read, Grep, Glob, Bash
model: inherit
---

Bạn review độc lập. KHÔNG sửa file.

Đọc theo thứ tự: contract liên quan → git diff → CLAUDE.md của thư mục bị chạm.

Kiểm bắt buộc:

TENANT SECURITY (#21) — vi phạm là lỗi bảo mật, không phải code style
- Mọi entity tenant-scoped có ShopId
- Mọi query đi qua Global Query Filter theo TenantContext
- Child resource validate ownership TRONG CÂU QUERY (WHERE ParentId = ...),
  không load rồi check ở memory
- Không nhận ShopId từ request body
- Quyền theo shop kiểm ở Authorization Handler, không tin claim trong token

KHỚP CONTRACT
- Từng field, kể cả nullable và enum value
- FE có bám vào hành vi không có trong contract không

RANH GIỚI KIẾN TRÚC
- Module không reference project của module khác (#1)
- packages không import từ apps
- builder-renderer không import builder-core, vẫn isomorphic (#23)
- Không sửa tay file generated (api-sdk, registry/generated)

Báo cáo: Critical / Warning / Suggestion.
Nêu cả thứ diff KHÔNG làm mà lẽ ra phải làm.
```

### Gate 2

Người đọc `docs/tasks/{ID}/review.md`:

```markdown
## Tự động
- [ ] BE test: x/y
- [ ] FE test: x/y
- [ ] Integration (MSW off): PASS / FAIL
- [ ] contract-sync sau cùng: không còn diff ngoài contract đã duyệt
- [ ] check-additive (nếu chạm registry): PASS / FAIL

## change-reviewer
Critical: …
Warning: …

## Nợ kỹ thuật cố ý + task theo dõi
## Điểm cần anh quyết trước khi merge
```

Có mục FAIL hoặc Critical chưa xử lý → không đưa lên Gate 2.

---

## 13. Contract thứ hai — Component Registry (Bước 2)

Manifest không đi qua Orval mà qua pipeline codegen riêng, phục vụ 5 consumer.

```text
*.manifest.ts  (SINGLE SOURCE OF TRUTH)
      │  pnpm gen:registry
      ▼
generated/  component-types.ts · props-schemas.ts (Zod)
            ai-tool-schema.json · property-panel.ts · registry-map.ts
            + JSON Schema cho BE validate
      │
      ▼
check-additive.ts  ←  registry.lock.json
```

- Manifest là **build-time artifact**, không phải bảng database. Sửa manifest = đổi contract, phải qua Gate 1.
- Muốn breaking → phải cố ý cập nhật `registry.lock.json`, và việc đó cần người duyệt.
- Thêm operation type mới vào `builder-core` phải cập nhật **đồng thời 6 chỗ**: union type · Zod schema · `apply()` · `invert()` · AI tool schema · test cho cả apply và invert. Thiếu một = chưa xong. Đưa checklist này vào `packages/builder-core/CLAUDE.md`.

Bước 2 chạy theo lane riêng: không có BE, không có FE, không có contract OpenAPI. Chỉ manifest → codegen → fixture → test → dev harness, rồi Gate 2.

---

## 14. Thứ tự module

`docs/architecture/dependency-map.json`:

```json
{
  "Identity":  { "dependsOn": [], "phase": 1 },
  "Shop":      { "dependsOn": ["Identity"], "phase": 1 },
  "Category":  { "dependsOn": [], "phase": 1 },
  "Listing":   { "dependsOn": ["Shop", "Category"], "phase": 1 },
  "Review":    { "dependsOn": ["Listing", "Identity"], "phase": 1 },
  "Lead":      { "dependsOn": ["Listing"], "phase": 1 },
  "Search":    { "dependsOn": ["Listing", "Category"], "phase": 1 },
  "MediaAsset":{ "dependsOn": ["Shop"], "phase": 2 },
  "Website":   { "dependsOn": ["Shop", "MediaAsset"], "phase": 2 },
  "Service":   { "dependsOn": ["Shop"], "phase": 2 },
  "Product":   { "dependsOn": ["Shop", "MediaAsset"], "phase": 2 }
}
```

**Thứ tự phụ thuộc bắt buộc (Phase 1):** `Identity` → `Shop` → `Category` → `Listing` → `Search` / `Review` / `Lead`.
Lý do: `Listing.ShopId` NOT NULL, `Review.UserId` NOT NULL, và Listing/Search/Review/Menu đều neo vào `Category`. `MediaAsset` là FK bắt buộc trước mọi entity Website.

### 14.1 Hai loại thứ tự, đừng lẫn

| | Ràng buộc bởi | Vi phạm thì sao |
|---|---|---|
| **Thứ tự phụ thuộc entity** | FK NOT NULL, global query filter theo `TenantContext` | Không migrate được, hoặc migrate xong phải sửa lại |
| **Thứ tự triển khai** (Bước 1 → 2 → 3 → 4) | Rủi ro kỹ thuật, không phải FK | Phát hiện sai lầm muộn, phải làm lại nhiều thứ |

Hai thứ tự này độc lập. `dependency-map.json` mô tả loại thứ nhất; lộ trình Bước mô tả loại thứ hai.

### 14.2 Vì sao Component Registry manifest chạy ở Bước 2 (cố ý)

Manifest thuộc `Website` — Phase 2 — nhưng được kéo lên trước `Identity` có chủ ý. Đây **không** mâu thuẫn với thứ tự phụ thuộc entity:

- Manifest là build-time artifact, không FK, không nằm trong global query filter, không cần `Shop` hay `Identity` tồn tại. Output là fixture JSON, không phải dữ liệu thật.
- Manifest là chỗ **rủi ro cao nhất**: schema sai làm hỏng đồng thời 5 consumer. Phát hiện muộn thì phải sửa cả 5 nơi.
- Bước 2 kiểm chứng **pipeline codegen** — thứ mọi module sau đều dựa vào, và là chỗ Quyết định #17 đặt cược. Sai thì biết ở Bước 2 rẻ hơn biết ở Phase 2.
- `check-additive.ts` + `registry.lock.json` phải có **trước** component thật đầu tiên.

Đổi lại: Bước 2 **không** được đụng database, entity, migration. Ghi vào task spec của Bước 2 như một mục Out of scope cứng — nếu Bước 2 bắt đầu sinh entity thì nó đã lấn sang Bước 3 và mất luôn lý do được chạy sớm.

---

## 15. Folder structure

```text
vsite/
├── CLAUDE.md                       ← product, module map, quy tắc contract, anti-pattern (#36)
├── .claude/
│   ├── agents/change-reviewer.md
│   ├── skills/contract-sync/SKILL.md
│   ├── commands/                   ← /task-new · /sync · /gate2
│   └── settings.json               ← hooks
│
├── docs/
│   ├── architecture/dependency-map.json
│   └── tasks/LISTING-001/
│       ├── data-needs.md           ← Bước 0, người viết
│       ├── contract-diff.md        ← session BE sinh, người đọc ở Gate 1
│       ├── brief.md                ← session BE viết sau Gate 1
│       └── review.md               ← người đọc ở Gate 2
│
├── contracts/
│   ├── contract.lock
│   └── openapi/
│       ├── .staging/               ← script ghi ở đây, không ghi ra ngoài
│       ├── identity.v1.json
│       ├── shop.v1.json
│       ├── category.v1.json
│       ├── listing.v1.json
│       ├── review.v1.json
│       ├── lead.v1.json
│       └── search.v1.json
│
├── config/reserved-routes.json     ← FE import build-time, BE đọc startup (#24)
│
├── backend/
│   ├── CLAUDE.md
│   └── src/Modules/{Identity,Shop,Category,Listing,Review,Lead,Search}/
│
├── apps/
│   ├── web/     (SSR — TanStack Start)  + CLAUDE.md
│   └── portal/  (CSR — Vite)            + CLAUDE.md
│
├── packages/
│   ├── api-sdk/               ← GENERATED, không sửa tay
│   ├── ui/  shared/  theme-engine/
│   ├── builder-components/    ← registry manifest
│   ├── builder-renderer/  builder-core/  ai-agent/
│   └── */CLAUDE.md
│
└── tools/
    ├── contract-sync/
    └── registry-codegen/
```

### Trách nhiệm `CLAUDE.md`

| File | Chứa |
|---|---|
| Root | Product overview, module map, quy tắc contract/version, ba lane, anti-pattern cấm kế thừa từ VSite 4.8 (#36) |
| `backend/` | Clean Architecture + DDD Lite, ranh giới module (#1), tenant invariants (#21), quy ước codegen (#19), cách export OpenAPI theo document module |
| `apps/web/` | SSR, SEO, phân biệt Shop Profile vs Shop Site |
| `apps/portal/` | CSR, builder, state ownership Query vs Zustand (#20) |
| `packages/builder-renderer/` | Isomorphic; không đụng `window`/`document` trong render chính; `basePath`/`resolveUrl()`; không import `builder-core` |

---

## 16. Hooks — chặn bằng cơ chế, không chỉ bằng lời

Lời dặn trong prompt là tầng thấp nhất (#17). Thêm vào `.claude/settings.json`:

| Hook | Việc |
|---|---|
| `PreToolUse` matcher `Write\|Edit` | Chặn ghi vào `contracts/openapi/*.json` (cho phép `.staging/`), `contracts/contract.lock`, `config/reserved-routes.json`, `packages/api-sdk/**`. Exit code 2 |
| `PreToolUse` matcher `Bash` | Kiểm **nội dung lệnh**: chặn `cat >`, `tee`, `>` redirect vào các đường dẫn trên. Không có bước này thì hook trên đi vòng được bằng bash |
| `PostToolUse` matcher `Write\|Edit` trong `registry/` | Chạy `check-additive.ts` |

---

## 17. Rules

| # | Rule |
|---|---|
| 1 | **Tuần tự BE → sync → FE.** Không chạy song song trừ khi có lý do cụ thể |
| 2 | **Swagger đề xuất, người duyệt, contract chốt.** Không bao giờ overwrite contract bằng runtime |
| 3 | **Script chỉ ghi `.staging/`.** Promote là hành động sau khi người duyệt |
| 4 | **Contract đã duyệt phải lock.** Sha256 lệch thì dừng |
| 5 | **BREAKING mặc định là bug implementation**, không phải lý do tạo `v2` |
| 6 | **Generated code không sửa tay** — `api-sdk`, `registry/generated` |
| 7 | **FE code với MSW trước**, kể cả ở lane A/B khi cùng một session viết cả hai phía |
| 8 | **Context quan trọng nằm trong file**, không nằm trong chat |
| 9 | **Session không sửa phạm vi của session kia.** Contract sai → dừng và báo |
| 10 | **Tenant invariants (#21) là điều kiện merge**, không phải gợi ý |
| 11 | **Chọn nhầm lane thì dừng và báo**, không âm thầm đi tiếp |
| 12 | **Lane A là mặc định.** Chỉ lên B/C khi thật sự cần |

---

## 18. Điểm còn trống cần chốt

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | Thư viện sinh OpenAPI cho .NET 9 và cách đặt tên document theo module | ⚠️ Chốt cùng Bước 1 — chặn toàn bộ bước sync |
| 2 | Quy tắc normalize trong `tools/contract-sync` (bỏ field nào, so ở mức nào) | ⚠️ Chốt cùng Bước 1 |
| 3 | ~~Vị trí thư mục backend~~ | ✅ `backend/` |
| 4 | ~~Bước 2 (manifest) trước Bước 3 (Identity)~~ | ✅ Cố ý — lý do ở §14.2 |
| 5 | Task ID convention khi một task chạm nhiều module | ⏳ |
| 6 | Ngưỡng phân lane A/B/C — cần con số cụ thể hay để cảm tính | ⏳ Chạy 3–4 task rồi rút ra, đừng chốt sớm |
| 7 | Có cần branch riêng mỗi task không, hay commit thẳng vào nhánh dev | ⏳ |
