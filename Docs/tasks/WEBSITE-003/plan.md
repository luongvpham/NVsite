# WEBSITE-003 — builder-renderer hoàn thiện + preview toàn trang (Bước 6)

> **Đã chốt 2026-11-11 — chưa code** (người duyệt đồng ý plan + giả định G1–G6)
>
> **Lane C** (chỉ FE): màn mới + chạm nhiều package + chốt API renderer cho 8b; không chạm contract/Registry/config
> → không Gate 1, chỉ Gate 2. Một session S2, T1 → T5.
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc `superpowers:executing-plans`.

**Mục tiêu:** chủ shop xem trước cả trang (khung Header/Footer + theme + nội dung) như khách sẽ thấy, đọc từ
**draft**, bấm link sang trang khác của shop ngay trong preview. Đồng thời chốt API của `builder-renderer`
(`basePath` #11, `locale`/`defaultLocale` #98, một `resolveUrl`, snapshot SSR↔CSR #23) trước khi `apps/web`
dùng thật ở Bước 8b.

**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) · **Spec:** `DesignIdeal/step.md` §6 · `05` §8 (menu ngoài
Component Tree), §26 (#98) · **Quyết định:** #11, #23, #98, #103 (mới).

## 1. Yêu cầu (đã chốt với người, 2026-11-11)
- **Người dùng:** chủ shop (Owner) trong editor Portal.
- **Điểm chạm:** editor Portal → nút xem trước → preview toàn trang; `packages/builder-renderer` (API mới).
- **Header/Footer:** khung layout của renderer **nằm ngoài Component Tree** (đúng `05` §8). Header hiện tên + logo
  shop, Footer thông tin cơ bản; chỗ menu nhận props rỗng — Bước 7 (`NavigationConfig`) đổ dữ liệu. **Không** thêm
  manifest Header/Footer vào Registry.
- **Preview:** toàn màn hình trang đang sửa; bấm link tới trang khác của shop → preview mở **draft** của trang đó.
- **Locale (#98):** `RenderTree` nhận `locale`/`defaultLocale`; `resolveUrl` thêm tiền tố `/{locale}/` khi khác
  ngôn ngữ mặc định. **Chưa** đọc overlay `i18n` trên node.
- **Xong nghĩa là:** từ editor bấm "Xem trước" thấy cả trang (Header/Footer + theme + nội dung đang sửa), bấm link sang
  trang khác của shop xem draft của nó, link hỏng hiện NotFound + lý do; renderer chạy `renderToString` ở Node và hydrate không lệch.
- **Không làm trong task này:** render công khai ở `apps/web` (8b, cần `SitePublication`; web không bao giờ đọc
  draft — `05` §7) · menu thật (`NavigationConfig`, Bước 7) · overlay i18n / bảng dịch (bước L) · Header/Footer
  chỉnh được trong builder.

## 2. Hiện trạng (khảo sát 2026-11-11)

| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|
| Render cây trang (gốc `Page`, đệ quy) | ✅ | [renderTree.tsx:50-67](../../../packages/builder-renderer/src/renderTree.tsx#L50-L67) | Props chỉ có `tree`, `decorateNode` — chưa có `basePath`/`locale` |
| Context render (`basePath`, `mediaMap`, `resolveImage`, `resolveUrl`) | 🟡 | [context.tsx:19-26](../../../packages/builder-components/src/context.tsx#L19-L26) | `basePath` khai báo nhưng **không ai đọc**; mặc định `''` |
| `resolveUrl` cho link `page`/`systemPage` | ❌ stub | [context.tsx:71-82](../../../packages/builder-components/src/context.tsx#L71-L82) | Trả `'#'`; chỉ `external`/`anchor` chạy thật |
| `resolveImage` + `mediaMap` | ✅ | [context.tsx:49-64](../../../packages/builder-components/src/context.tsx#L49-L64), [use-media-map.ts](../../../apps/portal/src/hooks/use-media-map.ts) | Canvas đã nạp ảnh thật theo id |
| Theme qua CSS vars | ✅ | [css-vars.ts:144-152](../../../packages/theme-engine/src/css-vars.ts#L144-L152) | **Người gọi** bọc `style={themeStyle(...)}`, renderer không tự bọc ([editor-canvas.tsx:106-108](../../../apps/portal/src/components/website/editor-canvas.tsx#L106-L108)) |
| Khung trang Header/Footer | ❌ | — | Registry chỉ có Gallery/Hero/RichText/Section/ServiceGrid |
| Locale trong renderer | ❌ | — | `locale` chỉ có ở schema `i18n` của envelope ([envelope-schema.ts:46-48](../../../packages/builder-components/meta/envelope-schema.ts#L46-L48)) |
| Test SSR | 🟡 | [ssr-theme.node.test.tsx](../../../packages/builder-renderer/tests/ssr-theme.node.test.tsx) | `renderToString` ở Node, kiểm theme; **chưa có** snapshot SSR↔CSR (nợ BOOTSTRAP-002) |
| Lint renderer | 🟡 | [eslint.config.mjs:45-70](../../../eslint.config.mjs#L45-L70) | Chặn `window`/`document` và import `builder-core`; **chưa chặn** import `apps/**` |
| Nơi gọi `RenderTree` trong app | ✅ 1 chỗ | [editor-canvas.tsx:118-120](../../../apps/portal/src/components/website/editor-canvas.tsx#L118-L120) | Canvas chặn điều hướng khi bấm link ([:186-191](../../../apps/portal/src/components/website/editor-canvas.tsx#L186-L191)); `apps/web` chưa dùng |
| Dữ liệu preview cần (API có sẵn) | ✅ | api-sdk generated | `useGetWebsite` (`defaultLocale`, `locales`, `homePageId`) · `useListPages` (`id, kind, systemType, slug, title, isEnabled, isHomePage`) · `useGetPageDraft` · `useGetShop` (`name`, `logoUrl`) · `useGetTheme` · `useGetAssetsByIds` — **không cần endpoint mới** |
| Route preview trong Portal | ❌ | — | Editor ở [`…website.pages.$pageId.tsx`](../../../apps/portal/src/routes/_authenticated.shops.$shopId.website.pages.$pageId.tsx); toolbar ở [editor-shell.tsx:214-259](../../../apps/portal/src/components/website/editor-shell.tsx#L214-L259) |
| Component có link | ✅ | [hero.manifest.ts:83-87](../../../packages/builder-components/registry/hero.manifest.ts#L83-L87) | Chỉ Hero `cta.target` (`page`/`systemPage`/`external`/`anchor`) gọi `resolveUrl` |

### Phát hiện khi khảo sát
- **Quy tắc slug → URL chưa ghi ở đâu.** Đã biết: trang chủ `slug = ""`, `NotFound` `slug = NULL`
  ([website.md:28](../../../backend/docs/modules/website.md#L28)); slug không trùng mã locale ([:113](../../../backend/docs/modules/website.md#L113));
  System page có route trong [config/system-pages.json](../../../config/system-pages.json) (`Contact` → `lien-he`…). Cách ghép
  `basePath` + `/{locale}` + slug **chưa có tài liệu nào chốt** → task này chốt (§4).
- **#11 ghi "vsite.vn/{slug}"** nhưng Shop Site thực tế là `{slug}.vsite.vn` hoặc domain riêng ([website.md:18](../../../backend/docs/modules/website.md#L18));
  `vsite.vn/shop/{slug}` là Shop **Profile** (không dùng renderer). Với Site, `basePath` thật ở 8b gần như luôn `''`;
  giá trị khác `''` dùng cho **preview** (`/shops/{id}/website/preview`). Ghi vào changelog khi code.
- `ProductListing`/`ProductDetail`/`ProductSearch`/`Booking` có route nhưng `seedOnCreate: false` — link `systemPage` tới loại chưa có trang phải có hành vi rõ (§3).
- Nợ cũ chạm phạm vi: snapshot SSR↔CSR (#23, BOOTSTRAP-002), lint chặn renderer import `apps/**` (TOOLING-001) — cả hai thuộc task này.

## 3. Flow (đã duyệt nhánh lỗi 2026-11-11)

### Flow 1: Mở xem trước từ editor
```text
1. Editor → nút "Xem trước" ở toolbar                       ❌ editor-shell.tsx:214-259
2. Mở LỚP PHỦ toàn màn hình trong editor (không phải tab/route mới)        ❌
   Đóng bằng nút "Đóng xem trước" / Escape → về editor, không mất gì
3. Nạp: website (locale) + listPages + shop (tên/logo) — hook có sẵn       ✅ hook / ❌ màn
   Trang đang sửa: cây + themeDraft từ store (kể cả phần chưa autosave)
4. Render: khung SiteLayout (Header tên+logo, menu rỗng; Footer tên + © năm, vùng liên hệ trống)
   → RenderTree(basePath, locale, defaultLocale) → bọc themeStyle         ❌
   Đang tải → khung + "Đang tải trang"; lỗi API → báo + Thử lại; trang rỗng → thân trang trống
```

### Flow 2: Bấm link trong preview
```text
1. Link page/systemPage → href = basePath + [/{locale} nếu ≠ defaultLocale] + /{slug}   ❌ resolveUrl thật
   trang chủ (slug "") → basePath + [/{locale}] + "/"
   systemPage → slug của trang có systemType đó (slug System đã cố định ở BE)
2. Preview chặn điều hướng thật, tra href → trang trong listPages → nạp draft trang đó   ❌
   Trang đích = trang đang sửa → dùng cây trong store
3. external → mở tab mới; anchor → cuộn tới node trong preview                         🟡
Nhánh (Q2):
- trang đích isEnabled=false → hiện trang NotFound + dải "Trang này đang tắt — khách sẽ thấy trang 404"
- systemPage chưa có trang (vd. ProductListing) → href theo route trong config/system-pages.json; preview hiện NotFound + dải báo
- link tới trang đã xoá → href '#' (không còn slug); bấm trong preview → dải "Liên kết trỏ tới trang đã xoá"
- có nút "← Trang trước" trong preview để quay lại
```

### Flow 3: Renderer chạy ở server (chuẩn bị 8b)
```text
1. renderToString(<SiteLayout>+<RenderTree basePath locale …>) ở Node, không window/document   🟡 đã có test theme
2. HTML server == HTML client lần đầu (hydrate không lệch)                                        ❌ snapshot SSR↔CSR
3. Lint: renderer/components không import apps/**                                                 ❌
```

## 4. Đã chốt trong session lập plan (2026-11-11)
| # | Chốt |
|---|---|
| **#103** | **URL của Shop Site** (ghi ở `DesignIdeal/DECISIONS.md`): `basePath` + (`/{locale}` khi khác `defaultLocale`) + `/{slug}`; trang chủ `slug ""` → `…/`; `systemPage` → slug của trang có `systemType` đó, chưa có trang → route đầu tiên trong `config/system-pages.json`; `NotFound` không có URL; trang đã xoá → `#`; `basePath = ''` trên site thật, khác `''` chỉ ở preview |
| — | Header/Footer = `SiteLayout` của renderer, **ngoài** Component Tree (`05` §8), không thêm manifest. Header: tên + logo shop, `menuItems` (props, Bước 6 rỗng). Footer: tên + © năm, `contact` (props tuỳ chọn, Bước 6 trống) |
| — | Preview = lớp phủ toàn màn hình trong editor; trang đang sửa đọc `tree` + `themeDraft` từ store; trang khác đọc draft server |
| — | Link hỏng (trang tắt / systemPage chưa có trang) → preview hiện trang NotFound + dải báo; trang đã xoá → dải báo |
| — | Locale: renderer nhận `locale`/`defaultLocale`, chỉ dùng để dựng URL; **không** đọc overlay `i18n` |
| — | Lane C, chỉ S2 (FE). Không chạm contract/Registry/config → không Gate 1; Gate 2 cuối task |

## 5. Trình tự session
Chỉ **S2 (FE)**, một nhánh `feature/WEBSITE-003`, T1 → T5 tuần tự. T1–T3 là nền packages (API renderer phải chốt
trước khi portal dùng ở T4). Phát hiện cần endpoint/field mới giữa chừng → **dừng**, báo.

## 6. Ràng buộc toàn cục
- **Ghi:** `packages/{builder-renderer, builder-components (trừ generated/ và registry/), theme-engine}`, `apps/portal/**`,
  `eslint.config.mjs` (T3), `Docs/tasks/WEBSITE-003/**`. **Cấm:** `backend/`, `contracts/`, `config/`, code sinh ra của
  `api-sdk` và `builder-components`.
- **Isomorphic (#23):** mọi thứ mới trong renderer/components (`createResolveUrl`, `matchSiteUrl`, `SiteLayout`) không
  dùng `window`/`document`; không `Date.now()`/`new Date()` trong render (năm © truyền qua props — tránh lệch SSR↔CSR).
- `builder-renderer` không import `builder-core`; `packages/` không import `apps/` (T3 thêm lint).
- **Invariant renderer #1:** không hardcode `href` — mọi link qua `ctx.resolveUrl`. Logo dùng `logoUrl` của `ShopDto`
  nguyên văn (đã có `/media/`, không qua `mediaUrl`).
- Enum TS dạng const object (#19). Không copy server state Query vào Zustand (#20) — preview đọc Query trực tiếp,
  "đang xem trang nào" là state cục bộ của lớp phủ.
- Preview nằm trong `OwnerOnlyGate` sẵn có của editor (#100) — không route mới, non-Owner không gọi API.

## 7. Review focus — lỗi dễ lọt khi spec im lặng
| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | Trang chủ ở locale mặc định vs locale khác | `basePath + "/"` vs `basePath + "/en/"`; không ra `//` hay thiếu `/` khi `basePath` có/không có `/` cuối | T1 |
| R2 | `systemPage` chưa có trang / trang tắt / trang đã xoá | URL theo #103; preview: NotFound + dải báo đúng lý do | T1, T4 |
| R3 | SSR ra HTML khác lần render client đầu (năm ©, id) | Hydrate không cảnh báo lệch | T3 |
| R4 | Bấm link trong preview điều hướng thật / rời editor | Bị chặn; chỉ đổi trang trong lớp phủ; external mở tab mới `rel="noopener"` | T4 |
| R5 | Preview hiện bản server cũ thay vì cái đang thấy | Trang đang sửa dùng `tree`/`themeDraft` của store, kể cả chưa autosave | T4 |
| R6 | `href` tra ngược có query/hash, slug lạ, locale lạ | `matchSiteUrl` bỏ query/hash; không khớp → `deleted`/missing, không ném lỗi | T1 |

## 8. Sơ đồ file
```text
packages/builder-components/src/site-url.ts (+ .test.ts)      createResolveUrl, matchSiteUrl, kiểu SitePageRef (#103)
packages/builder-components/src/context.tsx                   + locale, defaultLocale
packages/builder-renderer/src/renderTree.tsx                  + props basePath, locale, defaultLocale, pages
packages/builder-renderer/src/site-layout.tsx (+ test)        SiteLayout (Header/Footer)
packages/builder-renderer/tests/ssr-hydrate.node.test.tsx     snapshot SSR↔CSR (T3)
eslint.config.mjs                                             + chặn packages/** import apps/**
apps/portal/src/components/website/site-preview.tsx (+ test)  lớp phủ preview
apps/portal/src/components/website/editor-shell.tsx           + nút "Xem trước"
apps/portal/src/hooks/use-preview-page.ts                     nguồn cây (store vs draft) + mediaMap
```

## 9. Task

### T1: `createResolveUrl` + `matchSiteUrl` (#103) và API `RenderTree`
- `site-url.ts`: `SitePageRef = { id, slug: string|null, systemType: string|null, isEnabled }`;
  `createResolveUrl({ basePath, locale, defaultLocale, pages })` → `(link) => string` theo #103 (external/anchor như cũ);
  `matchSiteUrl(href, sameOptions)` → `{ kind:'page', pageId, enabled }` | `{ kind:'systemMissing', systemType }` |
  `{ kind:'deleted' }` | `{ kind:'external' }` | `{ kind:'anchor', nodeId }`. Route System đọc `config/system-pages.json`
  (import build-time, như `sanitize-profiles.json`).
- `context.tsx`: thêm `locale`, `defaultLocale` vào `RenderContextValue`; không đổi hành vi khi không truyền.
- `renderTree.tsx`: props tuỳ chọn `basePath`, `locale`, `defaultLocale`, `pages` → bọc `RenderContextProvider` với
  `resolveUrl` từ `createResolveUrl` **chỉ khi** có `pages`; giữ `decorateNode`. Canvas hiện tại không vỡ.
- [ ] Test trước (bảng): home vi/en, slug thường, `basePath` `''`/`/x`/`/x/`, systemPage có trang / tắt / chưa có trang,
  page đã xoá → `#`, external/anchor giữ nguyên; `matchSiteUrl` là nghịch đảo của `resolveUrl` với mọi trang + query/hash + locale lạ
- [ ] `renderTree.test.tsx`: Hero CTA `page` ra đúng href khi truyền `pages`; không truyền → `#` như cũ
- [ ] FAIL → PASS · test/typecheck/lint `builder-components` + `builder-renderer` ·
  Commit `feat(builder-renderer): resolveUrl theo quy tắc URL Shop Site + basePath/locale (WEBSITE-003 T1, #103)`

### T2: `SiteLayout` (Header/Footer ngoài Component Tree)
- `site-layout.tsx`: `SiteLayout({ shop:{ name, logoUrl }, homeHref, menuItems?: { label, href }[], contact?: { address?, phone? },
  year, children })`. Header logo + tên (link `homeHref`), menu rỗng → không render `<nav>`; Footer tên + `© {year}` +
  vùng liên hệ chỉ khi có dữ liệu. Style qua `var(--vs-*)` như component Registry. Export từ `builder-renderer/src/index.ts`.
- [ ] Test trước: không logo → chỉ tên; có logo → `<img src={logoUrl}>` nguyên văn; `menuItems` rỗng → không `<nav>`, có → href đúng;
  `contact` trống → không có vùng liên hệ; năm lấy từ props
- [ ] FAIL → PASS · Commit `feat(builder-renderer): SiteLayout Header/Footer ngoài Component Tree (WEBSITE-003 T2)`

### T3: Snapshot SSR↔CSR + lint `apps/**`
- `ssr-hydrate.node.test.tsx`: `renderToString(<SiteLayout><RenderTree …/></SiteLayout>)` ở Node rồi `hydrateRoot` cùng cây
  trong jsdom → không có `console.error` lệch hydrate; HTML server khớp snapshot. Phủ mọi fixture `valid-*.json` ở
  `packages/builder-components/tests/fixtures/tree/`.
- `eslint.config.mjs`: `packages/**` cấm import `apps/*`, `@vsite/portal`, `@vsite/web`, đường dẫn tương đối tới `apps/`.
- [ ] Test trước: đột biến tạm `new Date()` trong render → test hydrate FAIL; file tạm import `apps/` → lint FAIL
- [ ] FAIL → PASS · Commit `test(builder-renderer): snapshot SSR↔CSR + lint chặn packages import apps (WEBSITE-003 T3, #23)`

### T4: Preview trong editor Portal
- `editor-shell.tsx`: nút "Xem trước" ở toolbar → mở `SitePreview` (lớp phủ `role="dialog"` toàn màn hình; Escape / nút
  "Đóng xem trước"; phím tắt editor tắt khi đang mở).
- `site-preview.tsx`: `useGetWebsite`, `useListPages`, `useGetShop`; state cục bộ `currentPageId` + lịch sử ("← Trang trước");
  render `themeStyle(themeDraft)` → `SiteLayout` → `RenderTree(basePath = PREVIEW_BASE, locale = defaultLocale, pages)`;
  bắt click `<a>` ở capture phase → `matchSiteUrl` → đổi trang / NotFound + dải báo / external mở tab mới.
  Loading · error (Thử lại) · empty đủ cả ba.
- `use-preview-page.ts`: trang đang sửa → `store.tree`; trang khác → `useGetPageDraft` (Zod như editor); `useMediaMap`.
- [ ] Test trước (MSW): mở/đóng (nút + Escape); hiện cái đang thấy kể cả thay đổi chưa autosave; Header tên + logo;
  CTA tới trang khác → nạp draft trang đó, "← Trang trước" quay lại; trang tắt → NotFound + dải "đang tắt";
  systemPage chưa có trang → NotFound + dải; trang đã xoá → dải; external không điều hướng trang hiện tại;
  lỗi `listPages` → báo + Thử lại; không có request PUT nào phát sinh từ preview
- [ ] FAIL → PASS · Commit `feat(portal): xem trước toàn trang trong editor (WEBSITE-003 T4)`

### T5: Đóng task
- [ ] `pnpm build && pnpm test` toàn repo; `pnpm -r typecheck`, `pnpm -r lint`; `gen:registry` không diff
- [ ] Nghiệm thu tay trên trình duyệt với BE thật: mở preview, bấm link qua lại, trang tắt, theme đang chỉnh hiện đúng
- [ ] `changelog.md` (lệch thiết kế; #11 "vsite.vn/{slug}" → Site thật là `{slug}.vsite.vn`) · `DECISIONS.md` #11/#23/#103
  cập nhật trạng thái · `00-INDEX` §3 (Bước 6), §4 (trả nợ SSR↔CSR BOOTSTRAP-002, lint TOOLING-001) · `step.md` ·
  banner `05`/`07` nếu chạm · `builder-renderer/CLAUDE.md` (API mới, #103)
- [ ] `change-reviewer` → `review.md` · `pnpm check:docs` PASS

## 10. Không làm
Render công khai ở `apps/web` (8b) · menu thật / `NavigationConfig` (Bước 7) · overlay `i18n`, nút đổi ngôn ngữ trong
preview · địa chỉ/điện thoại shop (task Shop/Marketplace, #93) · Header/Footer chỉnh được trong builder · preview ở
tab/URL riêng chia sẻ được.

## 11. Giả định tôi đã tự đặt (người đã duyệt 2026-11-11)
| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|
| G1 | `SiteLayout` đặt ở `builder-renderer`, không ở `builder-components` | `builder-components` là nơi của component Registry; layout không có manifest | Chuyển file, API không đổi |
| G2 | `createResolveUrl`/`matchSiteUrl` đặt ở `builder-components` (cạnh context) | Context sống ở đây (tránh vòng phụ thuộc); renderer re-export | Chuyển sang renderer |
| G3 | Link tới trang đã xoá → `href="#"` | Không còn slug để dựng URL | 8b có thể muốn lọc link mồ côi lúc publish |
| G4 | Preview luôn ở `defaultLocale`, không có nút đổi ngôn ngữ | Chưa có bản dịch để xem | Thêm nút ở bước L |
| G5 | `PREVIEW_BASE` là chuỗi ảo (vd. `/__preview`), không phải route thật của Portal | Lớp phủ không đổi URL trình duyệt | Đổi khi muốn preview ở tab riêng |
| G6 | Năm © truyền qua props (caller tính) | Tránh lệch SSR↔CSR (R3) | — |
