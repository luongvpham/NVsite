# Review — WEBSITE-004

## Tự động
_(gate kiểm tự động: build, test, lint, check:docs — điền khi nghiệm thu)_

## change-reviewer
_(tổng hợp Critical/Warning còn mở sau burn — anh rà lại ở Gate 2)_

## Nợ kỹ thuật
- [S0-T1] `packages/builder-components/CLAUDE.md:58-59` vẫn ghi `requiresProps` "để dành cho Bước 5"; thực tế tiêu thụ ở Bước 8b. Reviewer đề xuất rà cùng T9 (tài liệu `backend/docs/modules/website.md`). Không chặn.

## Điểm cần anh quyết
_(để trống — anh điền ở Gate 2)_

## Ma sát quy trình
_(để trống — chép từ "Ma sát — nháp từ burn" của batch file những dòng đáng giữ)_

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
