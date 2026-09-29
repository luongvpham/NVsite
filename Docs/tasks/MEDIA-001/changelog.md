# MEDIA-001 — changelog: lệch so với thiết kế

File này còn trống trước T5 (T1–T4 không để lại changelog riêng — nợ tài liệu có sẵn từ trước, không
thuộc phạm vi task này). Từ đây ghi tiếp theo từng task chạm code của module `Media`.

**Đọc kèm:** `Docs/tasks/MEDIA-001/contract-diff.md` (Gate 1, T5 — 2 endpoint upload).

---

## T5 — Upload vào slot (hai chế độ) và upload vào Media Library

### Lệch có chủ đích (giữ nguyên, không phải bug)

#### 1. Multipart binding: đọc `HttpRequest.ReadFormAsync` thủ công, không dùng `[FromForm]` complex-type

- **Brief nói gì:** "dùng `[FromForm]` request type hoặc explicit IFormFile parameters".
- **Thực thi:** endpoint nhận `HttpRequest request` làm tham số, tự gọi
  `request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>().MaxRequestBodySize = 11MB`
  RỒI `await request.ReadFormAsync(ct)`.
- **Nguyên nhân:** Minimal API bind tham số complex-type (`[FromForm]`) TRƯỚC khi endpoint filter
  chạy — set giới hạn dung lượng trong filter sẽ set QUÁ TRỄ, body đã đọc hết. Đọc form thủ công
  ngay đầu handler đảm bảo thứ tự đúng: set giới hạn → đọc body lần đầu.
- **Vẫn giữ khai báo OpenAPI:** `.Accepts<UploadToSlotForm>("multipart/form-data")` /
  `.Accepts<UploadToLibraryForm>(...)` — hai class `UploadToSlotForm`/`UploadToLibraryForm` ở
  `MediaEndpoints.cs` CHỈ dùng cho schema tài liệu, không phải tham số bind thật.
- **413 giờ có mapping đúng (sửa sau review):** vượt `MaxRequestBodySize` khiến Kestrel ném
  `BadHttpRequestException` (StatusCode nội bộ = 413) NGAY TRONG `ReadFormAsync` — không phải
  `AppException` nên `AppExceptionHandler` bỏ qua, mặc định ASP.NET Core trả 413 KHÔNG có body.
  Thêm `Vsite.Api.ExceptionHandling.BadHttpRequestExceptionHandler` (chỉ bắt đúng case
  `StatusCode == 413`, để nguyên các `BadHttpRequestException` khác cho framework xử lý mặc định) →
  ProblemDetails `error_code = MEDIA_FILE_TOO_LARGE`. Test không cần Docker:
  `BadHttpRequestExceptionHandlerTests` (gọi thẳng handler với `DefaultHttpContext`) — xác nhận PHẦN
  DỊCH LỖI đúng, KHÔNG xác nhận TestServer/Kestrel thật sự enforce limit ở ngưỡng nào (cần Docker
  để gửi request >11MB qua `MediaApiFactory` thật — chưa làm, xem "Chưa làm xong" bên dưới).
- **415 cho request không phải multipart (sửa sau review):** trước đây `ReadFormAsync` trên request
  không multipart ném `InvalidOperationException` → rơi xuống 500 mặc định, không ProblemDetails/
  error_code (#19). Thêm `MediaEndpoints.RequireMultipart` (check `request.HasFormContentType` TRƯỚC
  khi đọc form) ném `Vsite.Domain.Exceptions.UnsupportedMediaTypeException("MEDIA_MULTIPART_REQUIRED", …)`
  — `AppExceptionHandler` xử lý (đã thêm nhánh 415 vào `TitleFor`). Test:
  `AppExceptionHandlerTests` (không Docker) + `UploadEndpointTests.SlotUpload_without_multipart_content_type_returns_415_MEDIA_MULTIPART_REQUIRED` (Docker).

#### 2. (ĐÃ SỬA sau review) "Preset lạ" giờ trả đúng `error_code: MEDIA_UNKNOWN_PRESET` — không còn qua FluentValidation

- **Trạng thái CŨ (sai, bản T5 gốc):** `UploadToSlotValidator` check preset tồn tại bằng
  `RuleFor(x => x.Preset).Must(...)` — lỗi đi qua `ValidationBehavior` chung, LUÔN trả
  `error_code = "VALIDATION_ERROR"` bất kể message, không khớp brief.
