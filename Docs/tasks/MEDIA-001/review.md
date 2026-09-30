# MEDIA-001 — review cho Gate 2

Nhánh `feature/MEDIA-001`, base `6330ba8`, 49 commit, 235 file, +19.4k/−0.1k dòng. Chưa push, chưa merge.
Phạm vi: Bước 4 (`MediaAsset` + pipeline ảnh + Media Library + logo + `/media/*`) và các task con
`MEDIA-001-D2` (gộp schema OpenAPI trùng), `-D3` (endpoint `derivatives`), `-D4` (`logoUrl` trên
`ShopDto`/`ShopSummaryDto`).

## Tự động

Chạy lại độc lập ở commit cuối (`d9719ab`), Docker bật:

- [x] **BE test:** IntegrationTests **273 pass / 0 fail / 1 skip** (test skip có từ trước: ràng buộc `Hosted → ExternalOnly` chờ module Marketplace) · ArchitectureTests **8/8** · ComponentSchemaTests **12/12** · `dotnet build` **0 warning / 0 error**
- [x] **FE test:** `pnpm test` **7/7 tác vụ** (portal 48, builder-components 88, builder-renderer 3, shared 2, web 1). Chạy nhiều lần liên tiếp đều xanh; `git status` sạch sau khi chạy
- [ ] **Integration (MSW tắt, API thật): CHƯA CHẠY.** FE chỉ được kiểm bằng MSW; endpoint BE được kiểm qua HTTP thật bằng Testcontainers. Chưa ai chạy trình duyệt + backend thật. Xem "Điểm cần anh quyết" #1
- [x] **contract-sync sau cùng:** `check-no-drift` OK, không còn diff ngoài contract đã duyệt. sha256 của `media.v1.json` và `shop.v1.json` khớp `contract.lock` và `brief.md`
- [x] **check:docs + gen:doc-index --check:** PASS (còn 13 cảnh báo đường dẫn không thuộc phạm vi task, có từ trước)
- [x] **check-additive:** OK (`registry.lock.json` chỉ thêm `imagePresets` ở `ServiceGrid.source`)
- [x] **`Docs/DOCKER-TEST-DEBT.md`:** rỗng. Mọi test cần Docker đã chạy thật và pass

## change-reviewer

Review toàn nhánh (opus, độc lập, chỉ đọc), rồi một lượt re-review có phạm vi cho đợt sửa cuối.

