# MEDIA-001 — Hướng dẫn nghiệm thu tay (tiêu chí dừng `08` §9)

Chạy hệ thống thật (MSW tắt), thao tác trên trình duyệt. Mọi bước dưới đây đã được đối chiếu với cấu hình
thật của repo ngày 2026-09-30. **Làm theo thứ tự, tick từng ô.** Tổng thời gian khoảng 45 phút.

> ⚠️ **Đừng tải lại trang (F5) và đừng gõ URL mới lên thanh địa chỉ sau khi đã đăng nhập.** Token chỉ
> sống trong bộ nhớ (Quyết định #3), tải lại là mất phiên. Chỉ đi qua các link trong ứng dụng. Lỡ mất
> phiên thì đăng nhập lại (việc này cũng là một phần của bước 3.4).

**Đã kiểm sẵn ngày 2026-09-30 (không cần làm lại, chỉ để bạn biết nền đã vững):**
API đang chạy ở cổng 5270 là bản mới, phục vụ `/media/…` với đúng header (`image/webp`, `Cache-Control`,
`nosniff`); qua proxy của `apps/web`, một ảnh thử ở host `spa-abc.vsite.local:3000` ra `200`, host lạ ra `403`,
truy cập vượt thư mục ra `404`; DB dev đã có cả hai migration; đoạn JSON Gallery ở bước 5.6 hợp lệ với schema
của registry. Hai lỗi FE (proxy `apps/web` không hoạt động, không có link nội bộ tới `/dev-registry`) đã được
sửa khi soạn tài liệu này, xem `changelog.md` mục 18. **Chưa kiểm được:** phía `apps/portal` (cần file hosts),
toàn bộ luồng trình duyệt, và 413 thật. Đó là phần việc của bạn.

## Phần 0 — Chuẩn bị (làm một lần)

- [ ] **0.1 Kiểm cổng 5270 (API).** Lúc tôi kiểm, cổng này **đã có một `Vsite.Api.exe` đang chạy**
  (PID 13648, khởi động 21:48 ngày 2026-09-30, chạy từ `backend\src\Vsite.Api\bin\Debug`). Tôi đã thử tài
  liệu OpenAPI của nó: **đã có endpoint mới** (`derivatives`, `logoUrl`), và nó phục vụ được `/media/…` đúng.
  Nếu đó là API bạn vừa chạy thì **dùng luôn, đừng tắt**, và bỏ qua cửa sổ A ở Phần 1 (nhưng bạn vẫn phải
  xem log của terminal đang chạy nó để lấy link xác thực email ở bước 2.2). Để tự kiểm một API đang chạy có
  phải bản mới không:
  ```
  curl.exe -s http://localhost:5270/openapi/media.json | findstr /c:"derivatives"
  ```
  Có dòng kết quả là bản mới. Không có, hoặc bạn muốn chạy lại từ đầu, thì tắt nó rồi làm cửa sổ A:
  ```
  netstat -ano | findstr :5270
  taskkill /PID <PID> /F
  ```
  (Bản API đang chạy giữ khoá file trong `bin`, nên nếu `dotnet build` hay `dotnet ef` báo file bị khoá thì
  cũng phải tắt nó trước.)
- [ ] **0.2 File hosts.** Mở Notepad bằng quyền **Administrator**, sửa `C:\Windows\System32\drivers\etc\hosts`,
  thêm hai dòng (hiện chưa có dòng `vsite` nào):
  ```
  127.0.0.1  admin.vsite.local
  127.0.0.1  spa-abc.vsite.local
  ```
  `spa-abc` là slug của shop sẽ tạo ở bước 2.4; dùng slug khác thì sửa theo. Không cần khởi động lại máy.
- [ ] **0.3 Postgres và Redis.** Đang chạy sẵn dạng cài trực tiếp: Postgres cổng **5433** (user `postgres`,
  mật khẩu `password`), Redis cổng **6379**, đúng với `appsettings.Development.json`. **Không dùng
  `docker compose up` cho việc này:** compose tạo Postgres ở cổng 5432 với user `vsite`, không khớp cấu hình
  (đây là một điểm lệch có sẵn, chưa được sửa).
- [ ] **0.4 Áp dụng migration** (API không tự migrate). Từ thư mục gốc repo:
  ```
  dotnet ef database update --project backend/src/Vsite.Infrastructure --startup-project backend/src/Vsite.Api
  ```
  Kỳ vọng kết thúc bằng `Done.`, trong đó có migration `AddMediaAsset`. Chạy lại nhiều lần cũng an toàn.
  **Đã kiểm trên máy bạn:** `dotnet ef migrations list` cho thấy DB dev `vsite_dev` đã có cả `InitialSchema` lẫn
  `AddMediaAsset` (không có mục Pending), nên bước này có thể là no-op. Cứ chạy lại cho chắc.
- [ ] **0.5 Sinh artifact codegen** mà backend cần lúc khởi động:
  ```
  pnpm install
  pnpm --filter @vsite/builder-components run gen:registry
  ```
  (Thiếu file thì `dotnet build` báo lỗi có hướng dẫn chạy đúng lệnh này.)

## Phần 1 — Chạy ba tiến trình (ba cửa sổ terminal, đều ở thư mục gốc repo)

| Cửa sổ | Lệnh | Kỳ vọng |
|---|---|---|
| **A · API** | `dotnet run --project backend/src/Vsite.Api --launch-profile http` | `Now listening on: http://localhost:5270`. **Giữ cửa sổ này mở**, link xác thực email sẽ in ở đây |
| **B · Portal** | `pnpm --filter @vsite/portal dev` | mở được `http://admin.vsite.local:5173` |
| **C · Web (site shop)** | `pnpm --filter @vsite/web dev` | mở được `http://localhost:3000` |

- [ ] Cả ba đang chạy. (Tab trình duyệt tự mở khi API khởi động thì đóng đi được.)

## Phần 2 — Tài khoản và shop

- [ ] **2.1** Mở `http://admin.vsite.local:5173/register`, đăng ký bằng một email tuỳ ý.
- [ ] **2.2 Xác thực email.** Email không được gửi thật. Xem **cửa sổ A** tìm dòng `[DEV EMAIL] To=<email>`,
  trong nội dung có link dạng `http://localhost:5270/auth/verify-email?token=…`. Mở link đó trong trình duyệt.
  Link hết hạn sau **20 phút**; quá hạn thì đăng ký lại bằng email khác.
- [ ] **2.3** Vào `http://admin.vsite.local:5173/login`, đăng nhập. Ứng dụng chuyển tới `/shops`.
- [ ] **2.4** Tạo shop: tên tuỳ ý, slug **`spa-abc`**, loại **Hosted**. Bạn thành **Owner** của shop.
- [ ] **2.5** Mở shop vừa tạo. **Copy Shop ID** trong URL (`/shops/<GUID>`) và ghi lại, bước 5 cần.

## Phần 3 — Logo shop (F4 + D4)

Chuẩn bị 1 ảnh PNG **nền trong suốt, hình ngang** (ví dụ 1000×200) và 1 ảnh khác để đổi logo.

- [ ] **3.1** Ở trang sửa shop, mục logo hiện **"Chưa có logo"** và có nút tải logo.
- [ ] **3.2** Tải PNG lên. Logo hiện ngay, **giữ nền trong suốt**.
- [ ] **3.3 Kiểm URL.** Chuột phải logo → Inspect. `src` phải dạng `/media/shops/<shopId>/website/<yyyy>/<MM>/<uuid>.webp`
  với **đúng một** `/media/` (không phải `/media//media/…`). Ảnh gốc 1000×200 thì kích thước thật của ảnh
  là **320×64** (Console: `document.querySelector('img[src*="/media/"]').naturalWidth`).
- [ ] **3.4 Logo còn sau khi mất phiên.** Nhấn **F5**. Bạn bị đưa về đăng nhập; đăng nhập lại, vào lại shop:
  logo **vẫn hiện**. Đây chính là chỗ D4 vá (trước đó tải lại là mất logo).
- [ ] **3.5** Ở danh sách `/shops`, mục shop có logo nhỏ 40px (shop chưa có logo hiện chữ cái đầu).
- [ ] **3.6 Đổi logo.** Tải ảnh thứ hai: logo mới thay logo cũ.
- [ ] **3.7 File không hợp lệ.**
  - File `.gif` → báo lỗi định dạng.
  - File lớn hơn 10 MB → báo lỗi ngay ở trình duyệt, chưa gửi lên server.
  - Đổi đuôi một file `.jpg` thành `.png` (nhưng nội dung vẫn là JPEG) → server **chấp nhận** vì kiểm bằng
    magic bytes chứ không tin đuôi; đổi đuôi một file `.txt` thành `.png` → server **từ chối**.
  - Có ảnh `.heic` thì thử. Hướng dẫn chuyển sang JPEG chỉ hiện khi trình duyệt nhận ra file là
    `image/heic` hoặc `image/heif`. **Windows thường không nhận ra** (loại file rỗng), khi đó bạn chỉ thấy
    lỗi định dạng chung. Đó là hạn chế đã biết của cách nhận diện dựa vào loại file do trình duyệt báo, không
    phải lỗi của bạn; ghi lại kết quả bạn thấy.

## Phần 4 — Ảnh đọc được trên mọi domain (test bắt buộc 12)

Lấy đường dẫn ảnh logo ở 3.3, phần từ `/media/…` trở đi (gọi là `<path>`). Mở **từng URL** dưới đây,
tốt nhất trong **cửa sổ ẩn danh** (để chứng minh không cần đăng nhập). Cả bốn phải hiện **cùng một ảnh**:

- [ ] `http://admin.vsite.local:5173<path>` (qua proxy portal)
- [ ] `http://spa-abc.vsite.local:3000<path>` (qua proxy web, **host của shop**; đây là chỗ hôm nay mới sửa: trước đó bị chặn 403 và proxy không chạy)
- [ ] `http://spa-abc.vsite.local:5270<path>` (thẳng vào API)
- [ ] `http://localhost:5270<path>`

Kiểm thêm chống truy cập ra ngoài thư mục ảnh (dùng `curl.exe`, tham số `--path-as-is` để curl không tự rút gọn `..`).
Cả hai phải trả **404**, không được trả nội dung file:

- [ ] `curl.exe -i --path-as-is "http://localhost:5270/media/../appsettings.json"`
- [ ] `curl.exe -i --path-as-is "http://localhost:5270/media/shops/%2e%2e/%2e%2e/appsettings.json"`

## Phần 5 — Hero và Gallery trong dev-registry (F5, tiêu chí dừng `08` §9)

- [ ] **5.1** Từ thanh đầu trang (đang đăng nhập), bấm link **`dev-registry`** (chỉ hiện ở chế độ dev, mới thêm).
  **Không gõ URL.** Bấm nút Back của trình duyệt sẽ quay về `/shops` mà không mất phiên.
- [ ] **5.2** Cột trái, ô **Shop ID**: dán GUID ở bước 2.5. Fixture mặc định là **Hero (đơn)**.
- [ ] Mở DevTools → tab **Network**, lọc theo `media`. Bạn sẽ dùng nó để đọc phản hồi ở các bước dưới.

**Hero, chế độ 1 — không lưu vào thư viện**
- [ ] **5.3** Chọn node Hero, ở Inspector (cột phải) bấm **Tải ảnh lên**, chọn một ảnh ngang lớn (≥ 1600×900,
  tỉ lệ khác 16:9 để thấy được crop), bấm lên ảnh chọn focal point, **KHÔNG tick** "Lưu vào thư viện", tải lên.
  - Ảnh xuất hiện ở Hero. Ảnh nhỏ trong Inspector có kích thước thật **1600×900** (preset `1600x900,cover`).
  - Network, request `slot-uploads`, phản hồi: `asset.isInLibrary = false`, `asset.preset = "1600x900,cover"`,
    `asset.sourceAssetId = null`, **không có** `libraryAsset`.

**Hero, chế độ 2 — có lưu vào thư viện**
- [ ] **5.4** Tải một ảnh khác, lần này **tick** "Lưu vào thư viện".
  - Phản hồi `slot-uploads`: `libraryAsset` có `isInLibrary = true`, `preset = null`; còn `asset` là một
    bản khác với `isInLibrary = false`, `preset = "1600x900,cover"` và `sourceAssetId` **bằng** `libraryAsset.id`.

**Chọn từ thư viện, sinh clone độc lập**
- [ ] **5.5** Bấm **Chọn từ thư viện**, chọn ảnh vừa lưu, xác nhận.
  - Network, request `.../library/<id>/clones`: phản hồi có `id` **mới** (khác id bản Library),
    `sourceAssetId` = id bản Library, `isInLibrary = false`, `storageKey` **khác** bản Library.
  - Trong "JSON thô" (cột trái), `imageId` của Hero là id của clone, **không bao giờ** là id bản Library.
- [ ] **5.6 Gallery.** Fixture không có sẵn Gallery, nên dán đoạn JSON sau vào ô **JSON thô** rồi áp dụng
  (đã kiểm hợp lệ với schema sinh tự động của registry):
  ```json
  {
    "type": "Gallery",
    "variant": "Gallery01",
    "id": "c_gallery",
    "props": {
      "heading": "Gallery thử",
      "columns": 3,
      "items": [
        { "image": { "imageId": "media_demo" }, "caption": "Ô 1" },
        { "image": { "imageId": "media_demo" }, "caption": "Ô 2" }
      ]
    }
  }
  ```
  Bấm **Áp dụng JSON** (cột trái). Sau đó ở ô chọn node đầu cột Inspector (cột phải) chọn
  **`c_gallery (Gallery/Gallery01)`**, vì Inspector không tự đổi sang node mới. Ở **Ô 1** chọn một ảnh từ
  thư viện, ở **Ô 2** chọn **cùng ảnh đó**:
  - Hai request `clones` cho hai `id` **khác nhau** (hai clone độc lập), cùng `sourceAssetId`,
    `preset = "800x800,cover"` và ảnh thật **800×800**.

**Xoá khỏi thư viện (test bắt buộc 9)**
- [ ] **5.7 Xoá một ảnh thường.** Mở lại **Chọn từ thư viện**, bấm **Xoá** ở bản Library mà một clone đang dùng
  (nút Xoá chỉ hiện với Owner). Hộp thoại chỉ hỏi "Xoá ảnh này khỏi thư viện?" (không có cảnh báo vì ảnh này
  không được nơi nào khác tham chiếu). Xác nhận xoá:
  - bản Library biến mất khỏi danh sách;
  - URL `/media/<storageKey của clone>` (lấy ở Inspector) **vẫn mở được** ở tab mới (test bắt buộc 9).
- [ ] **5.8 Xoá ảnh gốc của logo (kiểm quyết định #72/A11).** Trong thư viện có cả bản gốc của logo bạn đã
  tải ở Phần 3. Bấm **Xoá** ở đó: hộp thoại phải **cảnh báo** "Ảnh này đang được dùng ở 1 nơi (vd. logo
  shop). Ảnh sẽ ẩn khỏi thư viện; các nơi đang dùng vẫn hiển thị bình thường" và **không chặn** việc xoá.
  Xác nhận xoá, rồi đăng nhập lại (vì cần tải lại) và vào lại `/shops`: **logo vẫn hiện**.

## Phần 6 — 413 thật ở Kestrel (nâng cao, tuỳ chọn nhưng nên làm)

Trình duyệt chặn file > 10 MB trước khi gửi, nên phải dùng `curl.exe` để chạm được giới hạn thật của server.
Chưa ai kiểm ngưỡng này (mới có test dịch lỗi 413 sang ProblemDetails).

- [ ] Lấy token (thay email, mật khẩu, host `admin` để đúng audience):
  ```
  curl.exe -s -X POST http://admin.vsite.local:5173/auth/login -H "Content-Type: application/json" -d "{\"email\":\"<email>\",\"password\":\"<mật khẩu>\"}"
  ```
  Copy `accessToken` trong kết quả.
- [ ] Tạo file 12 MB và gửi lên:
  ```
  powershell -Command "[IO.File]::WriteAllBytes('big.png', (New-Object byte[] 12582912))"
  curl.exe -i -X POST "http://admin.vsite.local:5173/shops/<shopId>/media/library" -H "Authorization: Bearer <accessToken>" -F "file=@big.png;type=image/png"
  ```
  Kỳ vọng **HTTP 413** với body `error_code: MEDIA_FILE_TOO_LARGE`. Nếu ra 500 hoặc body rỗng thì **ghi lại**, đó là lỗi thật.

## Khi có gì đó không chạy

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| API báo `address already in use` | Đã có một `Vsite.Api.exe` ở cổng 5270 (bước 0.1): dùng luôn nó, hoặc tắt nó rồi chạy lại |
| API lỗi kết nối DB hoặc `relation "MediaAsset" does not exist` | Postgres cổng 5433 chưa chạy, hoặc chưa làm bước 0.4 |
| `dotnet build` báo thiếu `derivative-presets.json` | Chưa làm bước 0.5 |
| Portal không khởi động, `ENOTFOUND admin.vsite.local` | Chưa sửa file hosts (0.2) |
| `Blocked request. This host is not allowed` | Đang mở host không thuộc `*.vsite.local`; với `apps/web` chỉ `.vsite.local` được phép |
| Bị đưa về trang đăng nhập bất ngờ | Đã tải lại trang hoặc gõ URL (mất phiên, xem cảnh báo đầu file) |
| Link xác thực email báo hết hạn | Quá 20 phút; đăng ký lại bằng email khác |

## Ghi kết quả

Tick các ô ở trên. Bước nào **fail**, ghi lại: URL, ảnh chụp màn hình, phản hồi trong tab Network, và vài dòng
log ở cửa sổ A. Đưa cho tôi thì tôi phân tích; hoặc ghi thẳng vào `Docs/tasks/MEDIA-001/review.md` mục
"Điểm cần anh quyết" #1.

**Dọn dẹp sau khi xong:** thư mục `.media/` ở gốc repo chứa ảnh đã upload (đã nằm trong `.gitignore`, xoá
được). Dữ liệu người dùng và shop test nằm trong DB dev `vsite_dev`. Hai dòng trong file hosts có thể giữ
hoặc xoá.