- **Sửa:** bỏ hẳn rule `Must(...)` khỏi validator (chỉ còn `NotEmpty()`). Check preset tồn tại chuyển
  hẳn sang `UploadToSlotHandler` (đã có sẵn từ bản gốc, chỉ là bị validator che mất vì
  `ValidationBehavior` chạy TRƯỚC handler) — handler tự `throw new UnprocessableException("MEDIA_UNKNOWN_PRESET", ...)`
  khi `IImagePresetCatalog.TryGet` trả false. Test: `UploadHandlerTests.UploadToSlot_with_unknown_preset_throws_Unprocessable_MEDIA_UNKNOWN_PRESET`
  (không Docker) + `UploadEndpointTests.SlotUpload_with_unknown_preset_returns_422_MEDIA_UNKNOWN_PRESET` (Docker).
- **`FocalX`/`FocalY` ngoài [0,1] VẪN đi qua FluentValidation** (`error_code = VALIDATION_ERROR`) —
  brief không đòi mã riêng cho case này, chỉ preset lạ mới cần `MEDIA_UNKNOWN_PRESET` cụ thể.

#### 3. `MediaAssetWriter` + `TimeProvider` đăng ký ở `AddInfrastructure()`, dù `MediaAssetWriter` là type của Application

- **Nguyên nhân:** backend/CLAUDE.md chốt `AddInfrastructure()` là điểm wiring DI DUY NHẤT — dù
  `MediaAssetWriter` (Application/Media) không phải implementation của interface Infrastructure,
  vẫn đăng ký ở đó (section `AddMediaModule` mới) để giữ đúng quy ước "một điểm wiring".
- Không có clock abstraction sẵn có trong codebase (grep `TimeProvider` ra rỗng trước T5) — dùng
  thẳng `TimeProvider.System` làm singleton, `MediaAssetWriter` nhận qua constructor injection thay
  vì gọi `DateTimeOffset.UtcNow` trực tiếp, để test thay được bằng `FakeTimeProvider` nếu cần sau này.

#### 4. `MediaAssetWriter._writtenKeys` không clear sau `SaveChangesAsync` thành công (bug, đã sửa sau review)

- **Bug:** writer scoped, `_writtenKeys` cộng dồn qua nhiều lần gọi `WriteXxxAsync` trong CÙNG scope
  nhưng KHÔNG BAO GIỜ được xoá sau khi `SaveChangesAsync` commit thành công. Hai thao tác tuần tự
  trong cùng scope (vd. test gọi `UploadToLibraryHandler` rồi `UploadToSlotHandler` với cùng
  `MediaAssetWriter`) — nếu thao tác THỨ HAI fail, rollback best-effort sẽ xoá NHẦM cả file của thao
  tác THỨ NHẤT đã commit thành công từ trước.
- **Sửa:** `SaveChangesAsync` clear `_writtenKeys` ngay sau khi `db.SaveChangesAsync` thành công;
  `CleanupWrittenKeysAsync` cũng tự clear sau khi dọn xong (tránh double-delete lặp vô ích nếu writer
  còn được dùng tiếp sau một lần fail).
- Test: `UploadHandlerTests.Writer_does_not_delete_files_of_a_previously_committed_operation_when_a_later_save_fails`
  (không Docker) — verify bằng RED/GREEN thật (revert fix → test fail đúng dự đoán, khôi phục → pass).

### Chưa làm xong (nợ kỹ thuật, không phải lệch có chủ đích)

- **`UploadEndpointTests` (Media/UploadEndpointTests.cs, MediaApiFactory) chưa chạy pass thật** — máy
  làm task này không có Docker daemon. Đã ghi vào `Docs/DOCKER-TEST-DEBT.md`. Hành vi tương đương đã
  verify KHÔNG cần Docker qua `UploadHandlerTests` (EF InMemory + `ImageSharpImageProcessor` +
  `LocalDiskObjectStorage` thật) — 9/9 pass sau review, gồm cả rollback R4 và bug #6.
- **413 thật qua Kestrel/TestServer chưa verify được** — `BadHttpRequestExceptionHandlerTests` chỉ
  xác nhận phần DỊCH lỗi → ProblemDetails đúng, không xác nhận TestServer có thật sự ném
  `BadHttpRequestException` ở đúng ngưỡng 11MB hay không (cần Docker gửi request thật qua
  `MediaApiFactory`, hoặc research riêng về hành vi `IHttpMaxRequestBodySizeFeature` dưới TestServer).

