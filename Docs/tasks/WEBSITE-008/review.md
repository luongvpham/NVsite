# WEBSITE-008 — Review toàn nhánh + Gate 2

> `change-reviewer` (model mạnh nhất) review toàn nhánh `ad99e66..e879d06`, chỉ đọc; re-review sau đợt sửa: Clean.
> **Gate 2: người duyệt 2026-11-26.** Chưa merge — người tự merge.

## Tự động
- [x] BE test: IntegrationTests suite đầy đủ 1122 pass / 1 skip / 0 fail, chạy 3 lần liên tiếp (Docker có sẵn, không nợ `DOCKER-TEST-DEBT.md`) · ArchitectureTests 22/22
- [x] FE test: không áp dụng (Lane B chỉ BE, không chạm FE)
- [x] Integration (MSW off): không áp dụng — job nền, có test end-to-end qua DI thật (`WebsiteJobsEndToEndTests`)
- [x] contract-sync: không chạm `contracts/` (không Gate 1)
- [x] `pnpm check:docs`: PASS
- [x] check-additive: không chạm registry

## change-reviewer — Critical / Warning
- Critical: không có.
- I1 (nguồn ảnh đang dùng phải đăng ký `IMediaUsageSource` khi thêm tham chiếu media mới) — đã sửa: quy tắc ở `media.md` §5 + `step.md` §9 (`fb990bb`, `9796607`).
- W1–W5, M1, M2, M4 — đã sửa trong `fb990bb` (gồm `IAppDbContext.Database` → `BeginTransactionAsync`). M3 (trang `IsDeleted` thiếu `DeletedAt` không bao giờ purge) — không sửa, hệ thống luôn đóng dấu `DeletedAt`.

## Nợ kỹ thuật cố ý + task theo dõi
- Khe race còn lại trong lúc vòng xoá ảnh đang chạy (đã thu hẹp bằng thu lại tập "đang dùng" trước pha xoá) — `changelog.md`.
- Walker chế độ chặt: draft không còn hợp lệ làm job ảnh của shop đó dừng + Hangfire retry, không mất dữ liệu.
- `OrphanGrace = 0` cho phép nhưng nguy hiểm — cảnh báo ở `backend/CLAUDE.md` §Job nền.
- MEDIA-001 #10 vẫn mở.

## Điểm cần anh quyết trước khi merge
- [x] Plan ghi FK OG/favicon `Restrict`, code là `SET NULL` → người chọn giữ hành vi, sửa tài liệu.
- [x] Race xoá ảnh → người chọn "đóng nếu rẻ" (`7054fa9`).
- [x] #117/#118 → ✅ (Gate 2 duyệt).

## Ma sát quy trình
| Ma sát | Nguyên cớ gốc | Mức | Bằng chứng | Đề xuất (bớt trước, thêm sau) |
|---|---|---|---|---|
| Subagent T3 chạy `git stash pop` | Prompt implementer không cấm thao tác stash | B | stash `00a498f` bị drop (nội dung trùng HEAD, không mất) | Thêm "không `git stash`" vào template implementer |
| Plan nêu FK OG là `Restrict`, code là `SET NULL` | Plan khảo sát FK theo trí nhớ, không trỏ `file:dòng` | B | `AddWebsiteModule.cs:148-159`; phải dừng hỏi giữa T7 | `plan-task`: ràng buộc DB mà plan dựa vào phải kèm `file:dòng` migration |
| Test chập chờn Hangfire (host lỗi khởi động làm hỏng `LogProvider` static) | State static của thư viện sống qua nhiều host test | B | `5b070df`, `e879d06` | Không đề xuất thêm — đã sửa gốc + test hồi quy |
| T4, T6 viết code trước test | Implementer bỏ thứ tự TDD khi brief dài | C | report T4/T6; T6 bù bằng mutation check | Không có |
| Script `task-brief` không nhận heading `T1:` | Plan dùng `### T1:` thay vì `### Task 1:` | C | dựng brief bằng awk | Không có |
| `review.md` chỉ viết sau khi người duyệt Gate 2 | Controller báo Gate 2 bằng tin nhắn, quên file §9 | C | file này | Thêm `review.md` vào checklist T9 của plan mẫu |
