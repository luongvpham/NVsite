# MEDIA-001-ACCEPT — nghiệm thu tay tiêu chí dừng `08` §9

Ngày 2026-10-11. Lane A (không đổi code, không đổi contract). Checklist từng bước dùng lại
`Docs/tasks/MEDIA-001/acceptance.md` (còn đúng phần lớn — xem "Tài liệu lệch" bên dưới).
Nguồn: `MEDIA-001/changelog.md` "Chưa làm xong" #5, #7.

**Trạng thái: ĐANG CHẠY — phần API/curl xong, phần UI (Phần 3, 5 của acceptance.md) chờ người bấm.**
`00-INDEX.md` §3/§4 CHƯA đổi, vì task chưa xong.

## Môi trường dựng được

- Postgres 16 (service, cổng 5433), DB `vsite_dev` đã ở migration mới nhất (chỉ còn `InitialSchema` — đã gộp ở REFACTOR-DB-001).
- **Redis không chạy sẵn.** Dựng bằng `docker run -d --name vsite-redis-accept -p 6379:6379 redis:7-alpine`. Không có Redis thì mọi request
  qua `TenantResolutionMiddleware` ra 500 (riêng `/media/*` không bị vì mount trước middleware đó).
- API `:5270`, portal `:5173`, web `:3000` đang chạy. Hosts đã có `admin.vsite.local`, `spa-abc.vsite.local`.
- Dữ liệu test: user `accept302343612@example.com` / `Passw0rd!Accept1`, shop `spa-abc` = `078fba2b-2db2-4415-9980-7d7e2997bcc1`.

## Kết quả phần chạy bằng API (curl, Kestrel thật)

| Mục | Kết quả |
|---|---|
| Logo PNG 1000×200 → phái sinh | ✅ `320x96,inside` = 320×64, `96x96,cover` = 96×96, cả hai webp, `sourceAssetId` = bản Library |
| `ShopDto.logoUrl` | ✅ đúng một `/media/`, GET shop trả lại sau mỗi lần gọi; URL trả 200 `image/webp` |
| `.gif` / `.txt` đổi đuôi `.png` | ✅ 422 `MEDIA_UNSUPPORTED_FORMAT` |
| `.jpg` đổi đuôi `.png` | ✅ chấp nhận (magic bytes, không tin đuôi) — đúng như `acceptance.md` 3.7 |
| Hero chế độ 1 (không tick) | ✅ 1 record, 1600×900, `isInLibrary=false`, `preset=1600x900,cover`, `sourceAssetId=null`, không có `libraryAsset`, focal giữ 0.3/0.4 |
| Hero chế độ 2 (tick) | ✅ 2 record; bản Library `preset=null` 1600×1600 (không upscale/crop); clone `sourceAssetId`=Library, `storageKey` khác |
| Chọn Library → clone (Hero + 2 ô Gallery 800×800) | ✅ 3 id khác nhau, 3 `storageKey` khác nhau, cùng `sourceAssetId`, đúng kích thước preset |
| Clone từ clone | ✅ bị từ chối (404 `MEDIAASSET_NOT_FOUND`) |
| `GET library` | ✅ chỉ trả `isInLibrary=true` |
| Xoá bản Library đang có clone (test 9) | ✅ 204; biến khỏi list; file clone vẫn 200; `assets?ids=` vẫn trả cả bản đã xoá và clone còn `sourceAssetId` |
| `references` + xoá ảnh gốc của logo (A11) | ✅ `references` báo `ShopLogo`; xoá 204 (không chặn); logo vẫn hiện, URL vẫn 200 |
| Quota | ✅ `usage` giảm khi xoá bản Library (30214 → 15768 B). ⚠️ "không tăng khi clone" chưa đo trước/sau từng clone — test tự động phủ |
| `/media/*` (test 12): `localhost:5270`, `spa-abc.vsite.local:5270` | ✅ 200, `image/webp`, `Cache-Control: public, max-age=3600`, `nosniff`, không cần token |
| `/media/*` qua portal `admin.vsite.local:5173` | ✅ 200 |
| `/media/*` qua web `localhost:3000` và `[::1]:3000` | ✅ 200; Host `spa-abc.vsite.local` → 200; Host lạ → 403 |
| `/media/*` qua web **bằng hosts IPv4** `spa-abc.vsite.local:3000` | ❌ không kết nối được — xem lỗi L1 |
| Truy cập vượt thư mục (3 biến thể), POST `/media/…` | ✅ 404, 404, 404; POST → 405 |
| **413 thật (#7)**: 12 MB tới `library`, `slot-uploads`, `PUT logo` | ✅ cả ba **413** + `error_code: MEDIA_FILE_TOO_LARGE`; API vẫn sống |
| 10,6 MB (giữa 10 và 11 MB) | ✅ 422 `MEDIA_FILE_TOO_LARGE` (giới hạn 10 MB của pipeline) |

Chưa kiểm tay: EXIF (test tự động đọc lại file thật), test 7/8/11 (Testcontainers, đã xanh ở REFACTOR-DB-001).

## Lỗi phát hiện — CHƯA SỬA, chờ quyết định

- **L1 (trung bình) — `apps/web` dev chỉ nghe IPv6.** `Local: http://localhost:3000/`, socket `[::1]:3000`. Hosts file trỏ `spa-abc.vsite.local`
  về `127.0.0.1` nên trình duyệt bị từ chối kết nối (curl `000`). Cùng request qua `--resolve …:::1` ra 200. Chặn mục 4 của nghiệm thu ở trình duyệt.
  Cách tránh không đụng code: thêm `::1  spa-abc.vsite.local` vào hosts. Cách sửa: `server.host` trong `apps/web/vite.config.ts`
  (portal đã nghe `127.0.0.1`).
- **L2 (thấp) — request >11 MB qua proxy portal ra 502** (`vite http proxy error … ECONNRESET`) thay vì 413: API trả lời rồi đóng kết nối khi
  body chưa đọc hết. Trình duyệt chặn >10 MB trước khi gửi nên người dùng thường không gặp; chỉ lộ với client gọi thẳng portal.

## Tài liệu lệch (`acceptance.md`, chưa sửa)

- 0.3 nói Redis chạy sẵn — sai trên máy này; thiếu hẳn bước dựng Redis.
- Link xác thực email in ra là `/api/auth/verify-email?token=…`, doc ghi `/auth/verify-email`.
- Phần 6 dùng `/auth/login` và `/shops/<id>/media/library` — thiếu tiền tố `/api` (Quyết định #91); gọi như vậy sẽ rơi vào SPA.
- 0.4 nói có migration `AddMediaAsset` — nay chỉ còn `InitialSchema`.
- Phần "Đã kiểm sẵn" ghi PID/ngày 2026-09-30 đã cũ.

## Còn lại

Phần 3 (logo trên UI), Phần 5 (Hero/Gallery trong dev-registry) và các mục `/media` ở trình duyệt (Phần 4) — kết quả điền bổ sung vào đây,
rồi cập nhật `00-INDEX.md` §3 (dòng 4/4.x) và §4 (dòng nghiệm thu tay).
