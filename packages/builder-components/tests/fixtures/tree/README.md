# Fixture Tree dùng chung FE + BE (WEBSITE-001 S0-T4)

Bộ này là **một nguồn** cho hai phía (#97, #17): FE chạy bằng vitest (`tests/envelope-schema.test.ts`,
`tests/fixtures-tree.test.ts`, `src/sanitize-html.test.ts`); BE (.NET, task T4/T5b) duyệt **đúng thư mục này**
bằng một test theory (`PageTreeValidatorTests`, `SystemPageGuardTests`, `RichTextSanitizerTests`). BE **không** được
chép fixture sang chỗ khác. Thêm/sửa fixture ở đây, chạy `pnpm --filter @vsite/builder-components test` cho xanh rồi mới
sang BE.

Đường dẫn từ repo root: `packages/builder-components/tests/fixtures/tree/*.json`.

## Định dạng một fixture cây (mọi `*.json` trừ `xss-richtext.json`)

```jsonc
{
  "description": "vì sao fixture tồn tại, tham chiếu R-xx của plan",
  "pageKind": "Composable",            // hoặc "System" — loại trang đang lưu (bắt buộc, kể cả valid)
  "systemType": "Contact",              // tuỳ chọn, chỉ khi pageKind = System
  "tree": { ... },                      // cây gửi lên (PUT draft) — HOẶC cặp stored/incoming bên dưới
  "context": { "media": { ... } },      // tuỳ chọn, xem "Ảnh"
  "rulesOverride": { "types": { ... } },// tuỳ chọn, xem "rulesOverride"
  "expect": "valid",                    // hoặc "invalid"
  "errorCodes": ["..."],                // chỉ khi invalid: TẬP mã lỗi (không phân thứ tự, không trùng) phải xuất hiện đúng
  "expectedTree": { ... },              // tuỳ chọn, chỉ khi valid: cây sau khi lưu (richText đã thay bằng bản sạch)
  "expectSanitized": true               // tuỳ chọn, chỉ khi valid: response phải có sanitized:true (mặc định false)
}
```

`expect = "valid"` nghĩa là 200 và không có lỗi nào. `expect = "invalid"`: **tập mã khác nhau** của mọi lỗi (mã
cấp problem + `errors[].code`) phải **bằng** `errorCodes` (so bằng tập, không phải "chứa"). Mỗi fixture chỉ vi phạm
đúng một điều nên tập luôn có đúng mã mong đợi.

### Fixture cặp `stored` / `incoming` (trang System — T5b)

```jsonc
{ "pageKind": "System", "systemType": "Contact", "stored": { ...tree đang lưu... }, "incoming": { ...tree gửi lên... },
  "expect": "valid" | "invalid", "errorCodes": ["SYSTEM_PAGE_..."] }
```

Dùng cho `SystemPageGuard.Compare(stored, incoming, editableProps)` **chỉ** — **không** chạy toàn pipeline
(`incoming` có thể không qua được các luật khác, vd. variant đổi sang một variant chưa có). Luật: cùng tập id, type,
variant, thứ tự, cha-con; prop ngoài `editableInSystemPageProps` (lấy từ `generated/tree-rules.json`) phải bằng nhau
(so JSON canonical).

### Ảnh — `context.media`

Kiểm ảnh ở BE cần DB (`IMediaReferenceValidator`), nên fixture mô tả "thế giới ảnh" của shop đang lưu:

```jsonc
"context": { "media": { "media_clone_1": { "kind": "Clone", "shop": "own", "deleted": false } } }
```

- `kind`: `Clone` | `Direct` | `Library`. `shop`: `own` (mặc định) | `other`. `deleted`: mặc định `false`.
- Một `imageId` trong cây hợp lệ **khi và chỉ khi** có trong `context.media`, `kind` ∈ {Clone, Direct}, `shop = own`,
  chưa xoá. Thiếu entry = không hợp lệ. Mọi trường hợp sai cho cùng mã `MEDIA_INVALID_IMAGE_REFERENCE` (không lộ id
  có tồn tại hay không, R1).
- Test BE dựng các `MediaAsset` tương ứng (hoặc stub `IMediaReferenceValidator` theo bảng này).

### `rulesOverride`

