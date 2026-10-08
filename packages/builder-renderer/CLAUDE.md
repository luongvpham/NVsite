# builder-renderer — vsite

> **Cần nền thiết kế?** `05` §6 (shape Component Tree), §11 (Operations Engine) · `07` (manifest,
> ⚠️ đã code — đọc `Docs/tasks/BOOTSTRAP-002/changelog.md` kèm).
> Quyết định `#N` → `DesignIdeal/DECISIONS.md`. Trạng thái tài liệu: `DesignIdeal/00-INDEX.md` §2.

## Invariant — vi phạm là bug, không phải lựa chọn phong cách

1. KHÔNG hardcode `href`. Luôn `ctx.resolveUrl(link)`. (#11)
2. KHÔNG nối chuỗi URL ảnh. Luôn `ctx.resolveImage(imageId, preset)`. (#53)
3. KHÔNG dùng `window`/`document` trong logic render chính — chỉ trong effect sau hydrate. Renderer phải chạy được server-side. (#23)

Sửa manifest, prop kind, binding source (kể cả cấm `"Review"`) → luật nằm ở
`packages/builder-components/CLAUDE.md`.

## Ranh giới kiến trúc

- `builder-renderer` **không** import `builder-core` — phải chạy được ở `apps/web` (chỉ render, không có Operations Engine).
- `packages/` không import từ `apps/`.
