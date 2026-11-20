# WEBSITE-006 — Changelog (Bước 8e: UI publish/rollback ở Portal + nạp lại version draft sau publish)

Lane A, chỉ FE (`apps/portal`, `packages/shared`). Không đổi contract, không đổi BE. Plan: [`plan.md`](plan.md); nền:
`Docs/tasks/WEBSITE-004/brief.md` §B.

## Đã làm (theo task)

### F1 — Kiểu lỗi + thông điệp (`1cfa66a`)
- `packages/shared` `problem-details.ts`: `PublishBlockedError` (`pageId`/`pageTitle` `string | null` — `null` = lỗi cấp
  website), `PublishBlockedParams {errors, truncated}`, guard `isPublishBlockedParams`. `isDraftConflictParams` dùng chung
  `isStringOrNull`.
- `apps/portal/src/lib/error-messages.ts`: `PUBLISH_BLOCKED`, `PUBLISH_CONFLICT`, `ROLLBACK_CONFLICT`,
  `PUBLICATION_NOT_FOUND`, `REQUIRED_PROP_MISSING`.

### F2 — Autosave khoá + lấy lại version quanh publish (R9) (`76222ff`, `5db0c0e`, `f06ef85`, `b56f582`, `e297982`)
- `AutosaveController.prepareForPublish()` → `{ok:true} | {ok:false, reason:'invalid'|'conflict'|'blocked'|'error'}`:
  flush bản đang chờ (không bao giờ hai PUT song song), rồi khoá autosave. `finishPublish()`: `GET draft` lấy `version`
  mới (publish hạ `isDirty` ⇒ version đổi), mở khoá, lưu ngay những gì gõ trong lúc khoá. **Không nạp lại cây** (G6).
- Gọi lồng nhau xếp hàng (`preparing`/`finishing`), bộ đếm phiên (`dispose` → `start` không ăn kết quả phiên cũ),
  `flush` trả `FlushOutcome` tường minh. Hợp đồng gọi ghi ở comment lớp: prepare → POST → finish, chỉ cho publish.
- `serverTree` = cây chắc chắn đang ở server tại `version` hiện tại; `finishPublish` chỉ nhận version từ GET khi cây GET
  trùng `serverTree` (`same-json.ts`, so cấu trúc không phụ thuộc thứ tự khoá). Xem "Lệch có chủ đích".

### F3 — Dialog xuất bản + danh sách lỗi (`ff32e33`, `349eda0`)
- `components/website/publish-dialog.tsx`: ghi chú tuỳ chọn (giới hạn độ dài), không đóng được khi đang chạy, thành công →
  "Đã xuất bản bản vN" + "Mở site" + "Xong". Body đặt `key` theo phiên mở nên mở lại luôn sạch.
- `components/website/publish-errors.tsx`: 422 `PUBLISH_BLOCKED` nhóm theo trang (lỗi cấp website "Cài đặt website" đứng
  đầu), mỗi trang có "Mở trang" (hoặc nhãn "trang này" nếu là trang đang mở), `truncated` → báo còn nữa.
- `hooks/use-publish-website.ts`: `usePublishWebsiteFlow` (`retry:false`, `onSettled` cấp hook — callback theo lần gọi bị
  TanStack bỏ khi unmount), `classifyPublishError`, invalidate `getWebsite`/`listPages`/`listPublications`.
- `lib/site-url.ts`: `shopSiteUrl(slug)` từ `VITE_SHOP_SITE_ORIGIN_TEMPLATE` (`.env.development`/`.env.test`:
  `http://{slug}.vsite.local:3000`; `.env.production` để trống ⇒ ẩn link).

### F4 — Tổng quan + lịch sử + rollback (`5df3343`, `0ecf94f`)
- `website-overview.tsx`: khối trạng thái ("Chưa xuất bản" / "Đang chạy bản vN" / cảnh báo Suspended), nút Xuất bản,
  "Mở site" (slug qua `useGetShop`).
- `publication-history.tsx`: danh sách bản (giờ `Asia/Ho_Chi_Minh`, nhãn "Đang chạy", ghi chú), "Quay về bản này" → Dialog
  xác nhận; 404 `PUBLICATION_NOT_FOUND` đóng xác nhận và báo ở cấp danh sách.
