# IDENTITY-002 — Refresh token sang cookie httpOnly

> **Đã code 2026-10-18 — chờ Gate 2** (xem `changelog.md`; G3 đã đảo lại sau review) (người đồng ý #101 và toàn bộ giả định §12; #101 đã ghi vào `DesignIdeal/DECISIONS.md`)
>
> **Lane B.** Đổi contract `identity.v1` (BREAKING, trong một module) nhưng nhỏ: 3 endpoint + 1 DTO.
> Cổng: Gate 1 (#89: có câu hỏi/BREAKING-có-câu-hỏi → dừng chờ người) → Gate 2. Tuần tự S1 (BE) → S2 (FE).
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** JS của trình duyệt không đọc được refresh token nữa (chống XSS đánh cắp phiên dài 30 ngày).
Phụ: F5 không mất phiên; có nút Đăng xuất thật (thu hồi token phía server).
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) · **Spec:** `backend/docs/modules/identity.md`
· **Quyết định liên quan:** #3 (rotation), #27/#31 (audience), #91 (prefix `/api`), #89 (Gate 1) · **#101** (mới)

## 1. Yêu cầu (đã chốt với người 2026-10-18)
- Người dùng: mọi người đăng nhập qua Portal (và sau này Main/Shop site — cùng endpoint).
- Điểm chạm: `POST /api/auth/login`, `POST /api/auth/refresh-token`, **mới** `POST /api/auth/logout`; Portal login + route guard + nút Đăng xuất.
- Xong nghĩa là: refresh token chỉ còn trong cookie `HttpOnly; Secure; SameSite=Lax; Path=/api/auth` (host-only);
  response/body không bao giờ chứa refresh token; F5 ở Portal giữ phiên; Đăng xuất thu hồi token + xoá cookie.
- **Không làm:** `apps/web` SSR (không giữ refresh token) · "remember me" · đổi thời hạn 30 ngày · đổi cơ chế rotation/reuse-detection · grace window cho đa tab (xem R3, nợ).

## 2. Hiện trạng (khảo sát 2026-10-18)
| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Rotation + phát hiện dùng lại + thu hồi nhóm | ✅ | [RefreshTokenHandler.cs:25-85](../../../backend/src/Vsite.Application/Identity/Auth/Commands/RefreshToken/RefreshTokenHandler.cs#L25-L85) | Giữ nguyên, không sửa |
| Login tạo token, trả trong `AuthTokenResult` | ✅ | [LoginHandler.cs:115-129](../../../backend/src/Vsite.Application/Identity/Auth/Commands/Login/LoginHandler.cs#L115-L129) | Handler giữ nguyên; endpoint tách cookie ra |
| Endpoint login/refresh trả body có refresh token | ✅ | [IdentityEndpoints.cs:49-61](../../../backend/src/Vsite.Api/Identity/IdentityEndpoints.cs#L49-L61) | Cần sửa |
| `RefreshTokenRequest` nhận token từ body | ✅ | [IdentityEndpoints.cs:109](../../../backend/src/Vsite.Api/Identity/IdentityEndpoints.cs#L109) | Xoá |
| Logout | ❌ | — | Thêm |
| CORS gửi cookie | ❌ | [Program.cs:128-132](../../../backend/src/Vsite.Api/Program.cs#L128-L132) | Thêm `AllowCredentials` (dev) |
| FE giữ refresh token trong biến JS | ✅ | [axios-instance.ts:27-35,66-85](../../../packages/api-sdk/src/mutator/axios-instance.ts#L27-L85), [session-store.ts:22-31](../../../apps/portal/src/stores/session-store.ts#L22-L31) | Gỡ |
| Route guard chỉ nhìn cờ `isAuthenticated` | ✅ | [_authenticated.tsx:10-17](../../../apps/portal/src/routes/_authenticated.tsx#L10-L17) | F5 = mất phiên hôm nay; cần khôi phục qua cookie |
| Nút Đăng xuất chỉ clear state | ✅ | [_authenticated.tsx:42-51](../../../apps/portal/src/routes/_authenticated.tsx#L42-L51) | Gọi thêm `logout` |

### Phát hiện khi khảo sát
- Test chạm refresh token cần sửa theo contract mới: [refresh-relative-base.test.ts:21-41](../../../packages/api-sdk/tests/refresh-relative-base.test.ts#L21-L41), `packages/api-sdk/tests/api-client.test.ts`, `apps/portal/src/test/shop-onboarding-flow.test.tsx`, `apps/portal/src/test/shop-list-logo.test.tsx`, `backend/tests/IntegrationTests/Identity/MembershipLifecycleTests.cs` (7 chỗ). Test Media chỉ dính `db.RefreshTokens` — thực thi viên grep lại để chắc.
- Backend **chưa bật `UseForwardedHeaders`** (xem chú thích [axios-instance.ts:131-135](../../../packages/api-sdk/src/mutator/axios-instance.ts#L131-L135)) → sau Caddy `Request.IsHttps` là `false`. **Không** dùng `IsHttps` quyết định `Secure`; dùng môi trường (xem §4).
- Cookie host-only mỗi domain một bản → khớp token tách theo audience theo Host; nhưng `/auth/refresh-token` hiện **không** đối chiếu audience của token với Host của request ([RefreshTokenHandler.cs:25-85](../../../backend/src/Vsite.Application/Identity/Auth/Commands/RefreshToken/RefreshTokenHandler.cs#L25-L85)). Host-only cookie che được trong thực tế; không sửa ở task này, ghi nhận.

## 3. Flow
### Flow 1: Đăng nhập
1. FE `POST /api/auth/login` (`withCredentials`) ✅ [login.tsx:31](../../../apps/portal/src/routes/login.tsx#L31)
2. Handler tạo access + refresh ✅ [LoginHandler.cs:112-129](../../../backend/src/Vsite.Application/Identity/Auth/Commands/Login/LoginHandler.cs#L112-L129)
3. Endpoint set cookie `vsite_rt` (value = refresh thô, `Expires` = `RefreshTokenExpiresAt`), trả `{ accessToken, accessTokenExpiresAt }` ❌ làm mới
4. FE lưu access token vào memory ✅ [session-store.ts:22-26](../../../apps/portal/src/stores/session-store.ts#L22-L26) (bỏ phần refresh)
- Lỗi: 401 sai credential / 429 lockout giữ nguyên, **không** set cookie.

### Flow 2: Refresh (401 giữa chừng, hoặc F5)
1. Interceptor 401 → refresh ✅ [axios-instance.ts:87-114](../../../packages/api-sdk/src/mutator/axios-instance.ts#L87-L114); thêm: refresh lúc khởi động khi store rỗng ❌
2. FE `POST /api/auth/refresh-token` không body, `withCredentials` 🟡 (hiện có body)
3. Endpoint đọc cookie `vsite_rt`; **thiếu cookie → 401** (`UnauthorizedAccessException`, đã map [UnauthorizedAccessExceptionHandler.cs](../../../backend/src/Vsite.Api/ExceptionHandling/UnauthorizedAccessExceptionHandler.cs)) ❌
4. Handler xoay vòng ✅; endpoint set cookie mới, trả access token ❌
- Lỗi: token sai/hết hạn/reuse → 401, FE `onSessionExpired` → `/login` ✅. Cookie cũ không xoá (xem §12 G3).

### Flow 3: Đăng xuất
1. Bấm nút → FE `POST /api/auth/logout` (`withCredentials`), rồi clear state + về `/login` ❌
2. Endpoint đọc cookie, thu hồi **token đó** (nếu có và chưa thu hồi), xoá cookie (cùng `Path`), trả **204 luôn** (idempotent, không lộ token có tồn tại không) ❌
- Lỗi mạng khi gọi logout: FE vẫn clear state (cookie còn sống tới hết hạn — chấp nhận).

## 4. Đã chốt trong session lập plan
| # | Chốt |
|---|---|
| #101 *(đề xuất, chờ người đồng ý)* | **Refresh token chỉ sống trong cookie `HttpOnly; Secure; SameSite=Lax; Path=/api/auth`, host-only; không bao giờ trong body/response/JS.** Có `POST /api/auth/logout` thu hồi + xoá cookie. |
| — | Cookie tên `vsite_rt`. `Secure = !Environment.IsDevelopment()` (không dùng `IsHttps`, xem Phát hiện). `Expires` = `RefreshTokenExpiresAt`. |
| — | Application giữ nguyên `AuthTokenResult` (nội bộ, có refresh). Endpoint map sang DTO công khai mới `AccessTokenResponse(AccessToken, AccessTokenExpiresAt)` đặt cạnh các record request ở cuối [IdentityEndpoints.cs](../../../backend/src/Vsite.Api/Identity/IdentityEndpoints.cs). operationId `login`/`refreshToken` giữ; thêm `logout`. |
| — | `/auth/refresh-token` không còn body, bỏ `ProducesValidationProblem(422)` (không còn input để validate). `RefreshTokenRequest` xoá. |
| — | FE: bỏ `setRefreshToken`/biến `refreshToken`; `axiosInstance.defaults.withCredentials = true` (chỉ instance Portal, **không** `createApiClient` SSR); export `restoreSession()` (refresh một lần, memo) cho route guard. |
| — | CORS dev thêm `AllowCredentials()` (origin đã liệt kê tường minh nên hợp lệ). Prod cùng origin sau Caddy (`/api`, #91) không cần CORS. |

## 6. Ràng buộc toàn cục
- Tenant #21: không có `ShopId` từ request. Logout/refresh chỉ tra `RefreshToken` theo hash; scope lấy từ dòng DB, không từ client.
- #19: `logout` khai đủ `Produces(204)`; có `.WithName("logout")`. `ApiRoutePrefixTests` phải xanh.
- `packages/api-sdk/src/generated/**` chỉ đổi bằng `pnpm gen:api`. Contract chỉ qua `.staging` + `promote.mjs`.
- Cookie không bao giờ được log. Không đặt `Domain`.

## 7. Review focus — lỗi dễ lọt khi spec im lặng
| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Response login/refresh **và** OpenAPI còn lộ `refreshToken` | Không field nào tên đó | T1 (assert body), T4 (contract-diff) |
| R2 | Refresh thiếu cookie / cookie rác | 401 `UNAUTHORIZED`, không 500, không set cookie | T1 |
| R3 | **Hai tab cùng refresh một lúc** — chung một cookie, tab thứ hai gửi token vừa bị xoay → reuse-detection thu hồi cả nhóm → cả hai tab bị đăng xuất | Chưa xử lý (nợ, §12 G2); FE chỉ chống đua trong **một** tab bằng `refreshPromise` ([axios-instance.ts:64,98](../../../packages/api-sdk/src/mutator/axios-instance.ts#L64)) | F1 ghi nhận bằng comment |
| R4 | Logout với token đã revoke / không tồn tại / không có cookie | 204, xoá cookie, không ném, không thu hồi nhóm | T2 |
| R5 | Logout chỉ thu hồi đúng token đó, không đụng token của audience/shop khác của cùng user | Đúng | T2 |
| R6 | Cookie flags: `HttpOnly`, `SameSite=Lax`, `Path=/api/auth`, không `Domain`, `Secure` đúng môi trường | Assert header `Set-Cookie` | T1 |
| R7 | F5 trên Portal: guard phải **chờ** `restoreSession()` rồi mới quyết redirect; không nhấp nháy `/login` | Vào thẳng route con nếu cookie hợp lệ | F2 |

## 8. Sơ đồ file
```
backend/src/Vsite.Api/Identity/IdentityEndpoints.cs        ← sửa login/refresh, thêm logout, DTO
backend/src/Vsite.Api/Identity/RefreshTokenCookie.cs       ← MỚI: Append/Read/Delete (một chỗ giữ cờ cookie)
backend/src/Vsite.Application/Identity/Auth/Commands/Logout/{LogoutCommand,LogoutHandler}.cs  ← MỚI
backend/src/Vsite.Api/Program.cs                           ← CORS AllowCredentials
backend/tests/IntegrationTests/Identity/…                  ← sửa + test mới
contracts/openapi/identity.v1.json                         ← CHỈ qua promote
packages/api-sdk/src/mutator/axios-instance.ts             ← sửa; tests/ sửa
apps/portal/src/{stores/session-store.ts,routes/login.tsx,routes/_authenticated.tsx,test/*}
backend/docs/modules/identity.md · DesignIdeal/DECISIONS.md (#101) · Docs/tasks/IDENTITY-002/changelog.md
```

## 9. Session S1: Backend
### T1: Cookie helper + login/refresh trả cookie
`RefreshTokenCookie` (const `Name="vsite_rt"`, `Path="/api/auth"`): `Append(ctx, token, expiresAt, isDev)`, `Read(ctx)`, `Delete(ctx)`.
Login/refresh nhận `HttpContext`, trả `AccessTokenResponse`; refresh đọc cookie, thiếu → `UnauthorizedAccessException`.
- [ ] Test trước (`IdentityEndpoints` integration, tạo file mới cạnh `MembershipLifecycleTests.cs`): login set cookie đủ cờ (R6) + body không có `refreshToken` (R1); refresh đọc cookie, xoay vòng, cookie mới khác cũ; thiếu/rác → 401 (R2); reuse cookie cũ → 401 + thu hồi nhóm (giữ hành vi #3)
- [ ] FAIL → hiện thực → PASS (`dotnet test backend/vsite.sln --filter Identity`)
- [ ] Sửa `MembershipLifecycleTests.cs` theo shape mới
- [ ] Commit `feat(identity): refresh token qua cookie httpOnly (#101)`

### T2: `POST /auth/logout`
`LogoutCommand(string? RefreshToken)` → handler tra theo hash, set `RevokedAt` nếu có và chưa revoke; **không** thu hồi nhóm.
- [ ] Test trước: cookie hợp lệ → 204 + token revoked + `Set-Cookie` xoá (R5); token đã revoke / không tồn tại / không cookie → 204 (R4); refresh bằng token vừa logout → 401
- [ ] FAIL → hiện thực → PASS · `ApiRoutePrefixTests` xanh
- [ ] Commit `feat(identity): POST /auth/logout thu hồi refresh token + xoá cookie (#101)`

### T3: CORS dev cho cookie
- [ ] Thêm `.AllowCredentials()` ở [Program.cs:130-131](../../../backend/src/Vsite.Api/Program.cs#L130-L131); kiểm tay bằng Portal :5173 → :5270 ở F2 (không có test tự động hợp lý)
- [ ] Commit chung T1 hoặc riêng `chore(api): CORS dev AllowCredentials`

### T4: Kết thúc S1
- [ ] `dotnet test backend/tests/ArchitectureTests` + `dotnet test backend/vsite.sln` (nợ Docker → `Docs/DOCKER-TEST-DEBT.md`)
- [ ] `pnpm contract:export identity` → skill `contract-sync` → `contract-diff.md` → **Gate 1: dừng chờ người** (có BREAKING + REMOVED `refreshToken`)
- [ ] `changelog.md` + cập nhật `backend/docs/modules/identity.md` + ghi `#101` vào `DesignIdeal/DECISIONS.md` (sau khi người đồng ý) · banner `STATUS` nếu `03` bị lệch

## 10. Session S2: Frontend (mở SAU Gate 1)
### F1: api-sdk mutator
- [ ] `pnpm gen:api` (không sửa tay generated)
- [ ] Sửa test trước: [refresh-relative-base.test.ts](../../../packages/api-sdk/tests/refresh-relative-base.test.ts) (body `undefined`, config `{ baseURL: '/', withCredentials: true }`), `api-client.test.ts`; thêm test `restoreSession()` (thành công / 401 → `undefined`, gọi một lần dù gọi nhiều)
- [ ] Hiện thực: gỡ `refreshToken`/`setRefreshToken`, refresh không body + `withCredentials`, `axiosInstance.defaults.withCredentials = true`, export `restoreSession`
- [ ] Commit `feat(api-sdk): refresh qua cookie, restoreSession (#101)`

### F2: Portal
- [ ] Test trước: `shop-onboarding-flow`/`shop-list-logo` (mock login không còn `refreshToken`); guard: store rỗng + restore OK → vào route (R7); restore 401 → `/login`; nút Đăng xuất gọi `logout` rồi clear, và vẫn clear khi logout lỗi
- [ ] `session-store.ts` bỏ refresh; `login.tsx` chỉ `setSession({ accessToken })`; `_authenticated.tsx` `beforeLoad` async dùng `restoreSession()`; nút Đăng xuất dùng `useLogout`
- [ ] Sửa comment lỗi thời ở [_authenticated.tsx:32-35](../../../apps/portal/src/routes/_authenticated.tsx#L32-L35) (F5 không còn mất phiên)
- [ ] **Smoke chạy app thật** (workflow §8): login → F5 vẫn ở trong → Đăng xuất → F5 về `/login`; devtools: cookie `HttpOnly`, `document.cookie` không thấy
- [ ] Commit `feat(portal): khôi phục phiên qua cookie + logout thật (#101)`

## 11. Đóng task (trước Gate 2)
- [ ] Integration tắt MSW · `change-reviewer` (model mạnh nhất — chạm auth/bảo mật/contract) · `review.md` kèm "Ma sát quy trình"

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)
| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | Cấp số **#101** cho quyết định cookie | #100 là số cuối hiện có ([DECISIONS.md:142](../../../DesignIdeal/DECISIONS.md#L142)) | Đổi số, cập nhật tham chiếu |
| G2 | **Không** làm grace window cho đa tab (R3); chấp nhận hai tab đua refresh có thể đăng xuất cả hai | Ngoài phạm vi "chuyển sang cookie"; grace window nới reuse-detection của #3 — là quyết định bảo mật của người | Thêm task riêng: cho phép dùng lại token vừa xoay trong ~10s, trả cùng token mới |
| G3 *(đã đảo — xem changelog "Ruling khi review")* | Refresh 401 **không** xoá cookie phía server (middleware xử lý lỗi có thể xoá header); cookie cũ vô hại vì đã revoke/hết hạn | Đơn giản, đủ an toàn | Xoá cookie trong exception handler của refresh |
| G4 | **CSRF:** chỉ dựa `SameSite=Lax` + `Path=/api/auth`, không thêm header chống CSRF. Lưu ý: `{slug}.vsite.vn` (site shop, nội dung shop kiểm soát) cùng *site* với `admin.vsite.vn` nên cookie Lax vẫn được gửi — một trang shop độc hại có thể ép logout/ép xoay token (không đọc được response). Tác hại: gây phiền (đăng xuất), không lấy được token | Người chọn Lax; thêm header là việc nhỏ nếu cần | Yêu cầu header tuỳ biến (vd. `X-Requested-With`) trên refresh/logout → buộc preflight CORS |
| G5 | Endpoint `logout` là `AllowAnonymous` (đọc cookie, không cần access token còn hạn) | Phải đăng xuất được kể cả khi access token hết hạn | Thêm `RequireAuthorization()` |
| G6 | Contract-diff sẽ hiện BREAKING nên Gate 1 dừng chờ người (không tự promote) | #89 | — |

## 13. Verification tổng
- `dotnet test backend/tests/ArchitectureTests` xanh; `dotnet test backend/vsite.sln` xanh (hoặc nợ ghi `DOCKER-TEST-DEBT.md`)
- `pnpm gen:api && pnpm build && pnpm test` xanh; `pnpm check:docs` xanh
- `grep -rn "refreshToken" apps packages/api-sdk/src --include=*.ts* ` chỉ còn tên hàm generated `refreshToken()` (operationId), không còn field/biến token
- Smoke tay Flow 1–3 trên app thật, cookie `HttpOnly` thấy trong devtools, `document.cookie` rỗng
