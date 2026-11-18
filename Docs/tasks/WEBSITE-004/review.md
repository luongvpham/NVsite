# Review — WEBSITE-004

## Tự động
- `dotnet test backend/vsite.sln` (Docker thật): ComponentSchemaTests 12/12, ArchitectureTests 22/22, IntegrationTests 897 passed / 1 skipped (skip có sẵn, `ShopEndpointTests` chờ module Listing).
- `pnpm build` 4/4 · `pnpm test` 10/10 (lần đầu 3 test `gen-registry` timeout do chạy song song với dotnet — chạy lại riêng xanh).
- `dotnet ef migrations has-pending-model-changes` → "No changes". `pnpm check:docs` xanh. `check-no-drift` OK. Thử Orval trên staging: không `…Dto2`.

## change-reviewer (2026-11-18, model mạnh nhất, toàn diff `7c783a6..HEAD`)
**0 Critical** — tenant isolation sạch: `GET /api/site` chỉ lấy shop từ `ITenantContext` khi audience = Shop, không bind tham số nào, không chạm `PageDrafts`/`Pages`/`Themes`, không `IgnoreQueryFilters`;
publish/rollback có `ShopId` tường minh trong query, FK ghép chặn trỏ chéo; hai `ReferenceSource` đúng `dependency-map.json`; contract khớp code.

**Important (3) — chưa sửa, cần anh quyết (xem "Điểm cần anh quyết")**
- **I1** `Website` không có concurrency token ⇒ publish ∥ rollback có thể mất cập nhật im lặng: rollback bỏ qua draft X đang bẩn, publish hạ cờ X, rollback commit sau → con trỏ về bản cũ mà X `IsDirty = false`.
  `ROLLBACK_CONFLICT` thực tế gần như không bắn (rollback không ghi draft bẩn). (`RollbackPublicationHandler.cs:43-62`, `PublishWebsiteHandler.cs:156-173`)
