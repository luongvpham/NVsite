# JOBS-001 — review toàn nhánh (trước Gate 2)

> `change-reviewer` (opus), diff `838eeff..HEAD` trên `feature/JOBS-001-fe` (BE đã merge qua PR #30 + FE S2).
> Từng task BE đã review riêng ở S1 (xem ledger); từng task FE review bằng opus + re-review.

## Kết luận
**Không có Critical** về tenant (#21) hay lệch contract. **1 Warning bảo mật (W1) cần người quyết ở Gate 2**; còn lại là tài liệu (đã sửa) và Suggestion.

## Đã kiểm và đạt
- #21/job nền: `BeginShopScope` luôn scope mới từ root, `Set` đúng một lần; `ShopJobRunner` kiểm shop trong câu query; không thêm `IgnoreQueryFilters()`; endpoint không body.
- Policy PlatformAdmin: kiểm role **và** `aud = vsite-portal`, gắn ở cấp nhóm; claim chỉ phát cho portal (login lẫn refresh); không `PlatformAdmin` trong `Vsite.Api/Tenancy/**`.
- Cookie `/hangfire` (R2/R3): đòi `AudienceKind == Portal` + cookie hợp lệ; mọi exception ⇒ 401, không 500; test phủ không cookie / hết hạn / sửa / rác / purpose khác / host shop / host api.
- Contract: `platform.v1.json` = staging, `contract.lock` có sha; FE chỉ dùng hook không tham số; 403 xử lý chung đúng giả định A3.
- FE: claim chỉ hiện/ẩn nút; token chỉ ở memory; generated sinh bằng `gen:api` (CI có bước chặn sửa tay).
- Ràng buộc plan §6 "có test chặn": đã mở từng test, đều tồn tại và bắt được vi phạm (lưu ý W7).

## Warning
| # | Nội dung | Xử lý |
|---|---|---|
| W1 | **CSRF dashboard Hangfire.** Không đăng ký `IAntiforgery` ⇒ Hangfire bỏ kiểm token cho POST (test `JobsDashboardTests` xác nhận POST không token vẫn qua). `SameSite=Strict` chỉ chặn khác *site*; `spa-abc.vsite.vn` và `admin.vsite.vn` cùng site ⇒ trang shop (nội dung shop kiểm soát) có thể form-POST trigger/xoá/requeue job trong 15' cookie còn sống. Plan G3 ("Strict chặn CSRF") không đủ. | **CHƯA SỬA** — S2 cấm sửa `backend/`. Đề xuất: `AddAntiforgery()` hoặc filter chặn không-GET khi `Sec-Fetch-Site`/`Origin` ngoài `admin.*`, kèm test POST thiếu token bị từ chối. Ghi ở changelog + 00-INDEX §4 (⛔). Người quyết: sửa trước merge hay trước ra mắt. |
| W2 | changelog còn "S2 chưa mở", thiếu commit/kiểm chứng FE | Đã sửa (phần kiểm chứng FE: portal 434, api-sdk 29+7 — xem báo cáo) |
| W3 | 00-INDEX §3/§4 còn ghi S2 chưa làm | Đã sửa |
| W4 | Nợ S2 chưa có nơi theo dõi | Đã thêm vào 00-INDEX §4 |
| W5 | #114 ghi nút Portal "chờ S2" ở 3 chỗ | `DECISIONS.md` đã sửa. **`backend/docs/modules/identity.md:135` và `platform.md:9` chưa sửa** (cấm sửa `backend/`) — người/BE session cập nhật |
| W6 | plan §8 trích đường dẫn nút sai (`components/platform/…`) | Đã sửa plan + changelog mục 21 |
| W7 | plan §6 trích `PlatformPolicyTests` cho "claim và audience" nhưng test thật là Theory `PlatformAdminClaimTests.Policy_requires_both_role_and_portal_audience` | Ghi ở changelog; plan chưa đổi |

## Suggestion (chưa làm, đã ghi changelog)
`DisplayStorageConnectionString=false` · `hangfire` vào reserved-routes hoặc ghi "chỉ admin.*" ở nợ Caddy · mở rộng test dò bypass (`IsInRole`, `"role"`, `Common/Behaviors`) · `denied` không reset khi token đổi · ngoại lệ `ApiRoutePrefixTests` nên là `"hangfire/"` · ghi rõ dashboard cũng hưởng độ trễ role ≤ 15' (#114 mục 4).

## Nghiệm thu F2
5/5 đạt do người báo 2026-11-25 (agent không chứng kiến; Safari/popup chưa báo). Chi tiết ở changelog.

## Ma sát quy trình
1. Trạng thái task rải ở 6 chỗ (changelog, 00-INDEX §3/§4, DECISIONS, identity.md, platform.md) — sau mỗi session phải sửa tay, S2 quên cả 6; không check nào bắt câu "S2 chưa làm" lỗi thời. Gom về một nguồn hoặc `check:docs` cảnh báo.
2. Plan §6 trích test theo mô tả, không theo tên method ⇒ dễ nhầm test (W7).
3. Đường dẫn trong code block của plan không được `check:docs` kiểm (W6).
4. Ruling 6 đổi R4 nhưng plan §3/§7 vẫn ghi cũ — "thay chứ không chồng" chưa áp cho plan.
5. `guard-write` chỉ chặn Edit/Write, không chặn bash; bảo vệ thật cho generated là CI — tài liệu nên nói rõ.
6. Dev server của người dùng khoá DLL ⇒ S1 phải build trên bản copy ~1.6 GB; nên có lệnh test dùng output riêng.
7. "Strict chặn CSRF" (G3) không bị thách thức qua nhiều vòng review — checklist review cookie nên có câu "same-site có gồm subdomain shop không?".
8. Ranh giới "không sửa backend/" ở S2 làm lỗi BE phát hiện ở review toàn nhánh (W1) phải quay lại vòng BE riêng; nên có sẵn lối vòng `JOBS-001-D{n}` cho phát hiện muộn kiểu này.
9. Barrel của api-sdk không xử lý contract không body (file zod rỗng) — brief bảo thêm `platformZod` mà không lường; sửa ở script (changelog mục 17).