`invalid-binding-review-in-list.json` cần một type có `list` chứa `binding`, mà registry hiện tại không có. Fixture khai
thêm type giả `Carousel` bằng `rulesOverride.types.<Type>` có **cùng shape** một mục của `generated/tree-rules.json`.
Test gộp nó vào `tree-rules.json` thật; với type override **không** áp `props-schemas.json` (chỉ bản đồ prop).

## Mã lỗi

Mã cấp problem (HTTP 422, `error_code`): `TREE_INVALID` (mọi lỗi cây — chi tiết ở `errors[]`),
`MEDIA_INVALID_IMAGE_REFERENCE`, `SCHEMA_VERSION_UNSUPPORTED`. Fixture gom **mã chi tiết** như bảng dưới; với mã nằm
trong `errors[].code` thì problem là `TREE_INVALID`.

> Tên các mã **chi tiết** là đề xuất của S0 (plan chỉ cố định `RICHTEXT_UNSAFE`, `MEDIA_INVALID_IMAGE_REFERENCE`,
> `SCHEMA_VERSION_UNSUPPORTED`, `TREE_INVALID`). BE hiện thực **đúng tên này**; nếu muốn đổi tên thì đổi đồng thời
> fixture + bảng này + hiện thực tham chiếu `tests/helpers/reference-tree-validator.ts`, đừng để hai phía lệch nhau.

