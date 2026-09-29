# MEDIA-001 — Bước 4: `MediaAsset` + pipeline ảnh + Media Library + 9 preset + `/media/*`

> **Lane C.** Có module mới (`Media`) và migration, chạm cả ba loại contract. Cổng: duyệt Registry,
> rồi Gate 1, rồi Gate 2. **Tuần tự S0 → S1 → S2**, không chạy song song.
>
> **Trạng thái:** plan đã lập ngày 2026-09-26, **chưa code**. `brief.md` **chưa** viết: theo workflow §7, session BE viết nó
> **sau** Gate 1 (mẫu: `Docs/tasks/SHOP-001/brief.md`).
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc `superpowers:executing-plans`.
> Mỗi task có checkbox `- [ ]`.

**Mục tiêu:** shop upload được ảnh vào slot Hero/Gallery theo cả hai chế độ (#70), chọn lại ảnh
Library sinh clone độc lập (#71), upload logo sinh đủ bộ phái sinh (#73), và mọi ảnh phục vụ tại
`{domain bất kỳ}/media/{relativePath}`.

**Kiến trúc:** pipeline ảnh và object storage nằm ở namespace dùng chung `Imaging` (#82), dùng
ImageSharp (#84), sau `IObjectStorage` có hai provider LocalDisk và S3 (#83). Module `Media` sở hữu
bảng `MediaAsset`, các luồng upload/clone/library/logo, và Public Contract `IMediaReferenceValidator`
cho Bước 5. FE thay thân `resolveImage()` (giữ chữ ký, #74) và dựng dialog upload, picker, logo trong
`apps/portal`.

**Stack:** .NET 9 · EF Core + Npgsql · SixLabors.ImageSharp · AWSSDK.S3 · Testcontainers (PostgreSQL,
MinIO) · TS/React · Vitest + MSW.

**Spec:** [`DesignIdeal/08-media-asset-design.md`](../../../DesignIdeal/08-media-asset-design.md). §9 là
ranh giới Bước 4. Quyết định: #53, #55–#58, #64, #69–#86 ở
[`DECISIONS.md`](../../../DesignIdeal/DECISIONS.md). Bàn giao: [`guide.md`](guide.md) · nhu cầu dữ
liệu: [`data-needs.md`](data-needs.md).

---

## 1. Đã chốt trong session lập plan (2026-09-26)

Đã cấp số trong `DECISIONS.md` và viết nội dung ở `08` §0:

| # | Chốt |
|---|---|
| #82 | Pipeline + storage ở namespace dùng chung `Imaging` (`Vsite.Application.Common.Imaging`, `Vsite.Infrastructure.Imaging`; thêm `Imaging` vào `SharedSegments` của Infrastructure). Endpoint upload logo thuộc `Media` và ghi `Shop.LogoId` qua `IShopLogoWriter` của `Shop`. `Media` giữ phase 2 |
| #83 | `IObjectStorage` có hai provider `LocalDisk` + `S3`, chọn bằng `Storage:Provider` (mặc định `LocalDisk`). `/media/*` do .NET phục vụ qua cùng interface |
| #84 | ImageSharp |
| #85 | File ≤ 10 MB, ảnh ≤ 25 MP (kiểm từ header, trước decode). Chỉ nhận JPEG/PNG/WebP. HEIC trả `MEDIA_HEIC_UNSUPPORTED` |
| #86 | Prop `binding` thêm `imagePresets?: Partial<Record<source, preset[]>>`. Codegen ghi bộ phái sinh vào `packages/builder-components/generated/`, hợp với tập `surfaces` trong `config/image-presets.json` |

Những điểm ở `guide.md` §3 đã chốt từ trước, **không mở lại**.

---

## 2. Trình tự session: vì sao S0 đi trước BE

Session BE **không được ghi** `config/` và `packages/` (workflow §4). Nhưng BE phải đọc hai thứ:
danh sách 9 preset, và bộ phái sinh theo nguồn (để sinh phái sinh cho logo). Hai thứ đó cũng là
contract (Component Registry và reserved routes). Vì vậy tách thành ba session:

```
S0 · config + Component Registry (config/, packages/builder-components/)
     → 🧑 DUYỆT REGISTRY: diff registry.lock.json + reserved-routes + image-presets
S1 · Backend (backend/ only)
     → contract-sync → contract-diff.md → 🧑 GATE 1 → promote → brief.md
S2 · Frontend (apps/portal, apps/web, packages/builder-components/src)
     → integration → change-reviewer → review.md → 🧑 GATE 2
```

Tiền lệ: Component Registry (Bước 2) cũng đã làm trước Identity (workflow §12).

---

## 3. Ràng buộc toàn cục

Mọi task đều phải tuân các ràng buộc sau:

- Tenant (#21): `ShopId` chỉ lấy từ route `/shops/{shopId}` qua `RequireShopMembership()` → `TenantContext`, **không bao giờ** lấy từ body hay form field. Ownership kiểm **trong câu query**.
- Mọi endpoint có `{shopId}` phải gọi `.RequireShopMembership()`, nếu không `ShopScopedRouteFilterTests` báo đỏ.
- Enum serialize dạng string. TS dùng `const object + union`, không dùng `enum` (#19).
- Lỗi trả ProblemDetails có `error_code`. Phân trang dùng đúng shape `{ items, total, page, pageSize }`.
- File ảnh bất biến (#75): `IObjectStorage.PutAsync` **không ghi đè**. Key đã tồn tại thì ném lỗi, không ghi lặng lẽ.
- `MimeType` sau xử lý luôn là `image/webp`, quality 82. Bản Library và file full có cạnh dài ≤ 1600px, **không upscale**.
- DB lưu **đường dẫn tương đối**, không lưu URL. Ảnh trong nội dung builder đi qua `resolveImage`; field DTO `*Url` cho Portal (vd. `logoUrl`, #88) do BE trả sẵn có tiền tố `/media/` (`ImagePaths.MediaUrl`).
- Không sửa tay `packages/api-sdk/src/generated/**` và `packages/builder-components/generated/**`.
- Không ghi thẳng `contracts/openapi/*.json`; hook `guard-bash.mjs` chặn cả lệnh đọc đường dẫn contract, nên dùng Read/Grep.
- Bước 4 **không xoá file nào**, ngoài thao tác shop chủ động xoá khỏi Library (và đó cũng chỉ là soft delete) (`08` §4).
- `builder-renderer` / `builder-components` isomorphic: `resolveImage` không đụng `window`/`document` (#23).

---

## 4. Review focus: năm lỗi dễ lọt nhất khi spec im lặng

Mỗi dòng dưới đây đã có test ở task chịu trách nhiệm:

| # | Input / tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | `GET /media/../appsettings.json`, `/media/shops/%2e%2e/...`, `\`, đường dẫn tuyệt đối | 404. Không bao giờ đọc ra ngoài root của storage | T9 |
| R2 | Ảnh điện thoại chụp dọc (EXIF `Orientation = 6`) | File sau xử lý đứng đúng chiều **rồi mới** xoá EXIF. Xoá EXIF trước thì ảnh bị xoay nằm ngang | T1 |
| R3 | Ảnh nhỏ hơn preset (400×300 vào `1600x900,cover`) | Không upscale: crop đúng tỉ lệ 16:9 ở độ phân giải gốc (400×225); `Width`/`Height` ghi kích thước thật | T1 |
| R4 | Lỗi DB sau khi đã ghi file (ví dụ vi phạm CHECK, hay mất kết nối) | Request fail; best-effort xoá các key vừa ghi; không để record trỏ file không tồn tại | T5 |
| R5 | PNG trong suốt (logo), GIF, WebP động | PNG giữ kênh alpha trong webp. GIF → `MEDIA_UNSUPPORTED_FORMAT`. WebP động → chỉ lấy khung đầu | T1 |

---

## 5. Sơ đồ file

```
config/
  image-presets.json            ← đổi shape thành { presets, surfaces }, đủ 9 preset (T0.1)
  reserved-routes.json          ← + "media" (T0.2)
  binding-sources.json          ← + "Shop" (T0.2)
packages/builder-components/
  meta/prop-kinds.ts            ← BindingPropDef.imagePresets (T0.3)
  meta/manifest-schema.ts       ← zod cho imagePresets (T0.3)
  scripts/gen-registry.ts       ← đọc shape mới, validate imagePresets, ghi derivative-presets.json (T0.1, T0.3)
  scripts/lib/lock-snapshot.ts  ← snapshot imagePresets, đổi → warning (T0.3)
  registry/service-grid.manifest.ts ← imagePresets cho Service/ServiceGroup (T0.3)
  generated/derivative-presets.json ← sinh ra (T0.3)
  src/context.tsx               ← thay thân resolveImage + mediaMap (F1)
apps/portal/vite.config.ts      ← đọc shape mới (T0.1); proxy /media; sửa comment (F1)

backend/src/
  Vsite.Domain/Exceptions/UnprocessableException.cs           (T1)
  Vsite.Domain/Media/Entities/MediaAsset.cs                   (T4)
  Vsite.Domain/Shop/Entities/Shop.cs                          ← + Guid? LogoId (T4)
  Vsite.Application/Common/Imaging/                           (T1–T3)
    ImageUploadOptions.cs IImageProcessor.cs ISourceImage.cs ImageTransform.cs EncodedImage.cs
    IObjectStorage.cs StorageOptions.cs ImagePaths.cs IImagePresetCatalog.cs IDerivativePresetCatalog.cs
  Vsite.Application/Media/                                    (T5–T8)
    Dtos/ Interfaces/IMediaReferenceValidator.cs
    Uploads/Commands/{UploadToSlot,UploadToLibrary}/  Library/Commands/{CloneFromLibrary,DeleteFromLibrary}/
    Library/Queries/{ListLibrary,GetReferences}/  Assets/Queries/{GetAssetsByIds,GetUsage}/
    Logo/Commands/UploadShopLogo/  MediaAssetWriter.cs (dùng chung cho các handler)
  Vsite.Application/Shop/Interfaces/IShopLogoWriter.cs        (T7)
  Vsite.Infrastructure/Imaging/                               (T1–T3)
    ImageSharpImageProcessor.cs LocalDiskObjectStorage.cs S3ObjectStorage.cs
    ImagePresetCatalog.cs DerivativePresetCatalog.cs
  Vsite.Infrastructure/Media/MediaReferenceValidator.cs       (T8)
  Vsite.Infrastructure/Shop/ShopLogoWriter.cs                 (T7)
  Vsite.Infrastructure/Persistence/Configurations/Media/MediaAssetConfiguration.cs (T4)
  Vsite.Infrastructure/Persistence/Migrations/*_AddMediaAsset.cs (T4)
  Vsite.Api/Media/MediaEndpoints.cs  Vsite.Api/Media/MediaFileMiddleware.cs (T5–T9)
backend/tests/
  ArchitectureTests/ModuleBoundaryTests.cs  ← SharedSegments + "Imaging" (T3)
  ArchitectureTests/ReservedRoutesTests.cs  ← khẳng định có "media" (T3)
  IntegrationTests/Imaging/  IntegrationTests/Media/  (T1–T9)
  IntegrationTests/TestAssets/  ← ảnh mẫu thật: exif-gps.jpg, rotated-6.jpg, alpha.png, sample.heic, animated.gif
```

---

## 6. Session S0: config + Component Registry

Chỉ ghi `config/` và `packages/builder-components/` (ngoài `src/`), cộng một dòng ở
`apps/portal/vite.config.ts` để plugin placeholder đọc được shape mới.

### T0.1: `image-presets.json` đủ 9 preset, shape `{ presets, surfaces }`

**Vì sao đổi shape:** hiện file là object phẳng khoá theo tên preset. Nếu thêm key `surfaces` vào
cùng cấp, gen-registry sẽ coi `"surfaces"` là một preset hợp lệ. Xem "Giả định" A1.

```json
{
  "presets": {
    "1600x900,cover":   { "width": 1600, "height": 900,  "fit": "cover",  "usage": "Hero full-width (16:9)" },
    "1600x600,cover":   { "width": 1600, "height": 600,  "fit": "cover",  "usage": "Banner mỏng, nền section" },
    "1200x630,cover":   { "width": 1200, "height": 630,  "fit": "cover",  "usage": "OG image (chuẩn Facebook)" },
    "1200x1200,inside": { "width": 1200, "height": 1200, "fit": "inside", "usage": "Ảnh trong nội dung, giữ tỉ lệ gốc" },
    "800x800,cover":    { "width": 800,  "height": 800,  "fit": "cover",  "usage": "Gallery, ảnh vuông" },
    "800x600,cover":    { "width": 800,  "height": 600,  "fit": "cover",  "usage": "Thẻ dịch vụ / thẻ nội dung (4:3)" },
    "320x96,inside":    { "width": 320,  "height": 96,   "fit": "inside", "usage": "Logo trên Header, không crop" },
    "160x160,cover":    { "width": 160,  "height": 160,  "fit": "cover",  "usage": "Thumbnail dải ảnh" },
    "96x96,cover":      { "width": 96,   "height": 96,   "fit": "cover",  "usage": "Avatar" }
  },
  "surfaces": {
    "Shop": ["320x96,inside", "96x96,cover"]
  }
}
```

- [ ] Viết test trước trong `tests/config-files.test.ts`: `presets` có đúng 9 key đã liệt kê; mọi `fit` ∈ {`cover`, `inside`}; mọi `height` là số (không còn `null`); không còn `600xR,cover`; mọi preset trong `surfaces.*` đều có trong `presets`; mọi key của `surfaces` đều có trong `binding-sources.json`
- [ ] Chạy `pnpm --filter @vsite/builder-components test`: FAIL
- [ ] Sửa `config/image-presets.json` như trên
- [ ] Sửa `scripts/gen-registry.ts:23` đọc `.presets`; sửa `apps/portal/vite.config.ts:16` đọc `.presets`
- [ ] `pnpm gen:registry && pnpm --filter @vsite/builder-components test`: PASS; `registry.lock.json` **không đổi** (Hero/Gallery vẫn dùng `1600x900,cover` và `800x800,cover`)
- [ ] Commit `feat(config): 9 image presets + surfaces (#78, #86)`

### T0.2: `media` reserved, `Shop` là binding source

- [ ] Test trong `tests/config-files.test.ts`: `reservedPaths` chứa `media`; `binding-sources` chứa `Shop`
- [ ] FAIL → thêm `"media"` vào `reservedPaths` và `"Shop"` vào `binding-sources.json` → PASS
- [ ] Commit `feat(config): reserve /media, bind source Shop (#24, #73)`

### T0.3: field `imagePresets` trên `binding` + artifact bộ phái sinh

```ts
// meta/prop-kinds.ts
interface BindingPropDef extends PropDefBase {
  kind: 'binding';
  sources: string[];
  allowFilters?: string[];
  /** #86 — preset ảnh component hiển thị cho dữ liệu của từng source. Key ⊆ sources, value ⊆ image-presets. */
  imagePresets?: Partial<Record<string, string[]>>;
}
```

Artifact `generated/derivative-presets.json` (sắp xếp key và preset để diff ổn định):

```json
{ "Service": ["800x600,cover"], "ServiceGroup": ["800x600,cover"], "Shop": ["320x96,inside", "96x96,cover"] }
```

= hợp theo từng source của mọi `imagePresets` trong manifest ∪ `surfaces` của `config/image-presets.json`.

- [ ] Test trong `tests/gen-registry.test.ts`:
  - FAIL khi key của `imagePresets` không nằm trong `sources`
  - FAIL khi preset không có trong `presets`
  - output hợp đúng manifest ∪ surfaces, không trùng, đã sort
- [ ] Test trong `check-additive`: thêm `imagePresets` → PASS; bỏ một preset khỏi `imagePresets` → **warning**, không fail (cùng khuôn preset của `image`)
- [ ] FAIL → sửa `prop-kinds.ts`, `manifest-schema.ts` (zod `z.record(z.array(z.string()).min(1)).optional()`), `gen-registry.ts` (validate + ghi file thứ 8), `scripts/lib/lock-snapshot.ts` (snapshot `imagePresets`)
- [ ] `registry/service-grid.manifest.ts`: thêm `imagePresets: { Service: ['800x600,cover'], ServiceGroup: ['800x600,cover'] }`
- [ ] `pnpm gen:registry && pnpm check:registry-additive && pnpm --filter @vsite/builder-components test`: PASS
- [ ] Commit `feat(registry): binding.imagePresets + derivative-presets artifact (#86)`

### Kết thúc S0: 🧑 duyệt Registry

Viết `Docs/tasks/MEDIA-001/registry-diff.md` gồm: diff `registry.lock.json`, shape mới của
`image-presets.json`, nội dung `derivative-presets.json`, `media` trong reserved routes, cùng mục
**"Giả định tôi đã tự đặt"** (A1, A2 ở §10). **Dừng lại chờ duyệt.** Chưa được duyệt thì chưa mở S1.

---

## 7. Session S1: Backend

Chỉ ghi `backend/`. Chạy test bằng `dotnet test backend/vsite.sln`. Test có Testcontainers mà máy
không có Docker thì **vẫn viết đầy đủ** và ghi nợ vào `Docs/DOCKER-TEST-DEBT.md` theo quy ước của file.

### T1: `IImageProcessor` (ImageSharp)

**Interface do task này tạo ra:**

```csharp
namespace Vsite.Application.Common.Imaging;

public sealed class ImageUploadOptions
{
    public const string Section = "Imaging:Upload";
    public long MaxBytes { get; init; } = 10 * 1024 * 1024;   // #85
    public long MaxPixels { get; init; } = 25_000_000;        // #85
    public int MaxLongEdge { get; init; } = 1600;             // #53
    public int WebpQuality { get; init; } = 82;
}

public interface IImageProcessor
{
    /// Thứ tự cố định: size → magic bytes (JPEG/PNG/WebP; HEIC → lỗi riêng) → pixel từ header
    /// (Image.IdentifyAsync, CHƯA decode) → decode → AutoOrient → xoá toàn bộ metadata (EXIF/ICC/XMP/IPTC).
    Task<ISourceImage> LoadAsync(Stream input, CancellationToken ct);
}

public interface ISourceImage : IDisposable
{
    int Width { get; }
    int Height { get; }
    Task<EncodedImage> RenderAsync(ImageTransform transform, CancellationToken ct);
}

public abstract record ImageTransform
{
    public sealed record LongEdge(int Max) : ImageTransform;                         // bản Library, file full
    public sealed record Cover(int Width, int Height, FocalPoint Focal) : ImageTransform;
    public sealed record Inside(int Width, int Height) : ImageTransform;
}

public readonly record struct FocalPoint(float X, float Y)
{
    public static readonly FocalPoint Center = new(0.5f, 0.5f);
}

public sealed record EncodedImage(byte[] Bytes, int Width, int Height)
{
    public string MimeType => "image/webp";
}
```

Lỗi ném ra là `UnprocessableException(errorCode, message)`, tạo mới trong
`Vsite.Domain/Exceptions/`: `: AppException(errorCode, message, statusCode: 422)`. Có các mã:
`MEDIA_FILE_TOO_LARGE`, `MEDIA_UNSUPPORTED_FORMAT`, `MEDIA_HEIC_UNSUPPORTED`, `MEDIA_TOO_MANY_PIXELS`,
`MEDIA_CORRUPT_IMAGE`.

Luật không upscale (R3): `Cover` crop về đúng tỉ lệ khung quanh focal point, rồi chỉ thu nhỏ nếu
lớn hơn khung. `Inside` và `LongEdge` chỉ thu nhỏ.

- [ ] Thêm `SixLabors.ImageSharp` vào `Directory.Packages.props` (lấy bản 3.x mới nhất) và reference từ `Vsite.Infrastructure`
- [ ] Thêm ảnh mẫu **thật** vào `IntegrationTests/TestAssets/` (không sinh bằng code, vì test EXIF phải chạy trên file thật): `exif-gps.jpg`, `rotated-6.jpg` (dọc, Orientation=6), `alpha.png`, `sample.heic`, `animated.gif`, `animated.webp`
- [ ] Viết test `IntegrationTests/Imaging/ImageSharpImageProcessorTests.cs` (không cần container):
  - `Jpg_renamed_to_png_is_detected_by_magic_bytes`: bytes JPEG → `LoadAsync` thành công và **không** tin extension. Bytes rác kèm tên `.png` → `MEDIA_UNSUPPORTED_FORMAT` *(test bắt buộc 1)*
  - `Exif_gps_is_stripped`: load `exif-gps.jpg`, render, đọc lại **byte output** bằng `Image.Identify` → `Metadata.ExifProfile` là null *(test bắt buộc 2)*
  - `Orientation_is_applied_before_strip`: `rotated-6.jpg` (lưu ngang 4000×3000, Orientation=6) → output có `Height > Width` (R2)
  - `Heic_is_rejected_with_specific_code` → `MEDIA_HEIC_UNSUPPORTED`
  - `Gif_is_rejected` → `MEDIA_UNSUPPORTED_FORMAT`; `Animated_webp_takes_first_frame` → output chỉ có 1 frame (R5)
  - `Over_max_bytes_is_rejected` (options `MaxBytes = 1024`) → `MEDIA_FILE_TOO_LARGE`
  - `Over_max_pixels_is_rejected_before_decode` (options `MaxPixels = 100`) → `MEDIA_TOO_MANY_PIXELS`
  - `Cover_crops_to_exact_size` (2000×1500 → `Cover(1600,900)` → 1600×900); `Cover_never_upscales` (400×300 → `Cover(1600,900)` → 400×225) (R3)
  - `Inside_keeps_ratio` (2000×500 → `Inside(320,96)` → 320×80); `LongEdge_caps_at_1600`
  - `Focal_point_shifts_crop`: ảnh nửa trái đỏ, nửa phải xanh, `Cover(100,100)` với focal (0.9, 0.5) → pixel giữa output là xanh
  - `Png_alpha_is_preserved` (R5); `Output_is_webp` (magic `RIFF....WEBP`)
- [ ] Chạy: FAIL (chưa có type)
- [ ] Viết `ImageSharpImageProcessor` + `UnprocessableException`; đăng ký DI trong `AddInfrastructure()` cùng `services.Configure<ImageUploadOptions>(config.GetSection(ImageUploadOptions.Section))`
- [ ] Chạy: PASS
- [ ] Commit `feat(imaging): ImageSharp pipeline — magic bytes, EXIF strip, cover/inside (#53, #84, #85)`

### T2: `IObjectStorage`, LocalDisk + S3

```csharp
namespace Vsite.Application.Common.Imaging;

public interface IObjectStorage
{
    /// Không ghi đè (#75): key đã tồn tại → ObjectAlreadyExistsException.
    Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken ct);
    Task<bool> ExistsAsync(string key, CancellationToken ct);
    /// null nếu không tồn tại.
    Task<StoredObject?> OpenReadAsync(string key, CancellationToken ct);
    /// Chỉ dùng cho rollback best-effort (R4) và soft-delete không đụng file. Bước 4 không gọi từ luồng nghiệp vụ.
    Task DeleteAsync(string key, CancellationToken ct);
}

public sealed record StoredObject(Stream Content, string ContentType, long Length);

public sealed class StorageOptions
{
    public const string Section = "Storage";
    public StorageProvider Provider { get; init; } = StorageProvider.LocalDisk;
    public string LocalDiskRoot { get; init; } = "../../../.media";   // tương đối với ContentRoot (backend/src/Vsite.Api) → <repo>/.media
    public S3StorageOptions S3 { get; init; } = new();
}
public enum StorageProvider { LocalDisk, S3 }
public sealed class S3StorageOptions
{
    public string BucketName { get; init; } = "";
    public string? ServiceUrl { get; init; }       // MinIO / S3-compatible
    public string Region { get; init; } = "ap-southeast-1";
    public bool ForcePathStyle { get; init; }
    public string? AccessKey { get; init; }         // null → credential chain mặc định của AWS
    public string? SecretKey { get; init; }
}
```

Mọi key đều qua `ImagePaths.ValidateKey` (T3). Key phải bắt đầu bằng `shops/`, không chứa `..`,
`\`, `//` hay ký tự điều khiển, và không bắt đầu bằng `/`. LocalDisk còn kiểm thêm:
`Path.GetFullPath(Path.Combine(root, key))` phải nằm dưới `root`, và ghi bằng `FileMode.CreateNew`.
S3 dùng `PutObjectRequest.IfNoneMatch = "*"`; lỗi `412` thì ném `ObjectAlreadyExistsException`.

- [ ] Thêm `AWSSDK.S3` và `Testcontainers.Minio` (3.10.0, cùng bản với các Testcontainers đang dùng) vào `Directory.Packages.props`
- [ ] Viết test `IntegrationTests/Imaging/ObjectStorageContractTests.cs` dạng abstract, hai lớp con `LocalDiskObjectStorageTests` (thư mục temp, không cần Docker) và `S3ObjectStorageTests` (MinIO container):
  - put rồi open trả đúng bytes và content type
  - put hai lần cùng key → `ObjectAlreadyExistsException`, nội dung cũ giữ nguyên
  - open key không tồn tại → null
  - key `shops/../x`, `/etc/passwd`, `shops\\a`, `other/a` → `ArgumentException`, không chạm đĩa hay bucket
- [ ] FAIL → viết `LocalDiskObjectStorage`, `S3ObjectStorage`; DI chọn theo `StorageOptions.Provider` → PASS (phần S3 ghi nợ Docker nếu không có Docker)
- [ ] Thêm `.media/` vào `.gitignore`
- [ ] Commit `feat(imaging): IObjectStorage LocalDisk + S3, no-overwrite (#75, #83)`

### T3: `ImagePaths`, catalog preset, ranh giới module

```csharp
namespace Vsite.Application.Common.Imaging;

public static class ImagePaths
{
    public static string NewWebsiteKey(Guid shopId, DateTimeOffset now)            // shops/{shopId}/website/{yyyy}/{MM}/{uuid}.webp
    public static string ListingFolder(Guid shopId, Guid listingId)                // shops/{shopId}/listings/{id}/
    public static string ProductFolder(Guid shopId, Guid productId)                // shops/{shopId}/products/{id}/
    public static string AttributeFolder(Guid shopId, Guid attributeId)            // shops/{shopId}/attributes/{id}/
    public static string NewFullKey(string folder)                                 // {folder}{uuid}.webp
    public static string Thumb(string fullKey)                                     // .../thumb_{uuid}.webp
    public static string FeaturedThumb(string fullKey)                             // .../fthumb_{uuid}.webp
    public static bool IsUnder(string key, string folder)                          // dùng cho kiểm #81
    public static void ValidateKey(string key)                                     // ném ArgumentException
}

public sealed record ImagePreset(string Name, int Width, int Height, PresetFit Fit)
{
    public ImageTransform ToTransform(FocalPoint focal) => Fit == PresetFit.Cover
        ? new ImageTransform.Cover(Width, Height, focal)
        : new ImageTransform.Inside(Width, Height);
}
public enum PresetFit { Cover, Inside }

public interface IImagePresetCatalog { bool TryGet(string name, out ImagePreset preset); IReadOnlyCollection<ImagePreset> All { get; } }
public interface IDerivativePresetCatalog { IReadOnlyList<string> For(string source); }   // "Shop" → ["320x96,inside","96x96,cover"]
```

Hai file `config/image-presets.json` và `packages/builder-components/generated/derivative-presets.json`
được **link** vào output của `Vsite.Api` giống cách `reserved-routes.json` đang làm (`Vsite.Api.csproj:38-39`),
rồi đọc một lần lúc startup. Thiếu file hoặc sai shape thì **app không khởi động** (fail fast), không
chạy tiếp với danh sách rỗng.

- [ ] Test `IntegrationTests/Imaging/ImagePathsTests.cs`: `Thumb("shops/a/products/b/c.webp") == "shops/a/products/b/thumb_c.webp"`; `FeaturedThumb` tương tự; `IsUnder` từ chối prefix giả (`shops/a/products/b-evil/…` so với folder `…/b/`); `NewWebsiteKey` đúng format
- [ ] Test `ImagePresetCatalogTests`: đọc file thật ở repo → đúng 9 preset; `"320x96,inside"` có `Fit = Inside`; `DerivativePresetCatalog.For("Shop")` bằng nội dung artifact; `For("Unknown")` → rỗng
- [ ] `ArchitectureTests/ReservedRoutesTests.cs`: thêm khẳng định `ReservedPaths` chứa `"media"` (test bắt buộc 12, nửa sau)
- [ ] `ModuleBoundaryTests.SharedSegments["Infrastructure"]` thêm `"Imaging"` (#82). **Application không cần thêm**, vì `Common` đã là shared
- [ ] FAIL → viết code, link file trong csproj → PASS
- [ ] Commit `feat(imaging): ImagePaths + preset catalogs; Imaging shared segment (#82)`

### T4: Entity `MediaAsset`, `Shop.LogoId`, migration

`MediaAsset : ShopAuditableEntity` (được Global Query Filter theo `ShopId` và soft delete). Cột theo
`08` §2, có hai điểm lệch có chủ đích (ghi vào changelog):

- dùng `IsDeleted` của base class thay cho `DeletedAt`
- tên bảng `MediaAsset` theo quy ước bảng đang có trong code (`Shop`, `UserShop`), không dùng `media_assets`

```csharp
public sealed class MediaAsset : ShopAuditableEntity
{
    public string StorageKey { get; private set; }         // varchar(300), bất biến
    public string MimeType { get; private set; }           // varchar(80)
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long SizeBytes { get; private set; }
    public string? AltText { get; set; }                   // varchar(200)
    public float FocalPointX { get; private set; } = 0.5f;
    public float FocalPointY { get; private set; } = 0.5f;
    public string? OriginalFileName { get; private set; }  // varchar(200)
    public string? Folder { get; set; }                    // varchar(100)
    public bool IsInLibrary { get; private set; }
    public string? Preset { get; private set; }            // varchar(40)
    public Guid? SourceAssetId { get; private set; }

    public static MediaAsset NewLibrary(Guid shopId, string key, EncodedImage img, FocalPoint focal, string? fileName, string? alt);
    public static MediaAsset NewDirect(Guid shopId, string key, EncodedImage img, string preset, FocalPoint focal, string? fileName, string? alt);
    /// Ném DomainException nếu source.IsInLibrary == false (#71: không clone từ clone)
    public static MediaAsset NewDerived(MediaAsset source, string key, EncodedImage img, string preset, FocalPoint focal);
}
```

Cấu hình trong `Persistence/Configurations/Media/MediaAssetConfiguration.cs`:

- CHECK `ck_media_library_preset` (§2.1 [1])
- UNIQUE `StorageKey`; UNIQUE `(Id, ShopId)`
- index filter `ix_media_library (ShopId, CreatedAt DESC) WHERE "IsInLibrary" AND NOT "IsDeleted"`
- index `ix_media_derivative (SourceAssetId, Preset) WHERE "SourceAssetId" IS NOT NULL`
- FK `ShopId → Shop` (Restrict); FK `SourceAssetId → MediaAsset` `ON DELETE SET NULL`
- **FK ghép khai từ phía Media** (cùng khuôn `UserShop` ở `ShopConfiguration.cs:28-35`, và `Persistence` là shared nên không vi phạm ranh giới):

```csharp
modelBuilder.Entity<Shop>()
    .HasOne<MediaAsset>().WithMany()
    .HasForeignKey(s => new { s.LogoId, s.Id })
    .HasPrincipalKey(m => new { m.Id, m.ShopId })
    .OnDelete(DeleteBehavior.Restrict);
```

(Postgres `MATCH SIMPLE`: khi `LogoId IS NULL` thì FK không áp dụng, đúng ý.)

Thêm `DbSet<MediaAsset> MediaAssets` vào `IAppDbContext` / `AppDbContext`, đặt trong section riêng của Media.

- [ ] Test `IntegrationTests/Media/MediaDbConstraintTests.cs` (Postgres Testcontainers):
  - insert `IsInLibrary=false, Preset=NULL` bằng SQL thô → `PostgresException` vi phạm `ck_media_library_preset` *(test bắt buộc 11)*
  - insert `IsInLibrary=true, Preset='96x96,cover'` → cũng bị từ chối (chiều ngược lại)
  - trùng `StorageKey` → bị từ chối
  - `UPDATE "Shop" SET "LogoId" = <asset của shop B> WHERE "Id" = <shop A>` → vi phạm FK *(test bắt buộc 8)*
  - xoá cứng bản Library → `SourceAssetId` của clone thành NULL
- [ ] Unit test `MediaAssetTests`: `NewDerived` từ record `IsInLibrary=false` → `DomainException("MEDIA_CLONE_FROM_CLONE")`
- [ ] FAIL → entity, configuration, `Shop.LogoId`, `dotnet ef migrations add AddMediaAsset` (project Infrastructure, startup Api) → đọc lại SQL migration, kiểm đủ CHECK, index filter và FK ghép → PASS
- [ ] Tenant isolation: thêm `MediaAsset` vào `IntegrationTests/Identity/TenantIsolationTests.cs` (hoặc test cùng khuôn trong `Media/`): shop A query không thấy asset của shop B
- [ ] Commit `feat(media): MediaAsset entity + Shop.LogoId composite FK (#69, #76)`

### T5: Upload vào slot (hai chế độ) và upload vào Library

Helper dùng chung `MediaAssetWriter` (Application/Media), scoped. Nó nhận `ISourceImage` cùng danh
sách `(preset | LongEdge)`, render từng bản, `PutAsync` từng key, ghi lại các key đã ghi, rồi tạo
entity. Nếu `SaveChangesAsync` ném lỗi thì gọi `DeleteAsync` best-effort cho từng key đã ghi, xong
**ném lại** exception gốc (R4).

| Command | Input | Tạo ra |
|---|---|---|
| `UploadToSlotCommand(ShopId, Stream File, string FileName, string Preset, float FocalX, float FocalY, bool SaveToLibrary, string? AltText)` | không tick | 1 record `NewDirect` (#70) |
| (cùng command) | có tick | `NewLibrary` (LongEdge 1600) + `NewDerived(library, preset)`, cùng **một** `SaveChangesAsync` |
| `UploadToLibraryCommand(ShopId, Stream File, string FileName, string? AltText, string? Folder)` | | 1 record `NewLibrary` |

Validator (FluentValidation):

- `Preset` phải có trong `IImagePresetCatalog` → 422 `MEDIA_UNKNOWN_PRESET`
- `FocalX`/`FocalY` ∈ [0, 1]
- `AltText` ≤ 200 ký tự, `Folder` ≤ 100 ký tự
- `FileName` cắt còn 200 ký tự

Endpoint dùng multipart, `.DisableAntiforgery()` (API dùng JWT bearer, không dùng cookie), và
`[RequestSizeLimit(11 MB)]` để Kestrel chặn sớm → 413.

- [ ] Test `IntegrationTests/Media/UploadEndpointTests.cs` (`MediaApiFactory`: Postgres + Redis, storage LocalDisk thư mục temp):
  - không tick → đúng 1 record, `Preset` = preset slot, `SourceAssetId IS NULL`, `IsInLibrary=false`, file tồn tại và có đúng kích thước preset *(test bắt buộc 3)*
  - có tick → 2 record; clone có `SourceAssetId` = id bản Library; response trả id **clone** làm asset đặt vào tree *(test bắt buộc 4)*
  - preset lạ → 422 `MEDIA_UNKNOWN_PRESET`; focal 1.5 → 422
  - user shop A gọi `/shops/{B}/media/slot-uploads` → 403 `SHOP_ACCESS_DENIED`
  - body multipart có field `shopId` khác → bị bỏ qua, record vẫn thuộc shop trong route (#21.4)
  - R4: thay `IAppDbContext.SaveChangesAsync` bằng bản ném lỗi (factory override), sau request thì thư mục storage **rỗng**
- [ ] FAIL → handler, validator, `MediaAssetWriter`, `MediaEndpoints` (`AddOpenApi("media", …)` trong `Program.cs`, group `.WithGroupName("media")`, `RequireGlobalScope` + `RequireShopMembership()`, `Produces`/`ProducesProblem` đủ mã) → PASS
- [ ] Commit `feat(media): slot upload (2 modes) + library upload (#70)`

### T6: Library: list, clone, delete, references, tra theo lô, quota

| Query / Command | Luật |
|---|---|
| `ListLibraryQuery(ShopId, Page, PageSize)` | `IsInLibrary = true`, chưa xoá, `CreatedAt DESC`, trả `PagedResult<MediaAssetDto>`. `pageSize` mặc định 24, tối đa 100 |
| `CloneFromLibraryCommand(ShopId, AssetId, Preset, float? FocalX, float? FocalY)` | Nguồn load **trong một query** `Id == AssetId && IsInLibrary` (filter đã ràng `ShopId`), không thấy → 404. Focal mặc định lấy từ bản Library. Sinh key mới (#75) |
| `DeleteFromLibraryCommand(ShopId, AssetId)` | Chỉ `Owner` (cùng khuôn `UpdateShopHandler`). Soft delete bản Library. **Không** đụng clone hay phái sinh, **không** xoá file. Asset đó đang là `Shop.LogoId` thì vẫn xoá được (cảnh báo nằm ở `references`) |
| `GetReferencesQuery(ShopId, AssetId)` | Hiện chỉ có một nguồn tham chiếu thật: `Shop.LogoId`. Trả `{ references: [{ kind: "ShopLogo" }] }`. Tree và snapshot chưa tồn tại (Bước 5/8), nên ghi TODO có trỏ `08` §4 trong code |
| `GetAssetsByIdsQuery(ShopId, Guid[] Ids)` | Tối đa 200 id. Trả những id tìm thấy **kể cả đã soft delete** (clone của ảnh đã xoá vẫn phải render, #72) nhưng vẫn cùng shop: `IgnoreQueryFilters()` rồi `Where(ShopId == tenant)` viết tay trong cùng query |
| `GetUsageQuery(ShopId)` | `SUM(SizeBytes) WHERE SourceAssetId IS NULL AND NOT IsDeleted` (#55) |

- [ ] Test `IntegrationTests/Media/LibraryEndpointTests.cs`:
  - clone → record mới, `StorageKey` khác bản Library, `SourceAssetId` đúng, kích thước đúng preset *(test bắt buộc 5)*
  - clone từ id là clone → 404 (không lộ là tồn tại)
  - clone asset của shop B → 404
  - xoá bản Library → list không còn; `GetAssetsByIds([cloneId])` vẫn trả clone, file clone vẫn mở được qua `/media/…` *(test bắt buộc 9)*
  - usage trước/sau clone và trước/sau upload logo (sinh phái sinh) **không đổi**; sau upload thẳng thì tăng đúng `SizeBytes` *(test bắt buộc 10)*
  - list phân trang đúng shape; không chứa record `IsInLibrary=false`
  - `GetAssetsByIds` chứa id của shop B → id đó không có trong kết quả
  - delete bởi non-Owner → 403 `MEDIA_OWNER_REQUIRED`. Phase 1 chỉ có Owner, nên dựng membership role khác bằng SQL trong test
- [ ] FAIL → code → PASS
- [ ] Commit `feat(media): library list/clone/delete/references, assets lookup, usage (#55, #71, #72)`

### T7: Upload logo

```csharp
namespace Vsite.Application.Shop.Interfaces;
/// Public Contract của Shop cho Media (#82). Chỉ set property trên entity đang được track;
/// KHÔNG gọi SaveChanges. Người gọi lưu trong cùng unit of work.
public interface IShopLogoWriter { Task SetLogoAsync(Guid shopId, Guid libraryAssetId, CancellationToken ct); }
```

`UploadShopLogoCommand(ShopId, Stream File, string FileName)`: chỉ `Owner`. Luồng:

1. Pipeline → `NewLibrary`.
2. Với mỗi preset trong `IDerivativePresetCatalog.For("Shop")`: `NewDerived` với focal `Center`. Nếu artifact rỗng thì fail fast ngay lúc startup, không sinh logo thiếu.
3. `IShopLogoWriter.SetLogoAsync`, rồi **một** `SaveChangesAsync`.

Trả `ShopLogoDto(libraryAsset, derivatives[])`.

`ShopDto` thêm `logoId` (additive trên contract `shop`). Xem câu hỏi mở ở `data-needs.md`.

- [ ] Test `IntegrationTests/Media/ShopLogoTests.cs`:
  - upload → 1 bản Library + đúng N phái sinh, N = `For("Shop").Count`, mỗi `Preset` một lần, cùng `SourceAssetId`
  - `Shop.LogoId` = id bản Library; `GET /shops/{id}` trả `logoId`
  - logo PNG trong suốt `1000×200` → phái sinh `320x96,inside` có kích thước 320×64 và giữ alpha
  - upload lần hai → `LogoId` đổi sang bản mới, bản cũ vẫn còn trong Library (không xoá gì)
  - non-Owner → 403
- [ ] Test kiến trúc: `ModuleBoundaryTests` vẫn xanh (Media → Shop hợp lệ; Shop **không** reference Media)
- [ ] FAIL → code → PASS
- [ ] Commit `feat(media): shop logo upload + derivatives (#73, #82)`

### T8: Public Contract `IMediaReferenceValidator` (cho Bước 5)

Bước 4 chưa có endpoint lưu draft hay page. Vì vậy test bắt buộc 6 và 7 kiểm **service** mà Bước 5
sẽ gọi, không kiểm qua endpoint (ghi vào changelog là "chưa làm xong: nối vào handler lưu draft ở Bước 5").

```csharp
namespace Vsite.Application.Media.Interfaces;
public interface IMediaReferenceValidator
{
    /// Một câu query (08 §3.5). ShopId lấy từ TenantContext. Sai bất kỳ id nào → UnprocessableException("MEDIA_INVALID_IMAGE_REFERENCE").
    Task EnsureValidAsync(IReadOnlyCollection<Guid> treeImageIds, IReadOnlyCollection<Guid> businessImageIds, CancellationToken ct);
}
```

- [ ] Test `IntegrationTests/Media/MediaReferenceValidatorTests.cs`:
  - id bản Library trong `treeImageIds` → ném lỗi *(test bắt buộc 6)*
  - id clone của shop B trong `treeImageIds` (tenant context = A) → ném lỗi *(test bắt buộc 7)*
  - id clone trong `businessImageIds` → ném lỗi
  - id đã soft delete → ném lỗi
  - hợp lệ, có id lặp → không ném (đếm theo id **khác nhau**)
  - dùng interceptor đếm lệnh: đúng **1** lệnh SQL cho cả hai danh sách
- [ ] FAIL → `MediaReferenceValidator` (Infrastructure/Media) → PASS
- [ ] Commit `feat(media): IMediaReferenceValidator public contract (#77)`

### T9: Phục vụ `/media/*`

`MediaFileMiddleware` gắn bằng `app.Map("/media", …)` **trước** `TenantResolutionMiddleware`: không
tra Redis, không kiểm tenant (#81), không auth. Chỉ nhận `GET` và `HEAD`. Luồng:

1. `ImagePaths.ValidateKey`; sai → 404.
2. `IObjectStorage.OpenReadAsync`; null → 404.
3. Stream ra với `Content-Type` theo dữ liệu đã lưu, `Cache-Control: public, max-age=3600` (`immutable` để Bước 8, `08` §10 #7), `X-Content-Type-Options: nosniff`.

Không xuất hiện trong OpenAPI (không phải contract API).

- [ ] Test `IntegrationTests/Media/MediaFileServingTests.cs`:
  - upload một ảnh rồi `GET /media/{storageKey}` với `Host: vsite.local`, `Host: spa-abc.vsite.local` (shop có slug `spa-abc`) và `Host: admin.vsite.local` → cả ba 200, bytes giống nhau *(test bắt buộc 12)*
  - R1: `/media/../appsettings.json`, `/media/shops/%2e%2e/%2e%2e/appsettings.json`, `/media/shops%5c..%5cx`, `/media//etc/passwd` → 404, và root storage không bị đọc ngoài phạm vi
  - `POST /media/x` → 405
  - không cần token
- [ ] FAIL → code → PASS
- [ ] Commit `feat(media): serve /media/* on every host via IObjectStorage (#53, #81, #83)`

### T10: Kết thúc S1

- [ ] `dotnet build backend/vsite.sln`: 0 error, 0 warning; `dotnet test`: xanh, hoặc phần cần Docker đã ghi nợ vào `Docs/DOCKER-TEST-DEBT.md` kèm lệnh chính xác, lý do, ngày, task và điều kiện xong
- [ ] `appsettings.json`: thêm section `Storage` (Provider `LocalDisk`) và `Imaging:Upload`; `appsettings.Development.json` có ví dụ S3/MinIO được **comment**, không có secret thật
- [ ] Viết `backend/docs/modules/media.md`: bảng 3 loại record, vì sao không có proxy, luồng logo đi `Media → IShopLogoWriter`, cảnh báo "xoá node không xoá ảnh" (`08` §4); thêm dòng Media vào bảng "Tài liệu từng module" ở `backend/CLAUDE.md`
- [ ] Chạy skill `contract-sync` → `contracts/openapi/.staging/media.v1.json` (+ `shop.v1.json` vì có `logoId`) → viết `Docs/tasks/MEDIA-001/contract-diff.md` theo mẫu workflow §6, có đủ mục **"Giả định tôi đã tự đặt"**
- [ ] **DỪNG. Chờ Gate 1.** Sau khi duyệt: promote → `contract.lock` → viết `brief.md` (bốn mục bắt buộc; mục "Không thuộc phạm vi" ghi rõ: không sửa `backend/`, không đổi chữ ký `resolveImage`)

**Endpoint đề xuất (chốt ở Gate 1):**

| Method | Path | Policy | Trả về |
|---|---|---|---|
| POST | `/shops/{shopId}/media/slot-uploads` | member | `SlotUploadResultDto { asset, libraryAsset? }` |
| POST | `/shops/{shopId}/media/library` | member | `MediaAssetDto` |
| GET | `/shops/{shopId}/media/library?page&pageSize` | member | `PagedResult<MediaAssetDto>` |
| POST | `/shops/{shopId}/media/library/{assetId}/clones` | member | `MediaAssetDto` |
| GET | `/shops/{shopId}/media/library/{assetId}/references` | member | `MediaReferencesDto` |
| DELETE | `/shops/{shopId}/media/library/{assetId}` | Owner | 204 |
| GET | `/shops/{shopId}/media/assets?ids=…` | member | `MediaAssetDto[]` |
| GET | `/shops/{shopId}/media/usage` | member | `MediaUsageDto { usedBytes }` |
| PUT | `/shops/{shopId}/logo` | Owner | `ShopLogoDto` |

Mọi endpoint đều có `RequireGlobalScope` + `RequireShopMembership()`.

`MediaAssetDto`: `id, storageKey, mimeType, width, height, sizeBytes, altText, focalPointX,
focalPointY, originalFileName, folder, isInLibrary, preset, sourceAssetId, createdAt`.

---

## 8. Session S2: Frontend (mở SAU Gate 1)

Đọc `brief.md` → kiểm sha256 với `contract.lock` → `pnpm gen:api`. **Code với MSW trước**, chỉ bật
API thật ở bước integration.

### F1: `resolveImage` thật + proxy

```ts
// packages/builder-components/src/context.tsx — GIỮ chữ ký resolveImage(imageId, preset)
export interface MediaRef { storageKey: string; preset: string | null }
export interface RenderContextValue {
  basePath: string;
  mediaMap: Readonly<Record<string, MediaRef>>;        // mới, mặc định {}
  resolveImage(imageId: string, preset: string): string;
  resolveUrl(link: LinkValue): string;
}
// thân mặc định: không có trong mediaMap → PLACEHOLDER; preset lệch → console.warn rồi VẪN trả /media/{storageKey} (#74)
```

- [ ] Test Vitest: id có trong map → `/media/shops/…webp`; id không có → placeholder; preset lệch → `console.warn` được gọi đúng 1 lần **và** vẫn trả URL thật; id bản Library (`preset: null`) → vẫn render và warn
- [ ] Test không có `window` (môi trường `node`) → không throw (#23)
- [ ] `apps/portal/vite.config.ts`: proxy `/media` (giữ `changeOrigin: false`), cùng proxy các path `/shops/*/media` và `/shops/*/logo` (đã có sẵn nhờ proxy `/shops`); sửa comment dòng 12 (bỏ chữ "image proxy thật"). Giữ `devPlaceholderImagePlugin` làm placeholder
- [ ] `apps/web`: thêm proxy `/media` → API trong cấu hình dev của nitro, để `{slug}.vsite.local:3000/media/…` chạy được

### F2: Dialog upload vào slot

- [ ] Input `accept="image/jpeg,image/png,image/webp"`; client kiểm ≤ 10 MB trước khi gửi
- [ ] Xem trước ảnh, bấm để đặt focal point (hiện khung crop theo tỉ lệ preset); ẩn chọn focal khi preset là `inside`
- [ ] Checkbox "Lưu vào thư viện"
- [ ] Lỗi `MEDIA_HEIC_UNSUPPORTED` → hướng dẫn: "Trên iPhone: Cài đặt → Camera → Định dạng → Tương thích nhất, hoặc chọn ảnh qua nút Chọn ảnh" (text cuối cùng để người duyệt Gate 2 chỉnh); các `error_code` khác có message riêng
- [ ] Đủ ba trạng thái loading / error / empty; test bằng MSW

### F3: Picker Media Library

- [ ] Grid ảnh `loading="lazy"`, phân trang, nút upload vào thư viện
- [ ] Chọn ảnh → chỉnh focal → gọi clone → trả `MediaAssetDto` để đặt vào slot
- [ ] Xoá: gọi `references` trước; có tham chiếu thì hiện cảnh báo, **không chặn**
- [ ] Hiện dung lượng đã dùng

### F4: Logo trong trang sửa shop

- [ ] Upload (chỉ Owner thấy nút), hiện logo qua phái sinh `320x96,inside`

### F5: Nối vào trang `dev-registry`

- [ ] Hero và Gallery trong `dev-registry` dùng dialog/picker thật; `mediaMap` điền từ response upload/clone, rồi gọi `GET …/media/assets?ids=` khi tải lại trang
- [ ] **Tiêu chí dừng `08` §9 chạy tay một lần trên API thật:** upload vào Hero theo cả hai chế độ, ảnh đúng kích thước preset; chọn lại ảnh Library cho một ô Gallery → clone mới; upload logo → đủ phái sinh

---

## 9. Đóng task (trước Gate 2)

- [ ] `Docs/tasks/MEDIA-001/changelog.md` (mẫu `IDENTITY-001`), gồm:
  - **Lệch có chủ đích:** `IsDeleted` thay cho `DeletedAt`; tên bảng `MediaAsset`; shape `{ presets, surfaces }`; test 6/7 kiểm qua service
  - **Chưa làm xong:** nối `IMediaReferenceValidator` vào lưu draft (Bước 5); quét tham chiếu tree/snapshot trong `references` (Bước 5/8); bản TS của `ImagePaths` (làm cùng FE Listing đầu tiên)
- [ ] Banner `> **STATUS:**` của `DesignIdeal/08-media-asset-design.md` trỏ changelog; `pnpm gen:doc-index && pnpm check:docs` PASS
- [ ] `DECISIONS.md`: #82–#86 và các quyết định đã code chuyển trạng thái từ 📐 sang ✅ enforce, kèm tên test
- [ ] Spawn `change-reviewer` → `Docs/tasks/MEDIA-001/review.md` theo mẫu workflow §9 → 🧑 Gate 2
- [ ] Session riêng sau Gate 2: `session-retro`

---

## 10. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)

| # | Giả định | Duyệt ở |
|---|---|---|
| A1 | `image-presets.json` đổi shape thành `{ presets, surfaces }`: cần thiết để thêm `surfaces` mà không lẫn với tên preset. Ba nơi đang đọc file (gen-registry, vite plugin, test) sửa trong T0.1 | Duyệt Registry |
| A2 | Tập `surfaces.Shop` = `320x96,inside` (Header, Shop Profile) + `96x96,cover` (avatar). Chưa đưa `1200x630,cover` vào như ví dụ ở `08` §3.6: crop logo thành ảnh OG gần như luôn xấu. ServiceGrid khai `800x600,cover` cho cả hai source | Duyệt Registry |
| A3 | `IsDeleted` (base class) thay cho `DeletedAt`; tên bảng `MediaAsset` theo quy ước code | Gate 1 |
| A4 | Mọi lỗi từ chối ảnh trả **422** + `error_code`; chỉ vượt `RequestSizeLimit` của Kestrel mới ra 413 | Gate 1 |
| A5 | Không upscale: ảnh nhỏ hơn preset thì record có `Width`/`Height` nhỏ hơn preset nhưng `Preset` vẫn ghi preset slot (đúng tỉ lệ, chỉ mờ hơn) | Gate 1 |
| A6 | `GetAssetsByIds` trả cả asset đã soft delete (cùng shop), để clone của ảnh đã xoá vẫn render (#72) | Gate 1 |
| A7 | Picker MVP hiện thẳng file bản Library (≤ 1600px, lazy load), không sinh thumbnail riêng cho picker | Gate 1 |
| A8 | `Cache-Control: public, max-age=3600` cho `/media/*` tới Bước 8 | Gate 1 |
| A9 | Test không cần container (pipeline, paths, LocalDisk) đặt trong `IntegrationTests/Imaging/`. Không tạo project test thứ tư | Gate 1 |
| A10 | Thư mục LocalDisk mặc định là `.media/` ở root repo (gitignore); prod đặt `Storage:LocalDiskRoot` hoặc chuyển `S3` | Gate 1 |
| A11 | Xoá bản Library đang là `Shop.LogoId` được phép (chỉ cảnh báo). FK trỏ vào row soft delete vẫn hợp lệ, logo vẫn hiện | Gate 1 |

---

## 11. Verification tổng

- `dotnet build`: 0 warning; `ArchitectureTests` xanh, đặc biệt `ModuleBoundaryTests` (Shop không reference Media; Imaging là shared) và `ShopScopedRouteFilterTests`
- Đủ 12 test bắt buộc của `08` §9. Bảng đối chiếu: 1, 2 → T1 · 3, 4 → T5 · 5, 9, 10 → T6 · 6, 7 → T8 · 8, 11 → T4 · 12 → T3 + T9
- R1–R5 (§4) có test
- `pnpm check:registry-additive`, `pnpm test`, `check-no-drift` sạch sau promote
- Tiêu chí dừng `08` §9 chạy tay một lần trên máy có Docker
