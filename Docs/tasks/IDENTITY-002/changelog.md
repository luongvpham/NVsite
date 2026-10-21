# IDENTITY-002 — changelog: lệch so với thiết kế

Refresh token chuyển sang cookie httpOnly (#101). Đọc kèm `plan.md`, `contract-diff.md`,
`backend/docs/modules/identity.md` mục "Refresh token = cookie httpOnly".

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Refresh token không nằm trong body/response
- **Thiết kế nói gì:** `03` §6.4 và Quyết định #3 mô tả rotation với refresh token đi qua body
  (`AuthTokenResult` có `refreshToken`).
- **Thực thi:** cookie `vsite_rt` (`HttpOnly; Secure (trừ Development); SameSite=Lax; Path=/api/auth`,
  host-only). Response công khai là `AccessTokenResponse`. Application vẫn dùng `AuthTokenResult` nội bộ.
- **Nguyên nhân:** JS (XSS) không đọc được phiên dài 30 ngày. Quyết định #101.

### 2. `POST /api/auth/logout` — endpoint mới, không có trong `03`
Thu hồi đúng token đang dùng (không cả nhóm), xoá cookie, luôn 204, `AllowAnonymous`.

### 3. `refresh-token` không còn 422; cookie sai bị xoá
Không còn body để validate; thiếu cookie → 401 `UNAUTHORIZED` (không đụng cookie). Cookie **có nhưng** sai/hết
hạn/đã thu hồi → 401 **và xoá cookie** (endpoint tự trả ProblemDetails cùng shape `UNAUTHORIZED` vì
`UseExceptionHandler` xoá header đã set). Đây là **đổi so với G3 đã duyệt** — xem Ruling bên dưới. `RefreshTokenValidator` vẫn còn (command
vẫn mang chuỗi token) nhưng không bao giờ bắn qua HTTP.

### 4. `Secure` theo môi trường, không theo `IsHttps`
Backend chưa bật `UseForwardedHeaders` nên sau Caddy `Request.IsHttps` luôn false.

## Ruling khi review (đã làm khác plan)
- **G3 đảo lại:** G3 nói "refresh 401 không xoá cookie vì cookie cũ vô hại". Review chỉ ra tiền đề sai: cookie đã
  thu hồi còn trong trình duyệt → mỗi lần mở Portal gửi lại → `RefreshTokenHandler` coi là reuse và **thu hồi cả nhóm**
  (đăng xuất cả thiết bị khác cùng audience). Chuyển sang phương án thay thế G3 đã ghi sẵn: xoá cookie khi refresh
  lỗi. Chi phí nếu sai: một `catch` thừa ở endpoint.
- **Đăng xuất FE thoát ngay, logout gọi nền sau khi refresh đang bay xong** (I1/I3): `waitForPendingRefresh()` +
  `sessionEpoch` ở mutator — kết quả refresh của phiên đã clear bị bỏ, không hồi sinh access token. Nút không còn
  chờ mạng nên server treo không kẹt UI.
- Thêm `WithDescription` mô tả cookie cho `login`/`refreshToken`/`logout` — contract promote lần hai
  (chỉ khác mô tả; classifier báo không đổi API) để FE không dựa vào hành vi ngoài contract.

## Chưa làm xong / nợ có chủ đích (người đã chấp nhận ở plan §12)
- **G2 — đa tab:** hai tab cùng refresh một lúc bị coi là reuse → đăng xuất cả hai. Cần grace window nếu gây phiền.
- **G4 — CSRF chỉ dựa `SameSite=Lax`:** `{slug}.vsite.vn` cùng *site* với `admin.vsite.vn`.
- Đăng nhập lại không thu hồi token cũ trong cookie (nằm tới hết 30 ngày) · logout chưa xoá cache React Query.
- `/auth/refresh-token` vẫn không đối chiếu audience của token với Host của request (có từ trước).
- **Kiểm chứng đã chạy (2026-10-18, Docker có sẵn):** `dotnet test backend/vsite.sln` — ArchitectureTests 12/12,
  ComponentSchemaTests 12/12, IntegrationTests xanh toàn bộ (1 skip có sẵn); `pnpm build && pnpm test` xanh; không nợ
  Docker mới.
- Smoke trình duyệt thật (login → F5 → logout, `document.cookie` rỗng) **chưa chạy**: không có Docker dev
  đang chạy và process `Vsite.Api` của người dùng đang khoá build. BE được test qua pipeline HTTP thật
  (Testcontainers), FE qua MSW — chỗ chưa được phủ là cookie thật xuyên trình duyệt ↔ proxy Vite.

## Ma sát khi làm (cho review.md)
- Orval chỉ **thêm** vào `model/index.ts`, không bỏ export của file đã xoá → TS2307 sau `gen:api`. Phải xoá
  `index.ts` generated rồi sinh lại. `scripts/gen.mjs` dọn file mồ côi nhưng không dọn dòng trong index.
- `WebApplicationFactory.CreateClient()` có cookie jar → che mất lỗi cookie; test phải `HandleCookies = false`.
- `pnpm contract:export` build vào `bin` mặc định nên vấp khoá DLL khi dev server đang chạy; phải build ra
  `--artifacts-path` *bên trong repo* (ngoài repo thì test tìm `pnpm-workspace.yaml` sẽ hỏng) rồi lấy `obj/openapi`.
- `diff-report.mjs` nhận `<taskId>` làm đối số đầu — truyền tên module làm taskId sẽ sinh `Docs/tasks/{module}/`.
- Pre-commit chạy `check:docs` toàn repo: lỗi của task khác (`WEBSITE-002` thiếu changelog) chặn commit task này.