| Mã | Khi nào |
|---|---|
| `TREE_ENVELOPE_INVALID` | Vỏ sai theo `generated/tree-envelope.schema.json` (thiếu/sai kiểu `schemaVersion`, gốc sai, node có trường lạ, id sai khuôn `c_…`, i18n locale lạ). Dừng ngay, không duyệt node |
| `SCHEMA_VERSION_UNSUPPORTED` | `schemaVersion` nguyên > hiện hành (1). Kiểm **trước** các lỗi vỏ khác chỉ khi vỏ hợp lệ |
| `NODE_TYPE_UNKNOWN` | `type` không có trong `tree-rules.json` |
| `NODE_VARIANT_UNKNOWN` | `variant` không thuộc `types.<type>.variants` |
| `NODE_ID_DUPLICATE` | Trùng id trong cây (kể cả trùng `root`) |
| `NODE_NOT_ALLOWED_IN_PAGE_KIND` | `pageKind` không ∈ `allowedInPageKinds` |
| `NODE_CHILDREN_NOT_ACCEPTED` | `acceptsChildren = false` mà node có children |
| `NODE_CHILD_TYPE_NOT_ALLOWED` | `allowedChildTypes` khác `null` và child không ∈ |
| `NODE_MAX_PER_PAGE_EXCEEDED` | Số node cùng type trên cả trang > `maxPerPage` |
| `PROP_UNKNOWN` | Prop không có trong bản đồ prop (kể cả trong `list`/`group`) |
| `PROP_INVALID` | Sai shape theo `generated/props-schemas.json` |
| `I18N_NOT_ENABLED` | Node có key `i18n` (chưa bật #98) |
| `BINDING_SOURCE_NOT_ALLOWED` | `binding.source` không ∈ `config/binding-sources.json` **hoặc** không ∈ `sources` khai ở prop (loại `Review`); walker đi mọi độ sâu, kể cả `list`/`group` |
| `RICHTEXT_UNSAFE` | Phần sanitizer cắt có thẻ/thuộc tính/scheme thuộc `config/sanitize-profiles.json#dangerous` |
| `MEDIA_INVALID_IMAGE_REFERENCE` | Xem "Ảnh" |
| `SYSTEM_PAGE_STRUCTURE_CHANGED` | Trang System: đổi id/type/variant/thứ tự/cha-con (thêm, xoá, đảo node) |
| `SYSTEM_PAGE_PROP_NOT_EDITABLE` | Trang System: đổi prop không nằm trong `editableInSystemPageProps` |

Thứ tự kiểm (Flow 2 của plan): phiên bản → vỏ → node → prop/binding/richText → ảnh. Vì mỗi fixture chỉ vi phạm một
điều, thứ tự chỉ ảnh hưởng nơi dừng sớm: lỗi vỏ và `SCHEMA_VERSION_UNSUPPORTED` dừng trước khi duyệt node.

## `xss-richtext.json` (R4, R5)

```jsonc
{ "profile": "basic",
  "vectors": [ { "id": "...", "input": "<html>", "expect": "reject" | "clean" | "pass",
                 "clean": "<exact>",              // tuỳ chọn
                 "mustNotContain": ["<script"],   // tuỳ chọn, so không phân biệt hoa/thường trên OUTPUT sạch
                 "parserDependent": true,         // tuỳ chọn
                 "note": "..." } ] }
```

`sanitize(input, profile)` rồi:

- `reject` — phần bị cắt có thứ **nguy hiểm** → BE trả `RICHTEXT_UNSAFE`. Bản sạch (FE) vẫn không còn thứ nguy hiểm.
- `clean` — có thứ bị cắt nhưng **không** nguy hiểm → lưu bản sạch, **không lỗi** (response `sanitized: true`). Nếu có
  `clean` thì output phải **bằng** chuỗi đó; luôn khác `input`.
- `pass` — output **bằng** `input` (kể cả chuỗi đã encode như `&lt;script&gt;` — là văn bản, không phải script).
- Mọi vector: `sanitize(sanitize(x)) == sanitize(x)` (idempotent); output không chứa mục `mustNotContain`.
- `parserDependent` — kết quả phụ thuộc cách parser tách thẻ (`<scr<script>ipt>`): chỉ kiểm `mustNotContain`, không có
  `clean` chính xác. Phân loại `reject`/`clean` của BE phải dựa trên **sự kiện của sanitizer** (thứ bị cắt), không so
  chuỗi trước/sau (R5). FE kiểm nhãn bằng bộ phân loại trong test (`tests/helpers/richtext-danger.ts`) đọc cùng danh
  sách `dangerous` — bộ đó chỉ để kiểm nhãn fixture, **không** phải hiện thực cho BE.

Danh sách nguy hiểm (nguồn duy nhất, BE đọc cùng file): `config/sanitize-profiles.json#dangerous` =
`{ "tags": ["script","iframe","object","embed","style"], "attrPrefixes": ["on"], "schemes": ["javascript","data","vbscript"] }`.
Khoá `dangerous` **không phải profile** (đừng liệt kê nó như profile sanitize).

## Danh mục

| File | expect | Mã |
|---|---|---|
| `valid-home`, `valid-empty-home`, `valid-system-contact` | valid | — |
| `valid-richtext-reformatted` | valid + `expectedTree` + `expectSanitized` | — (R5) |
| `valid-system-edit-content` (stored/incoming) | valid | — |
| `invalid-library-image-id`, `invalid-other-shop-image-id`, `invalid-deleted-image-id` | invalid | `MEDIA_INVALID_IMAGE_REFERENCE` |
| `invalid-binding-review`, `-nested`, `-in-list`, `invalid-binding-source-not-declared` | invalid | `BINDING_SOURCE_NOT_ALLOWED` |
| `invalid-unknown-prop`, `invalid-prop-wrong-type` | invalid | `PROP_UNKNOWN`, `PROP_INVALID` |
| `invalid-i18n-key` | invalid | `I18N_NOT_ENABLED` |
| `invalid-unknown-type`, `invalid-unknown-variant`, `invalid-duplicate-id` | invalid | `NODE_TYPE_UNKNOWN`, `NODE_VARIANT_UNKNOWN`, `NODE_ID_DUPLICATE` |
| `invalid-hero-in-system-page`, `invalid-hero-max-per-page`, `invalid-children-not-accepted` | invalid | `NODE_NOT_ALLOWED_IN_PAGE_KIND`, `NODE_MAX_PER_PAGE_EXCEEDED`, `NODE_CHILDREN_NOT_ACCEPTED` |
| `invalid-richtext-unsafe` | invalid | `RICHTEXT_UNSAFE` |
| `invalid-envelope-*` | invalid | `TREE_ENVELOPE_INVALID` |
| `invalid-schema-version-999` | invalid | `SCHEMA_VERSION_UNSUPPORTED` |
| `invalid-system-structure-*` (stored/incoming) | invalid | `SYSTEM_PAGE_STRUCTURE_CHANGED` |
| `invalid-system-prop-not-editable` (stored/incoming) | invalid | `SYSTEM_PAGE_PROP_NOT_EDITABLE` |
| `xss-richtext` | theo từng vector | `RICHTEXT_UNSAFE` (reject) |

Không có fixture cho giới hạn kích thước (R8: 1 MB / sâu 20 / 2000 node) — quá lớn để commit, và là giới hạn riêng
của BE (G6, `appsettings`); BE sinh cây trong test.
