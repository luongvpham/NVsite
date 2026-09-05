# vsite — Component Manifest Schema & Codegen

> **Tài liệu liên quan:** `02-tech-stack-and-decision.md` (Quyết định #11, #12, #14, #16, **#17**, #19, #23) · `05-website-builder-and-product-design.md` (§6 Component Tree, §9.3 Preset, §11 Operations Engine, #43, #46, #53) · `06-service-design.md` (§7)
>
> **Phạm vi:** định nghĩa **shape của một component manifest**, meta-schema validate nó, tập artifact codegen sinh ra, và cơ chế enforce additive-only bằng máy. Đây là hiện thực hoá cụ thể của Quyết định #17.
>
> **Vị trí trong lộ trình:** đây là input trực tiếp của **Bước 2** (Component Manifest + codegen + component mẫu). Bước 2 **không tạo migration nào** — toàn bộ tài liệu này là build-time.
>
> **Quy ước:** `type` PascalCase, `variant` PascalCase + số thứ tự, tên prop camelCase, tên file kebab-case.

---

## 0. Quyết định chốt trong tài liệu này

Chép sang `02-tech-stack-and-decision.md` với số hiệu tương ứng.

| # | Nội dung |
|---|---|
| **#59** | Manifest là **file `.ts` chứa object literal thuần**, không import runtime, không hàm, không điều kiện. Meta-schema viết bằng Zod (`manifestSchema`), codegen import manifest trực tiếp qua `tsx` và validate trước khi sinh gì cả. Lý do chọn `.ts` thay vì `.json`: có autocomplete + type check ngay lúc gõ, mà vẫn phân tích tĩnh được vì cấm logic |
| **#60** | BE **không** port Zod sang C#. Codegen sinh thêm `props-schemas.json` (JSON Schema draft 2020-12); .NET validate bằng thư viện JSON Schema. Một nguồn sự thật, hai runtime, không có bản dịch tay nào để lệch |
| **#61** | Props khai ở tầng **`type`**. `variant` **không** có props riêng — nó chỉ khai `usesProps` (hiện gì trong Inspector) và `requiresProps` (bắt buộc phải có giá trị). Đây là cơ chế làm cho #43 (additive-only) khả thi: thêm prop optional ở type level không phá vỡ variant cũ |
| **#62** | `registry.lock.json` **commit vào repo**. CI so manifest hiện tại với lock; đổi kiểu prop, xoá prop, xoá variant, đổi `kind` → **fail build**. #43 được enforce bằng máy, không bằng code review |
| **#63** | `kind` của prop là **tập đóng 12 giá trị**. Thêm kind mới = sửa meta-schema + generator + Inspector, là việc của người bảo trì framework. Người viết component mới **không** được phát minh kind |
| **#64** | Mọi prop `kind: "image"` **bắt buộc** khai `preset`, và preset phải nằm trong `config/image-presets.json` (whitelist §9.3 của `05`). Codegen fail nếu khai preset không tồn tại. Component không bao giờ tự nối URL — hệ quả trực tiếp của #53 |
| **#65** | Prop `kind: "binding"` khai `sources` tường minh. Codegen **hard-fail** nếu `sources` chứa `"Review"` — ranh giới cứng của `01` §7, chặn ở generator chứ không chỉ ở Operations Engine bước [5] |
| **#66** | Nhãn Inspector là **tiếng Việt thuần** ở Phase 2, không dùng i18n key. Đa ngôn ngữ cho *portal* khác với đa ngôn ngữ cho *website shop* (`05` §25 #2) — trộn hai việc lúc này là tự tạo việc |
| **#67** ⚠️ | **Sanitize profile cho `richText`** — đề xuất hai profile `inline` / `basic` ở §7.2. **Cần bạn xác nhận** trước khi code, vì nó khoá `05` §25 #3 và `06` §10 #4 |

---

## 1. Manifest phục vụ ai

Đây là lý do manifest phải chín trước mọi thứ khác trong module Website. Một file manifest là nguồn duy nhất cho **năm** người tiêu thụ, và cả năm phải khớp nhau:

| Người tiêu thụ | Lấy gì từ manifest | Sinh ra |
|---|---|---|
| `builder-renderer` | Kiểu của `props`, map `type+variant` → React component | `component-types.ts`, `registry-map.ts` |
| Operations Engine (`05` §11) | `acceptsChildren`, `allowedInPageKinds`, `editableInSystemPage` | `op-rules.ts` |
| Validator FE | Zod schema theo `type` | `props-schemas.ts` |
| Validator BE | JSON Schema tương đương (#60) | `props-schemas.json` |
| Property Inspector | `kind`, `label`, `group`, `order`, `usesProps` | `property-panel.ts` |
| AI Chat Builder (#14, #16) | enum `type`/`variant`, template summary | `ai-tool-schema.json` |

**Rủi ro thật của Bước 2 không phải "render có ra hay không"**, mà là: *một manifest duy nhất có mô tả nổi cả sáu nhu cầu đó không, hay sẽ phải nhét thêm metadata rời rạc ở từng nơi.* Nếu manifest thiếu, phát hiện ở Bước 5 nghĩa là `Page`/`PageDraft`/Operations Engine đã xây trên nền sai.

---

## 2. Ranh giới của Bước 2

| Có làm | Không làm |
|---|---|
| Meta-schema + validator manifest | Bất kỳ entity / migration nào |
| Generator + 6 artifact sinh ra | `PageDraft`, autosave, API |
| 4 manifest + 4 React component | Operations Engine thật (chỉ sinh `op-rules.ts` để bước 5 dùng) |
| `builder-renderer` nhận tree JSON → React | Publish, `SitePublication`, cache |
| `resolveImage()` / `resolveUrl()` **dạng stub** | Image proxy thật (Bước 4) |
| Dev harness đọc fixture (ném đi) | Undo/redo, zundo, Selection Context |
| Lock file + CI additive check | Binding Resolver thật (Bước 9) |

**Tiêu chí dừng:** Bước 2 xong khi `pnpm gen:registry` chạy sạch, `pnpm test` xanh, và mở được một trang render từ file fixture trong trình duyệt. Nếu Claude Code sinh ra một file migration, nó đã đi lệch.

---

## 3. Cấu trúc thư mục

```
config/
  image-presets.json          ← dùng chung FE/BE, cùng khuôn reserved-routes.json
  binding-sources.json        ← whitelist source, KHÔNG chứa "Review"
  sanitize-profiles.json      ← #67

packages/builder-components/
  meta/
    manifest-schema.ts        ← Zod meta-schema (nguồn sự thật của "manifest hợp lệ là gì")
    prop-kinds.ts             ← 12 kind, tập đóng (#63)
  registry/
    hero.manifest.ts          ← SINGLE SOURCE OF TRUTH cho component Hero
    section.manifest.ts
    rich-text.manifest.ts
    gallery.manifest.ts
  src/
    hero/Hero01.tsx  Hero02.tsx
    section/Section01.tsx
    rich-text/RichText01.tsx
    gallery/Gallery01.tsx  Gallery02.tsx
  generated/                  ← .gitignore, TRỪ registry.lock.json
    component-types.ts
    props-schemas.ts
    props-schemas.json
    registry-map.ts
    property-panel.ts
    op-rules.ts
    ai-tool-schema.json
  registry.lock.json          ← COMMIT (#62)
  scripts/
    gen-registry.ts
    check-additive.ts

packages/builder-renderer/
  src/renderTree.tsx
  src/context.tsx             ← basePath, resolveImage, resolveUrl (#11)
  CLAUDE.md                   ← invariant, xem §13
```

**Vì sao `config/*.json` nằm ngoài package:** cùng lý do như `config/reserved-routes.json` — cả FE (Vite JSON import) và BE (.NET đọc lúc startup) đều cần. Đặt trong `packages/` là ép .NET reach vào cây node_modules.

---

## 4. Meta-schema — shape của một manifest

### 4.1 Cấp component

```ts
interface ComponentManifest {
  /** PascalCase, UNIQUE toàn registry. Chính là node.type trong tree (05 §6) */
  type: string;

  /** Nhóm trong palette của builder */
  category: 'Layout' | 'Content' | 'Media' | 'Commerce' | 'Service' | 'Form' | 'Social';

  /** Nhãn tiếng Việt trong palette (#66) */
  label: string;

  /** Mô tả một dòng — dùng cho palette VÀ cho context của AI (#14 bước [2]) */
  description: string;

  /** Tên icon lucide-react */
  icon: string;

  /** 05 §6 quy tắc 5 — chỉ container mới được có children */
  acceptsChildren: boolean;

  /** Nếu acceptsChildren: giới hạn type con. null = mọi type không phải container */
  allowedChildTypes: string[] | null;

  /** 05 §11 bước [2] */
  allowedInPageKinds: Array<'Composable' | 'System'>;

  /** Tối đa bao nhiêu instance trên một trang. null = không giới hạn */
  maxPerPage: number | null;

  variants: VariantDef[];

  /** Props khai ở tầng type (#61) */
  props: Record<string, PropDef>;

  /** Template sinh summary cho Selection Context (#16). Placeholder {{propName}} */
  aiSummary: string;

  /** Version manifest lần đầu phát hành component này. Không bao giờ sửa */
  since: string;
}
```

### 4.2 Cấp variant

```ts
interface VariantDef {
  /** PascalCase + số: 'Hero01'. Chính là node.variant trong tree */
  key: string;
  label: string;

  /** Đường dẫn ảnh preview trong palette (tương đối trong package) */
  preview: string;

  /** Prop nào hiện trong Inspector khi variant này được chọn (#61) */
  usesProps: string[];

  /** Prop nào BẮT BUỘC có giá trị. Phải là tập con của usesProps */
  requiresProps: string[];

  since: string;

  /** true = không cho chọn mới, tree cũ vẫn render. KHÔNG BAO GIỜ xoá variant */
  deprecated?: boolean;
}
```

**Vì sao có `deprecated` mà không có xoá:** tree đã publish là bất biến (#41). Xoá một variant nghĩa là mọi `SitePublication` tham chiếu tới nó vỡ, và không sửa được. `deprecated` ẩn nó khỏi palette mà vẫn render được — đây là cách duy nhất tương thích với #41.

### 4.3 Prop — tập kind đóng (#63)

Mọi `PropDef` có các trường chung:

```ts
interface PropDefBase {
  kind: PropKind;
  label: string;                    // nhãn tiếng Việt trong Inspector
  help?: string;                    // tooltip
  group: string;                    // nhóm collapse trong Inspector: 'Nội dung' | 'Hiển thị' | ...
  order: number;                    // thứ tự trong group
  /** 05 §11 — prop này có sửa được trên trang System không (#46) */
  editableInSystemPage: boolean;
  since: string;
}
```

Mười hai `kind`:

| `kind` | Trường riêng | Lưu trong tree | Control Inspector |
|---|---|---|---|
| `text` | `maxLength`, `multiline`, `default` | `string` | input / textarea |
| `richText` | `profile` (#67), `maxLength` | `string` (HTML đã sanitize) | Lexical |
| `number` | `min`, `max`, `step`, `unit`, `default` | `number` | number input + slider |
| `boolean` | `default` | `boolean` | switch |
| `select` | `options: {value,label}[]`, `default` | `string` | select |
| `color` | `allowCustom`, `default` | `string` (token key hoặc hex) | token picker |
| `image` | **`preset`** (#64), `allowFocalPoint` | `{ imageId, alt? }` | media picker |
| `icon` | `set: 'lucide'` | `string` | icon picker |
| `link` | `allowKinds: ('page'\|'systemPage'\|'productCategory'\|'external'\|'anchor')[]` | xem §7.3 | link picker |
| `list` | `itemProps: Record<string,PropDef>`, `minItems`, `maxItems`, `itemLabel` | `array` | repeater kéo-thả |
| `group` | `props: Record<string,PropDef>` | `object` | fieldset lồng |
| `binding` | **`sources`** (#65), `allowFilters` | xem §7.4 | binding builder |

**Tại sao đóng chứ không mở:** mỗi kind cần đúng bốn thứ — một Zod branch, một JSON Schema branch, một React control trong Inspector, một quy tắc serialize. Mở cho người viết component tự khai kind mới nghĩa là bốn chỗ đó lệch nhau ngay lần thứ hai. Đây chính là tinh thần *"codegen > skill > CLAUDE.md > hy vọng agent nhớ"* của #17: kind mới là việc sửa framework, có PR review riêng.

`list` **không lồng `list`**. Meta-schema chặn. Repeater trong repeater là UX tệ và làm shape tree khó đoán; nếu thật cần thì đó là dấu hiệu nên tách thành container + children.

---

## 5. Variant và additive-only — cơ chế cụ thể

`05` #43 nói additive-only, và hệ quả là node **không cần** `schemaVersion`. §4.2 + §4.3 là chỗ điều đó thành hiện thực:

```
Thêm prop mới          → OK, phải optional (không có trong requiresProps của variant cũ)
Thêm variant mới       → OK
Thêm option vào select → OK
Nới maxLength / max    → OK
Đổi label / help       → OK
Thêm vào usesProps     → OK

Đổi kind của prop      → FAIL   (tree cũ chứa giá trị kiểu khác)
Xoá prop               → FAIL
Đổi tên prop           → FAIL   (= xoá + thêm)
Xoá variant            → FAIL
Xoá option khỏi select → FAIL   (tree cũ có thể đang giữ giá trị đó)
Siết maxLength / min   → FAIL
Thêm vào requiresProps → FAIL   (tree cũ hợp lệ bỗng thành không hợp lệ)
Đổi preset của image   → CẢNH BÁO (không vỡ dữ liệu, nhưng đổi layout hàng loạt site)
```

Bảy dòng `FAIL` được kiểm bằng `scripts/check-additive.ts` so với `registry.lock.json` (#62). Chạy trong CI **và** trong pre-commit hook.

**Vì sao lock file thay vì "tin vào review":** người phá vỡ additive-only sẽ không phải bạn — sẽ là một AI agent sáu tháng nữa nhận task *"đổi prop `title` thành `heading` cho nhất quán"*. Task đó nghe hợp lý, agent làm rất nhanh, và nó phá vỡ mọi site đã publish. Chỉ có build fail chặn được.

**Cần đổi phá vỡ thật thì làm gì:** tạo `type` mới (`Hero` → `HeroV2`) hoặc variant mới. Không migrate tree. Đây là cái giá đã chấp nhận khi chốt #43, và nó rẻ hơn hẳn việc viết migration cho jsonb nằm trong snapshot bất biến.

---

## 6. Codegen — sáu artifact

`pnpm gen:registry`:

```
registry/*.manifest.ts
   │
   ├─[1] validate từng manifest theo manifestSchema (Zod)
   ├─[2] kiểm invariant chéo (§6.1)
   ├─[3] check-additive vs registry.lock.json
   ▼
generated/
  component-types.ts     type ComponentType = 'Hero'|'Section'|...
                         type HeroProps = { ... }
                         type ComponentNode = discriminated union theo type
  props-schemas.ts       heroPropsSchema: ZodType<HeroProps>  (FE)
  props-schemas.json     JSON Schema 2020-12                  (BE, #60)
  registry-map.ts        Record<`${type}/${variant}`, ComponentType<any>>
  property-panel.ts      config Inspector: group, order, control, visibleWhen
  op-rules.ts            acceptsChildren / allowedInPageKinds / editableInSystemPage
  ai-tool-schema.json    enum type+variant cho Function Calling (#14 bước [3])
```

### 6.1 Invariant codegen phải hard-fail

Đây là danh sách đắt giá nhất của tài liệu, vì mỗi dòng là một lỗi mà agent chắc chắn sẽ mắc nếu không có máy chặn:

| # | Kiểm | Vì sao fail chứ không cảnh báo |
|---|---|---|
| 1 | `binding.sources` chứa `"Review"` | Ranh giới cứng `01` §7. Cảnh báo là sẽ bị bỏ qua (#65) |
| 2 | `image.preset` không có trong `config/image-presets.json` | Preset ngoài whitelist = URL 404 hoặc mở cửa đốt CPU (#53, #64) |
| 3 | `requiresProps` ⊄ `usesProps` | Prop bắt buộc mà Inspector không hiện = shop không có cách nhập |
| 4 | `usesProps` chứa tên không có trong `props` | Inspector crash lúc runtime |
| 5 | `acceptsChildren: false` mà có `allowedChildTypes` | Mâu thuẫn nội tại |
| 6 | `allowedInPageKinds` chứa `System` mà **không** prop nào `editableInSystemPage: true` | Component vô dụng trên trang System — `05` §11 chỉ cho `update` ở đó |
| 7 | `registry-map` thiếu file component cho một `variant` đã khai | Render trả `undefined`, trang trắng |
| 8 | Hai `variant.key` trùng nhau trong cùng `type` | Map bị ghi đè âm thầm |
| 9 | `list` lồng `list` | §4.3 |
| 10 | `type` trùng giữa hai manifest | Ghi đè âm thầm |
| 11 | Vi phạm additive-only vs lock | #62 |
| 12 | `richText.profile` không có trong `config/sanitize-profiles.json` | #67 |

---

## 7. Bốn kind cần đặc tả riêng

### 7.1 `image` — không bao giờ có URL trong tree

```ts
avatar: {
  kind: 'image',
  label: 'Ảnh nền',
  preset: '1600x900,cover',      // BẮT BUỘC, từ whitelist (#64)
  allowFocalPoint: true,
  group: 'Nội dung', order: 2,
  editableInSystemPage: false,
  since: '1.0.0',
}
```

Lưu trong tree: `{ "imageId": "media_8891", "alt": "..." }` — `alt` optional, rỗng thì fallback về `MediaAsset.AltText` (`05` §9).

Renderer **chỉ** được gọi `ctx.resolveImage(imageId, preset)`. Ở Bước 2 hàm này là stub:

```ts
// Bước 2: stub. Bước 4 thay thân hàm, KHÔNG đổi chữ ký.
resolveImage: (id, preset) => `/_dev/placeholder/${preset}.svg`
```

Chữ ký phải đúng ngay từ Bước 2 — đó là toàn bộ lý do stub tồn tại.

### 7.2 `richText` — hai profile đề xuất (#67 ⚠️ cần xác nhận)

```json
{
  "inline": {
    "tags": ["strong", "em", "u", "s", "br", "a"],
    "attrs": { "a": ["href", "target", "rel"] }
  },
  "basic": {
    "tags": ["p", "strong", "em", "u", "s", "br", "a",
             "ul", "ol", "li", "h2", "h3", "h4", "blockquote"],
    "attrs": { "a": ["href", "target", "rel"] }
  }
}
```

Ràng buộc chung cho cả hai profile:

- **Không** `style`, `class`, `id`, `data-*` — mở `style` là mở `background: url(javascript:...)` và các biến thể
- **Không** `img` — ảnh đi qua kind `image` để đảm bảo đi qua `resolveImage()`
- **Không** `script`, `iframe`, `object`, `embed`, `form`, `input`
- `a[href]` chỉ nhận `https:`, `http:`, `mailto:`, `tel:`, và path nội bộ; **chặn** `javascript:`, `data:`
- `a[target="_blank"]` → **ép** thêm `rel="noopener noreferrer"` lúc sanitize
- Sanitize **hai lần**: FE lúc nhập (UX), **BE lúc ghi** (bảo mật). BE là nơi quyết định — FE chỉ là tiện nghi

**Vì sao whitelist chặt tới mức này:** ở chế độ path-based (`vsite.vn/{slug}`, Quyết định #7), XSS trên site của một shop là XSS trên `vsite.vn` — nghĩa là cookie của chính vsite bị lộ, kéo theo mọi shop khác. Đây cùng một lập luận với ràng buộc `TrackingSnippets` ở `05` §2, và cùng một kết luận: không cho HTML tự do.

Profile này dùng chung cho `Product.Description` và `Service.Description` (`06` §10 #4).

### 7.3 `link` — luôn qua `resolveUrl()`

Lưu trong tree, discriminated union theo `kind`:

```json
{ "kind": "page",            "pageId": "uuid" }
{ "kind": "systemPage",      "systemType": "ProductListing" }
{ "kind": "productCategory", "categoryId": 12 }
{ "kind": "external",        "url": "https://..." }
{ "kind": "anchor",          "nodeId": "c_a3f21" }
```

**Không lưu URL đã dựng.** Renderer gọi `ctx.resolveUrl(link)`, hàm này biết `basePath` (#11) — cùng một tree render tại `vsite.vn/spa-abc` và tại `spa-abc.com` phải ra hai href khác nhau. Đây là invariant #11 và là lỗi agent vi phạm liên tục nếu không nói trước.

Ở Bước 2, `resolveUrl` stub trả `#` cho `page`/`systemPage`/`productCategory` (chưa có DB), trả thẳng `url` cho `external`, trả `#${nodeId}` cho `anchor`.

### 7.4 `binding` — whitelist tường minh (#65)

```ts
items: {
  kind: 'binding',
  label: 'Nguồn dữ liệu',
  sources: ['Service', 'ServiceGroup'],     // 'Review' → codegen FAIL
  allowFilters: ['groupId', 'sort', 'take'],
  group: 'Dữ liệu', order: 1,
  editableInSystemPage: true,
  since: '1.0.0',
}
```

`config/binding-sources.json` là tập đóng toàn hệ: `Product`, `ProductCategory`, `Service`, `ServiceGroup`, `MediaAsset`. **`Review` không có mặt** — không phải bị đánh dấu cấm, mà là **không tồn tại** trong file. Cấm bằng cách vắng mặt mạnh hơn cấm bằng cờ, vì không có gì để ai đó bật lên.

`props` **không bao giờ** chứa dữ liệu đã materialize (`05` §6 quy tắc 3) — chỉ chứa mô tả cách lấy. Binding Resolver thật làm ở Bước 9.

---

## 8. Bốn manifest mẫu

Chọn **Hero, Section, RichText, Gallery** thay vì danh sách gợi ý ở `05` §24 (Hero, Timeline, ProductGrid, Header). Lý do:

- `ProductGrid` cần `Product` — chưa tồn tại ở Bước 2
- `Header` nhận navigation đã resolve (`05` §8) và **không nằm trong Component Tree** — nó là chrome cấp site, để nó ở Bước 7 cùng `NavigationConfig`
- `Section` thay vào để test `acceptsChildren` (`05` §6 quy tắc 5) — nếu không có container, quy tắc children không được kiểm chứng lần nào
- Bốn cái này phủ **9 trong 12 kind**; ba kind còn lại (`binding`, `icon`, `group`) phủ bằng manifest thứ năm ở §8.5

### 8.1 `hero.manifest.ts`

```ts
import type { ComponentManifest } from '../meta/manifest-schema';

export default {
  type: 'Hero',
  category: 'Content',
  label: 'Banner đầu trang',
  description: 'Khối lớn đầu trang với tiêu đề, mô tả ngắn và nút hành động.',
  icon: 'Image',
  acceptsChildren: false,
  allowedChildTypes: null,
  allowedInPageKinds: ['Composable'],
  maxPerPage: 1,
  aiSummary: 'Banner: "{{title}}"',
  since: '1.0.0',

  variants: [
    { key: 'Hero01', label: 'Ảnh nền toàn khối', preview: 'hero/hero01.webp',
      usesProps: ['title', 'subtitle', 'image', 'overlayOpacity', 'align', 'cta'],
      requiresProps: ['title', 'image'], since: '1.0.0' },
    { key: 'Hero02', label: 'Chia đôi: chữ trái, ảnh phải', preview: 'hero/hero02.webp',
      usesProps: ['title', 'subtitle', 'image', 'align', 'cta'],
      requiresProps: ['title'], since: '1.0.0' },
  ],

  props: {
    title: {
      kind: 'text', label: 'Tiêu đề', maxLength: 120, multiline: false,
      group: 'Nội dung', order: 1, editableInSystemPage: false, since: '1.0.0',
    },
    subtitle: {
      kind: 'richText', label: 'Mô tả ngắn', profile: 'inline', maxLength: 300,
      group: 'Nội dung', order: 2, editableInSystemPage: false, since: '1.0.0',
    },
    image: {
      kind: 'image', label: 'Ảnh', preset: '1600x900,cover', allowFocalPoint: true,
      group: 'Nội dung', order: 3, editableInSystemPage: false, since: '1.0.0',
    },
    cta: {
      kind: 'group', label: 'Nút hành động',
      group: 'Nội dung', order: 4, editableInSystemPage: false, since: '1.0.0',
      props: {
        label: { kind: 'text', label: 'Chữ trên nút', maxLength: 40,
                 group: '_', order: 1, editableInSystemPage: false, since: '1.0.0' },
        target: { kind: 'link', label: 'Liên kết tới',
                  allowKinds: ['page', 'systemPage', 'external', 'anchor'],
                  group: '_', order: 2, editableInSystemPage: false, since: '1.0.0' },
      },
    },
    overlayOpacity: {
      kind: 'number', label: 'Độ mờ lớp phủ', min: 0, max: 80, step: 5, unit: '%',
      default: 35, group: 'Hiển thị', order: 1,
      editableInSystemPage: false, since: '1.0.0',
    },
    align: {
      kind: 'select', label: 'Căn chữ', default: 'center',
      options: [{ value: 'left', label: 'Trái' }, { value: 'center', label: 'Giữa' }],
      group: 'Hiển thị', order: 2, editableInSystemPage: false, since: '1.0.0',
    },
  },
} satisfies ComponentManifest;
```

### 8.2 `section.manifest.ts` — container

```ts
export default {
  type: 'Section',
  category: 'Layout',
  label: 'Khối chứa',
  description: 'Khối bọc các component khác, đặt được nền và khoảng cách.',
  icon: 'Square',
  acceptsChildren: true,
  allowedChildTypes: null,          // mọi component không phải container
  allowedInPageKinds: ['Composable'],
  maxPerPage: null,
  aiSummary: 'Khối chứa {{_childCount}} thành phần',
  since: '1.0.0',

  variants: [
    { key: 'Section01', label: 'Một cột, giới hạn bề rộng', preview: 'section/s01.webp',
      usesProps: ['background', 'paddingY', 'maxWidth'], requiresProps: [], since: '1.0.0' },
    { key: 'Section02', label: 'Tràn viền', preview: 'section/s02.webp',
      usesProps: ['background', 'paddingY'], requiresProps: [], since: '1.0.0' },
  ],

  props: {
    background: {
      kind: 'color', label: 'Màu nền', allowCustom: false, default: 'background',
      group: 'Hiển thị', order: 1, editableInSystemPage: false, since: '1.0.0',
    },
    paddingY: {
      kind: 'select', label: 'Khoảng cách trên dưới', default: 'lg',
      options: [{ value: 'sm', label: 'Hẹp' }, { value: 'lg', label: 'Rộng' }],
      group: 'Hiển thị', order: 2, editableInSystemPage: false, since: '1.0.0',
    },
    maxWidth: {
      kind: 'select', label: 'Bề rộng tối đa', default: 'xl',
      options: [{ value: 'lg', label: '1024px' }, { value: 'xl', label: '1280px' }],
      group: 'Hiển thị', order: 3, editableInSystemPage: false, since: '1.0.0',
    },
  },
} satisfies ComponentManifest;
```

`allowCustom: false` trên `background` là có chủ đích: buộc chọn từ token theme (`05` §3). Cho nhập hex tự do thì `changeTheme` (#14 operation thứ 5) mất tác dụng — đổi theme mà nền các khối vẫn giữ màu cũ.

`{{_childCount}}` là biến hệ thống trong `aiSummary`, không phải prop.

### 8.3 `rich-text.manifest.ts`

```ts
export default {
  type: 'RichText',
  category: 'Content',
  label: 'Đoạn văn bản',
  description: 'Văn bản định dạng: tiêu đề nhỏ, danh sách, liên kết.',
  icon: 'Type',
  acceptsChildren: false,
  allowedChildTypes: null,
  allowedInPageKinds: ['Composable', 'System'],
  maxPerPage: null,
  aiSummary: 'Văn bản: "{{_excerpt}}"',
  since: '1.0.0',

  variants: [
    { key: 'RichText01', label: 'Một cột', preview: 'richtext/r01.webp',
      usesProps: ['content', 'align'], requiresProps: ['content'], since: '1.0.0' },
  ],

  props: {
    content: {
      kind: 'richText', label: 'Nội dung', profile: 'basic', maxLength: 5000,
      group: 'Nội dung', order: 1, editableInSystemPage: true, since: '1.0.0',
    },
    align: {
      kind: 'select', label: 'Căn lề', default: 'left',
      options: [{ value: 'left', label: 'Trái' }, { value: 'justify', label: 'Đều hai bên' }],
      group: 'Hiển thị', order: 1, editableInSystemPage: false, since: '1.0.0',
    },
  },
} satisfies ComponentManifest;
```

Đây là component duy nhất trong bốn cái có `allowedInPageKinds` gồm `System` và có một prop `editableInSystemPage: true` — cố ý, để test invariant codegen #6 (§6.1) và test op `update` bị giới hạn trên trang System (`05` §11).

### 8.4 `gallery.manifest.ts` — repeater

```ts
export default {
  type: 'Gallery',
  category: 'Media',
  label: 'Thư viện ảnh',
  description: 'Lưới hoặc băng ảnh, mỗi ảnh có chú thích tuỳ chọn.',
  icon: 'Images',
  acceptsChildren: false,
  allowedChildTypes: null,
  allowedInPageKinds: ['Composable'],
  maxPerPage: null,
  aiSummary: 'Thư viện {{_itemCount}} ảnh',
  since: '1.0.0',

  variants: [
    { key: 'Gallery01', label: 'Lưới 3 cột', preview: 'gallery/g01.webp',
      usesProps: ['heading', 'items', 'columns'], requiresProps: ['items'], since: '1.0.0' },
    { key: 'Gallery02', label: 'Băng cuộn ngang', preview: 'gallery/g02.webp',
      usesProps: ['heading', 'items'], requiresProps: ['items'], since: '1.0.0' },
  ],

  props: {
    heading: {
      kind: 'text', label: 'Tiêu đề', maxLength: 120, multiline: false,
      group: 'Nội dung', order: 1, editableInSystemPage: false, since: '1.0.0',
    },
    items: {
      kind: 'list', label: 'Danh sách ảnh', itemLabel: 'Ảnh',
      minItems: 1, maxItems: 24,
      group: 'Nội dung', order: 2, editableInSystemPage: false, since: '1.0.0',
      itemProps: {
        image: { kind: 'image', label: 'Ảnh', preset: '800x800,cover',
                 allowFocalPoint: true, group: '_', order: 1,
                 editableInSystemPage: false, since: '1.0.0' },
        caption: { kind: 'text', label: 'Chú thích', maxLength: 120, multiline: false,
                   group: '_', order: 2, editableInSystemPage: false, since: '1.0.0' },
      },
    },
    columns: {
      kind: 'number', label: 'Số cột', min: 2, max: 4, step: 1, default: 3,
      group: 'Hiển thị', order: 1, editableInSystemPage: false, since: '1.0.0',
    },
  },
} satisfies ComponentManifest;
```

### 8.5 `service-grid.manifest.ts` — tuỳ chọn, khuyến nghị làm

Manifest thứ năm, **chỉ khai báo + render skeleton**, không nối dữ liệu thật. Nó phủ ba kind còn thiếu (`binding`, `icon`, `boolean`) và — quan trọng hơn — nó là cái duy nhất kiểm chứng được invariant codegen #1 (`Review` bị hard-fail).

```ts
export default {
  type: 'ServiceGrid',
  category: 'Service',
  label: 'Lưới dịch vụ',
  description: 'Hiển thị danh sách dịch vụ của shop dạng lưới thẻ.',
  icon: 'LayoutGrid',
  acceptsChildren: false,
  allowedChildTypes: null,
  allowedInPageKinds: ['Composable'],
  maxPerPage: null,
  aiSummary: 'Lưới dịch vụ: {{heading}}',
  since: '1.0.0',

  variants: [
    { key: 'ServiceGrid01', label: 'Thẻ có ảnh', preview: 'servicegrid/sg01.webp',
      usesProps: ['heading', 'source', 'showPrice', 'columns'],
      requiresProps: ['source'], since: '1.0.0' },
  ],

  props: {
    heading: { kind: 'text', label: 'Tiêu đề', maxLength: 120, multiline: false,
               group: 'Nội dung', order: 1, editableInSystemPage: false, since: '1.0.0' },
    source: { kind: 'binding', label: 'Nguồn dữ liệu',
              sources: ['Service', 'ServiceGroup'],
              allowFilters: ['groupId', 'sort', 'take'],
              group: 'Dữ liệu', order: 1, editableInSystemPage: true, since: '1.0.0' },
    showPrice: { kind: 'boolean', label: 'Hiện giá', default: true,
                 group: 'Hiển thị', order: 1, editableInSystemPage: true, since: '1.0.0' },
    columns: { kind: 'number', label: 'Số cột', min: 2, max: 4, step: 1, default: 3,
               group: 'Hiển thị', order: 2, editableInSystemPage: false, since: '1.0.0' },
  },
} satisfies ComponentManifest;
```

Renderer ở Bước 2 trả về skeleton card theo `columns`. Bước 9 thay bằng dữ liệu thật qua Binding Resolver. **Không** viết Binding Resolver ở Bước 2.

---

## 9. Kế hoạch test cho Bước 2

Đây là câu trả lời cho *"chưa có DB thì test cái gì"*. Fixture JSON thay chỗ database — vì renderer nhận **cùng một shape** dù tree đến từ `PageDraft.Tree` (Bước 5), từ `SitePublication.Snapshot.pages[].tree` (Bước 8), hay từ file.

```
tests/fixtures/
  home.tree.json              tree hợp lệ, đủ 5 component, có Section lồng con
  bad-prop-type.json          overlayOpacity: "nhiều"        → Zod reject
  bad-required.json           Hero01 thiếu image             → reject (requiresProps)
  bad-nesting.json            children trong RichText        → reject (acceptsChildren)
  bad-unknown-variant.json    variant "Hero99"               → reject
  bad-maxlength.json          title 300 ký tự                → reject
  xss-richtext.json           <img onerror=...>, javascript: → sanitize sạch
  system-page.tree.json       Kind=System, chỉ RichText      → op update chỉ chạm content
```

Sáu nhóm assertion:

| # | Test | Chứng minh điều gì |
|---|---|---|
| 1 | Sửa `kind` của một prop → `tsc` báo lỗi tại chỗ renderer dùng | Pipeline codegen thật sự nối được manifest → type. Không báo lỗi = pipeline vô nghĩa |
| 2 | `props-schemas.ts` (Zod) và `props-schemas.json` (JSON Schema) cho **cùng** kết quả trên toàn bộ fixture | #60 đứng vững. Đây là test quan trọng nhất — nếu hai runtime lệch, BE và FE sẽ chấp nhận hai tập dữ liệu khác nhau |
| 3 | Mỗi fixture `bad-*` bị reject với **error path đúng** (`items[2].image.imageId`) | Inspector hiện được lỗi ở đúng ô nhập, không phải "props không hợp lệ" |
| 4 | `xss-richtext.json` sau sanitize không còn `on*`, `javascript:`, `<img>`, `<script>` | #67 hoạt động. Chạy cả ở Node và ở .NET |
| 5 | `check-additive.ts` fail đúng 7 trường hợp ở §5, pass 6 trường hợp OK | #62 hoạt động. Viết test cho **chính cái checker** |
| 6 | Snapshot HTML của `home.tree.json` render **giống nhau** ở SSR và CSR | Invariant isomorphic của #23. Đây là lỗi agent vi phạm liên tục |

Thêm hai test rẻ mà đáng:

- **Codegen là hàm thuần:** chạy hai lần ra output byte-identical. Không thì CI drift check (#18) vô dụng.
- **Không component nào import `resolveImage`/`resolveUrl` từ chỗ khác ngoài context:** ESLint rule `no-restricted-imports`. Enforce #11 và #53 bằng lint chứ không bằng CLAUDE.md.

---

## 10. Dev harness — cái bạn xem bằng mắt

Một route trong `apps/portal` (hoặc app Vite riêng), **ném đi sau Bước 5**:

```
/_dev/registry
  ├── trái:   dropdown chọn fixture + textarea JSON thô
  ├── giữa:   builder-renderer render tree đó, live
  └── phải:   Inspector sinh từ property-panel.ts cho node đang chọn
```

Không save, không API, không undo, không autosave. Sửa trong Inspector → tree trong React state đổi → render lại. Reload là mất.

**Vì sao vẫn nên làm dù không save được:** đây là cách duy nhất kiểm chứng người tiêu thụ thứ năm ở §1. Nếu manifest không đủ metadata để sinh form dùng được (thiếu `group`, thiếu `order`, `list` không có nút kéo-thả, `link` picker không biết hiện gì khi chưa có `Page`), bạn muốn biết **bây giờ** — lúc sửa meta-schema còn miễn phí — chứ không phải ở Bước 5 khi `PageDraft` đã vào DB và Operations Engine đã viết xong.

Không save được là **đúng và cố ý**: save là việc của Bước 5. Harness này chết sau Bước 5, và nó nên chết.

---

## 11. Validate hai tầng (#60)

```
Inspector / AI Chat
   ↓
Zod (FE)            props-schemas.ts     ← UX: lỗi hiện ngay ô nhập
   ↓ HTTP
JSON Schema (BE)    props-schemas.json   ← BẢO MẬT: nơi quyết định
   ↓
sanitize richText (BE)                   ← #67, chạy lại dù FE đã chạy
   ↓
ghi PageDraft.Tree
```

**FE validate là tiện nghi, BE validate là bảo mật.** Client nào cũng bỏ qua được — kể cả `apps/portal` do chính ta viết, vì request nào cũng giả lập được bằng curl. Bỏ tầng BE nghĩa là tin vào client.

**Vì sao không port Zod sang C# bằng tay:** đó là bản dịch thứ hai của cùng một sự thật, và nó sẽ lệch — không phải hôm nay mà ở component thứ 20. JSON Schema sinh từ cùng manifest thì không có bản dịch nào tồn tại để lệch. Cùng logic với việc `config/reserved-routes.json` được cả hai phía đọc thay vì mỗi phía giữ một danh sách.

---

## 12. `ai-tool-schema.json` (#14, #16)

Sinh sẵn ở Bước 2 dù AI Chat Builder là Phase 3. Nó gần như miễn phí khi đã có manifest, và có nó thì Phase 3 không phải sửa lại manifest:

```json
{
  "add_component": {
    "componentType": { "enum": ["Hero", "Section", "RichText", "Gallery", "ServiceGrid"] },
    "variant": { "enum": ["Hero01", "Hero02", "Section01", "..."] }
  },
  "update_component_props": {
    "componentId": { "type": "string", "pattern": "^c_[a-zA-Z0-9_-]{5}$" }
  }
}
```

`componentType`/`variant` là **enum**, đúng như #17 đã chốt: *"không thể bịa"*. `aiSummary` của mỗi manifest là template sinh mô tả node cho Selection Context (#16) khi user không click chọn gì.

---

## 13. `CLAUDE.md` cho `builder-renderer` và `builder-components`

`02` đã ghi ba lần rằng có những invariant *"AI agent sẽ vi phạm liên tục nếu không nói trước"* (#11, #23, và invariant identity). Tập cho hai package này:

```markdown
## Invariant — vi phạm là bug, không phải lựa chọn phong cách

1. KHÔNG hardcode href. Luôn ctx.resolveUrl(link).                    (#11)
2. KHÔNG nối chuỗi URL ảnh. Luôn ctx.resolveImage(imageId, preset).   (#53)
3. KHÔNG dùng window/document trong logic render chính — chỉ trong
   effect sau hydrate. Renderer phải chạy được server-side.          (#23)
4. KHÔNG sửa file trong generated/. Sửa manifest rồi chạy gen.        (#17)
5. KHÔNG thêm/xoá/đổi kiểu prop đã phát hành. Cần đổi phá vỡ → variant
   hoặc type mới. check-additive sẽ fail build.                       (#43, #62)
6. KHÔNG thêm kind mới vào prop-kinds.ts khi viết component thường.   (#63)
7. KHÔNG khai binding source ngoài config/binding-sources.json.
   "Review" không tồn tại ở đó và không được thêm vào.                (#65, 01 §7)
8. KHÔNG tạo migration trong Bước 2.
```

Bốn trong tám dòng này đã được enforce bằng codegen hoặc lint (§6.1, §9). Đó là đích cần tới: `CLAUDE.md` chỉ nên chứa thứ **chưa** đẩy lên máy được — đúng tinh thần *"codegen > skill > CLAUDE.md > hy vọng agent nhớ"*.

---

## 14. Thứ tự làm Bước 2

```
2.1  config/image-presets.json + binding-sources.json + sanitize-profiles.json
2.2  meta/prop-kinds.ts + meta/manifest-schema.ts        (Zod meta-schema)
2.3  scripts/gen-registry.ts — sinh 3 artifact trước:
       component-types.ts, props-schemas.ts, registry-map.ts
2.4  hero.manifest.ts + Hero01.tsx + Hero02.tsx
       → chạy codegen end-to-end với ĐÚNG MỘT component
       → đây là điểm kiểm tra quan trọng nhất; sai thì sửa meta-schema
2.5  builder-renderer + context (basePath, resolveImage stub, resolveUrl stub)
2.6  render fixture home.tree.json ra HTML → XEM ĐƯỢC BẰNG MẮT
2.7  section / rich-text / gallery manifest + component
       (Section kiểm children, RichText kiểm System page, Gallery kiểm list)
2.8  props-schemas.json + validator .NET + test tương đương Zod ↔ JSON Schema
2.9  property-panel.ts + Inspector + dev harness /_dev/registry
2.10 op-rules.ts + ai-tool-schema.json
2.11 registry.lock.json + check-additive.ts + CI + pre-commit
2.12 service-grid.manifest.ts (tuỳ chọn) + test hard-fail "Review"
```

**Điểm dừng để review với tôi: sau 2.4.** Lúc đó manifest schema đã bị một component thật kiểm chứng nhưng chưa có ba cái nữa xây lên trên. Sửa meta-schema ở đó rẻ; sửa sau 2.7 thì phải sửa bốn manifest.

---

## 15. Điểm còn trống

| # | Vấn đề | Trạng thái |
|---|---|---|
| 1 | Whitelist sanitize `richText` — hai profile ở §7.2 | ⚠️ **Cần xác nhận trước 2.1**. Khoá `05` §25 #3 và `06` §10 #4 |
| 2 | Danh sách preset đầy đủ (§9.3 của `05` ghi "khoảng 12", liệt kê 6) | ⚠️ Cần liệt kê đủ trước 2.1, vì codegen fail nếu preset không có trong file |
| 3 | `visibleWhen` — hiện prop có điều kiện theo prop khác (ví dụ `columns` chỉ hiện khi `Gallery01`) | ⏳ Hiện dùng `usesProps` theo variant là đủ. Nếu cần điều kiện theo *giá trị* prop khác thì thêm sau — additive, không phá vỡ |
| 4 | Responsive props (giá trị khác nhau theo breakpoint) | ⏳ Hoãn cùng `srcset` (#58). Nếu làm thì là kind mới, không sửa kind cũ |
| 5 | Đa ngôn ngữ cho **website shop** — prop nào dịch được | ⏳ `05` §25 #2. Manifest nên có `localizable: boolean` từ đầu để không phải migrate — **cân nhắc thêm ngay ở 2.2**, rẻ hơn thêm sau |
| 6 | Ai được thêm component mới: chỉ vsite, hay shop cao cấp | ⏳ Phase 4. Ảnh hưởng chỗ nào chứa manifest (repo vs DB) |
| 7 | Ảnh preview cho variant trong palette — ai chụp, chụp lúc nào | ⚠️ Việc vận hành, nhưng chặn UX palette. Bước 2 dùng placeholder |

---

*Tài liệu này chỉ bao gồm tầng manifest và codegen. `Page`/`PageDraft`/Operations Engine thật: Bước 5. Binding Resolver: Bước 9. Header + NavigationConfig: Bước 7.*
