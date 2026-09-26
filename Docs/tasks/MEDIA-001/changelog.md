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
- **Chưa có test tự động cho 413** — không nằm trong danh sách test bắt buộc của brief T5; cơ chế
  set `IHttpMaxRequestBodySizeFeature` đã verify bằng đọc code + so với tài liệu ASP.NET Core, chưa
  có integration test xác nhận response 413 thật (nợ nhỏ, để T6+ hoặc khi có endpoint thứ ba dùng
  chung pattern này bổ sung test).

#### 2. "Preset lạ" / "focal ngoài [0,1]" trả `error_code: VALIDATION_ERROR`, không phải mã riêng theo brief

- **Brief nói gì:** "Preset phải có trong IImagePresetCatalog → 422 `MEDIA_UNKNOWN_PRESET`" (liệt kê
  trong mục "Validator (FluentValidation)").
- **Thực thi:** `UploadToSlotValidator` dùng `RuleFor(x => x.Preset).Must(...)` — khi fail, đi qua
  `ValidationBehavior` chung → `Vsite.Application.Common.Exceptions.ValidationException`, LUÔN có
  `error_code = "VALIDATION_ERROR"` ở top-level (per-field message có nhắc `MEDIA_UNKNOWN_PRESET` để
  debug, nhưng không lộ ra `error_code` field của ProblemDetails).
- **Nguyên nhân:** đây là hành vi CÓ SẴN của `ValidationException` (base class không nhận error code
  theo từng `ValidationFailure`) — đúng tiền lệ `UpdateShopValidator.IsReserved` (slug trong danh sách
  reserved cũng trả `VALIDATION_ERROR`/422, không có mã riêng; `ShopEndpointTests` cũng chỉ assert
  status, không assert `error_code` cho case đó). Đổi hành vi này (cho FluentValidation gắn error code
  riêng lên top-level `AppException.ErrorCode`) là thay đổi hạ tầng dùng chung mọi module, ngoài phạm
  vi T5 — cần người duyệt trước khi làm.
- **Điều kiện đảo lại:** nếu Gate 1/FE thực sự cần `error_code = MEDIA_UNKNOWN_PRESET` ở top-level,
  cách rẻ nhất là chuyển check preset ra khỏi FluentValidation, làm ở đầu handler
  (`UploadToSlotHandler`) và tự `throw new UnprocessableException("MEDIA_UNKNOWN_PRESET", ...)` —
  chưa làm vì brief nhóm nó chung với các FluentValidation rule khác.

#### 3. `MediaAssetWriter` + `TimeProvider` đăng ký ở `AddInfrastructure()`, dù `MediaAssetWriter` là type của Application

- **Nguyên nhân:** backend/CLAUDE.md chốt `AddInfrastructure()` là điểm wiring DI DUY NHẤT — dù
  `MediaAssetWriter` (Application/Media) không phải implementation của interface Infrastructure,
  vẫn đăng ký ở đó (section `AddMediaModule` mới) để giữ đúng quy ước "một điểm wiring".
- Không có clock abstraction sẵn có trong codebase (grep `TimeProvider` ra rỗng trước T5) — dùng
  thẳng `TimeProvider.System` làm singleton, `MediaAssetWriter` nhận qua constructor injection thay
  vì gọi `DateTimeOffset.UtcNow` trực tiếp, để test thay được bằng `FakeTimeProvider` nếu cần sau này.

### Chưa làm xong (nợ kỹ thuật, không phải lệch có chủ đích)

- **`UploadEndpointTests` (Media/UploadEndpointTests.cs, MediaApiFactory) chưa chạy pass thật** — máy
  làm task này không có Docker daemon. Đã ghi vào `Docs/DOCKER-TEST-DEBT.md`. Hành vi tương đương đã
  verify KHÔNG cần Docker qua `UploadHandlerTests` (EF InMemory + `ImageSharpImageProcessor` +
  `LocalDiskObjectStorage` thật) — 7/7 pass, gồm cả rollback R4.
- Test 413 (giới hạn 11 MB) chưa có — xem lệch #1 ở trên.
