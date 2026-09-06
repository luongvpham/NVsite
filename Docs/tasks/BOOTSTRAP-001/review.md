# BOOTSTRAP-001 — review (Gate 2)

Phạm vi: toàn bộ Bước 1 (Phase 1A khung monorepo → 1B backend + Sample module + contract-sync → 1C frontend + Orval + MSW).

## Tự động

- [x] BE test: 11/11 (6 ArchitectureTests + 5 Sample.IntegrationTests)
- [x] FE test: 10/10 (@vsite/shared 2 · @vsite/web 4 · @vsite/portal 4)
- [x] Integration (MSW off): PASS — apps/web SSR + apps/portal CSR đều gọi được backend thật qua `http://localhost:5270`, CORS đã bật cho dev (`:3000`, `:5173`)
- [x] contract-sync sau cùng: PASS — `node tools/contract-sync/export.mjs && node tools/contract-sync/check-no-drift.mjs` → "không còn diff ngoài contract đã duyệt"
- [ ] check-additive (registry): N/A — Component Registry là Bước 2, chưa tới trong Bước 1

## change-reviewer

Agent tổng-quát (đóng vai change-reviewer, đọc độc lập, không có context phiên trước) đã review toàn bộ backend + FE + tooling.

**Critical (đã xử lý):**
- `error_code` gắn qua `ProblemDetails.Extensions` lúc runtime, `Microsoft.AspNetCore.OpenApi` (reflection-based) không tự xuất field này vào schema → contract đã duyệt thiếu field mà response thật luôn có, vi phạm Quyết định #19. **Fix:** `backend/src/Api/OpenApi/ProblemDetailsSchemaTransformer.cs` (`IOpenApiSchemaTransformer`, áp dụng cho mọi document — module thật sau này không phải tự thêm lại). Đã chạy qua đúng flow: export → diff (SAMPLE-002, ADDITIVE only) → duyệt → promote → `pnpm gen:api` lại. Đồng thời phát hiện và fix một bug thật trong `tools/contract-sync/lib/diff.mjs`: `extractSchemaRefs` trước đó chỉ đọc content-type `application/json`, bỏ sót `application/problem+json` (nơi ProblemDetails sống) — nghĩa là tool sẽ không bao giờ bắt được thay đổi ở error schema. Đã tổng quát hoá đọc mọi content-type, có test tái tạo bằng dữ liệu tổng hợp xác nhận ADDITIVE hiện lên đúng.

**Warning (chấp nhận, ghi vào nợ kỹ thuật bên dưới, không chặn Gate 2):**
- Sample dùng EF InMemory — ĐÚNG theo skill Bước 1 ("KHÔNG database thật"), reviewer thiếu context này nên flag nhầm, không phải bug thật.
- `apps/web` `/samples` fetch qua hook trong component thay vì TanStack Start loader — sai pattern SSR nếu bị copy làm mẫu, nhưng route này throwaway (xoá cùng Sample).
- `nitro-nightly` ghim theo ngày — có chủ đích (TanStack Start + Nitro v3 yêu cầu bản nightly per skill chính thức), pin chính xác an toàn hơn dùng `latest`.

**Suggestion:** trùng lặp `samples-page.tsx`/`reserved-routes.ts` giữa web và portal — chấp nhận được vì throwaway.

**Đã kiểm và đạt:** tenant ownership-in-query đúng (filter ShopId+Id ngay trong query, test xác nhận sai shop → 404), layering/module-boundary enforce thật bằng NetArchTest (không chỉ nằm ở tài liệu), guard-write/guard-bash hooks chặn đúng path không có lỗ hổng bypass rõ ràng, contract.lock sha256 khớp, TanStack Start pin version chính xác, access token in-memory.

## Nợ kỹ thuật cố ý + task theo dõi

- `tools/contract-sync/lib/diff.mjs` chỉ resolve `$ref` một cấp (không đệ quy sâu hơn) — đủ cho Bước 1, cần mở rộng khi module thật có schema lồng sâu hơn (vd. `Shop` chứa `ShopDomain[]`).
- OpenAPI enum schema (`SampleStatus`) thiếu `"type": "string"` tường minh — hạn chế của bộ sinh built-in .NET 9; Orval tự suy luận đúng nên không chặn, nhưng cần theo dõi nếu đổi sang tool khác.
- `apps/web` test suite in ra `ReferenceError: module is not defined` (không fail test) — nghi do tương tác giữa Vitest module runner và các Vite environment mà `tanstackStart()`/`nitro()` plugin tạo ra. Chưa root-cause, không chặn Gate 2 vì không ảnh hưởng kết quả test.
- CORS "LocalDev" policy trong `backend/src/Api/Program.cs` chỉ bật ở `Development` — cần chốt chính sách CORS thật khi có domain production (Quyết định #7/#9).
- `apps/web` bundle chunk `router-*.js` > 500kB (cảnh báo build, không lỗi) — chưa code-split, chấp nhận được ở Bước 1 vì chưa có nhiều route.
- `apps/web` `/samples` không dùng loader để prefetch — nếu module thật (Shop Profile) copy pattern này sẽ mất lợi ích SEO của SSR. Route bị xoá cùng Sample nên không fix, chỉ cảnh báo trong `apps/web/CLAUDE.md`/`CLEANUP-SAMPLE.md`.

## Điểm cần anh quyết trước khi merge

Không có — mọi điểm Critical đã xử lý và re-approve qua Gate 1 (SAMPLE-002). Warning/Suggestion đều đã có lý do chấp nhận rõ ràng ở trên.
