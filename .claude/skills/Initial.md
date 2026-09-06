---
name: bootstrap-framework
description: Dựng khung monorepo vsite ở Bước 1 — Turborepo, backend .NET 9 Clean Architecture, apps/web + apps/portal, pipeline Orval/MSW/Zod, contracts/openapi, config/reserved-routes.json, và test enforce ranh giới module. Chia làm ba phase 1A/1B/1C có điểm dừng bắt buộc. Dùng khi bắt đầu Bước 1, hoặc khi tiếp tục một phase còn dở.
---

# Bootstrap Framework — Bước 1

## Mục tiêu

Dựng **khung**, không dựng nghiệp vụ. Kết thúc Bước 1 phải chứng minh được một đường đi hoàn chỉnh:

```
endpoint BE  →  runtime OpenAPI  →  contract-sync  →  contract đã duyệt
             →  Orval  →  api-sdk (types + hooks + Zod + MSW)
             →  UI ở apps/web và apps/portal chạy được với mock
             →  tắt mock, gọi API thật, chạy được
```

Nếu đường đi này chạy thì mọi module về sau chỉ là lặp lại nó. Nếu nó không chạy, biết ngay bây giờ rẻ hơn nhiều so với biết ở Phase 2.

## Không thuộc phạm vi Bước 1

Vi phạm mục này là lấn sang Bước 2 hoặc Bước 3:

- ❌ Entity thật (`User`, `Shop`, `Listing`, …), migration thật
- ❌ Component Registry manifest — đó là Bước 2
- ❌ Elasticsearch, Redis, Hangfire, S3/MinIO, SignalR, Caddy — chỉ tạo chỗ cắm cấu hình nếu cần, không tích hợp
- ❌ Auth thật (ASP.NET Core Identity, JWT) — chỉ dựng chỗ cắm middleware
- ❌ UI nghiệp vụ, thiết kế màn hình

## Đọc trước khi bắt đầu