- `hooks/use-publication-mutations.ts`: `useIsMutating` theo mutation key sinh từ contract ⇒ publish và rollback khoá
  chéo nhau (R6). `test/render-with-query.tsx` thêm tuỳ chọn `mutationRetry`.

### F5 — Nút Xuất bản trong editor (`1d2d00f`, `51d804d`)
- `components/website/editor-publish.tsx` (`useEditorPublish`): bấm → `prepareForPublish` → mở dialog; theme còn chưa lưu,
  trang chưa hợp lệ, đang xung đột, quá lớn, lỗi lưu → chặn với thông điệp riêng. `finishPublish` **chỉ khi đóng dialog**
  (G5: khoá suốt lúc dialog mở; thử lại trong dialog vẫn giữ khoá). Dialog báo "Sửa sau khi bấm Xuất bản không nằm trong
  bản này". An toàn khi rời trang giữa chừng (`mountedRef`, `lockedRef`).
- `editor-shell.tsx`: nút trên toolbar; phím tắt tắt khi đang bận; dialog render ngoài toolbar.

### F6 — `decorateWithAnchor` (`27edf42`)
- `site-preview.tsx` dùng `decorateWithAnchor` của `@vsite/builder-renderer` (bỏ `decorateWithId` cục bộ). Test khẳng định
  wrapper `#c_hero1` là `div` `display: contents`.

### Test
Portal 385 test (35 file, gồm 4 ca thêm ở "Sửa sau final review") — mới/mở rộng: `autosave-controller.test.ts`, `same-json.test.ts`, `publish-dialog.test.tsx`,
`publication-history.test.tsx`, `site-url.test.ts`, `routes/-website.test.tsx`, `routes/-editor-publish.test.tsx` (14 ca:
R1, R2 422/409, R5, theme bẩn, trang lỗi, PUT trước POST, huỷ, gõ khi dialog mở, rời trang, prepare đang chạy),
`site-preview.test.tsx`, `error-messages.test.ts`. Shared: `problem-details.test.ts` (+`isPublishBlockedParams`).

## Sửa sau final review

