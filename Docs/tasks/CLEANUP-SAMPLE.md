# CLEANUP-SAMPLE

`Modules/Sample/` (Domain/Application/Infrastructure/Api) + `tests/Sample.IntegrationTests/`
tồn tại chỉ để chứng minh pipeline Bước 1 (framework → contract-sync → Orval → MSW → API thật).

**Phải xoá hoàn toàn trước khi bắt đầu Bước 3 (`Identity`).** Module mẫu sống sót vào
production là cách quen thuộc để rác tồn tại ba năm (xem `backend/CLAUDE.md`).

**✅ Đã dọn dẹp xong** — thực hiện ngay trước khi bắt đầu Bước 3, xem chi tiết ở cuối file.

## Việc cần làm lúc dọn dẹp

- [x] Xoá `backend/src/Modules/Sample/` (4 project)
- [x] Xoá `backend/tests/Sample.IntegrationTests/`
- [x] Gỡ reference `Sample.Api` khỏi `backend/src/Api/Api.csproj`, gỡ `app.MapSampleEndpoints()` khỏi `Program.cs`
- [x] Gỡ reference Sample projects khỏi `backend/tests/ArchitectureTests/ArchitectureTests.csproj`
- [x] Gỡ `AddOpenApi("sample")` khỏi `Program.cs`
- [x] Xoá `contracts/openapi/sample.v1.json` + entry tương ứng trong `contracts/contract.lock`
- [x] Xoá `contracts/openapi/.staging/sample.v1.json` nếu còn sót
- [x] Xoá `docs/tasks/SAMPLE-001/`
- [x] Cập nhật `backend/CLAUDE.md` — xoá mục "Sample module (throwaway, có chủ ý)"
- [x] `dotnet sln remove` 4 project Sample + 1 project test khỏi `backend/vsite.sln`

### Phía FE (Bước 1C)

- [x] Xoá `apps/web/src/routes/samples.tsx`, `apps/web/src/features/samples/`
- [x] Xoá `apps/portal/src/routes/samples.tsx`, `apps/portal/src/features/samples/`
- [x] Gỡ link "màn hình chứng minh pipeline" khỏi `apps/web/src/routes/index.tsx` và `apps/portal/src/routes/index.tsx`
- [x] Xoá `packages/api-sdk/orval.config.ts` config `sample`/`sampleZod` — `defineConfig({})` rỗng, chờ module thật đầu tiên (Identity)
- [x] Xoá `packages/api-sdk/src/generated/sample/`, `packages/api-sdk/src/mocks.ts` export tương ứng, `src/index.ts` export tương ứng
- [x] Xoá `apps/web/src/test/msw-server.ts` và `apps/portal/src/test/msw-server.ts` phần `getSampleMock()` — `setupServer()` rỗng, chờ handler module thật

## Không xoá

`Shared/`, `tools/contract-sync/`, `backend/tests/ArchitectureTests/LayeringTests.cs` +
`ModuleBoundaryTests.cs` + `ReservedRoutesTests.cs` (rule generic, không gắn cứng vào Sample) —
đây là hạ tầng dùng lại cho mọi module thật về sau.

Tương tự phía FE: `packages/api-sdk/src/mutator/axios-instance.ts`, `src/env.d.ts`,
`apps/*/src/lib/reserved-routes.ts` (+ test), `apps/*/src/router.tsx`, `apps/*/src/routes/__root.tsx`,
`packages/ui/`, `packages/theme-engine/`, `packages/shared/` — hạ tầng dùng chung, không gắn cứng vào Sample.

## Ghi chú khi thực thi dọn dẹp

- `LayeringTests.cs` không xoá được nội dung 4 test case cũ mà không sửa — chúng `typeof`
  trực tiếp vào `Sample.Domain`/`Sample.Application`/`Sample.Infrastructure`, không compile được
  sau khi gỡ ProjectReference. Đã rút gọn file thành class rỗng kèm TODO trỏ tới Bước 3 (Identity)
  — lịch sử 4 test case gốc nằm ở git blame của file, tái tạo lại với namespace `Identity.*` khi
  module đó tồn tại. `ModuleBoundaryTests.cs` không cần sửa nội dung (chỉ sửa 1 comment cũ) vì rule
  quét filesystem, không gắn cứng assembly nào — vacuously pass khi `Modules/` rỗng.
- `Program.cs`: gỡ luôn `app.MapOpenApi()` (không chỉ `AddOpenApi("sample", ...)`) — không còn
  document OpenAPI nào đăng ký, gọi `MapOpenApi()` với 0 document sẽ throw lúc build app. Bật lại
  cả hai dòng cùng lúc khi Identity đăng ký `AddOpenApi("identity", ...)`.
- Phát hiện `backend/src/Api/Api.csproj` và `backend/tests/ArchitectureTests/ArchitectureTests.csproj`
  đã có một phần thay đổi (gỡ 1-2 dòng reference) nằm sẵn trong working tree TRƯỚC khi cleanup này
  bắt đầu, không rõ nguồn gốc (không phải do người dùng yêu cầu, không khớp với bất kỳ thao tác nào
  trong phiên làm việc này) — đã hoàn tất nốt phần còn thiếu một cách nhất quán, không có tác dụng
  phụ nào khác được phát hiện qua `git diff`/`dotnet build`/`dotnet test` sau khi xong.