**Critical (đã sửa, đã xác minh):**
- **C1** Build .NET hỏng trên máy mới clone và ở hai job CI (`architecture-tests`, `contract-check`): `Vsite.Api.csproj` liên kết `derivative-presets.json` nằm trong thư mục `generated/` bị gitignore. → CI chạy `gen:registry` ở cả hai job, thêm target MSBuild báo lỗi có hướng dẫn, ghi vào `backend/CLAUDE.md`. Không commit artifact.
- **C2** Kiểm quyền Owner dùng `IgnoreQueryFilters()` mà thiếu `!IsDeleted` (#21) → membership đã xoá mềm vẫn được coi là Owner. → Sửa 4 chỗ (`ShopOwnershipService`, `UserShopMembershipService`, `UpdateShopHandler`, `ListShopsHandler`) kèm test Postgres thật.

**Important (đã sửa):**
- `ListShopsHandler` còn thiếu `!IsDeleted` phía `Shop` và `Role` của câu join (re-review bắt được; agent trước bỏ qua vì hiểu sai `IgnoreQueryFilters` tắt filter cho cả câu query). Commit `d9719ab`, test đỏ→xanh.

**Warning:** W1–W8, W10, W11 đã sửa (tài liệu mâu thuẫn, comment cũ, UI báo sai, nút xoá không mở được từ màn nào, tham chiếu tới file không có trong repo…). **W9** (giả định A1–A11 chưa có dòng `#N`) chưa quyết → "Điểm cần anh quyết" #2.

**Suggestion đã làm:** crop ảnh 1px không còn ra 500, test slug `media` → 422, assert `SourceAssetId` trên Postgres, hướng dẫn HEIC khi client tự chặn, timeout test codegen.

**Phát hiện thêm ngoài danh sách (đã sửa):** `turbo.json` khai `gen:api.outputs = ["src/**"]` nên turbo khôi phục cả các file **viết tay** của `api-sdk` (`index.ts`, `mocks.ts`) từ cache cũ, lặng lẽ xoá sửa đổi tay. Đã thu hẹp về `src/generated/**` và bỏ theo dõi 4 file `tsbuildinfo` (commit `c2eb5c4`).

**Lưu ý trung thực:** hai commit cuối (`c2eb5c4` sửa turbo, `d9719ab` sửa ListShops) được tạo sau diff mà reviewer đã đọc. Tôi tự kiểm (đọc code, test đỏ→xanh, chạy lại toàn bộ suite), nhưng không có reviewer độc lập nào đọc riêng hai commit này.

## Nợ kỹ thuật cố ý + task theo dõi

Đầy đủ ở `Docs/tasks/MEDIA-001/changelog.md` §"Chưa làm xong" (16 mục) và `DesignIdeal/00-INDEX.md` §4. Nổi bật:

| Việc | Ở đâu theo dõi |
|---|---|
| **Nghiệm thu tay tiêu chí dừng `08` §9** trên API + UI thật (chưa ai chạy) | 00-INDEX §4 |
| `IMediaReferenceValidator` chưa nối vào handler nào → test bắt buộc 6, 7 mới chứng minh ở tầng service; **#71, #77 vẫn 📐** | Bước 5 |
| `contract:diff` **mù với endpoint trả về mảng** (`GET /shops`, `…/derivatives`, `…/assets?ids=`) → đổi schema ở đó hiện là "không đổi" | task riêng (00-INDEX §4) |
| Audit `UserShops.IgnoreQueryFilters()` thiếu `!IsDeleted` **và** thiếu `Status == Active` ở ~8 handler Identity (Login, Refresh, Register, ChangePassword, ForgotPassword, ResetPassword, VerifyEmail) | task riêng |
| Test đột biến `gen-registry.test.ts` sửa tạm manifest đã commit; `finally` không chạy nếu worker bị kill → từng làm hỏng `service-grid.manifest.ts` | task riêng |
| Job dọn file mồ côi, bản TS của hàm `thumb_`/`fthumb_`, khung crop theo tỉ lệ preset, lỗi #19 cũ (400 binding và 415 endpoint JSON thiếu `error_code`) | changelog + 00-INDEX §4 |
| `dev-registry.tsx` là code tạm (xoá ở Bước 5) | changelog |

## Điểm cần anh quyết trước khi merge

1. **Nghiệm thu tay `08` §9 trên API thật** (bắt buộc theo workflow trước Gate 2). `dev-registry` có ô nhập Shop ID để chạy. Kiểm:
   - upload vào Hero ở **cả hai chế độ** → ảnh đúng kích thước preset;
   - chọn ảnh Library cho một ô Gallery → sinh clone độc lập (id khác bản Library);
   - upload logo → có `logoUrl`, tải lại trang vẫn hiện đúng;
   - `/media/…` đọc được ở `admin.vsite.local` **và** `{slug}.vsite.local:3000`;
   - 413 thật ở Kestrel khi file vượt ngưỡng (chưa ai kiểm; chỉ có test dịch lỗi).
2. **W9:** giả định A1–A11 (đã duyệt ở Gate 1) có cần dòng `#N` ở `DECISIONS.md` không? **Khuyến nghị: không**, vì A6 → #72 và A11 → `08` §4 đã định nghĩa đúng hành vi, và bảng ở `contract-diff.md` đã ánh xạ chúng. Chỉ cấp số nếu anh muốn khoá A6/A11 thành quyết định độc lập.
3. **Task theo dõi:** có mở ngay các task riêng cho (a) `contract:diff` mù mảng, (b) audit `IgnoreQueryFilters` ở handler Identity, (c) test đột biến `gen-registry` không? (a) và (b) là rủi ro thật, không chỉ dọn dẹp.
4. **Cách merge:** 49 commit, trong đó 19 commit `fix`. Squash hay giữ lịch sử? Chưa push, chưa mở PR.
5. **`config/reserved-routes.json`** dòng `"media"` thụt lề bằng tab, lệch phần còn lại (file bị hook bảo vệ, chỉ người sửa được; thuần thẩm mỹ).
6. **Test S3 chạy trên LocalStack `4.0.3`** vì image MinIO không còn công khai. Nếu LocalStack chuyển sang yêu cầu token thì phải đổi lại — đừng nâng tag mà không chạy lại test no-overwrite.

## Quyết định tôi đã tự đặt trong lúc thực thi

Ghi ở đây để không mất khi chat kết thúc (workflow rule #8). Cột cuối là hậu quả nếu sai.

| # | Quyết định | Hậu quả nếu sai |
|---|---|---|
| 1 | Gộp T0.1 + T0.2 (hai sửa config nhỏ) vào một lần giao việc | Không |
| 2 | Dùng `z.record(z.string(), …)` thay dạng một tham số trong plan | Không |
| 3 | Thêm `media` vào reserved routes để **người** làm (hook bảo vệ, #24); test để skip tới lúc đó | Đã xong, anh làm |
| 4 | Test Testcontainers viết đủ nhưng chỉ cần compile khi không có Docker; gộp một mục nợ Docker | Đã chạy thật khi có Docker: mọi lớp pass |
| 5 | Ảnh mẫu test sinh một lần bằng script rồi commit; `sample.heic` chỉ có header thật | Test EXIF chạy trên JPEG tổng hợp, không phải ảnh điện thoại |
| 6 | `changelog.md` viết một lần lúc đóng task, không viết theo từng task con | Không |
| 7 | Nâng `DeleteAsync` idempotent từ Minor lên bắt buộc sửa (rollback R4 dựa vào nó) | Không |
| 8 | Tạo `ImagePaths.ValidateKey` sớm ở T2 (plan đặt ở T3) | Không |
| 9 | Test 9 (clone vẫn render sau khi xoá Library) kiểm qua `IObjectStorage`, phần HTTP ở T9 | Không |
| 10 | Giữ mục nợ Docker mà implementer T5 tự thêm; T10 gộp lại | Không |
| 11 | Nâng lên bắt buộc sửa: 413/415 trả ProblemDetails, writer xoá danh sách key sau khi lưu xong | Thêm vài thay đổi nhỏ |
| 12 | Chuyển test 10 nhánh logo từ T6 sang T7 (lúc đó chưa có endpoint logo) | Không |
| 13 | Nâng lên bắt buộc sửa: resolve preset trước khi ghi file, đếm số lần `SaveChanges` | Thêm vài thay đổi nhỏ |
| 14 | Hai finding "Important" của T8 (nợ Docker, changelog) không mở vòng sửa vì đã có kế hoạch ở T10/lúc đóng | Không |
| 15 | T10 (chỉ tài liệu + contract-sync) không qua vòng review task, vì anh phàn nàn chậm | Lỗi tài liệu chờ anh bắt ở Gate 1 |
| 16 | 415 chỉ gắn `MEDIA_MULTIPART_REQUIRED` cho route multipart; 415 khác giữ nguyên | Endpoint JSON vẫn thiếu `error_code` (đã ghi nợ #19) |
| 17 | Gộp `MediaAssetDto2` ở backend bằng transformer (theo bảng Gate 1), và hỏi lại anh trước khi promote lại | Thêm một vòng duyệt |
| 18 | Tách hàm `mediaUrl()` dùng chung (một nơi biết cấu trúc URL ở FE) | Thêm một symbol phải giữ isomorphic |
| 19 | Lỗ hổng "logo không tra được" là lỗ hổng contract, báo anh quyết, không tự sửa trong vòng lặp | Anh chọn endpoint mới (D3) |
| 20 | Hoãn các Minor của D3 (khoá sắp xếp phụ, `?preset=` rỗng…) | Không ảnh hưởng tenant |
| 21 | Thiết kế D4 ban đầu: trả key tương đối + port `IShopLogoReader` (Shop khai báo, Media cài đặt); tôi tự cấp `#88` | **Anh đã đổi** sang `logoUrl` có `/media/`; #88 viết đè |
| 22 | Nâng lên sửa: test cho điều kiện `SourceAssetId` (shop đổi logo không được nhận ảnh cũ) | Thêm một commit nhỏ |
| 23 | Contract đổi hình dạng sau khi anh xem → **không** promote theo lần duyệt cũ, xin xác nhận lại | Thêm một lượt hỏi |
| 24 | Đợt sửa cuối: C1 sửa bằng CI + lỗi MSBuild rõ ràng (không commit artifact); C2 chỉ sửa 4 chỗ chặn Media/Shop, **không** động vào ~8 handler Identity; chọn các Suggestion rẻ và thật; **không** tự cấp số cho A1–A11 | Handler Identity còn lỗ hổng (đã ghi nợ) |
| 25 | `git checkout` khôi phục `api-sdk/src/index.ts` (sửa đổi chưa commit không rõ nguồn) | Không; xác minh `tsc` sạch |
| 26 | Sửa `turbo.json` + bỏ theo dõi `tsbuildinfo` ngay, ngoài phạm vi ban đầu | Turbo chạy lại `gen:api` nhiều hơn (an toàn hơn) |
| 27 | Sửa `ListShops` phía Shop/Role; hoãn export `ImagePickerControl` ở route dev; ghi 2 việc theo dõi | Không |
| 28 | Các implementer không sửa `DOCKER-TEST-DEBT.md`; T10 gộp | Không |