## F4/F5 — Logo shop (portal) + nối `dev-registry` vào dialog/picker thật

### Lệch có chủ đích / quyết định tự đặt (không phải bug)

#### 1. Owner signal cho nút "Tải logo mới" — tái dùng `ShopSummaryDto.roleCode` từ `GET /shops`, không phải field mới

- **Vấn đề:** `ShopDto` (`GET /shops/{shopId}`, dùng ở trang sửa shop) không có field role nào.
  Brief F4 giả định "đã có owner/role signal trên màn đó từ SHOP-001" nhưng thực tế màn sửa shop
  chưa từng dùng field đó.
- **Tra ra:** `ShopSummaryDto` (`GET /shops`, danh sách shop) có `roleCode: string` — đã hiện ở
  `_authenticated.shops.index.tsx` (`{shop.roleCode}`), đúng là "signal có sẵn từ SHOP-001" mà brief
  nhắc tới, chỉ là ở route khác. Trang sửa shop (`_authenticated.shops.$shopId.tsx`) giờ gọi thêm
  `useGetShops()`, tìm shop theo id, so `roleCode === 'Owner'`.
- **Giả định tôi tự đặt:** literal string `roleCode` cho Owner là `"Owner"` — `roleCode` là chuỗi tự
  do trong contract (không phải enum), nhưng `SHOP-001/brief.md` dùng nhất quán "Owner" (bảng quyền
  PATCH, message `SHOP_OWNER_REQUIRED`) và `MEDIA-001/brief.md` lặp lại đúng chữ đó cho
  `MEDIA_OWNER_REQUIRED`/upload logo. Suy luận từ tài liệu đã duyệt, không phải bịa — nhưng CHƯA có
  xác nhận trực tiếp từ BE/contract rằng đây đúng là literal string được serialize. Xem
  `apps/portal/src/lib/shop-role.ts`. **Cần người duyệt xác nhận** nếu giá trị thật khác "Owner".

#### 2. `ShopDto.logoId` được hiểu là id của chính phái sinh `320x96,inside`, không phải id bản gốc trong thư viện

- **Vấn đề:** brief F4 nói "hiện logo qua phái sinh 320x96,inside, lấy qua `ShopLogoDto.derivatives`
  hoặc `assets?ids=`" nhưng không nói rõ `logoId` trỏ tới asset nào. 9 endpoint media không có
  endpoint "lấy derivatives của asset gốc theo id gốc" — nếu `logoId` là id bản gốc trong thư viện
  thì không có cách nào tra ra derivative `320x96,inside` của nó qua contract hiện tại.
- **Quyết định:** hiểu `logoId` là id của CHÍNH bản phái sinh `320x96,inside` (BE lưu id derivative
  đó vào `ShopDto.logoId`, không phải id bản gốc) — khi đó `GET /shops/{shopId}/media/assets?ids={logoId}`
  trả thẳng `MediaAssetDto` có `storageKey` dùng ngay, khớp với gợi ý "check GET assets?ids= as the
  lookup" trong task brief. Nếu giả định này sai (logoId thực ra là id bản gốc), `ShopLogoSection`
  (`apps/portal/src/components/shop/shop-logo-section.tsx`) sẽ hiện "Không tìm thấy ảnh logo." thay vì
  lỗi âm thầm — không tự đoán thêm một endpoint không tồn tại để lấp. **Cần người duyệt BE xác nhận.**

#### 3. `Inspector` (builder-components) thêm prop `renderMediaPicker` để F5 cắm dialog/picker thật vào field ảnh

- **Không đổi hành vi mặc định:** prop optional, không có thì `Inspector` vẫn dùng STUB nhập imageId
  tay như cũ (backward-compatible, không có consumer nào khác của `Inspector` ngoài `dev-registry.tsx`
  tại thời điểm này).
- **Lý do đặt ở `builder-components` thay vì viết lại `Inspector` riêng trong `apps/portal`:**
  `Inspector` đệ quy qua `repeater`/`fieldset` để tới field ảnh lồng trong `Gallery.items[].image` —
  viết lại toàn bộ logic đệ quy đó ở portal để chỉ đổi một `case` là trùng lặp thật. `Inspector.tsx`
  vẫn giữ isomorphic (#23): không tự import dialog/API nào, chỉ render `ReactNode` được `apps/portal`
  truyền vào qua `renderMediaPicker`.
- Không đổi `resolveImage`, không đổi manifest hay `registry.lock.json` — `check:registry-additive`
  chạy sạch.
