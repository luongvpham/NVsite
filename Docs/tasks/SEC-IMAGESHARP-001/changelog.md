# SEC-IMAGESHARP-001 — changelog: lệch so với thiết kế

Lane A (không chạm contract OpenAPI/Registry/reserved-routes → không chạy `contract-sync`).
Nguồn việc: `DesignIdeal/step.md` §4.x việc 1, và `REFACTOR-BE-001/changelog.md` "Chưa làm xong" #4.

**Kết quả:** hết cảnh báo NU1902/NU1903. `dotnet list package --vulnerable --include-transitive` sạch trên cả 7 project.
Thay đổi lớn nhất so với yêu cầu ban đầu ("nâng version"): **thay thư viện**, không nâng.

## Vì sao thay thư viện thay vì nâng ImageSharp

- Cả 5 advisory (GHSA-j3p4-wp97-rph4, -j9gm-c75j-xc9q, -jjfr-hcj7-qf5w mức High; -gwg2-r3hj-4w44, -wmxv-xphr-5c9g mức Moderate)
  có `first_patched_version = 4.1.2`. Không có bản 3.x nào được vá (3.x mới nhất là 3.2.0, vẫn trong vùng bị ảnh hưởng).
- Lên 4.x là nhảy major → đã dừng báo người duyệt trước (đúng yêu cầu task).
- **Văn bản license của 4.1.2 y hệt 3.1.12** (Six Labors Split License 1.0, diff rỗng). Cái đổi là **cơ chế thực thi**: từ
  3.2.0, package có MSBuild target `ValidateLicenseTask` chạy trước `CoreCompile`; thiếu `sixlabors.lic` / khoá thì build lỗi
  (cấu hình Debug chỉ cảnh báo). Doanh nghiệp < 1M USD vẫn dùng miễn phí nhưng phải **xin license** tại licensing.sixlabors.com
  và mang file vào CI. Nguồn: file `build/SixLabors.ImageSharp.targets` trong gói 4.1.2,
  <https://sixlabors.com/posts/licence-enforcement-changes/>, <https://sixlabors.com/posts/security-support-and-license-enforcement/>.
- Người duyệt chọn tránh phụ thuộc vào license/quy trình xin của bên thứ ba → thay bằng **SkiaSharp (MIT)**. Magick.NET
  (Apache 2.0) cũng là ứng viên nhưng nặng hơn; SkiaSharp đủ cho nhu cầu (JPEG/PNG/WebP
  in, WebP out, crop/resize, EXIF Orientation).

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. Quyết định #84 đổi: ImageSharp → SkiaSharp

- Đã **viết đè** (không chồng) ở `DesignIdeal/08-media-asset-design.md` §0 (nơi định nghĩa) và `DECISIONS.md` #84.
  Điều khoản license "doanh thu < 1M USD" **không còn áp dụng** — SkiaSharp là MIT. `00-INDEX.md` §4 đã xoá dòng nợ CVE.