| Cần biết | Đọc |
|---|---|
| Tech stack, ràng buộc dependency, quy ước codegen (#19), tenant invariant (#21), monorepo (#22), SSR (#23), reserved routes (#24), portal URL (#25) | `DesignIdeal/02-tech-stack-and-decision.md` |
| Quy trình, contract, bước sync, cổng duyệt | `DesignIdeal/ai-agent-development-workflow.md` |
| Invariant chung, anti-pattern #36, từ vựng dễ nhầm | `CLAUDE.md` ở root |

---

## ⚠️ Ba điều phải làm rõ trước khi viết code

**Hỏi người dùng, không tự quyết.**

### 1. Thư viện sinh OpenAPI cho .NET 9

Hai lựa chọn: built-in `Microsoft.AspNetCore.OpenApi` của .NET 9, hoặc Swashbuckle. Điều kiện bắt buộc, bất kể chọn cái nào:

- Xuất được **document riêng cho từng module** (`identity`, `shop`, `listing`, …), không phải một file `v1` gộp tất cả.
- Xuất ra **file** được lúc build hoặc bằng lệnh CLI, không chỉ phục vụ qua HTTP endpoint.

Nếu không thoả cả hai thì bước sync không chạy được và toàn bộ quy trình sụp. Hỏi người dùng chọn, rồi ghi hướng dẫn cụ thể vào `backend/CLAUDE.md` thay cho đoạn đánh dấu "⚠️ Chưa chốt".

### 2. Quy tắc normalize khi diff contract

Đề xuất tối thiểu, chờ người duyệt: sort key đệ quy · bỏ `servers` và `info.version` · chuẩn hoá whitespace trong `description` · so sánh theo từng operation chứ không so text.

### 3. "Zod dùng chung" nghĩa là gì

⚠️ **Zod không chạy trên .NET.** Không tạo package Zod để "share giữa BE và FE" — điều đó bất khả thi.

"Dùng chung" ở Bước 1 nghĩa là **cùng sinh ra từ một nguồn**:

| | FE | BE |
|---|---|---|
| Nguồn | `contracts/openapi/*.json` | cùng file đó |
| Công cụ | Orval → Zod schema | FluentValidation viết theo contract |
| Kiểm chứng khớp nhau | — | contract-sync (diff runtime vs committed) |

Chỗ **duy nhất** có Zod schema thật sự dùng chung FE/BE là **Component Tree** ở Phase 2 (BE validate bằng JSON Schema sinh từ cùng manifest). Không phải việc của Bước 1.

---

## Phase 1A — Khung monorepo

**Bắt đầu khi:** repo trống hoặc chỉ có `DesignIdeal/`.

### Làm gì

1. **pnpm workspace + Turborepo.** Node và pnpm pin version qua `packageManager` + `.nvmrc`.
2. **Cây thư mục** đúng như `DesignIdeal/ai-agent-development-workflow.md` §15:
   ```
   backend/  apps/{web,portal}/  packages/{ui,shared,theme-engine,api-sdk}/
   contracts/openapi/.staging/   config/   docs/{tasks,architecture}/
   tools/contract-sync/          .claude/{agents,skills,commands}/
   ```
   Chưa tạo `packages/builder-*` và `packages/ai-agent` — Phase 2.
3. **`config/reserved-routes.json`** — copy nguyên văn từ Quyết định #24. `shop` và `admin` là bắt buộc.
4. **Copy các file `CLAUDE.md`** đã soạn vào root, `backend/`, `apps/web/`, `apps/portal/`.
5. **`.claude/settings.json` với hooks:**
   - `PreToolUse` matcher `Write|Edit`: chặn ghi vào `contracts/openapi/*.json` (cho phép `.staging/`), `contracts/contract.lock`, `config/reserved-routes.json`, `packages/api-sdk/**`. Exit code 2.
   - `PreToolUse` matcher `Bash`: kiểm **nội dung lệnh**, chặn `>`, `>>`, `tee`, `cp`, `mv` nhắm vào các đường dẫn trên. Không có hook này thì hook trên đi vòng được bằng bash.
6. **`.gitignore`**, `.editorconfig`, `prettier`, `eslint` (TypeScript strict).
7. **CI skeleton** — job rỗng có tên sẵn: `architecture-tests`, `contract-check`, `be-tests`, `fe-tests`.

### Definition of done

- [ ] `pnpm install` chạy sạch
- [ ] `pnpm build` chạy sạch (dù chưa có gì để build)
- [ ] Hook đã test: thử ghi vào `contracts/openapi/x.json` bị chặn; thử `echo x > contracts/contract.lock` cũng bị chặn
- [ ] `config/reserved-routes.json` có đủ `shop` và `admin`

### 🛑 DỪNG LẠI

Báo cáo cây thư mục đã tạo và kết quả test hook. **Chờ người xác nhận trước khi sang 1B.**

---

## Phase 1B — Backend framework + module mẫu + sync

**Bắt đầu khi:** 1A đã xác nhận, và ba câu hỏi ở §"Ba điều phải làm rõ" đã có câu trả lời.

### Làm gì

1. **Solution .NET 9**, Clean Architecture:
   ```
   backend/src/
     Api/                        ← host, middleware, DI
     Shared/                     ← primitives dùng chung, KHÔNG chứa logic module
     Modules/Sample/
       Sample.Domain/            ← không reference gì ngoài BCL
       Sample.Application/
       Sample.Infrastructure/
       Sample.Api/
   backend/tests/
     ArchitectureTests/
     Sample.IntegrationTests/
   ```

2. **Module `Sample` — throwaway, có chủ ý.**

   Đủ để chứng minh pipeline, không pre-empt thiết kế thật:
   - `GET /samples` — trả pagination đúng shape `{ items, total, page, pageSize }`
   - `GET /samples/{id}` — trả 200 hoặc 404 ProblemDetails có `error_code`
   - `POST /samples` — có validation, trả 400 ProblemDetails
   - `GET /shops/{shopId}/samples/{id}` — nested REST, để chứng minh hình dạng route ownership
   - Một enum trả về **dạng string**
   - Lưu in-memory hoặc EF InMemory. **KHÔNG migration, KHÔNG database thật.**

   ⚠️ **`Sample` phải bị xoá trước khi bắt đầu Bước 3 (`Identity`).** Ghi câu này vào `backend/CLAUDE.md` và tạo `docs/tasks/CLEANUP-SAMPLE.md`. Module mẫu sống sót vào production là cách quen thuộc để rác tồn tại ba năm.

3. **Quy ước #19 áp ngay từ `Sample`:** `JsonStringEnumConverter`, đủ `[ProducesResponseType]`, ProblemDetails có `error_code`, pagination một shape, nested REST.

4. **Đọc `config/reserved-routes.json` lúc startup**, cache trong memory, expose qua một service. Kèm test khẳng định file đọc được và có `shop`, `admin`.

5. **Architecture tests** — đây là phần quan trọng nhất của 1B:
   - `*.Domain` không reference EF Core, MediatR, ASP.NET
   - Module không reference project của module khác
   - Chiều `Domain ← Application ← Infrastructure ← Api` không bị đảo
   - Test phải **fail thật** khi cố tình vi phạm. Viết xong thì thử phá một lần để chắc chắn nó bắt được, rồi hoàn tác.

6. **Export OpenAPI theo document module** ra file, theo lựa chọn ở §1.

7. **`tools/contract-sync/`** — normalize, diff, phân loại `NEW_ENDPOINT` / `ADDITIVE` / `BREAKING` / `REMOVED` / `UNCHANGED`, sinh `docs/tasks/{ID}/contract-diff.md`.
   Script **chỉ ghi `contracts/openapi/.staging/`**. Lệnh promote tách riêng, cập nhật `contracts/contract.lock` cùng lúc.

8. **Chạy thử toàn bộ vòng sync** với `Sample`: export → staging → diff (lần đầu tất cả là `NEW_ENDPOINT`) → sinh `contract-diff.md`.

### Definition of done

- [ ] `dotnet build` và `dotnet test` sạch
- [ ] Architecture test đã được chứng minh là bắt được vi phạm
- [ ] `contracts/openapi/.staging/sample.v1.json` sinh ra được bằng một lệnh
- [ ] `contract-diff.md` sinh ra được, phân loại đúng
- [ ] `backend/CLAUDE.md` đã thay đoạn "⚠️ Chưa chốt" bằng hướng dẫn thật
- [ ] `docs/tasks/CLEANUP-SAMPLE.md` đã tạo

### 🛑 DỪNG LẠI — GATE 1

Trình `contract-diff.md` cho người duyệt. Báo cáo phải có mục **"Giả định tôi đã tự đặt"**.

Sau khi duyệt: promote staging → `contracts/openapi/sample.v1.json`, ghi `contract.lock`. **Chờ xác nhận trước khi sang 1C.**

---

## Phase 1C — Frontend framework + Orval + MSW

**Bắt đầu khi:** `contracts/openapi/sample.v1.json` đã được duyệt và có trong `contract.lock`.

### Làm gì

1. **`apps/web`** — TanStack Start (SSR).
   ⚠️ **Lock version chính xác**, không dùng `^`/`~` (Quyết định #23).

2. **`apps/portal`** — Vite + TanStack Router, CSR thuần. Không cài TanStack Start.

3. **`packages/`**: `ui` (shadcn/ui + Tailwind), `shared` (utils, hooks, types), `theme-engine` (token), `api-sdk` (generated).

4. **Orval** — config sinh vào `packages/api-sdk/`:
   - TypeScript types
   - API client (Axios, có interceptor cho refresh token — chỗ cắm, chưa có auth thật)
   - TanStack Query hooks
   - Zod schemas
   - MSW handlers

   Thêm header `// GENERATED — DO NOT EDIT` vào mọi file sinh ra. Thêm `packages/api-sdk/src` vào eslint ignore.

5. **Kiểm sha256 contract** khớp `contract.lock` trước khi chạy Orval. Lệch → dừng.

6. **Màn hình chứng minh** — nhỏ nhất có thể, ở **cả hai app**:
   - Gọi `GET /samples` qua hook Orval
   - Có đủ **loading · error · empty**
   - Một form dùng React Hook Form + Zod resolver với schema sinh từ contract
   - Chạy hoàn toàn bằng MSW mock

7. **Import `config/reserved-routes.json`** ở cả hai app (Vite JSON import native). Test khẳng định FE và BE cùng đọc một file và ra cùng kết quả.

8. **Test:** Vitest + MSW cho cả hai app.

9. **Integration:** tắt MSW, chạy backend thật, xác nhận màn hình vẫn chạy.

### Definition of done

- [ ] `pnpm gen:api` sinh đủ 5 loại artifact
- [ ] `pnpm build` sạch cho cả hai app
- [ ] `pnpm test` sạch
- [ ] `apps/web` render được ở server (xem HTML trong response, không phải div rỗng)
- [ ] Cả hai app chạy được với MSW, và chạy được khi tắt MSW gọi API thật
- [ ] Test reserved-routes FE/BE khớp nhau, pass
- [ ] Enum từ API là **string** ở cả hai phía
- [ ] CI: 4 job đều chạy thật, không còn rỗng

### 🛑 DỪNG LẠI — GATE 2

Sinh `docs/tasks/BOOTSTRAP-001/review.md`. Chạy subagent `change-reviewer`. Trình người duyệt.

---

## Quy tắc chung cho cả ba phase

1. **Không tự quyết những thứ ở §"Ba điều phải làm rõ".** Hỏi.
2. **Không thêm thư viện ngoài danh sách ở `DesignIdeal/02` §2.1** mà không hỏi. Thấy thiếu gì thì nêu ra, đừng tự cài.
3. **Không viết nghiệp vụ thật.** Gặp chỗ muốn "làm luôn cho tiện" thì dừng — đó là dấu hiệu đang lấn sang Bước 3.
4. **Ranh giới nào quan trọng thì phải có test enforce**, không để ở tài liệu (#17).
5. Mỗi phase kết thúc bằng báo cáo có: việc đã làm · file đã tạo · **giả định tự đặt** · rủi ro chưa xử lý · thứ cần người quyết.
6. Chưa qua điểm dừng thì **không** tự sang phase sau, kể cả khi thấy còn dư thời gian.
