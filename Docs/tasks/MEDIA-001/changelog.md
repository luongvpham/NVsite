# MEDIA-001 — changelog: lệch so với thiết kế

Ghi lại mọi chỗ thực thi khác với `DesignIdeal/08-media-asset-design.md` (và `05` §9, `07` §7.1/§7.4
nơi liên quan) cùng nguyên nhân, để người duyệt biết chỗ nào là **lệch có chủ đích** (giữ nguyên) và
chỗ nào là **việc chưa làm xong** (kèm nơi theo dõi).

**Task này gồm (Bước 4 của `step.md`):**

| Nhóm | Nội dung |
|---|---|
| S0 | Config dùng chung + Component Registry (`image-presets.json`, `binding-sources.json`, `binding.imagePresets`, artifact `derivative-presets.json`, reserved route `media`) — `registry-diff.md` |
| T1–T10 | Backend: pipeline `Imaging`, `IObjectStorage` (LocalDisk/S3), `IImagePresetCatalog`, entity + migration `MediaAsset`, upload slot/library, Library (list/clone/xoá/references/usage/assets), logo shop, `IMediaReferenceValidator`, phục vụ `/media/*`, docs + contract-sync |
| D1 | Sửa lỗi phát hiện khi có Docker: thay MinIO bằng LocalStack cho test S3; 415/413 → ProblemDetails |
| D2 / D3 / D4 | Contract follow-up (mỗi cái có `contract-diff.md` + `changelog.md` riêng): D2 OpenAPI nullable object `allOf+nullable` (#87); D3 thêm `GET …/library/{assetId}/derivatives`; D4 `logoUrl` trên `ShopDto` + `ShopSummaryDto` (#88) |
| F1–F6 | Frontend: Orval media client, `UploadSlotDialog` + `MediaLibraryPicker`, logo shop trong portal, nối vào `dev-registry`, `logoUrl` ở màn sửa + danh sách shop |

**Đọc kèm (KHÔNG chép lại ở đây):**
- `Docs/tasks/MEDIA-001/contract-diff.md` — Gate 1 vòng 1 (9 endpoint media, `ShopDto.logoId`).
- `Docs/tasks/MEDIA-001-D2/changelog.md` — transformer OpenAPI (#87).
- `Docs/tasks/MEDIA-001-D3/changelog.md` — endpoint derivatives (tra `(bản Library, preset)`).
- `Docs/tasks/MEDIA-001-D4/changelog.md` — `logoUrl` (#88).
- `Docs/tasks/MEDIA-001/plan.md` §10 — 11 giả định A1–A11 người duyệt đã chốt; `Docs/tasks/MEDIA-001/registry-diff.md`.
- `backend/docs/modules/media.md` — quy ước vận hành module.

**Viết lại ngày 2026-09-29 (đóng task).** File này trước đó chỉ có hai mảnh do hai agent khác nhau
ghi (T5, và F4/F5). Đã gộp thành một tài liệu; hai mảnh cũ được giữ nội dung đúng (T5 → mục 5, 6, 11
dưới đây; F4/F5 → mục FE) và **bỏ hẳn** một câu sai đã bị thay thế: mảnh F4/F5 cũ viết "`ShopDto.logoId`
được hiểu là id của chính phái sinh `320x96,inside`". **Sự thật:** `Shop.LogoId` là id **bản Library**
(`08` §3.6, #73). Portal không tự tra phái sinh — D4 (#88) cho `ShopDto` và `ShopSummaryDto` trả sẵn
`logoUrl` (xem mục 13). Riêng contract cũ `ShopDto.logoId` vẫn tồn tại (id bản Library) và không đổi
nghĩa.

Trạng thái kiểm thử khi đóng: `IntegrationTests` 259–260 pass / 0 fail / 1 skip (skip có sẵn của Shop —
Listing chưa tồn tại, không thuộc Media), `ArchitectureTests` 8/8, portal 43/43, `check:docs` xanh.
Docker chạy thật cho toàn bộ lớp Testcontainers — `Docs/DOCKER-TEST-DEBT.md` rỗng.

---

## Lệch có chủ đích (giữ nguyên, không phải bug)

### Mô hình dữ liệu

#### 1. `IsDeleted` của base class thay cho `DeletedAt`; tên bảng `MediaAsset`; tên index

- **`08` §2 nói gì:** cột `DeletedAt timestamptz?`; bảng `media_assets`; index `ux_media_storage_key`,
  `ux_media_id_shop`; factory nhận `EncodedImage`/`FocalPoint`.
- **Thực thi:** dùng `BaseAuditableEntity.IsDeleted` (bool, đi qua Global Query Filter + interceptor
  soft-delete có sẵn); bảng `MediaAsset` (số ít, PascalCase, theo `Shop`/`UserShop`); `StorageKey`
  unique mang tên EF mặc định `IX_MediaAsset_StorageKey`, khoá phụ `(Id, ShopId)` do EF đặt tên.
  Giữ đúng tên `ck_media_library_preset`, `ix_media_library`, `ix_media_derivative`. Factory của
  Domain nhận primitive vì Domain không được reference Application (nơi `EncodedImage` sống).
  Điều kiện lọc trong SQL/đoạn code của `08` (`DeletedAt IS NULL`) đọc là `NOT "IsDeleted"`.
- **Nguyên nhân:** giả định A3 (Gate 1) — dùng lại cơ chế xoá mềm có sẵn thay vì viết lần hai.
- **Điều kiện đảo lại:** nếu cần biết *thời điểm* xoá (job dọn Bước 8 có grace period, `08` §10 mục 6)
  thì thêm `DeletedAt` lúc đó.

#### 2. Xoá bản Library dùng `SoftDeleteFromLibrary()`, cấm `DbSet.Remove()`

- **Vì sao:** `Remove()` đưa entity vào `EntityState.Deleted`; FK self-reference `SourceAssetId`
  cấu hình `SetNull` khiến EF tự đặt `SourceAssetId = NULL` cho mọi clone đang tracked — vi phạm #72.
  Test: `LibraryHandlerTests.Delete_library_then_list_excludes_it_but_clone_still_resolves_and_file_still_opens`.
  Xoá **cứng** (job Bước 8) vẫn cho `SourceAssetId → NULL` đúng `08` §4:
  `MediaDbConstraintTests.Hard_delete_of_library_asset_sets_clone_SourceAssetId_to_null`.

#### 3. FK ghép khai từ phía Media; xoá bản Library đang là logo được phép; `assets?ids=` trả cả bản đã xoá

- FK ghép `Shop(LogoId, Id) → MediaAsset(Id, ShopId)` (#76) khai từ phía Media
  (`ShopLogoConfiguration`), vì `Shop` không được reference kiểu của `Media`. `Shop.LogoId` chỉ là
  `Guid?` thuần, không navigation.
- Giả định A11: xoá bản Library đang là `Shop.LogoId` **được phép** (chỉ cảnh báo qua endpoint
  `references`); FK tới hàng soft-delete vẫn hợp lệ, logo vẫn hiện.
- Giả định A6: `GET …/media/assets?ids=` trả cả asset **đã soft-delete** (cùng shop) để clone của ảnh
  đã xoá vẫn render (#72).

### Config, codegen, Component Registry (S0)

#### 4. `config/image-presets.json` đổi shape thành `{ presets, surfaces }`; `binding.imagePresets`; artifact thứ 8

- **`07` §3 / `08` §3.6 nói gì:** file là map phẳng `{ "<preset>": {...} }`; ví dụ artifact
  `Shop: ["320x96,inside", "1200x630,cover"]`.
- **Thực thi (#78, #86):** `{ "presets": {9 preset}, "surfaces": { "Shop": [...] } }`. Bỏ preset
  `600xR,cover` (không manifest nào dùng), thêm `fit: "inside"`. `surfaces.Shop` chỉ gồm
  `320x96,inside` + `96x96,cover` — **không** có `1200x630,cover` như ví dụ ở `08`: crop logo thành
  ảnh OG gần như luôn xấu (giả định A2). Prop `binding` thêm `imagePresets?` (key ⊆ `sources`, preset ⊆
  `presets`) — `gen-registry` fail cứng nếu sai; `registry.lock.json` chỉ đổi ở `ServiceGrid.source`.
  Sinh thêm `packages/builder-components/generated/derivative-presets.json` (artifact thứ 8) — BE đọc
  qua `DerivativePresetCatalog`, test `DerivativePresetCatalog_For_Shop_Matches_Artifact`.
- **Nguyên nhân:** thêm `surfaces` cùng cấp với tên preset sẽ bị `gen-registry` coi là một preset (A1).
  Người duyệt đã chấp nhận ở Registry gate (`registry-diff.md`).
- **Ba nơi đọc file đã sửa theo shape mới:** `gen-registry.ts`, plugin placeholder của
  `apps/portal/vite.config.ts`, `tests/config-files.test.ts`; `DesignIdeal/07` §3 vẫn vẽ map phẳng.
- `config/binding-sources.json` thêm `Shop` (#73). `config/reserved-routes.json` thêm `media` — do
  **người** ghi (hook chặn agent, #24), test `ReservedRoutesTests` + `config-files.test.ts` khẳng định.

### Pipeline, storage, phục vụ file

#### 5. Pipeline + storage ở namespace dùng chung `Imaging`; `IObjectStorage` hai implementation; test S3 dùng LocalStack

- **Thực thi (#82, #83, #84):** `Vsite.Application/Common/Imaging` (hợp đồng) +
  `Vsite.Infrastructure/Imaging` (ImageSharp 3.1.12, `LocalDiskObjectStorage`, `S3ObjectStorage`,
  `ImagePresetCatalog`, `DerivativePresetCatalog`); `Imaging` thêm vào `SharedSegments` của
  `ModuleBoundaryTests`. Chọn bằng `Storage:Provider` (mặc định `LocalDisk`).
- **`08` §5/#83 nói S3 "dùng được với MinIO":** thực tế image MinIO không còn pull được công khai
  (Docker Hub/quay: pull denied) nên test `S3ObjectStorageTests` chạy trên **LocalStack 4.0.3**
  (ghim tag; hỗ trợ `If-None-Match` cho no-overwrite). **Không nâng tag mà không chạy lại** cả pull
  lẫn test no-overwrite. Code S3 vẫn là S3 chuẩn — MinIO thật chưa được thử.
- `PutAsync` cấm ghi đè (#75): LocalDisk ghi qua file tạm + move không ghi đè; `ContentType` suy ra
  từ đuôi file (không sidecar) và bị ép khớp — hiện chỉ ánh xạ `.webp`, định dạng khác sau này phải mở
  rộng ánh xạ.
- **LocalDisk mặc định `../../../.media` (tương đối ContentRoot = `<repo>/.media`, gitignore)** — giả
  định A10; prod đặt `Storage:LocalDiskRoot` hoặc chuyển `S3`.
- **Test không cần container đặt ở `IntegrationTests/Imaging/`, không tạo project test thứ tư** (A9).

#### 6. Giới hạn upload, HEIC, không upscale

- **Thực thi (#85):** `ImageUploadOptions` (`Imaging:Upload`): 10 MB, 25 MP (đọc header, trước decode),
  cạnh dài 1600, webp quality 82. HEIC nhận diện bằng brand `ftyp` và trả mã riêng. Mọi từ chối ảnh
  trả **422** + `error_code` (A4); chỉ vượt giới hạn body của Kestrel mới ra 413. Animated webp chỉ
  decode frame đầu (`MaxFrames = 1`).
- **Không upscale (A5):** ảnh nhỏ hơn preset thì record có `Width`/`Height` nhỏ hơn preset nhưng
  `Preset` vẫn ghi preset của slot (đúng tỉ lệ, chỉ mờ hơn).
- **Giới hạn request 11 MB** đặt thủ công (10 MB file + phần form) — số này không có trong `08`.

#### 7. `/media/*` — `Cache-Control: public, max-age=3600`, không `immutable`

- **`08` §5 nói gì:** file bất biến (#75) nên `immutable` "được"; `08` §10 mục 7 hoãn tới Bước 8.
- **Thực thi (A8):** `public, max-age=3600` cho tới Bước 8 (cache publish). Middleware
  `MediaFileMiddleware` mount bằng `app.Map(ImagePaths.MediaPathPrefix, …)` **trước**
  `TenantResolutionMiddleware` (đọc không kiểm tenant, #53): chỉ GET/HEAD (405 + `Allow` cho phương
  thức khác), kiểm `RawTarget` trước khi ASP.NET giải mã (chỉ từ chối `%2f` và `%5c`, không phân biệt hoa
  thường) rồi `ValidateKey`; `..` bị chặn bởi `ImagePaths.ValidateKey` + `LocalDiskObjectStorage.ResolvePath`,
  không phải bởi kiểm `RawTarget`. Key phải bắt đầu bằng `shops/`.
- **Điều kiện đảo lại:** Bước 8 chốt cache/TTL (`05` §25 #4) thì đổi sang `immutable`.

### API

#### 8. Multipart bind thủ công, không dùng `[FromForm]`

- **Brief nói gì:** "dùng `[FromForm]` request type hoặc explicit IFormFile parameters".
- **Thực thi:** endpoint nhận `HttpRequest`, đặt `IHttpMaxRequestBodySizeFeature.MaxRequestBodySize`
  (11 MB) RỒI `await request.ReadFormAsync(ct)`. Minimal API bind `[FromForm]` complex-type TRƯỚC khi
  endpoint filter chạy nên đặt giới hạn trong filter là quá trễ.
- **Hệ quả:** khai `.Accepts<UploadToSlotForm>("multipart/form-data")` cho OpenAPI chỉ để có schema
  tài liệu; các field form (`file`, `preset`) **không** được đánh dấu required trong contract → type
  Orval sinh ra cho form là optional dù bắt buộc lúc runtime (FE không dựa vào type này để validate;
  `apps/portal/src/lib/media-validation.ts` làm việc đó).

#### 9. Mã lỗi thêm ngoài `08`; 413/415 → ProblemDetails

- `08` không định nghĩa `error_code` nào. Đã thêm (tất cả có test): `MEDIA_UNSUPPORTED_FORMAT`,
  `MEDIA_HEIC_UNSUPPORTED`, `MEDIA_FILE_TOO_LARGE` (422 khi vượt 10 MB đã đọc được; 413 khi Kestrel
  cắt body), `MEDIA_TOO_MANY_PIXELS`, `MEDIA_CORRUPT_IMAGE`, `MEDIA_UNKNOWN_PRESET` (handler, không qua
  FluentValidation nên không bị đổi thành `VALIDATION_ERROR`), `MEDIA_FILE_MISSING`,
  `MEDIA_MULTIPART_REQUIRED`, `MEDIA_CLONE_FROM_CLONE`, `MEDIA_OWNER_REQUIRED`,
  `MEDIA_INVALID_IMAGE_REFERENCE` (validator tham chiếu, chưa nối — mục 12).
- **413:** `BadHttpRequestExceptionHandler` bắt `BadHttpRequestException` có `StatusCode == 413`
  (toàn cục, nhưng CHỈ 413) → `MEDIA_FILE_TOO_LARGE`. **415:** `UnsupportedMediaTypeStatusCodeHandler`
  chỉ gắn `MEDIA_MULTIPART_REQUIRED` cho request khớp một endpoint khai `multipart/form-data`
  (`MultipartRouteMatcher`); mọi 415 khác (JSON endpoint Identity/Shop, `…/clones`) giữ nguyên hành vi
  cũ — xem "Chưa làm xong" về lỗ hổng #19 có từ trước.
- Focal point ngoài `[0,1]` vẫn ra `VALIDATION_ERROR` (brief không đòi mã riêng).

#### 10. Danh sách endpoint = 9 (Gate 1) + 1 (D3)

`POST slot-uploads`, `POST library`, `GET library`, `POST library/{assetId}/clones`,
`GET library/{assetId}/references`, `DELETE library/{assetId}` (Owner), `GET assets?ids=`,
`GET usage`, `PUT /shops/{shopId}/logo` (Owner), và — thêm ở D3 —
`GET library/{assetId}/derivatives?preset=` (tra `(bản Library, preset)`; cô lập tenant bằng `ShopId`
route + `IgnoreQueryFilters()` viết tay nên vẫn trả phái sinh của bản Library đã soft-delete; id lạ →
`200 []`, không 404). Chi tiết: `Docs/tasks/MEDIA-001-D3/changelog.md`.

#### 11. Ba port xuyên module mới (Public Contract, #1)

`IShopOwnershipService` (Shop → Media dùng để kiểm Owner/member; Media không reach được Identity),
`IShopLogoWriter` (Media ghi `Shop.LogoId` qua Shop, #82), `IShopLogoReader` (Shop đọc logo qua Media,
#88; có bản tra theo lô). Cả ba interface nằm ở `Application/Shop/Interfaces`, adapter ở
`Infrastructure/Shop` hoặc `Infrastructure/Media`; `Shop` không reference `Media`
(`ModuleBoundaryTests`, `dependency-map.json` không đổi). `MediaAssetWriter` + `TimeProvider` đăng ký ở
`AddInfrastructure()` (điểm wiring duy nhất) dù `MediaAssetWriter` là type của Application. Bug đã
sửa trong quá trình: `MediaAssetWriter._writtenKeys` không được clear sau `SaveChanges` thành công →
rollback của thao tác sau xoá nhầm file của thao tác trước
(`UploadHandlerTests.Writer_does_not_delete_files_of_a_previously_committed_operation_when_a_later_save_fails`).

#### 12. `IMediaReferenceValidator` tồn tại nhưng CHƯA nối vào handler nào

- **`08` §3.5 / §9 nói gì:** lưu draft/page kiểm id trong một câu query; tiêu chí dừng "test khẳng định
  tree không chứa id `IsInLibrary = true`"; test bắt buộc 6 và 7.
- **Thực thi:** `Application/Media/Interfaces/IMediaReferenceValidator` + `MediaReferenceValidator`
  (một câu `COUNT`, `ShopId` từ `TenantContext`, fail-closed khi tenant null) và test đầy đủ ở tầng
  validator (`MediaReferenceValidatorTests`, `MediaReferenceValidatorSqlCountTests`), nhưng chưa có
  handler lưu draft/page (Bước 5) để gọi. Test bắt buộc 6/7 vì thế mới được chứng minh ở tầng
  validator, **chưa** ở tầng request. Theo dõi: "Chưa làm xong" mục 1.

#### 13. #88 — `logoUrl` (có tiền tố `/media/`) trên `ShopDto` **và** `ShopSummaryDto`

- **Thiết kế đầu tiên (đã bị thay):** field `logoStorageKey` là đường dẫn **tương đối**, BE không bao
  giờ trả URL có `/media/` (`08` §5: chỉ `resolveImage()` thêm tiền tố), giữ API độc lập host.
- **Người duyệt yêu cầu ở Gate 1 vòng 4:** "cần trả url có /media" và cả `GET /shops`
  (`ShopSummaryDto`) cũng mang logo. **Bản cuối:** `logoUrl = "/media/" + storageKey` của phái sinh
  `320x96,inside`, `null` nếu chưa có logo hoặc chưa có phái sinh; FE dùng `<img src={logoUrl}>` nguyên
  trạng, **không** bọc thêm `mediaUrl()` (tránh `/media//media/`).
- **Đánh đổi đã chấp nhận:** BE giờ cũng biết tiền tố `/media` (trước đó chỉ FE biết). Giảm nhẹ: **một**
  hằng và **một** hàm dựng URL — `ImagePaths.MediaPathPrefix` / `ImagePaths.MediaUrl` — dùng chung với
  chỗ mount `/media` trong `Program.cs` (`ImagePathsMediaUrlTests`, `ShopLogoTests` GET đúng
  `logoUrl` và nhận 200). DB vẫn chỉ lưu key tương đối; nội dung ảnh trong builder vẫn qua
  `resolveImage()`. `ShopLogoReader` tra theo lô một câu SQL cho `GET /shops` (không N+1), có
  `!IsDeleted` của Shop viết tay vì `IgnoreQueryFilters()` tắt luôn filter soft-delete của Shop.
- Test "Program.cs mount dùng hằng" hiện chỉ là test đọc mã nguồn (giòn) — end-to-end `ShopLogoTests`
  mới là bằng chứng thật.
- Nhãn contract: `contract:diff` xếp thay đổi này BREAKING (field nullable "required" luôn có mặt);
  người duyệt đã chấp nhận (`Docs/tasks/MEDIA-001-D4/contract-diff.md`).

#### 14. Quy ước OpenAPI #87 (D2)

Property object nullable xuất `{ allOf: [$ref], nullable: true }` thay vì schema trùng `XDto2`
(Orval từng sinh `MediaAssetDto2` khiến `SlotUploadResultDto.libraryAsset` không tương thích). Transformer
dùng chung đăng ký cho mọi document module, ghi ở `backend/CLAUDE.md` §OpenAPI. Chi tiết:
`Docs/tasks/MEDIA-001-D2/changelog.md`.

### Frontend (F1–F6)

#### 15. Nối `dev-registry` vào dialog/picker thật là tạm thời

- `apps/portal/src/routes/dev-registry.tsx` cắm `UploadSlotDialog` / `MediaLibraryPicker` vào
  `Inspector` để nghiệm thu hai chế độ upload — đây là harness dev, **Bước 5 (Website/Page editor) thay
  bằng màn thật**. `Inspector` thêm prop tuỳ chọn `renderMediaPicker` (không có thì vẫn dùng STUB nhập
  `imageId` tay như cũ, tương thích ngược; `Inspector` vẫn isomorphic, #23). Không đổi manifest hay
  `registry.lock.json`.
- `mediaUrl(storageKey)` được export từ `packages/builder-components/src/context.tsx` và dùng cả bởi
  `resolveImage` lẫn picker (Library item không có trong `mediaMap` của tree) — giữ **một** chỗ nối
  `/media/` phía FE.
- `resolveImage()` bản thật: `mediaMap` (imageId → `{storageKey, preset}`) nạp trước khi render;
  thiếu → placeholder theo preset; preset lệch → `console.warn` một lần vẫn render (#74).

#### 16. Owner signal, `isOwner`, và các thay đổi phụ

- Owner cho nút "Tải logo mới" / "Xoá": tái dùng `ShopSummaryDto.roleCode === "Owner"`
  (`apps/portal/src/lib/shop-role.ts`; literal đã đối chiếu `RoleSeed.cs`), vì `ShopDto` không có field
  role. `MediaLibraryPicker.isOwner` **mặc định `false`** (fail-safe: ẩn nút Xoá nếu caller quên truyền).
- `packages/ui` thêm primitive `Dialog` (chưa có sẵn).
- `packages/api-sdk/src/index.ts` và `mocks.ts` là file **viết tay**: đã export thêm `generated/media/**`
  (Orval sinh nhưng không ai export); `orval.config.ts` thêm entry `media`/`mediaZod`; client `shop` được
  sinh lại (thiếu `logoId`). Không sửa file trong `generated/`.
- `fitOfPreset(preset)` suy `cover`/`inside` từ **chuỗi tên preset** (`…,inside`) — tránh gọi API chỉ để
  biết fit; sai nếu sau này đặt tên preset không theo khuôn.
- Focal point ở F2 là **một dấu chấm** click trên ảnh, chưa có khung crop (xem "Chưa làm xong" mục 6).

#### 17. Giả định phụ của T5 (trước đây nằm trong comment code)

- **Clock:** không có abstraction thời gian sẵn có nên `AddMediaModule` đăng ký `TimeProvider.System`
  (Singleton, stateless); test thay bằng `FakeTimeProvider` qua override DI.
- **Validator upload-to-slot:** chỉ kiểm focal ∈ [0, 1] và `Preset` không rỗng. Kiểm preset tồn tại nằm
  ở `UploadToSlotHandler` vì `ValidationException` luôn trả `VALIDATION_ERROR` chung, còn brief đòi mã
  `MEDIA_UNKNOWN_PRESET` ở top-level ProblemDetails. `FileName` cắt còn 200 ký tự ở `MediaAssetWriter`
  (khớp `MaxLength(200)` của cột), không phải lỗi validation.

#### 18. Hai lỗi FE chỉ lộ khi dựng thử nghiệm thu tay (đã sửa, không test tự động nào bắt được)

Phát hiện lúc chuẩn bị `Docs/tasks/MEDIA-001/acceptance.md` bằng cách chạy thật `apps/web` và soi
`apps/portal`. Cả hai đều là lỗi của F1/F5 mà review từng task không thể thấy vì chỉ đọc mã:

- **`apps/web`: proxy `/media` không hoạt động, và host `{slug}.vsite.local` bị chặn 403.**
  F1 khai `nitro({ devProxy: { '/media': … } })`, nhưng với TanStack Start + `nitro/vite` request
  `/media/…` vẫn bị SSR của app trả 404 HTML, không bao giờ tới API (so sánh: API trả 404 trống, web
  trả HTML giống hệt một route không tồn tại). Ngoài ra Vite mặc định chặn hostname lạ nên
  `http://spa-abc.vsite.local:3000` trả `403 Blocked request`. Đã đổi sang `server.proxy` của Vite
  (cùng cơ chế `apps/portal`) và thêm `server.allowedHosts: ['.vsite.local']`. Đã kiểm: host shop → 200,
  host lạ → vẫn 403, `/media/…` giờ ra 404 trống đúng dạng API. **Chưa kiểm dương tính bằng một ảnh thật**
  — đó là bước 8 của `acceptance.md`.
- **`apps/portal`: không có đường nội bộ nào mở `/dev-registry` khi đã đăng nhập.** Trang đó nằm ngoài
  layout `_authenticated` và cần token, mà token chỉ sống trong bộ nhớ (Quyết định #3) nên gõ URL sẽ mất
  phiên; link duy nhất tới nó nằm ở trang chủ `/`, không có nút đăng nhập. Đã thêm link `dev-registry`
  chỉ hiện khi `import.meta.env.DEV` ở thanh đầu trang sau đăng nhập. Xoá cùng `/dev-registry` ở Bước 5.

---

## Chưa làm xong (nợ kỹ thuật, không phải lệch có chủ đích)

Mọi mục dưới đây đã có chỗ theo dõi — cột cuối là nơi **đọc để biết ai làm / khi nào**.

| # | Việc | Theo dõi ở |
|---|---|---|
| 1 | **Nối `IMediaReferenceValidator` vào handler lưu draft/page** (và `Page.OgImageId`) — chưa có test request-level cho `08` §9 test bắt buộc 6/7 và tiêu chí dừng "tree không chứa id `IsInLibrary = true`". Kèm mã `MEDIA_INVALID_IMAGE_REFERENCE` | `step.md` Bước 5 · `00-INDEX.md` §4 |
| 2 | **`GET …/library/{assetId}/references` chỉ quét `Shop.LogoId`.** Chưa quét `page_drafts.Tree`, `site_publications.Snapshot`, `Service.ImageId`… — cảnh báo "ảnh đang được dùng" khi xoá chưa đầy đủ (`08` §4). Endpoint vẫn cảnh báo-không-chặn đúng thiết kế | `step.md` Bước 5 (tree) · Bước 8 (snapshot) · `00-INDEX.md` §4 |
| 3 | **Job dọn file mồ côi** + grace period trước khi xoá file thật (`08` §4, §10 mục 6). Bước 4 chủ ý không xoá gì ngoài thao tác xoá-khỏi-Library | `step.md` Bước 8 · `08` §10 mục 5–6 |
| 4 | **Bản TypeScript của hàm quy ước tên `thumb_`/`fthumb_`** (C# đã có ở `ImagePaths.Thumb`/`FeaturedThumb`, không idempotent với tên đã có tiền tố). `08` §9 yêu cầu "hàm quy ước tên"; FE chưa cần cho tới ảnh Listing/Product | `00-INDEX.md` §4 — làm cùng task Listing đầu tiên |
| 5 | **Chạy tay tiêu chí dừng `08` §9 trên API + UI thật — CHƯA AI CHẠY.** (a) upload vào Hero ở CẢ HAI chế độ, ảnh hiện đúng kích thước preset; (b) chọn ảnh Library cho slot Gallery sinh clone độc lập; (c) upload logo sinh đủ bộ phái sinh và còn nguyên sau reload; (d) `/media/…` đọc được trên `admin.vsite.local` **và** `{slug}.vsite.local:3000` (`apps/web devProxy` mới chỉ được kiểm qua type declaration của nitro). Test tự động phủ từng mảnh nhưng không thay thế được | `00-INDEX.md` §4 |
| 6 | **Khung crop theo tỉ lệ preset** trên dialog upload/chọn (F2 chỉ có dấu chấm focal point) | `00-INDEX.md` §4 |
| 7 | **Giới hạn 413 thật qua Kestrel chưa được chứng minh.** `BadHttpRequestExceptionHandlerTests` chỉ chứng minh phần dịch lỗi; không test nào gửi request > 11 MB qua server thật. Cùng bài nghiệm thu tay mục 5 | `00-INDEX.md` §4 (gộp vào dòng nghiệm thu tay) |
| 8 | **`tools/contract-sync` mù với response dạng mảng:** `extractSchemaRefs` chỉ đi theo `$ref` cấp cao nhất nên thay đổi ở `GET /shops`, `…/derivatives`, `…/assets?ids=` hiện là "UNCHANGED". D4 phải kiểm staging bằng tay. Cần task riêng (đi theo `items.$ref`/`allOf` + diff dự phòng `components.schemas`) và ghi chú vào skill `contract-sync` | `00-INDEX.md` §4 |
| 9 | **Lỗ hổng #19 có từ trước:** 400 do model-binding (guid/page/JSON sai) và 415 của endpoint JSON không có `error_code`; `page` tràn số → `Skip` âm → 500. D1 chỉ scope 415 cho route multipart, không đổi module khác | `00-INDEX.md` §4 |
| 10 | **`UploadShopLogoHandler` để lại file mồ côi** nếu shop biến mất giữa lúc kiểm tra và lúc ghi (race NotFound) — dọn ở job mồ côi (mục 3) | mục 3 |
| 11 | **`MediaAsset.SourceAssetId` không phải FK ghép** `(SourceAssetId, ShopId)` — ràng buộc "bản gốc cùng shop" chưa được DB bảo vệ | (chưa có nơi khác) |
| 12 | **`MediaAssetWriter`: lỗi sau commit** — trường hợp cạnh khi lỗi xảy ra sau khi transaction đã commit, rollback bù chưa được xử lý | (chưa có nơi khác) |
| 13 | **`config/reserved-routes.json` dùng tab để thụt lề** — file chỉ người sửa được (hook bảo vệ) | người duyệt |
| 14 | **`dev-registry` `useEffect` deps** chưa đầy đủ — code tạm (xem mục 15 ở phần Frontend phía trên) | mục 15 ở phần Frontend |
| 15 | **Audit `UserShops.IgnoreQueryFilters()` thiếu `!IsDeleted` VÀ thiếu `Status == Active`** ở các handler Identity có từ trước (Login, RefreshToken, Register, ChangePassword, ForgotPassword, ResetPassword, VerifyEmail). Riêng tra owner ở `LoginHandler` (~:91) và `RefreshTokenHandler` (~:44) hiện không kiểm `Status == Active` — audit phải phủ cả hai điều kiện, không chỉ `!IsDeleted`. MEDIA-001 chỉ sửa 4 chỗ cổng Media/Shop (`ListShopsHandler` nay cũng lọc tay `!s.IsDeleted`/`!r.IsDeleted` cho Shop/Role được join) | `00-INDEX.md` §4 |
| 16 | **Test đột biến của `packages/builder-components/tests/gen-registry.test.ts` (~:64-179, 5 test) sửa tạm các `registry/*.manifest.ts` đã commit** rồi khôi phục bằng `try/finally`. `finally` không chạy nếu worker vitest bị kill (vd turbo huỷ task anh em sau khi task khác lỗi) — từng để lại `registry/service-grid.manifest.ts` hỏng, và reader song song thấy manifest hỏng trong lúc đó. Khuyến nghị: chạy test đột biến trên bản sao tạm của `registry/` (vd tham số `--registry-dir` cho script gen) hoặc thêm global teardown/guard khôi phục/kiểm `git diff --quiet registry/`. Cần task riêng nhỏ | `00-INDEX.md` §4 |

### Các "minor" đã hoãn có chủ đích (review ghi nhận, không ảnh hưởng đúng/sai tenant)

Nhóm để tra khi cần, không có mục riêng ở nơi khác:
- **Storage/ranh giới:** symlink/junction thoát root nếu root dùng chung; `ValidateKey` cho phép segment rỗng/`.`, dấu `/` cuối, `:` (ADS), đuôi dấu chấm/khoảng trắng; race `Exists`/`Open`; S3 409 `ConditionalRequestConflict` chưa map, S3 không có `ListBucket` → 403 → 500 trên `/media`; không `ValidateOnStart` cho `StorageOptions`.
- **Pipeline:** test strip metadata mới chỉ khẳng định EXIF (chưa XMP/ICC/IPTC); `RenderAsync` clone cả ảnh nguồn (peak memory ×2); chưa chặn kích thước transform ≤ 0; danh sách brand HEIC gồm `mif1`/`msf1`; processor có thể là Singleton.
- **Entity/DB:** `Shop.Id` mất `ValueGeneratedOnAdd` trong model (chỉ rủi ro khi `Update()/Attach()` hoặc chèn Shop+logo cùng lúc); `NaN` focal được chấp nhận; preset rỗng được chấp nhận; thiếu tie-breaker `CreatedAt`/`Id` khi sắp xếp danh sách và derivatives; `ParseFloat` focal lỗi âm thầm rơi về 0.5; file mồ côi nếu ném lỗi giữa lúc put và `Add`.
- **Test:** vài test chỉ là regression guard hoặc đọc mã nguồn (Program.cs mount; sentinel; test HTTP path traversal bị `HttpClient` gộp trước khi tới middleware); `MediaReferenceValidator` có predicate `ShopId` viết tay dư so với global filter và không test nào fail nếu bỏ; test tên `…returns_NotFound` khẳng định 403 ở vài chỗ.
- **Frontend:** initial-letter fallback dùng `charAt(0)` (nên `Array.from(name.trim())[0]`); `<img>` ở danh sách shop chưa `loading="lazy"`; refetch sau upload có thể reset phần sửa chưa lưu của `EditShopForm` (hành vi có từ trước); `walkForImageIds` duck-type mọi object có `imageId` (chỉ dùng ở dev); `isShopOwner()` chưa có unit test riêng.
- **OpenAPI:** kiểu `nullable` không kèm `type` ở OAS 3.0 (Orval zod đã sinh đúng ở F1); `TaggedRefIdOwners` static sống suốt process; đường `CreateSchemaReferenceId` cấu hình chỉ được unit-test (Program.cs không đặt).
- **Quy trình:** `S0` gộp T0.1 + T0.2 vào một commit; test `gen-registry` "registry/ khôi phục" dùng timeout 5s mặc định, từng flake một lần khi máy tải cao.
- **Build/turbo (đã sửa):** `gen:api` từng khai `outputs: ["src/**"]` nên mỗi cache hit turbo khôi phục cả `packages/api-sdk/src/` và âm thầm ghi đè file viết tay (mất dòng export `getShopsShopIdMediaLibraryAssetIdDerivativesParams` trong `index.ts` sau mỗi `pnpm test`). Nay `outputs: ["src/generated/**"]`; file viết tay của api-sdk là `src/index.ts`, `src/mocks.ts`, `src/mutator/`, `src/env.d.ts`. Các `*.tsbuildinfo` đã bỏ khỏi git và thêm vào `.gitignore`.
