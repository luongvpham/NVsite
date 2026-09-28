# MEDIA-001-D2 — changelog: lệch so với thiết kế

MEDIA-001-D2 là một sub-task phát sinh khi session FE (S2, task F1) dừng lại vì Orval sinh ra hai TS
type không tương thích cho `MediaAssetDto`/`MediaAssetDto2`. Không có tài liệu `DesignIdeal/`
chuyên đề nào tả riêng việc sinh OpenAPI — quy ước liên quan nằm ở `backend/CLAUDE.md` §OpenAPI, đã
sửa cùng task này (thêm mục "Property object nullable → `allOf` + `nullable`").

**Đọc kèm:**
- `Docs/tasks/MEDIA-001-D2/contract-diff.md` — Gate 1 lần hai, khác biệt ở tầng API contract.
- `Docs/tasks/MEDIA-001/plan.md`, `Docs/tasks/MEDIA-001/changelog.md` — task cha.

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Transformer áp cho mọi property object nullable, không chỉ chỗ đang trùng

- **Vì sao không hẹp phạm vi hơn:** `Microsoft.AspNetCore.OpenApi` không cho document transformer
  biết lúc chạy schema transformer rằng type đang xử lý có "bản dùng non-null ở chỗ khác" hay
  không — thông tin đó chỉ có sau khi toàn bộ document đã duyệt xong. Thu hẹp phạm vi nghĩa là phải
  duyệt document hai lần (một lần dò trùng, một lần sửa), phức tạp hơn không có lợi rõ ràng.
- **Hệ quả:** mọi property object nullable ở mọi module, kể cả module chưa có bug thật, giờ xuất ra
  dạng `{ allOf: [$ref], nullable: true }` thay vì `{ $ref, nullable: true }` phẳng. Ghi thành
  Quyết định #87. JSON runtime không đổi.

### 2. Ref id tự resolve, có guard chặn trùng id giữa hai CLR type khác nhau

- **Thiết kế ban đầu (bản sửa lần 1, đã review reject):** dùng thẳng
  `CreateDefaultSchemaReferenceId(context.JsonTypeInfo)`.
- **Thực thi cuối:** đọc `OpenApiOptions.CreateSchemaReferenceId` đã cấu hình cho đúng document qua
  `IOptionsMonitor<OpenApiOptions>`, fallback về default chỉ khi không có custom delegate. Nếu một
  id bị hai CLR type khác nhau cùng chiếm thì `throw InvalidOperationException` lúc generate — không
  âm thầm trỏ nhầm type.
- **Nguyên nhân:** review Gate 1 nội bộ (`.superpowers/sdd/plan/progress.md`) phát hiện dùng thẳng
  default id có thể sai nếu tương lai có custom `CreateSchemaReferenceId`, hoặc hai type trùng tên ở
  hai module khác nhau.

---

## Chưa làm xong / còn hở

### 1. Đường resolve `CreateSchemaReferenceId` tuỳ biến chưa có test end-to-end

- **Hiện trạng:** `Program.cs` hiện không cấu hình `CreateSchemaReferenceId` riêng cho document nào,
  nên đường "đọc theo option tuỳ biến" chỉ được unit test dựng tay, chưa chạy qua
  `AddOpenApi(...)` thật.
- **Khi nào cần làm:** nếu có module tương lai cấu hình `CreateSchemaReferenceId` riêng, thêm test
  dựng `WebApplication` thật với option đó trước khi tin tưởng đường này.

### 2. `TaggedRefIdOwners` là static, sống suốt vòng đời process

- **Hiện trạng:** dictionary theo dõi id đã gắn cờ là `static ConcurrentDictionary`, không reset.
  Không gây vấn đề với cách dùng hiện tại (mỗi document tên riêng, mỗi lần build .NET là một
  process mới), nhưng nếu sau này có kịch bản build lại document nhiều lần trong cùng process
  (ví dụ hot-reload dev), có thể cần cơ chế reset theo document.
