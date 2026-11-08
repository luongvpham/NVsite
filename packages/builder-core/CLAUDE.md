# builder-core — vsite

> **Cần nền thiết kế?** `05` §11 (Operations Engine — bảng phép/cấm, 5 bước kiểm), §6 (shape Component Tree).
> Quyết định `#14` (5 operation) · `#20` (state) · `#42` (undo chỉ client) · `#43` (manifest additive).
> Tra `#N` ở `DesignIdeal/DECISIONS.md`, đừng mở thẳng `0*.md`. Task tạo package: `Docs/tasks/WEBSITE-002/` (F4).

**Operations Engine phía client**: nhận một op (`add`/`remove`/`move`/`update`/`changeTheme`), kiểm theo luật của
manifest, áp lên Component Tree bằng Immer, giữ lịch sử undo bằng zundo. Thuần TypeScript — chạy ở Node, test không cần jsdom.

## Ranh giới — vi phạm là lỗi kiến trúc

- **Chỉ `apps/portal` dùng package này.** `builder-renderer`, `builder-components` và `apps/web` **không** import nó
  (ESLint `no-restricted-imports` ở `eslint.config.mjs` chặn). Renderer phải chạy ở `apps/web` mà không có Engine.
- `builder-core` phụ thuộc `builder-components` (luật `opRules`/`treeRules`, Zod props, envelope, `isSafeLinkUrl`) — **không** ngược lại.
- `packages/` không import từ `apps/`. Không dùng `window`/`document`/React trong package.

## Invariant

1. **Mọi thay đổi cây đi qua `store.dispatch(op)`** (hoặc `executeOperation` khi không cần store). Không `set({ tree })` tay,
   không mutate cây — Immer đóng băng kết quả, mutate là ném lỗi.
2. **Engine "từ chối sớm", BE là nguồn sự thật.** Mã lỗi dùng ĐÚNG tên mã BE (`packages/builder-components/tests/fixtures/tree/README.md`)
   để một bảng thông báo lỗi dùng cho cả hai nguồn. Mã chỉ-FE (không gửi/nhận BE): `INVALID_OPERATION`, `TARGET_NOT_FOUND`,
   `INDEX_OUT_OF_RANGE`, `MOVE_INTO_DESCENDANT`, `NO_DRAFT`. Engine **không** kiểm: ảnh (cần DB), richText nguy hiểm
   (BE từ chối; FE làm sạch ở control Lexical), vỏ cây/`schemaVersion` (Zod envelope lúc nạp draft).
3. **State #20.** `tree` trong store là bản làm việc, nạp **một lần** từ draft (Query) bằng `loadDraft` — rồi Query không
   ghi đè nó nữa (kể cả khi refetch). Chỉ nạp lại có chủ đích (sau `sanitized: true`, sau 409 "Tải bản mới"). Đừng đồng bộ
   Query → Zustand ở chỗ khác; thấy cần làm vậy nghĩa là thiết kế đang sai.
4. **Undo = zundo chụp trạng thái cây (#42)**, không có `invert()`, không có op-log. Lịch sử chỉ gồm `tree`
   (`partialize`); chọn node và `themeDraft` không phải bước undo. Chỉ ghi bước khi **tham chiếu cây đổi** — op không đổi gì
   (update cùng giá trị, move về chỗ cũ) phải trả lại chính cây cũ. **Gộp bước:** các `update` liên tiếp cùng `nodeId`+`path`
   cách nhau < `UPDATE_COALESCE_MS` (2 s, cửa sổ trượt, bằng nhịp debounce autosave) là MỘT bước — bước của lần gõ đầu giữ bản
   trước đợt gõ. Op khác, undo/redo, `loadDraft` cắt đợt. Vẫn là snapshot, không op-log (WEBSITE-002 F8).
5. `toIndex` của `move` tính trong danh sách con của đích **sau khi gỡ node**. Độ sâu/số node dùng cùng quy ước BE
   (con của gốc = tầng 1; tối đa 20 tầng, 2000 node, 1 MB, theme 64 KB — `tree/limits.ts`).

## Thêm một operation mới — đủ 6 chỗ

| # | Chỗ | File |
|---|---|---|
| 1 | Union type + Zod (nguồn của union) | `src/operations/types.ts` (`operationSchema`) |
| 2 | Luật theo `05` §11 (System ✅/❌, mã lỗi đúng tên BE) | `src/operations/validate.ts` |
| 3 | Hiện thực Immer (op không đổi gì → trả chính cây cũ) | `src/operations/apply.ts` |
| 4 | Hoàn tác: không có `invert()` — kiểm store/zundo phủ op mới; op đổi state **ngoài** `tree` phải quyết định có vào lịch sử không | `src/store/editor-store.ts` |
| 5 | AI tool schema: `builder-components/generated/ai-tool-schema.json` sinh bởi `pnpm gen:registry`, **không sửa tay** — thêm tool cho op mới = sửa generator ở builder-components (đổi contract Registry, cần người duyệt) | — |
| 6 | Test: bảng phép/cấm, apply (immutable + structural sharing), fixture hợp đồng, store (lịch sử/chọn) | `src/**/*.test.ts` |

## Test

Fixture cây **dùng chung với BE**: `packages/builder-components/tests/fixtures/tree/*.json` — nạp thẳng bằng
`import.meta.glob` (`src/test-support/fixtures.ts`), **không chép** sang chỗ khác. `fixtures.test.ts` dựng lại mọi cây hợp lệ bằng
chuỗi op `add` và buộc Engine từ chối đúng mã BE; danh sách fixture bị bỏ qua có chủ đích được chốt trong test (thêm fixture
mới buộc người viết xem lại).

```bash
pnpm --filter @vsite/builder-core test        # vitest, môi trường node
pnpm --filter @vsite/builder-core typecheck
pnpm --filter @vsite/builder-core lint
```
