# MEDIA-001 — Registry diff (kết thúc session S0)

> Cổng duyệt Component Registry + config dùng chung, **trước** khi mở session BE (plan §6).
> Nhánh `feature/MEDIA-001`, commit `f49e9c7` (config) + `6c22a50` (registry).
> Test: `@vsite/builder-components` 78/78 pass (1 skip, xem mục ⚠️) · `gen:registry` · `check:registry-additive` · `tsc --noEmit` sạch.

## ⚠️ Việc anh phải tự làm (hook chặn agent, đúng thiết kế #24)

Agent không ghi được `config/reserved-routes.json` vì `.claude/hooks/guard-write.mjs` chặn file này.

1. Thêm `"media"` vào `reservedPaths` của `config/reserved-routes.json`.
2. Bỏ `it.skip` ở `packages/builder-components/tests/config-files.test.ts` (test "reservedPaths chứa media", khoảng dòng 86).
3. Chạy `pnpm --filter @vsite/builder-components test`, rồi commit `feat(config): reserve /media (#24)`.

Nếu thiếu bước này thì shop vẫn đăng ký được slug `media`, và test bắt buộc 12 ở `08` §9 sẽ đỏ trong S1.

## BREAKING / REMOVED

Không có. `check-additive` pass. Component đang dùng `1600x900,cover` và `800x800,cover` (Hero, Gallery) không đổi.

## `config/image-presets.json` — đổi shape (giả định A1)

```
trước: { "<preset>": {...}, ... }                       6 preset, có "600xR,cover" (height null)
sau:   { "presets": { "<preset>": {...} }, "surfaces": { "Shop": ["320x96,inside", "96x96,cover"] } }
```

- **9 preset (#78):** `1600x900,cover` · `1600x600,cover` · `1200x630,cover` · `1200x1200,inside` · `800x800,cover` · `800x600,cover` · `320x96,inside` · `160x160,cover` · `96x96,cover`.
- **Bỏ `600xR,cover`**: không manifest nào dùng preset này.
- **Thêm `fit: "inside"`.**
- **Nơi đã sửa theo shape mới:** `scripts/gen-registry.ts` và `apps/portal/vite.config.ts` (plugin placeholder). Backend chưa đọc file này.

**Vì sao đổi shape:** nếu thêm key `surfaces` vào cùng cấp với các preset, gen-registry sẽ coi `"surfaces"` là một tên preset hợp lệ.

## `config/binding-sources.json`

`+ "Shop"`, để component bind được logo shop (#73).

## Component Registry (#86)

**Prop kind `binding` có thêm field tuỳ chọn:**

```ts
imagePresets?: Partial<Record<string, string[]>>   // key ⊆ sources, preset ⊆ image-presets.presets
```

gen-registry **fail cứng** trong hai trường hợp: key không nằm trong `sources`, hoặc preset không có trong whitelist.

**`registry.lock.json`**: chỉ có một thay đổi, `ServiceGrid.props.source` thêm:

```json
"imagePresets": { "Service": ["800x600,cover"], "ServiceGroup": ["800x600,cover"] }
```

**`check-additive`**: thêm `imagePresets` thì pass. Bớt một preset thì chỉ **warning**, cùng khuôn với `image.preset`. Lý do: bớt preset chỉ làm thiếu phái sinh, không làm vỡ render.

**Artifact mới (file thứ 8)** `packages/builder-components/generated/derivative-presets.json`:

```json
{ "Service": ["800x600,cover"], "ServiceGroup": ["800x600,cover"], "Shop": ["320x96,inside", "96x96,cover"] }
```

Nội dung = `imagePresets` gom từ mọi manifest, hợp với `surfaces`. Key đã sort, mỗi mảng đã khử trùng và sort. Backend (S1) link file này vào output của `Vsite.Api` và đọc lúc startup, để biết cần sinh những phái sinh nào khi shop upload logo.

## Giả định tôi đã tự đặt

| # | Giả định |
|---|---|
| A1 | Đổi shape `image-presets.json` sang `{ presets, surfaces }` (lý do ở trên) |
| A2 | `surfaces.Shop` gồm `320x96,inside` (Header, Shop Profile) và `96x96,cover` (avatar). **Không** đưa `1200x630,cover` vào như ví dụ ở `08` §3.6, vì crop logo thành ảnh OG gần như luôn xấu |
| A2' | ServiceGrid khai `800x600,cover` cho cả `Service` lẫn `ServiceGroup`, dựa theo preset "thẻ dịch vụ 4:3" ở `08` §7 |
| — | T0.1 và T0.2 gộp chung một commit, vì test "key của `surfaces` phải có trong `binding-sources`" cần `Shop` đã có mặt |

## Ghi chú không chặn

Test "registry/ đã được khôi phục nguyên vẹn" (`tests/gen-registry.test.ts` khoảng dòng 122) đã có từ trước task này. Nó dùng timeout mặc định 5 giây, trong khi các test cùng nhóm dùng 20 giây, và từng bị timeout một lần khi máy tải nặng. Task này không sửa nó (ngoài phạm vi).

## Câu hỏi cần anh quyết

1. Chấp nhận A1, A2 và A2'?
2. Ảnh OG của Shop Profile lấy từ đâu? Hiện tại không lấy từ logo (xem A2).

## Người duyệt đã quyết   ← ĐIỀN LÚC DUYỆT

| Câu hỏi / giả định | Quyết định | Số hiệu |
|---|---|---|
| A1 | OK | |
| A2 | OK | |
| A2' | OK | |
