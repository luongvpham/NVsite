# WEBSITE-006 — Publish / rollback website từ Portal (Bước 8e)

> **Đã chốt 2026-11-19 — chưa code** (G1–G6 người đã duyệt)
>
> **Lane A** (không chạm contract — API publish/rollback/list đã có từ 8b, WEBSITE-004) + `plan.md` gọn vì
> R9 (version draft đổi sau publish) dễ sai. Cổng: Gate 2. Chỉ FE (`apps/portal`, nhỏ ở `builder-renderer` nếu cần).
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** Owner tự xuất bản website từ Portal, thấy lỗi chặn publish theo từng trang, xem lịch sử bản
và quay về bản cũ; editor đang mở sửa tiếp được sau publish/rollback mà không dính `409`.
**Spec:** `DesignIdeal/step.md` §8e · `Docs/tasks/WEBSITE-004/brief.md` §B · **Quyết định liên quan:** #112, #109, #111

## 1. Yêu cầu (đã chốt với người, 2026-11-19)
- Người dùng / vai trò: Owner của shop (Website chỉ Owner, #100).
- Điểm chạm:
  - Nút **Xuất bản** ở toolbar editor **và** ở màn tổng quan Website.
  - **Lịch sử bản publish + Rollback** chỉ ở màn tổng quan Website.
- Publish từ editor khi còn thay đổi chưa lưu: **lưu trước rồi publish**; đang lỗi validate hoặc `409` thì
  chặn publish và nói phải xử lý lỗi đó trước.
- Theme còn thay đổi chưa lưu: **chặn publish**, nhắc lưu hoặc huỷ thay đổi giao diện trước.
- Lỗi `PUBLISH_BLOCKED`: **danh sách theo trang + link "Mở trang"**, không tô đỏ khối trên canvas.
- Có link **"Mở site"** (màn tổng quan + sau khi xuất bản), cấu hình bằng env mới của portal; thiếu env thì ẩn link.
- Xong nghĩa là (`step.md` §8e): Owner publish từ editor và thấy site công khai đổi; trang đang bật mà lỗi thì
  Portal chỉ ra đúng trang đó; rollback từ danh sách được; sửa draft ngay sau publish không gặp `409`.
- **Không làm trong task này:**
  - Đổi contract hoặc BE (mọi endpoint đã có từ 8b).
  - Gỡ xuất bản / đình chỉ website (8b không có endpoint).
  - Tô đỏ khối lỗi publish trên canvas.
  - Tự lưu theme khi publish.
  - Phân trang lịch sử (BE trả ≤ 50 bản, G11) · prune bản cũ (8d).
  - Tên miền riêng, `https` production cho link "Mở site" (nợ deploy production).

## 2. Hiện trạng (khảo sát 2026-11-19, mọi dòng đã đọc code ✅)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| API publish / list / rollback | ✅ có | `packages/api-sdk/src/generated/website/website/website.ts:790` (`usePublishWebsite`), `:847` (`useListPublications`), `:933` (`useRollbackPublication`) | Hook Orval sẵn, không cần đổi contract |
| MSW mock cho 3 endpoint | ✅ có | `packages/api-sdk/src/generated/website/website/website.msw.ts:174`, `:186`, `:198` | Mock ngẫu nhiên; test phải override |
| `WebsiteDto.status` + `currentPublicationId` | ✅ có | `packages/api-sdk/src/generated/website/model/websiteDto.ts` | Đủ để hiện "Chưa xuất bản / đang chạy bản vN / Đình chỉ" |
| `PageDto.isDirty` + badge ở danh sách trang | ✅ có | `apps/portal/src/components/website/page-list.tsx:129` | Sau publish phải nạp lại `listPages` để badge tắt |
| Màn tổng quan Website | 🟡 một phần | `apps/portal/src/components/website/website-overview.tsx:34` | Chỉ có danh sách trang; chưa có nút publish, trạng thái, lịch sử |
| Toolbar editor | 🟡 một phần | `apps/portal/src/components/website/editor-shell.tsx:220` | Có Hoàn tác / Làm lại / Xem trước / trạng thái lưu; chưa có Xuất bản |
| Autosave giữ `version` | 🟡 một phần | `apps/portal/src/lib/autosave/autosave-controller.ts:60` | `version` nằm riêng trong controller; `flush()` là private (`:198`); chưa có cách "lưu ngay và chờ xong", "tạm dừng", hay "chỉ lấy lại version" |
| Theme chưa lưu | ✅ biết được | `apps/portal/src/hooks/use-theme-draft.ts:76` (`dirty`), `:95` (`save` bấm tay) | Theme không có `version` (last-write-wins) nên R9 không chạm theme; nhưng theme chưa lưu sẽ không vào bản publish |
| Đọc `params` lỗi có kiểm kiểu | ✅ có khuôn | `packages/shared/src/errors/problem-details.ts:42` (`DraftConflictParams`), `:81` (`getProblemParams`) | Chưa có type/guard cho `PUBLISH_BLOCKED` |
| Thông điệp lỗi theo `error_code` | 🟡 một phần | `apps/portal/src/lib/error-messages.ts:46` | Thiếu `PUBLISH_BLOCKED`, `PUBLISH_CONFLICT`, `ROLLBACK_CONFLICT`, `PUBLICATION_NOT_FOUND`, `REQUIRED_PROP_MISSING` |
| Anchor trong xem trước | 🟡 trùng lặp | `apps/portal/src/components/website/site-preview.tsx:37` (`decorateWithId`), `packages/builder-renderer/src/anchor.tsx:9` (`decorateWithAnchor`) | Portal còn bản riêng; site công khai đã dùng bản chung |
| Link "Mở site" | ❌ chưa có | — | Portal không biết root domain của Shop Site (`.env*` chỉ có `VITE_API_BASE_URL`); `ShopDto.slug` có (`packages/api-sdk/src/generated/shop/model/shopDto.ts:10`) |

### Phát hiện khi khảo sát
1. **R9 còn đúng cả với editor của chính tab đang publish.** Editor nạp draft **một lần** (`editor-shell.tsx:99`)
   và controller giữ `version` (`autosave-controller.ts:77`). Publish hạ `isDirty` của trang đang mở ⇒ `version` trên
   server đổi ⇒ lần autosave kế tiếp `409` ngay trong tab vừa bấm Xuất bản. Phải có cách lấy lại **chỉ `version`**
   (không đè cây local) sau publish.
2. **Có cửa sổ đua:** người dùng gõ tiếp trong lúc publish đang bay ⇒ autosave PUT với `version` cũ, hoặc PUT xen giữa
   publish ⇒ BE trả `409 PUBLISH_CONFLICT` (brief §5). Cần tạm dừng autosave trong lúc publish.
3. **Tab khác / rollback từ màn tổng quan:** editor ở tab khác sẽ nhận `409 DRAFT_CONFLICT` và hiện dialog có sẵn
   (`ConflictDialog`, `editor-shell.tsx:151`) — "Giữ bản của tôi" lấy version mới bằng GET (`autosave-controller.ts:155`).
   Hành vi đúng, không cần làm thêm.
4. `PUBLISH_BLOCKED` không có schema trong contract (`params` là `unknown` trong `ProblemDetails`) — FE tự viết guard
   theo brief §5, cùng khuôn `DraftConflictParams`.

## 3. Flow

### Flow 1: Xuất bản từ màn tổng quan Website
```text
1. Owner mở /shops/{id}/website                       ✅ website-overview.tsx
2. Thấy trạng thái: chưa xuất bản / bản vN / đình chỉ  ❌ thêm (WebsiteDto.status + currentPublicationId)
3. Bấm Xuất bản → dialog nhập ghi chú (≤ 200)          ❌ thêm
4. POST /publications                                  ✅ usePublishWebsite
5a. 201 → báo thành công, nạp lại website + pages + publications (badge "chưa xuất bản" tắt)   ❌
5b. 422 PUBLISH_BLOCKED → danh sách lỗi theo trang (lỗi cấp website đứng đầu), link "Mở trang", "còn lỗi khác" nếu truncated   ❌
5c. 409 PUBLISH_CONFLICT → "Có thao tác khác vừa chạy, bấm lại" — không tự thử lại   ❌
```

### Flow 2: Xuất bản từ editor
```text
1. Bấm Xuất bản ở toolbar                               ❌ thêm nút (editor-shell.tsx:220)
2. Autosave: còn thay đổi → lưu ngay và chờ xong        ❌ controller chưa có "flush và chờ"
   - đang invalid / conflict / blocked → chặn, chỉ lỗi cần xử lý trước
   - theme chưa lưu → chặn, nhắc lưu hoặc huỷ thay đổi giao diện   (chờ người chốt, §4)
3. Tạm dừng autosave trong lúc publish                  ❌
4. POST /publications (dialog ghi chú như Flow 1)       ✅ hook
5. Xong (thành công hay lỗi) → GET draft lấy version mới, KHÔNG đè cây → mở lại autosave   ❌
6. Kết quả hiện như Flow 1 (5a–5c)
```

### Flow 3: Xem lịch sử và rollback (màn tổng quan)
```text
1. Danh sách bản: vN, thời điểm, ghi chú, đánh dấu bản đang chạy   ❌ (useListPublications, ≤ 50, không phân trang)
2. Bấm "Quay về bản này" → dialog xác nhận            ❌
3. POST /{id}/rollback                                 ✅ hook
4a. 200 → nạp lại website + pages (mọi trang thành "chưa xuất bản") + publications   ❌
4b. 404 PUBLICATION_NOT_FOUND / 409 ROLLBACK_CONFLICT → báo, nạp lại danh sách, cho bấm lại   ❌
```

### Flow 4: Mở site
```text
1. Màn tổng quan + thông báo xuất bản thành công có link "Mở site" → tab mới   ❌
   URL = VITE_SHOP_SITE_ORIGIN_TEMPLATE thay {slug} bằng ShopDto.slug (shopDto.ts:10); thiếu env → ẩn link
   Website chưa từng xuất bản (currentPublicationId null) → ẩn link (site trả 404)
```

### Flow 5: Xem trước dùng anchor chung
```text
1. SitePreview dùng decorateWithAnchor của builder-renderer, bỏ decorateWithId   ❌ (site-preview.tsx:37, :245)
```

## 4. Đã chốt trong session lập plan

| # | Chốt | Ai chốt |
|---|---|---|
| — | Lane A (không chạm contract), vẫn có `plan.md` gọn; không `data-needs.md` | người |
| — | Nút Xuất bản ở toolbar editor **và** màn tổng quan; lịch sử + rollback chỉ ở màn tổng quan | người |
| — | Publish từ editor: lưu cây trước rồi publish; autosave đang `invalid`/`conflict`/`blocked` ⇒ chặn | người |
| — | Theme chưa lưu ⇒ chặn publish, nhắc lưu/huỷ | người |
| — | `PUBLISH_BLOCKED`: danh sách theo trang + link "Mở trang", không tô đỏ canvas | người |
| — | Link "Mở site" qua env portal `VITE_SHOP_SITE_ORIGIN_TEMPLATE`; thiếu env ⇒ ẩn | người |
| — | Khung F1–F7, một session FE | người |

Không cấp `#N` mới — toàn bộ là chi tiết thực thi trên quyết định đã có (#100, #109, #111, #112).

## 6. Ràng buộc toàn cục

| Ràng buộc | Giữ bằng | Ở task |
|---|---|---|
| Không sửa `packages/api-sdk/src/generated/**` (Rule 5) | hook `.claude/hooks/guard-write.mjs` | mọi task |
| Không đổi contract / BE | `contracts/contract.lock` + review | mọi task |
| Không copy Query → Zustand (#20): danh sách bản, trạng thái website sống ở TanStack Query | review | F3, F4 |
| `params` lỗi đọc qua guard, không cast mù | `packages/shared/src/errors/problem-details.test.ts` ca `isPublishBlockedParams` | F1 |
| Autosave không bao giờ hai PUT song song (R1 hiện có) và không PUT trong lúc publish | `apps/portal/src/lib/autosave/autosave-controller.test.ts` ca "không PUT khi đang publish" | F2 |
| Lấy lại `version` không đè cây local | `autosave-controller.test.ts` ca "refreshVersion giữ cây" | F2 |
| Không tự thử lại khi `409 PUBLISH_CONFLICT` / `ROLLBACK_CONFLICT` (brief §5, §7) | test MSW đếm đúng 1 request | F3, F4 |
| Website chỉ Owner (#100) | màn mới nằm trong `OwnerOnlyGate` có sẵn | F4, F5 |
| Mọi màn có dữ liệu: loading · error · empty | test F4 | F4 |
| Enum TS dạng const object (#19) | lint hiện có | mọi task |

## 7. Review focus — lỗi dễ lọt khi spec im lặng

| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Gõ tiếp trong lúc publish đang bay | Không PUT cho tới khi publish xong và đã lấy lại `version`; sau đó lưu bằng `version` mới, không `409` | F2, F5 |
| R2 | Publish bị `422` hoặc `409` | Vẫn lấy lại `version` rồi mở lại autosave (publish lỗi không ghi gì, nhưng không được để autosave treo) | F2, F5 |
| R3 | GET lấy lại `version` thất bại (mạng) | Autosave mở lại với `version` cũ; nếu sau đó `409` thì dialog có sẵn xử lý — không mất cây | F2 |
| R4 | `PUBLISH_BLOCKED` có lỗi cấp website (`pageId: null`) + `truncated: true` | Lỗi website đứng đầu, không có link "Mở trang"; dòng "còn lỗi khác" | F3 |
| R5 | `pageId` trong lỗi là trang đang mở | Link "Mở trang" không điều hướng lại vô ích (đã ở trang đó) — hiện "trang này" | F3, F5 |
| R6 | Bấm Xuất bản hai lần nhanh / bấm Rollback khi publish đang chạy | Nút disabled trong lúc mutation chạy; BE `409` vẫn được hiện đúng | F3, F4 |
| R7 | Rollback về chính bản đang chạy | `200`, không ghi gì; UI không cần chặn nhưng ẩn nút trên bản `isCurrent` | F4 |
| R8 | Website `Suspended` | Vẫn publish được (G6) nhưng trạng thái ghi rõ "đang bị đình chỉ — site công khai không hiển thị nội dung" | F4 |

## 8. Sơ đồ file

```text
packages/shared/src/errors/problem-details.ts        sửa: PublishBlockedParams + isPublishBlockedParams
packages/shared/src/errors/problem-details.test.ts   sửa
apps/portal/
  .env.development / .env.test                       sửa: VITE_SHOP_SITE_ORIGIN_TEMPLATE
  .env.production                                    sửa: để trống + comment (ẩn link tới khi deploy)
  src/lib/error-messages.ts (+ .test.ts)             sửa: 5 mã lỗi
  src/lib/site-url.ts (+ .test.ts)                   mới: shopSiteUrl(slug) | null
  src/lib/autosave/autosave-controller.ts (+ .test.ts)  sửa: prepareForPublish, finishPublish
  src/hooks/use-publish-website.ts                   mới: mutation + invalidate + phân loại lỗi
  src/components/website/publish-dialog.tsx (+ .test.tsx)    mới: ghi chú + kết quả + lỗi
  src/components/website/publish-errors.tsx          mới: danh sách PUBLISH_BLOCKED
  src/components/website/publication-history.tsx (+ .test.tsx) mới: lịch sử + rollback
  src/components/website/website-overview.tsx        sửa: khối trạng thái + nút + lịch sử
  src/components/website/editor-shell.tsx            sửa: nút Xuất bản ở Toolbar
  src/components/website/site-preview.tsx            sửa: decorateWithAnchor
  src/routes/-editor-publish.test.tsx                mới
  src/routes/-website.test.tsx                       sửa
```

## 10. Session FE (một session)

### F1: Kiểu lỗi publish + thông điệp
`PublishBlockedParams = { errors: { pageId: string|null; pageTitle: string|null; path: string; code: string; message: string }[]; truncated: boolean }`
theo `Docs/tasks/WEBSITE-004/brief.md` §5, cùng khuôn `DraftConflictParams` (`problem-details.ts:42`).
- [ ] Test trước: `packages/shared/src/errors/problem-details.test.ts` — guard nhận đúng dạng, `pageId: null`, từ chối thiếu `errors`/`truncated`, phần tử sai kiểu
- [ ] Test trước: `apps/portal/src/lib/error-messages.test.ts` — có thông điệp cho `PUBLISH_BLOCKED`, `PUBLISH_CONFLICT`, `ROLLBACK_CONFLICT`, `PUBLICATION_NOT_FOUND`, `REQUIRED_PROP_MISSING`
- [ ] FAIL → hiện thực → PASS (`pnpm --filter @vsite/shared test`, `pnpm --filter @vsite/portal test`)
- [ ] Commit `feat(shared): params PUBLISH_BLOCKED + thông điệp lỗi publish/rollback (WEBSITE-006)`

### F2: Autosave nhường chỗ cho publish (R9)
Thêm vào `AutosaveController` (`apps/portal/src/lib/autosave/autosave-controller.ts`), giữ nguyên các bất biến ở comment `:45–56`:
```ts
/** Lưu ngay phần còn lại rồi khoá autosave cho publish. Trả lý do nếu không publish được. */
prepareForPublish(): Promise<{ ok: true } | { ok: false; reason: 'invalid' | 'conflict' | 'blocked' | 'error' }>;
/** Sau publish (thành công hay lỗi): GET draft lấy `version` — KHÔNG đè cây — rồi mở khoá; thay đổi gõ trong lúc khoá được lưu tiếp. */
finishPublish(): Promise<void>;
```
Trong lúc khoá: `onTreeChanged` chỉ đánh dấu `unsaved`, không hẹn giờ PUT. `finishPublish` lỗi GET ⇒ vẫn mở khoá với `version` cũ (R3).
- [ ] Test trước (`autosave-controller.test.ts`, fake timer): đang có thay đổi ⇒ `prepareForPublish` PUT đúng một lần rồi `ok`; PUT đang bay ⇒ chờ xong; `invalid`/`conflict`/`blocked` ⇒ `ok:false` kèm lý do, không PUT; gõ trong lúc khoá ⇒ không PUT; `finishPublish` ⇒ dùng `version` từ GET cho PUT kế tiếp và cây local giữ nguyên tham chiếu; gõ trong lúc khoá ⇒ lưu ngay sau `finishPublish`; GET lỗi ⇒ mở khoá, giữ `version` cũ; `dispose` giữa chừng không ném
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(portal): autosave khoá + lấy lại version quanh publish (R9, WEBSITE-006)`

### F3: Luồng publish dùng chung
- `src/lib/site-url.ts`: `shopSiteUrl(slug)` thay `{slug}` trong `import.meta.env.VITE_SHOP_SITE_ORIGIN_TEMPLATE`; thiếu/rỗng ⇒ `null`. Ghi giá trị dev `http://{slug}.vsite.local:3000` (`apps/web/package.json:7` chạy cổng 3000).
- `src/hooks/use-publish-website.ts`: bọc `usePublishWebsite`; `onSettled` invalidate `getWebsite` + `listPages` + `listPublications`; trả lỗi đã phân loại (`blocked` kèm params / `conflict` / thông điệp khác). Không retry.
- `publish-dialog.tsx`: ô ghi chú (≤ 200, trim), nút Xuất bản disabled khi đang chạy, thành công ⇒ "Đã xuất bản bản vN" + link "Mở site" (nếu có URL).
- `publish-errors.tsx`: lỗi `pageId: null` đứng đầu, nhóm theo trang, link "Mở trang" (trang đang mở ⇒ "trang này"), `truncated` ⇒ "còn lỗi khác".
- [ ] Test trước: `site-url.test.ts` (có/không env, slug thay đúng chỗ) · `publish-dialog.test.tsx` với MSW: 201, 422 (lỗi website + 2 trang + truncated), 409 (đúng 1 request, không tự thử lại), note toàn khoảng trắng gửi `null`/bỏ trống
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(portal): dialog xuất bản + danh sách lỗi PUBLISH_BLOCKED (WEBSITE-006)`

### F4: Màn tổng quan Website
`website-overview.tsx`: khối trạng thái ở trên `PageList` — "Chưa xuất bản" / "Đang chạy bản vN" / "Đang bị đình chỉ" (R8), nút Xuất bản (mở dialog F3), link "Mở site" khi `currentPublicationId` khác null. `publication-history.tsx`: bảng bản (vN, thời điểm theo giờ VN, ghi chú, nhãn "Đang chạy"), nút "Quay về bản này" (ẩn ở bản `isCurrent`, R7) + dialog xác nhận; thành công/lỗi ⇒ invalidate như F3.
- [ ] Test trước (`publication-history.test.tsx`, `routes/-website.test.tsx`): loading · error (Thử lại) · empty ("Chưa có bản nào"); rollback 200 ⇒ nạp lại danh sách và trạng thái; 404/409 ⇒ thông điệp, nạp lại danh sách, đúng 1 request; nút disabled khi đang chạy (R6); trạng thái Suspended
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(portal): trạng thái xuất bản, lịch sử bản và rollback ở màn Website (WEBSITE-006)`

### F5: Xuất bản từ editor
`editor-shell.tsx` Toolbar thêm nút Xuất bản. Bấm ⇒ `theme.dirty` ⇒ thông báo chặn (không gọi gì) · không thì `controller.prepareForPublish()` ⇒ `ok:false` ⇒ thông báo theo lý do · `ok` ⇒ mở dialog F3 ⇒ đóng dialog hoặc publish xong/lỗi ⇒ `controller.finishPublish()`.
- [ ] Test trước (`routes/-editor-publish.test.tsx`, MSW): theme dirty ⇒ không POST; autosave `invalid` ⇒ không POST; có thay đổi chưa lưu ⇒ PUT trước rồi POST; **sau 201 gõ tiếp ⇒ PUT mang `version` của GET mới, không có dialog 409** (R1); 422 ⇒ danh sách lỗi, autosave chạy lại (R2); huỷ dialog ⇒ autosave chạy lại
- [ ] FAIL → hiện thực → PASS
- [ ] Commit `feat(portal): nút Xuất bản trong editor (WEBSITE-006)`

### F6: Anchor chung trong xem trước
- [ ] `site-preview.tsx`: import `decorateWithAnchor` từ `@vsite/builder-renderer`, xoá `decorateWithId` (`:37`)
- [ ] `site-preview.test.tsx` + `routes/-editor-preview.test.tsx` vẫn xanh (thêm ca: node có wrapper `id`)
- [ ] Commit `refactor(portal): xem trước dùng decorateWithAnchor của builder-renderer (WEBSITE-006)`

### F7: Smoke với BE thật
- [ ] Bật BE + portal + `apps/web` (tắt MSW). Owner: sửa trang → Xuất bản từ editor → "Mở site" thấy bản mới → gõ tiếp ⇒ lưu được, không 409 → làm một trang lỗi (prop bắt buộc trống) ⇒ publish chỉ ra đúng trang → rollback ở màn tổng quan ⇒ site về bản cũ
- [ ] Ghi kết quả từng bước vào `changelog.md` (kèm commit đo)

## 11. Đóng task (trước Gate 2)
- [ ] `pnpm lint && pnpm typecheck && pnpm test && pnpm build` toàn repo; `pnpm check:docs`
- [ ] `Docs/tasks/WEBSITE-006/changelog.md` (đã làm · lệch · giả định · nợ) · cập nhật `00-INDEX` §3 dòng 8e, §4 dòng WEBSITE-005 (bỏ `decorateWithAnchor`, UI publish)
- [ ] `change-reviewer` (model mạnh nhất không bắt buộc: không chạm tenant/contract — dùng model thường) → `review.md` kèm "Ma sát quy trình"

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)

| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | Tên env `VITE_SHOP_SITE_ORIGIN_TEMPLATE`, dạng `http://{slug}.vsite.local:3000`; production để trống (ẩn link) | Template gọn hơn tách protocol/domain/port; production chưa deploy | Đổi tên/dạng trong `site-url.ts` |
| G2 | Rollback có dialog xác nhận; publish có dialog ghi chú (ghi chú tuỳ chọn) | Rollback đổi ngay site công khai; ghi chú giúp đọc lịch sử | Bỏ dialog = bỏ một bước UI |
| G3 | Thời điểm trong lịch sử hiển thị theo `Asia/Ho_Chi_Minh` | Khớp B6 của WEBSITE-005 | Đổi formatter |
| G4 | `publishedByUserId` không hiển thị (chỉ có id, chưa có tên) | Không có endpoint tra tên người | Thêm sau khi có nhiều người quản trị |
| G5 | Publish từ editor khoá autosave cả trong lúc dialog ghi chú đang mở | Đơn giản, tránh PUT xen giữa publish | Khoá ngắn hơn (chỉ lúc POST) thì phải xử lý thêm PUT xen |
| G6 | Sau publish từ editor không tải lại cây, chỉ lấy `version` | Publish chỉ hạ `isDirty`, không đổi nội dung cây | Nếu BE sau này đổi cây lúc publish thì phải nạp lại cây |

## 13. Verification tổng
- `pnpm --filter @vsite/portal test` và `pnpm --filter @vsite/shared test` xanh, có các ca R1–R8.
- Toàn repo: `pnpm lint && pnpm typecheck && pnpm test && pnpm build` exit 0.
- F7 smoke: 6 bước đều ĐẠT, ghi trong `changelog.md`.
