# builder-renderer — vsite

## Invariant — vi phạm là bug, không phải lựa chọn phong cách

1. KHÔNG hardcode `href`. Luôn `ctx.resolveUrl(link)`. (#11)
2. KHÔNG nối chuỗi URL ảnh. Luôn `ctx.resolveImage(imageId, preset)`. (#53)
3. KHÔNG dùng `window`/`document` trong logic render chính — chỉ trong effect sau hydrate. Renderer phải chạy được server-side. (#23)
4. KHÔNG sửa file trong `packages/builder-components/generated/`. Sửa manifest rồi chạy `pnpm gen:registry`. (#17)
5. KHÔNG thêm/xoá/đổi kiểu prop đã phát hành. Cần đổi phá vỡ → variant hoặc type mới. `check-additive` sẽ fail build. (#43, #62)
6. KHÔNG thêm kind mới vào `prop-kinds.ts` khi viết component thường. (#63)
7. KHÔNG khai binding source ngoài `config/binding-sources.json`. `"Review"` không tồn tại ở đó và không được thêm vào. (#65, 01 §7)
8. KHÔNG tạo migration trong Bước 2.

## Ranh giới kiến trúc

- `builder-renderer` **không** import `builder-core` — phải chạy được ở `apps/web` (chỉ render, không có Operations Engine).
- `packages/` không import từ `apps/`.
