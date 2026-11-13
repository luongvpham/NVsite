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