- **I2** `listPublications` trả mảng trần (trái "một shape pagination", #19) và `Take(50)` có thể cắt mất bản current sau rollback về bản rất cũ (G11 chưa ai chốt).
- **I3** #110 là giả định của agent (T8), không phải người chốt; định nghĩa nằm ở `contract-diff.md`. → **đã sửa một phần:** `DECISIONS.md` nay ghi "chờ người duyệt ở Gate 2". Còn lại: dòng "đã chốt" không số ở plan §4 (vd. shop `Draft` không chặn site) chưa có `#N`.

**Minor (6)** — M1 ETag không đổi khi snapshot được upcast lúc đọc (nhét `schemaVersion` vào ETag khi có upcaster đầu tiên) · M2 thiếu theme/draft ⇒ 500 (`PublishWebsiteHandler.cs:48-49, 77`) ·
M3 Redis lỗi ⇒ `GET /api/site` 500, nên nuốt `SetAsync` · M4 tenant resolve theo nhãn đầu của Host (`evil.com` → slug `evil`): không lộ dữ liệu nhưng 8c/`ShopDomain` phải đối chiếu Host đầy đủ ·
M5 tài liệu lỗi thời → **đã sửa** (`builder-components/CLAUDE.md` `requiresProps`, comment XML `Website.cs`, đường dẫn tắt trong changelog) · M6 vài mục theo dõi BE chỉ nằm trong changelog, chưa có dòng ở `00-INDEX` §4.

## Nghiệm thu tay bằng HTTP (BE dev thật, Postgres + Redis container, header `Host` tự đặt)
Kịch bản `Host: {slug}.vsite.local`, Owner thật (đăng ký → verify → đăng nhập → tạo shop → tạo website). Tất cả khớp kỳ vọng:
- Chưa có website → `404 SITE_NOT_FOUND`; website `Draft` chưa publish → 404.
- Đặt draft A → publish #1 → `GET /api/site` 200 `status=Published`, `ETag` + `Cache-Control: no-cache`; `If-None-Match` → 304.
- Sửa draft sang B + `PUT theme` → snapshot **giống hệt** và ETag giữ nguyên; draft `isDirty = true`.
- Publish #2 → site thấy B ngay (không chờ TTL); `isDirty = false`; autosave bằng version cũ → `409` (R9 đúng như `brief.md`).
- Rollback về #1 → 200, site ra A, snapshot khớp lần đầu, danh sách `v2 v1*`, không sinh bản mới; draft `isDirty = true` (G7).
- Host `admin.…` / host lạ / `localhost` → 404; host lạ kèm `?shopId=<shop mình>&host=<slug mình>` → 404 (tham số bị bỏ qua); Host đúng + token Owner global → 200 như không token.
- Draft `Hero01` thiếu `image` lưu được (200); publish → `422 PUBLISH_BLOCKED` `params.errors[{pageId,pageTitle,path:"/children/0/props/image",code:REQUIRED_PROP_MISSING}]`, `truncated:false`; site vẫn ở bản cũ.
- Shop `Suspended` → `200 { status: "Suspended", publication: null }`, không ETag.
- **Quan sát thêm (nợ SHOP-001, phát hiện 4 của plan):** chính Owner PATCH được `status: "Suspended"` cho shop của mình (và tương tự tự gỡ). Không sửa ở task này.
- Thử sai (lỗi của kịch bản, không phải BE): id node phải khớp `^c_[A-Za-z0-9_-]{5,}$` (`NODE_ID_PATTERN`), nên 3 lần PUT đầu bị `422 TREE_ENVELOPE_INVALID`.

Không chạy: CDN/Caddy, `X-Forwarded-Host` với proxy thật, gửi `Host` từ Node (việc của 8c).

## Nợ kỹ thuật
Đã gộp vào `changelog.md` mục "Chưa làm xong / theo dõi" (T9) — không lặp lại ở đây. Đã đóng: `requiresProps` ở `builder-components/CLAUDE.md`, comment `Website.cs`, banner `05`/`08` (T9).

## Điểm cần anh quyết
1. **I1:** thêm concurrency token (`xmin`) cho `Website` + test race publish ∥ rollback — làm trước production hay ghi nhận chấp nhận? (Sửa là thay đổi code + migration model snapshot, không đổi contract.)
2. **I2:** `listPublications` giữ mảng trần có trần 50 (ghi ngoại lệ ở `website.md`) hay đổi sang `{items,total,page,pageSize}` trước khi FE 8c bám vào? Và có luôn kèm bản current khi nó nằm ngoài 50 bản mới nhất không?
3. **#110** (cặp OG/favicon báo hai mục) và hai giả định contract (`409 ROLLBACK_CONFLICT`, header `Location` trỏ tài nguyên chưa có GET) — duyệt hay đổi.
4. Cấp `#N` cho các quyết định "người chốt" không số ở plan §4 (đặc biệt "shop `Draft` không chặn site", "rollback không đụng draft").
5. Phát hiện 4 (Owner tự gỡ `Suspended`): xếp task SHOP riêng trước khi 8c đưa site lên production? (reviewer đề xuất coi là Important nếu vậy).

## Ma sát quy trình
- Dev server `Vsite.Api` chạy nền khoá DLL ⇒ `dotnet build`/`dotnet ef` lỗi MSB3027, reviewer không xác minh được `has-pending-model-changes`. Nên ghi "dừng dev server hoặc dùng output riêng" vào bước review.
- Quyết định "người chốt" và "agent chọn theo uỷ quyền" chỉ phân biệt bằng một dòng phân cách trong plan §4, dễ ghi nhầm giả định thành quyết định (I3); dòng "—" không số lọt kiểm DECISIONS.
- `check:docs` chỉ cảnh báo đường dẫn chết, 21 cảnh báo cũ che mất cảnh báo mới của task.
- `check:docs` đếm quyết định bằng regex 1–2 chữ số nên luôn in "109 quyết định".
- Banner STATUS 05/08 dài hàng nghìn ký tự trên một dòng, khó rà bằng mắt.
- Hook chặn heredoc nhắc đường dẫn contract; kịch bản nghiệm thu bash cũng dính lỗi escape nhiều lần — viết bằng Node (`http` đặt được `Host`) ổn hơn curl+bash.
- `changelog.md` ~490 dòng, mục "theo dõi" lặp ở từng T rồi gộp lại ở cuối.

---

## Burn 2026-11-13 — S0-T1
- **STATUS:** PASS
- **change-reviewer:** Không có Critical, không có Warning. Diff đúng phạm vi: thêm 1 dòng vào `generateTreeRules` (`gen-registry.ts`) xuất `requiresProps` bên cạnh `variants`, additive, `schemaVersion` giữ 1. Không chạm manifest, không chạm file generated bằng tay, `registry.lock.json` không đổi. Test TDD khớp dữ liệu manifest.
- **Suggestion (không chặn):** rà câu "để dành cho Bước 5" ở `builder-components/CLAUDE.md:58-59` cùng T9 (đã ghi vào Nợ kỹ thuật).
- **Vòng D:** không.

## Burn 2026-11-13 — S0-T2
- **STATUS:** PASS (sửa 1 vòng)
- **change-reviewer lần 1 — Critical:** Thiếu deliverable — `siteSnapshotSchema`/`SiteSnapshot` không được re-export từ `src/index.ts` (plan §8/§9 yêu cầu), và changelog claim sai là đã export + build dùng nó. Bước 8c sẽ không import được từ `@vsite/builder-components`.
- **change-reviewer lần 1 — Warning:** Comment đầu `meta/envelope-schema.ts` còn ghi "hai vỏ" trong khi đã có ba vỏ.
- **Sửa:** commit 3b9f7c7 — export schema (value) + `SiteSnapshot` (type) từ `src/index.ts`; sửa comment "hai vỏ → ba vỏ"; chỉnh changelog cho khớp sự thật. Test 535/535, build 4/4.
- **change-reviewer lần 2:** Không có Critical, không có Warning. JSON Schema `$ref` resolve đúng (`node` nâng lên `$defs` gốc); Zod↔JSON Schema đồng ý; không chạm manifest/registry.lock/file generated.
- **Lưu ý (không chặn):** cập nhật DesignIdeal `05` §7 (shape snapshot "thay shape 05 §7") + banner STATUS thuộc phạm vi đóng task (§11), không phải nợ của S0-T2.
- **Vòng D:** không.

## Burn 2026-11-13 — T1
- **STATUS:** PASS-WITH-DEBT
- **change-reviewer:** Không có Critical. Tenant #21 đúng (`SitePublication : TenantEntity`, Global Query Filter fail-closed, `Create()` nhận `shopId` qua tham số không từ body; test cross-tenant + tenant null phủ đủ). FK ghép `(website_id, shop_id)` và `(current_publication_id, id)` Restrict chặn cross-tenant + chặn xoá bản current ở tầng DB. Migration khớp entity/snapshot; FK vòng xử lý bằng hai SaveChanges. Mã lỗi domain có test. Ranh giới module #1 OK (`PublishedByUserId` là Guid thuần, không navigation sang Identity).
- **Warning 1:** comment XML doc `Website.cs` còn ghi `CurrentPublicationId (8b)` ở "chưa có" → đã thêm, cần bỏ (ghi Nợ kỹ thuật).
- **Warning 2:** banner `DesignIdeal/05` còn ghi `SitePublication` "chưa code" → phải sửa trước Gate 2 (ghi Nợ kỹ thuật).
- **Ghi chú (vô hại):** `Website.Id` mất `.ValueGeneratedOnAdd()` trong snapshot do tham gia FK ghép — id luôn sinh ở domain (`Website.Create`), không phụ thuộc store.
- **Vòng D:** không.

## Burn 2026-11-13 — T2
- **STATUS:** PASS
- **change-reviewer:** Không có Critical, không có Warning. `SnapshotBuilder.Build` thuần (không chạm DB) — tenant lấy đúng shop là việc T4. Shape khớp đủ `generated/site-snapshot.schema.json` (field/nullable/required/additionalProperties). Enum serialize string (#19). `ValidateSiteSnapshot` + provider eager fail-fast ở Program.cs. `DocumentSchemaVersions.Snapshot=1`, `Current(kind)` switch, test phủ version/newer/current. Ranh giới module #1 OK.
- **Suggestion (tuỳ chọn, cho T4):** thêm tiebreak `.ThenBy(p => p.Page.Id)` sau `SortOrder`/`Title` nếu muốn thứ tự xác định tuyệt đối khi trùng khoá — chốt cùng thứ tự query ở T4.
- **Vòng D:** không.

## Burn 2026-11-13 — T3
- **STATUS:** PASS
- **change-reviewer:** Không có Critical, không có Warning. `RequiredPropsChecker.IsMissing` khớp §4 từng kind (text whitespace, richText strip-tag, image imageId, list count>0, binding source). Đọc `requiresProps`/`kind` từ `generated/tree-rules.json` (một nguồn #17), không hardcode. Walk tree bằng stack, JSON-pointer có escape `~0/~1`, root Page không check. Ranh giới module #1 OK. Test phủ đủ nhánh + 3 seed tree (R8); SavePageDraft thiếu prop vẫn 200 (checker không chạy ở draft).
- **Ghi chú (đúng spec):** `richText` không decode entity — `<p>&nbsp;</p>` tính là còn ký tự (hợp lệ), khớp nguyên văn §4.
- **Vòng D:** không.
