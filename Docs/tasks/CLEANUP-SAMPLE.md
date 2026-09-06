# CLEANUP-SAMPLE

`Modules/Sample/` (Domain/Application/Infrastructure/Api) + `tests/Sample.IntegrationTests/`
tồn tại chỉ để chứng minh pipeline Bước 1 (framework → contract-sync → Orval → MSW → API thật).

**Phải xoá hoàn toàn trước khi bắt đầu Bước 3 (`Identity`).** Module mẫu sống sót vào
production là cách quen thuộc để rác tồn tại ba năm (xem `backend/CLAUDE.md`).

## Việc cần làm lúc dọn dẹp

- [ ] Xoá `backend/src/Modules/Sample/` (4 project)
- [ ] Xoá `backend/tests/Sample.IntegrationTests/`
- [ ] Gỡ reference `Sample.Api` khỏi `backend/src/Api/Api.csproj`, gỡ `app.MapSampleEndpoints()` khỏi `Program.cs`
- [ ] Gỡ reference Sample projects khỏi `backend/tests/ArchitectureTests/ArchitectureTests.csproj`
- [ ] Gỡ `AddOpenApi("sample")` khỏi `Program.cs`
- [ ] Xoá `contracts/openapi/sample.v1.json` + entry tương ứng trong `contracts/contract.lock`
- [ ] Xoá `contracts/openapi/.staging/sample.v1.json` nếu còn sót
- [ ] Xoá `docs/tasks/SAMPLE-001/`
- [ ] Cập nhật `backend/CLAUDE.md` — xoá mục "Sample module (throwaway, có chủ ý)"
- [ ] `dotnet sln remove` 4 project Sample + 1 project test khỏi `backend/vsite.sln`

### Phía FE (Bước 1C)

- [ ] Xoá `apps/web/src/routes/samples.tsx`, `apps/web/src/features/samples/`
- [ ] Xoá `apps/portal/src/routes/samples.tsx`, `apps/portal/src/features/samples/`
- [ ] Gỡ link "màn hình chứng minh pipeline" khỏi `apps/web/src/routes/index.tsx` và `apps/portal/src/routes/index.tsx`
- [ ] Xoá `packages/api-sdk/orval.config.ts` config `sample`/`sampleZod` — thay bằng config module thật đầu tiên (Identity)
- [ ] Xoá `packages/api-sdk/src/generated/sample/`, `packages/api-sdk/src/mocks.ts` export tương ứng, `src/index.ts` export tương ứng
- [ ] Xoá `apps/web/src/test/msw-server.ts` và `apps/portal/src/test/msw-server.ts` phần `getSampleMock()` — thay bằng handler của module thật

## Không xoá

`Shared/`, `tools/contract-sync/`, `backend/tests/ArchitectureTests/LayeringTests.cs` +
`ModuleBoundaryTests.cs` + `ReservedRoutesTests.cs` (rule generic, không gắn cứng vào Sample) —
đây là hạ tầng dùng lại cho mọi module thật về sau.

Tương tự phía FE: `packages/api-sdk/src/mutator/axios-instance.ts`, `src/env.d.ts`,
`apps/*/src/lib/reserved-routes.ts` (+ test), `apps/*/src/router.tsx`, `apps/*/src/routes/__root.tsx`,
`packages/ui/`, `packages/theme-engine/`, `packages/shared/` — hạ tầng dùng chung, không gắn cứng vào Sample.
