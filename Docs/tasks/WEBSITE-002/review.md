# WEBSITE-002 — review (sau nghiệm thu tay + Gate 2)

Reviewer: `change-reviewer` (Opus, độc lập, chỉ đọc), 2026-11-11, trên `git diff master...feature/WEBSITE-002`
(15 commit, 143 file). Reviewer đọc code và chạy hai lệnh node nhỏ; không chạy test suite.

## Verdict của reviewer
Bảo mật, tenant và ranh giới package: **không có gì chặn merge**. Có 1 major cần xác minh với BE thật
(M1) và 1 lỗi tài liệu Critical theo luật repo (banner `07`). Cả hai đã xử lý trước khi đóng task.

### Đã kiểm và đạt
- Không file nào trong `backend/`, `contracts/`, `config/`, `packages/api-sdk/src/generated/**`,
  `packages/builder-components/generated|registry/**` bị đổi.
- `builder-renderer` không import `builder-core` (ESLint chặn), không dùng `window`/`document`; `packages/` không import `apps/`;
  Lexical chỉ vào qua `import()` động (có `tests/lexical-isolation.test.ts`).
- Enum TS dạng const object (#19); store nạp draft một lần, autosave GET thẳng, không mirror Query → Zustand (#20).
- `shopId` chỉ từ route param, không body nào mang `shopId`; `OwnerOnlyGate` bọc cả trang website và editor (#100).
- XSS: `RichText01` sanitize khi render; `$importHtml` sanitize trước khi vào Lexical, parse bằng `DOMParser` (document trơ);
  xuất luôn qua `normalizeEditorHtml`; URL link kiểm `isSafeLinkUrl` hai lớp; `themeStyle` chỉ nhận giá trị whitelist, chặn `__proto__`.
- Engine: `setIn` chặn `__proto__`/`constructor`/`prototype`; zundo chỉ chụp `tree`.

## Finding → xử lý

| # | Mức | Finding | Xử lý |
|---|---|---|---|
| M1 | Major | FE xuất `<br />` và U+00A0, BE (Ganss/AngleSharp) xuất `<br>` và `&nbsp;`; BE so chuỗi Ordinal → mọi RichText có xuống dòng làm PUT trả `sanitized: true` → editor nạp lại, mất khối đang chọn + lịch sử undo sau mỗi đợt gõ | **Đã xác nhận** bằng chính `RichTextSanitizer` của BE (dotnet fsi trên DLL build + `config/sanitize-profiles.json`): đúng hai chỗ lệch đó. **Đã sửa**: `normalizeEditorHtml` viết theo dạng BE; fixture `tests/fixtures/richtext/be-canonical.json` (10 mẫu, FE điểm bất động, BE giữ nguyên 10/10 khi đối chiếu tay); `loadDraft(..., { keepSelection: true })` khi nạp lại do `sanitized`. Đột biến kiểm cả hai |
| m1 | Minor | `isSafeLinkUrl` ném `RangeError` với entity số > U+10FFFF (`String.fromCodePoint`) | Theo dõi — `00-INDEX` §4 |
| m2 | Minor | Op `update` từ nguồn không tin cậy: path `['image','__proto__']` làm `setIn` throw; index list 1e9 tạo mảng thưa → treo | Theo dõi — làm trước khi AI dùng `dispatch` |
| m3 | Minor | `useMediaMap` gửi mọi id một lần, BE giới hạn 200 (contract không khai `maxItems`) → >200 ảnh: 422, canvas không ảnh | Theo dõi — chia lô; `maxItems` đi qua quy trình contract, không tự sửa |
| m4 | Minor | Phím tắt editor (Ctrl+Z, Delete) vẫn chạy khi ConflictDialog mở | Theo dõi |
| m5 | Minor | Op RichText bị Engine từ chối (vượt `maxLength`) → editor hiện chữ, cây không có; quay lại khối thì mất | Theo dõi |
| m6 | Minor | Hai lớp undo (HistoryPlugin của Lexical + zundo) có thể gây rối | Theo dõi (nợ UX) |
| nit | — | `tokens: sent as never` ở `use-theme-draft.ts`; `allowProtocolRelative` mặc định của sanitize-html (có từ trước, `//host` lọt khi dán); `PageKind` tự định nghĩa ở `builder-core` thay vì dùng bản Orval | Theo dõi, không gấp |
| Doc | Critical | Banner `07` vẫn trỏ `/dev-registry` (đã xoá ở F6), thiếu WEBSITE-002 và thay đổi Inspector | **Đã sửa** banner + đoạn lệch #2 |
| Doc | Warning | Banner `05`/`00-INDEX` dòng 05 còn "chờ nghiệm thu + Gate 2"; header + "Chưa làm xong" của changelog; `DECISIONS` #42 còn "chờ Gate 1"; dòng TOOLING ghi sai F2/F9 (đúng là F5) và chưa gạch các mục đã trả; `MEDIA-001/acceptance.md` Phần 5 trỏ `dev-registry`; nợ WEBSITE-002 chưa có dòng ở §4 | **Đã sửa** tất cả; nợ thêm vào `00-INDEX` §4 |

Lỗ hổng phủ test reviewer nêu: không có test FE↔BE cùng dạng chuẩn HTML (đã thêm fixture phía FE; BE chưa đọc fixture —
theo dõi); `editor-dnd.test.tsx` gọi thẳng `onDragEnd` (kéo thật do nghiệm thu tay phủ — chấp nhận).

## Ma sát quy trình
- **Banner chỉ nhận một đường dẫn changelog** → chuỗi "Changelog trước X" nhồi vào Stale, banner `05` dài vài trăm ký tự, câu cũ ("chờ nghiệm thu") khó thấy.
- **Không có bước bắt buộc round-trip output FE thật qua BE** cho giá trị BE có chuẩn hoá. Smoke F8 gửi HTML viết tay, không phải output của `normalizeEditorHtml` → M1 lọt tới review. Đề xuất: mục "round-trip output FE thật qua BE" trong workflow, kèm fixture dùng chung kiểu `be-canonical.json`.
- **Xoá công cụ dev (`/dev-registry`) không có checklist dò người phụ thuộc** → gãy banner `07` và acceptance MEDIA-001. Đề xuất DoD: "grep tài liệu/acceptance trỏ tới thứ bị xoá".
- **Changelog viết dần theo F1…F11** → header và "Chưa làm xong" không được dọn khi trạng thái đổi. Đề xuất bước "đóng changelog" tách bạch khi qua Gate 2.
- **Helper test lên code production** (`isSafeLinkUrl`, F3) không kéo theo review độ bền với input xấu → m1. Đề xuất: lên production thì phải có test input hỏng.
- **jsdom không phủ kéo-thả, IME, layout khung crop** → F7–F11 dồn sang nghiệm thu tay cuối. Có Playwright sớm hơn sẽ rút ngắn vòng này.

## Cập nhật sau review
- 2026-11-11: sửa M1 + tài liệu như bảng trên. `builder-components` 491, `builder-core` 137, portal autosave 14 test xanh; lint/typecheck sạch. Task đóng.