- Gói: `SkiaSharp` 4.153.1 + `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.153.1 (bản NoDependencies để không cần
  `libfontconfig` trong container; CI chạy `ubuntu-latest`). Windows/macOS native asset đi kèm `SkiaSharp` mặc định.
- Hai gói native nằm trong `Vsite.Infrastructure` nên mọi host/test tự có. Test project khai thêm `SkiaSharp` vì dùng trực tiếp
  (`Imaging/TestImages.cs`).
- Đổi tên: `ImageSharpImageProcessor` → `SkiaImageProcessor` (file, DI, test, `backend/docs/modules/media.md`).
  `IImageProcessor`/`ISourceImage`/`ImageTransform`/`ImageErrorCodes` **không đổi** → không chạm contract, handler, endpoint.

### 2. Thứ tự xử lý `LoadAsync` giữ nguyên, nhưng cơ chế từng bước đổi

| Bước | ImageSharp | SkiaSharp |
|---|---|---|
| Pixel từ header, chưa decode | `Image.IdentifyAsync` | `SKCodec.Create` + `codec.Info` |
| Chỉ lấy khung đầu của ảnh động | `DecoderOptions.MaxFrames = 1` + `RemoveFrame` | `codec.GetPixels` mặc định chỉ giải mã khung 0 |
| Áp EXIF Orientation | `AutoOrient()` | tự áp ma trận theo `codec.EncodedOrigin` (`ApplyOrigin`, cùng ma trận `SkEncodedOriginToMatrix`) |
| Xoá metadata | gán `*Profile = null` | không cần: ảnh giữ dạng bitmap thô rồi mã hoá lại; chỉ Orientation phải áp **trước** |
| Resize | `Resize()` (Bicubic mặc định) | `DrawImage` + lọc Linear + mipmap Linear (crop và resize một lượt) |
| Encode | `WebpEncoder { Quality }` | `SKWebpEncoderOptions(Lossy, WebpQuality)` |

- Giới hạn 10 MB / 25 MP (#85), HEIC → `MEDIA_HEIC_UNSUPPORTED`, magic bytes, "cancel không thành 422": giữ nguyên, các test cũ pass.

### 3. WebP động có khung sau bị hỏng giờ bị **từ chối** (422 `CorruptImage`), không còn "lấy khung đầu rồi bỏ qua"

- ImageSharp đọc được khung 1 của `animated-frame2-corrupt.webp`. Demuxer WebP của Skia kiểm header mọi khung ngay ở
  `SKCodec.Create` nên trả `null` → `CorruptImage`.
- Chấp nhận: đây là file hỏng; từ chối sớm an toàn hơn. Test `Animated_webp_decode_never_touches_frame_2` được thay bằng
  `Animated_webp_with_corrupt_later_frame_is_rejected_as_corrupt`. WebP động **hợp lệ** vẫn ra khung đầu
  (`Animated_webp_takes_first_frame`).
- Skia báo `FrameCount` = 0 (không phải 1) cho WebP tĩnh → test kiểm `<= 1`.

### 4. Test đổi theo thư viện, ý nghĩa giữ nguyên

- Gom helper sinh/đọc ảnh vào `tests/IntegrationTests/Imaging/TestImages.cs`, thay 8 bản `EncodeJpeg` chép tay.
  Kiểm "EXIF bị xoá" giờ quét FourCC `EXIF`/`XMP `/`ICCP` trong container WebP (ImageSharp có `ExifProfile`).
- Bỏ hẳn `SixLabors.ImageSharp.Drawing` (chỉ test dùng để vẽ ảnh mẫu).

### 5. SSH.NET (GHSA-q939-rpr3-3284, High) — làm cùng task theo yêu cầu

- Là phụ thuộc bắc cầu của `Testcontainers` 3.10.0 (2023.0.0), chỉ test. Pin trực tiếp `SSH.NET` 2026.0.0
  (`Directory.Packages.props` + `IntegrationTests.csproj`), không nâng Testcontainers. Bỏ pin khi Testcontainers tự kéo
  bản ≥ 2026.0.0.

## Chưa làm xong / theo dõi

1. **Chưa so mắt chất lượng ảnh** giữa ImageSharp và Skia (độ sắc khi thu nhỏ mạnh, màu WebP lossy). Test chỉ kiểm kích thước,
   alpha, hướng, metadata. Nên nhìn vài ảnh thật khi nghiệm thu tay `08` §9 (step 4.x việc 2).
2. **Bộ nhớ native:** bitmap Skia nằm ngoài heap GC; `SourceImage` đã `Dispose` đủ đường nhưng chưa đo RSS dưới tải. Ảnh 25 MP
   ≈ 100 MB bitmap (cùng cỡ ImageSharp). Không có giới hạn đồng thời — như trước.
3. **Attribution:** SkiaSharp đóng gói libwebp/libjpeg-turbo/libpng (BSD/zlib-style) — chưa có file NOTICE trong sản phẩm.
   Nếu cần phát hành kèm thông báo bên thứ ba thì bổ sung.
4. **Deploy Linux chưa chạy thật:** CI/Docker image chưa được thử với `NativeAssets.Linux.NoDependencies` (máy này là Windows).
   Job CI `dotnet test` trên `ubuntu-latest` sẽ là lần chạy đầu — xem kết quả lần push đầu.

## Kiểm chứng (máy có Docker, 2026-10-11)

- `dotnet test backend/vsite.sln`: ComponentSchemaTests 12/12, ArchitectureTests 9/9, IntegrationTests 309 pass + 1 skip có sẵn
  (`ShopEndpointTests.UpdateShop_Hosted_to_ExternalOnly_must_reassign_or_unpublish_listings_targeting_website`, không liên quan).
  Gồm toàn bộ test Imaging + Media, kể cả S3/LocalStack qua Testcontainers → **không có nợ Docker**, `DOCKER-TEST-DEBT.md` không đổi.
- `dotnet list package --vulnerable --include-transitive`: cả 7 project "no vulnerable packages".
