# builder-components — vsite

## Invariant — vi phạm là bug, không phải lựa chọn phong cách

1. KHÔNG sửa file trong `generated/`. Sửa `registry/*.manifest.ts` rồi chạy `pnpm gen:registry`. (#17)
2. KHÔNG thêm/xoá/đổi kiểu prop đã phát hành trong một manifest đang tồn tại. Đổi phá vỡ →
   variant hoặc type mới. `check-additive` (chạy trong `gen:registry` và pre-commit hook) sẽ fail
   build khi vi phạm 1 trong 7 rule ở `DesignIdeal/07-component-manifest-schema.md` §5. (#43, #62)
3. KHÔNG thêm `PropKind` thứ 13 vào `meta/prop-kinds.ts` khi viết component thường. 12 kind (text,
   richText, number, boolean, select, color, image, icon, link, list, group, binding) là tập đóng —
   thêm kind mới là quyết định tầng framework, cần review riêng, không tự quyết trong lúc thêm
   component. (#63)
4. `list` không được lồng `list` trực tiếp — `ListPropDef.itemProps` chỉ nhận `NonListPropDef`
   (enforce trong `meta/manifest-schema.ts`, không phải quy ước tự nhớ).
5. KHÔNG khai `binding.sources` chứa `"Review"`. Không tồn tại trong `config/binding-sources.json`
   và bị hard-fail riêng trong `gen-registry.ts` (`checkPropInvariants`) — tách biệt khỏi việc check
   whitelist, nên thêm `"Review"` vào chính whitelist đó cũng không né được. (#65, 01 §7)
6. KHÔNG khai `image.preset` ngoài `config/image-presets.json`, `richText.profile` ngoài
   `config/sanitize-profiles.json` — cả hai bị check chéo (§6.1) và fail cứng nếu không khớp.
7. Preset ảnh (`ctx.resolveImage(imageId, preset)`) và profile sanitize
   (`sanitizeRichText(html, profile)`) trong component (`src/*/​*.tsx`) là **hardcode riêng biệt**
   với giá trị khai trong manifest tương ứng — codegen KHÔNG buộc hai nơi này khớp nhau (nợ kỹ
   thuật đã biết, xem `docs/tasks/BOOTSTRAP-002/review.md`). Khi đổi preset/profile trong manifest,
   phải tự đổi cả trong component, không có gì báo lỗi nếu quên.
8. KHÔNG import `resolveImage`/`resolveUrl` từ đâu khác ngoài `useRenderContext()` — không hardcode
   nối chuỗi URL ảnh hay `href`. (#11, #53)
9. KHÔNG tạo migration/entity trong package này — Bước 2 thuần build-time, không đụng database.

## Ranh giới kiến trúc

- `packages/` không import từ `apps/`.
- `RenderContextValue`/`RenderContextProvider`/`useRenderContext` **sống ở package này**
  (`src/context.tsx`), KHÔNG ở `builder-renderer` như bản vẽ thư mục ở `07` §3 — lệch có chủ đích để
  tránh vòng phụ thuộc thật: component lá (`src/hero/Hero01.tsx`...) cần gọi `ctx.resolveImage`, còn
  `builder-renderer` lại cần import component từ package này để render qua `registry-map.ts`. Nếu
  context sống ở `builder-renderer`, package này phải import ngược lại → vòng lặp thật, không phải
  giả định. `packages/builder-renderer/src/context.tsx` chỉ re-export lại để giữ đúng bề mặt API tài
  liệu mô tả. Xem thêm `docs/tasks/BOOTSTRAP-002/review.md`.
- Zod (`generated/props-schemas.ts`) và JSON Schema (`generated/props-schemas.json`) sinh **độc lập
  trực tiếp** từ manifest trong `gen-registry.ts` — không convert từ cái này sang cái kia. (#60)
- `props-schemas` (cả hai) chỉ validate **shape** — mọi prop optional ở tầng type-schema.
  `requiresProps` là ràng buộc theo **variant**, không enforce được tĩnh ở tầng này; để dành cho
  Operations Engine thật (Bước 5). Đừng "sửa cho chặt hơn" ở đây khi thấy field bắt buộc thiếu.
