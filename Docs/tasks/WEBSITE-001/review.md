# WEBSITE-001 — review (trước Gate 2)

Review xuyên đợt bằng `change-reviewer` (model mạnh nhất) trên 19 commit `6f44e5a..a53efc6`; mỗi đợt S0/A/B/C/D đã có review riêng (0 Critical ở mọi đợt).
Đợt sửa cuối (`653cca4`, `19c5ecd`, `e0314cb`) xử lý toàn bộ finding bên dưới trừ các mục "chờ người".

## Verdict của reviewer: chưa sẵn sàng Gate 2 vì 4 điều kiện
| # | Điều kiện | Trạng thái |
|---|---|---|
| 1 | Gate 1 chưa qua (contract chưa promote; CI `contract-check` sẽ đỏ trên `media.v1` cho tới khi promote) | **Chờ người** — 6 câu ở `contract-diff.md` |
| 2 | Key `reservedShopSitePaths` chưa có trong `config/reserved-routes.json` | **Chờ người** (hook chặn agent) |
| 3 | Thiếu `review.md` | Xong (file này) |
| 4 | C1: banner `05` sai trạng thái + backtick lẻ cắt mất ghi chú ở `00-INDEX` §2 | Đã sửa `19c5ecd` |

## Finding → xử lý
- **I1** link `url` không kiểm khi `kind` ≠ `external` → đã sửa (`653cca4`) + fixture dùng chung.
- **I2** `references` không báo `Page.OgImageId` → ghi nợ (changelog + `00-INDEX` §4).
- **I3** body `savePageDraft`/`updateTheme` không `required` → ghi vào `contract-diff.md` + câu Gate 1 số 6.
- **I4** `.superpowers/` không ignore → đã thêm `.gitignore`.
- **I5** nợ không có nơi theo dõi → đã chuyển vào `00-INDEX` §4.
- **M1** vector XSS → thêm 4 vector (sanitizer BE chặn đúng); `noscript` ghi nợ vì phụ thuộc parser. **M7** → sửa assertion rỗng.
- **M3** race 23505 CreatePage/CreateWebsite/UpdatePage chưa có test tất định · **M4** draft cũ không qua validate → `references` rỗng im lặng · **M6** thiếu test `has-pending-model-changes` → ghi nợ.

## Điểm cần anh quyết trước khi merge
1. Trả lời 6 câu ở `contract-diff.md` và chạy `promote.mjs WEBSITE-001 <tên> website.v1.json media.v1.json`.
2. Ai chạy `gen:api` + thêm `website` vào `packages/api-sdk/orval.config.ts` sau promote (plan §1 cấm Orval gen ở 5a, nhưng CI `fe-tests` đòi generated khớp contract): cho phép trong nhánh này, hay PR riêng.
3. Thêm `reservedShopSitePaths` (`["media","api","assets","static"]`), bỏ `Skip` ở `SlugRulesTests` + `ArchitectureTests`, đổi provider sang bắt buộc.
4. I2: chấp nhận ghi nợ hay làm `MediaReferenceKind.PageOgImage` ngay.

## Ma sát quy trình
- Việc người phải làm "trước S0-T1" (key reserved routes, bị hook chặn) bị bỏ qua suốt các đợt, chỉ còn `it.todo`/`Skip`; nên có cổng cứng trước S0.
- `check:docs` không bắt backtick lẻ trong banner — Stale bị cắt mà vẫn xanh.
- Finding I1 chỉ lộ khi đặt cạnh hai minor của hai đợt khác nhau (link kind khác không kiểm + props-schemas thiếu `additionalProperties:false`); deferred minor cần triage chéo trước đợt cuối.
- Plan Lane C cấm Orval gen nhưng CI bắt generated khớp contract — nên tính sẵn bước gen sau promote.
- Thư mục làm việc của agent (`.superpowers`, ~1.5 GB) không được ignore từ đầu.
- Hook `guard-bash` chặn nhầm lệnh Bash chỉ vì chuỗi `reserved-routes` xuất hiện trong nội dung lệnh (heredoc ghi ledger).

## Cập nhật sau review
- Gate 1 qua (người duyệt `luongpham`, 6 câu trả lời xong); contract `website.v1`/`media.v1` đã promote, `contract.lock` cập nhật; body `savePageDraft`/`updateTheme` đã khai `required` (điều kiện 1 và I3 xong).
- Key `reservedShopSitePaths` đã thêm; hai `Skip` bỏ; `ReservedRoutesProvider` fail-fast khi thiếu key (điều kiện 2 xong). Test: IntegrationTests 762 pass + 1 skip cũ của Shop, ArchitectureTests 12, ComponentSchemaTests 12, builder-components 251, 0 fail, `check:docs` xanh.
- `Docs/tasks/WEBSITE-001/brief.md` đã viết. Vẫn chờ Gate 2.
- Còn mở: `pnpm gen:api` + `website` vào `orval.config.ts` (5b/PR riêng, CI `fe-tests` có thể đỏ tới khi làm); I2 (`references` chưa báo `Page.OgImageId`) vẫn là nợ.