| # | Mục | Commit |
|---|---|---|
| W1 | `keepMine` ("Giữ bản của tôi") chuyển mốc `savedTree` (và `serverTree`) sang cây của GET — cây server đang giữ ở `version` mới; GET không đọc được cây ⇒ `savedTree = null`, mọi cây local đều bẩn. Trước đây PUT lỗi rồi undo về cây lúc mở phiên báo "Đã lưu" ⇒ publish xuất bản nội dung tab kia. Test: 2 ca `W1 —` ở `autosave-controller.test.ts` (không `saved`; `prepareForPublish` PUT cây A với version của GET trước khi `ok`) | `0f41032` |
| W2 | `packages/builder-renderer/CLAUDE.md`: Portal (xem trước) đã dùng `decorateWithAnchor` | `38378fe` |
| W3 | `eslint.config.mjs`: `no-restricted-syntax` chặn `TSEnumDeclaration` (#19) cho mọi file TS. `turbo.json`: `lint` lấy `eslint.config.mjs` gốc làm input — trước đó đổi rule mà turbo vẫn trúng cache (8/11) | `f2ec31e` |
| S6 | `website-overview.tsx`: `PublishDialog` nhận `siteUrl` bất kể `hasPublication` ⇒ lần xuất bản đầu dialog có "Mở site" ngay. Link ở khối trạng thái giữ điều kiện cũ. Test `S6:` ở `routes/-website.test.tsx` | `f50064f` |
| F5-S3 | `publish-dialog.test.tsx`: đóng → mở lại ⇒ ghi chú rỗng, không còn "Đã xuất bản bản vN", form hiện lại. Đột biến bỏ `key={session}` ⇒ test FAIL | `df62ac9` |

## Nghiệm thu với BE thật (F7) — 6/6 ĐẠT

> 2026-11-20, nhánh `feature/WEBSITE-006` @ **`27edf42`**, cây làm việc sạch. **Chạy lại sau đợt sửa final review @ `7da99cd`: 6/6 ĐẠT** (cùng kịch bản, shop `w6smoke34682`, kết quả từng bước giống bảng dưới).
> - **Trình duyệt thật:** Microsoft Edge headless qua `playwright-core` (cài trong scratchpad, không vào repo), bấm/gõ trên
>   UI Portal như người dùng.
> - Portal: dev server sẵn có của người dùng `admin.vsite.local:5173` (Vite phục vụ mã nguồn hiện tại; Portal không có MSW
>   trên trình duyệt — luôn gọi `/api` qua proxy).
> - BE: process `Vsite.Api` sẵn có (`:5270`, khởi động 2026-11-19 06:39, sau commit backend cuối trên `master`; nhánh này
>   không đổi backend). Postgres `:5433`, Redis `:6379`.
> - Web: `pnpm --filter @vsite/web build` rồi `PORT=3001 HOST=127.0.0.1 API_ORIGIN=http://localhost:5270
>   ROOT_DOMAIN=vsite.local node apps/web/.output/server/index.mjs`. Dev server `:3000` sẵn có trả **502 cho mọi shop**
>   (kể cả `spa-abc` mà BE trả 200) — không phải process của tôi nên không đụng; link "Mở site" trong Portal vẫn trỏ `:3000`
>   theo `.env.development`, site được mở ở `:3001`.
> - Owner mới `w6-smoke2-…@example.com`: link verify lấy từ log của một instance BE **tạm** (`:5271`, cùng bản build) rồi
>   tắt ngay. Shop smoke (`w6smoke32349`) + website + trang tạo bằng API; cây khởi đầu PUT bằng API: trang chủ = Hero02
>   "W6 ban A" + RichText; trang "Gioi thieu" = Hero02 "Gioi thieu W6".
> - ⚠️ Shop `w6smoke31981`, `w6smoke32288`, `w6smoke32349`, `w6smoke34682` (các lần chạy kịch bản) **vẫn còn trong DB dev**.

| # | Bước | Kết quả |
|---|---|---|
| 1 | Sửa trang trong editor | **ĐẠT** — chọn Hero trên canvas, sửa "Tiêu đề" → "W6 ban B"; autosave `PUT …/draft` 200, trạng thái "Đã lưu" |
| 2 | Xuất bản từ editor | **ĐẠT** — toolbar "Xuất bản" → dialog, ghi chú → `POST …/publications` 201, "Đã xuất bản bản v1"; link "Mở site" = `http://w6smoke32349.vsite.local:3000`; **0 PUT** trong lúc dialog mở (khoá G5) |
| 3 | "Mở site" thấy bản mới | **ĐẠT** — `http://w6smoke32349.vsite.local:3001/` → 200, "W6 ban B" ngay lần đầu |
| 4 | Gõ tiếp ⇒ lưu được, không 409 | **ĐẠT** — "Xong", sửa "W6 ban C" → `PUT` 200, không 409, không hộp xung đột. Site vẫn "W6 ban B" (draft ≠ bản đã publish). Đây cũng là kiểm chứng R9 + `finishPublish` nhận version: cây GET sau publish trùng `serverTree` (BE không chuẩn hoá cây khi đọc — mối lo (e) của F2 đã loại) |
| 5 | Trang lỗi ⇒ publish chỉ đúng trang | **ĐẠT** — ở trang "Gioi thieu" xoá tiêu đề Hero (bắt buộc), autosave 200 "Đã lưu"; về editor trang chủ bấm Xuất bản → `POST` 422, alert "Chưa xuất bản được: còn trang có lỗi cần sửa trước.", danh sách: "Gioi thieu — Mở trang — Prop bắt buộc 'title' bị thiếu." (không có Trang chủ); bấm "Mở trang" → editor trang Gioi thieu |
| 6 | Rollback ở tổng quan ⇒ site về bản cũ | **ĐẠT** — trước đó publish v2 ("W6 ban C") từ nút Xuất bản ở **màn tổng quan** (201, nhãn "Đang chạy bản v2", site thấy C). "Quay về bản này" ở dòng v1 → "Xác nhận quay về" → `POST …/rollback` 200, nhãn "Đang chạy bản v1", site → "W6 ban B", không còn C |

Ghi chú: console có một 404 mỗi lần tải — `GET /favicon.ico` của Portal (chưa có favicon; có từ trước, ngoài phạm vi).
Dọn process: instance BE tạm `:5271` và web `:3001` đã tắt; Edge headless đóng sau mỗi lần chạy. BE `:5270`, Portal
`:5173`, web `:3000` là của người dùng — để nguyên.

## Lệch có chủ đích so với plan

- **G6 / F2 — `finishPublish` không nhận version mù.** Plan: sau publish chỉ lấy `version` mới. Nếu nhận vô điều kiện, một
  tab khác lưu xen giữa (sau POST, trước GET) sẽ bị nuốt: tab này PUT tiếp với version mới và ghi đè âm thầm. Đã làm:
  chỉ nhận version khi cây GET trùng `serverTree`; không trùng (hoặc chưa biết) → giữ version cũ để BE trả 409
  `DRAFT_CONFLICT` → hộp thoại xung đột có sẵn. Cái giá: hiếm khi có 409 "giả" sau publish (chọn 409 giả hơn mất dữ liệu).
- **`finishPublish` chỉ cho publish, không cho rollback.** Rollback chỉ ở màn tổng quan; editor mở ở tab khác gặp 409 ở lần
  PUT kế tiếp → hộp xung đột có sẵn.
- **Bỏ tính năng "undo về bản đã lưu thì nạp cây đã sanitize".** Thêm ở vòng review 3 của F2, sinh lỗi Critical (có thể
  nạp cây của tab khác) → gỡ hẳn. Khe gốc có từ trước và vô hại (BE sanitize lại ở lần PUT sau).
- Rollback dùng `Dialog` dùng chung của `@vsite/ui` thay `alertdialog` tự viết.

## Giả định tôi đã tự đặt

- G1–G6 của plan giữ nguyên trừ G6 (xem trên).
- `finishPublish` gọi khi **đóng** dialog (không phải khi POST xong): thử lại trong dialog không phải mở/khoá lại autosave.
- Khoá chéo publish/rollback (R6) dùng `useIsMutating` theo mutation key sinh từ contract — không thêm state Zustand.
- Smoke chạy bằng Edge headless + kịch bản; không có người bấm tay.

## Chưa làm xong / nợ / theo dõi

- F2 nhỏ: undo về đúng cây chưa sanitize đã gửi thì không nạp bản sạch (vô hại); GET nạp bản sanitize lỗi ⇒ 409 giả sau
  publish; `dispose`→`start` bỏ kết quả PUT đang bay (chỉ dev/StrictMode); nhánh nạp bản sanitize nhận `latest.version`
  không đối chiếu (có từ trước).
- F3: `apps/portal/.env.*` có thừa một dòng trống cuối file (agent bị chặn đọc file env nên chưa dọn).
- F4: xác nhận rollback bị khoá vì publish đang chạy mà không giải thích; test R6 giả lập publish qua mutation cache;
  `Dialog` chưa có focus trap (có từ trước ở `@vsite/ui`); "Mở site" vẫn hiện khi shop Suspended (đúng spec); nhãn tạm
  "Đã xuất bản" khi chưa có số bản; `rel` chưa thống nhất.
- F5: thông điệp chặn đã ẩn có thể hiện lại ngoài ngữ cảnh; nút submit trong `PublishDialog` không tắt khi một publish
  khác đang chạy, nút Xuất bản ở tổng quan bỏ qua `isPublishing`;
  "Mở trang" khi đã gõ trong lúc khoá ⇒ LeavePrompt hiện chồng dialog (có cảnh báo, không mất dữ liệu).
- Môi trường: dev server `apps/web :3000` đang chạy trả 502 mọi shop (cấu hình process của người dùng, chưa điều tra);
  Portal thiếu `favicon.ico`; `getByLabel('Tiêu đề')` không tìm được ô trong Inspector (label không gắn với input —
  test hiện tìm theo giá trị).

## Kiểm tra

- `pnpm lint && pnpm typecheck && pnpm test && pnpm build` toàn repo: exit 0 (sau fix wave, `df62ac9`: portal 385,
  web 269, builder-components 535, builder-core 137, builder-renderer 36, theme-engine 49, api-sdk 12, shared 10).
- `pnpm check:docs`: OK.
