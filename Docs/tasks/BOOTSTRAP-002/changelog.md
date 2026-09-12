# BOOTSTRAP-002 — changelog: lệch so với thiết kế

Ghi lại mọi chỗ thực thi khác với `DesignIdeal/07-component-manifest-schema.md` (và plan đã duyệt)
cùng nguyên nhân, để người duyệt tài liệu gốc biết chỗ nào là **lệch có chủ đích** (giữ nguyên) và
chỗ nào là **việc chưa làm xong** (theo dõi ở Sub-phase sau). Không lặp lại nội dung đã có trong
`review.md` — file đó là đánh giá chất lượng, file này là đối chiếu với bản vẽ thiết kế.

## Lệch có chủ đích (giữ nguyên, không phải bug)

### 1. `RenderContextValue`/`RenderContextProvider`/`useRenderContext` đặt ở `builder-components`, không phải `builder-renderer`

- **Thiết kế nói gì:** §3 vẽ context sống tại `packages/builder-renderer/src/context.tsx`.
- **Thực thi:** implementation thật nằm ở `packages/builder-components/src/context.tsx`.
  `packages/builder-renderer/src/context.tsx` chỉ `export { ... } from '@vsite/builder-components'`
  để giữ đúng bề mặt API (import path) mà tài liệu mô tả cho phía tiêu thụ.
- **Nguyên nhân:** component lá (`Hero01.tsx`, `Gallery01.tsx`...) sống trong `builder-components`
  và phải gọi `ctx.resolveImage()`/`ctx.resolveUrl()`. `builder-renderer` lại phải import component
  từ `builder-components` để tra `registry-map.ts` và render. Nếu context sống ở `builder-renderer`
  như bản vẽ, `builder-components` phải import ngược lại để lấy type/hook cho component lá dùng —
  tạo vòng phụ thuộc thật giữa hai package (không phải giả thuyết, đã thử và TypeScript/bundler từ
  chối resolve). Đặt context ở package thấp hơn trong cây phụ thuộc là hướng đúng nguyên tắc phân
  lớp, không vi phạm invariant kiến trúc nào (`builder-renderer` chỉ phụ thuộc `builder-components`,
  không có chiều ngược lại).
- **Đã review:** change-reviewer (Gate 2) xác nhận đây là bắt buộc, không phải lựa chọn tuỳ tiện.

### 2. Route dev harness là `/dev-registry`, không phải `/_dev/registry`

- **Thiết kế nói gì:** §1/§9 gọi route dev harness là `/_dev/registry`.
- **Thực thi:** `apps/portal/src/routes/dev-registry.tsx`, phục vụ tại `/dev-registry`.
- **Nguyên nhân:** TanStack Router dành riêng tiền tố `_` trong tên file route để đánh dấu
  "pathless layout route" (route không tự thêm segment vào URL, chỉ bọc layout cho route con), chứ
  không phải một segment URL literal. Không có cách đặt tên file để route thật sự phục vụ tại
  `/_dev/registry` mà không đụng cơ chế reserved-prefix của router. Vì route này chỉ tồn tại tới hết
  Bước 5 (dev-only, không phải sản phẩm) và không có route nào khác khai `/dev-registry`, chọn bỏ
  qua đúng chữ `_dev` thay vì vật lộn với router.

### 3. ESLint enforce isomorphic bằng `no-restricted-globals`, không phải `no-restricted-imports` như tài liệu gợi ý

- **Thiết kế nói gì:** §9 liệt kê "test rẻ mà đáng": ESLint `no-restricted-imports` chặn import
  `resolveImage`/`resolveUrl` từ nơi khác ngoài context, để enforce #11/#53 (không hardcode URL).
- **Thực thi:** đã thêm rule `no-restricted-globals` chặn `window`/`document` trực tiếp trong
  `packages/builder-components/src/**` và `packages/builder-renderer/src/**` (trừ test) — enforce
  invariant #23 (isomorphic), KHÔNG phải #11/#53.
- **Nguyên nhân:** `resolveImage`/`resolveUrl` không tồn tại như export độc lập ở đâu để
  `no-restricted-imports` có target hợp lệ — chúng chỉ truy cập được qua `ctx.resolveImage()` sau
  khi gọi `useRenderContext()`. Rule import-restriction theo đúng nghĩa đen tài liệu mô tả không map
  được vào hình dạng code thật.
- **Còn thiếu (không phải lệch có chủ đích — xem changelog #B dưới):** rule #23 đã thêm không thay
  thế được ý định gốc của #11/#53. Cách enforce đúng tinh thần tài liệu (chặn hardcode URL) vẫn chưa
  có — xem mục "Việc chưa làm xong" bên dưới.

### 4. `service-grid.manifest.ts` không nối data thật (skeleton)

- **Thiết kế nói gì:** không mô tả chi tiết `ServiceGrid`, chỉ mở khả năng thêm nếu cần test
  invariant Review.
- **Thực thi:** manifest và `ServiceGrid01.tsx` chỉ có shape (props, binding source), không có
  Binding Resolver thật.
- **Nguyên nhân:** đúng phạm vi đã duyệt trong plan Sub-phase A/B — Binding Resolver là Bước 9,
  không phải Bước 2. Được chọn làm component thứ 5 CHỈ vì đây là manifest duy nhất test được
  invariant "Review" hard-fail (§6.1 #1), không phải vì cần render dữ liệu thật.

## Việc chưa làm xong so với đặc tả (không phải lệch có chủ đích — nợ kỹ thuật, theo dõi ở Sub-phase sau)

Chi tiết đầy đủ + đánh giá mức độ nằm trong `review.md` (mục Warning). Tóm tắt liên hệ tới thiết kế:

- **§15 điểm #2 (preset ảnh):** tài liệu tự đánh dấu cần "khoảng 12 preset" trước khi đóng Bước 2;
  hiện mới có 6 (đủ cho 5 manifest mẫu). Điểm mở này của chính tài liệu chưa được xác nhận đóng hay
  mở rộng — cần quyết định trước khi module Website thật (Bước 5+) cần thêm preset.
- **§9 test #2 (ESLint `no-restricted-imports` cho resolveImage/resolveUrl):** xem mục lệch #3 ở
  trên — chưa có cách enforce tương đương nào thay thế.
- **§9 test #6 (snapshot SSR ↔ CSR cho `RenderTree`):** chưa viết. Đây là bài test duy nhất chứng
  minh trực tiếp invariant isomorphic #23 bằng cách chạy `renderToString`/`renderToStaticMarkup` và
  so với output CSR — hiện chỉ có test CSR (`@testing-library/react`, jsdom).
- **§9 fixtures (`bad-prop-type.json`, `bad-required.json`, `bad-nesting.json`,
  `bad-unknown-variant.json`, `bad-maxlength.json`, `xss-richtext.json`, `system-page.tree.json`):**
  không tồn tại như file JSON riêng trong `tests/fixtures/` như tài liệu mô tả cấu trúc thư mục.
  Coverage tương đương có, nhưng nằm rải rác dạng object JS inline trong từng file test — lệch cấu
  trúc, không lệch nội dung kiểm tra.
- **`config/sanitize-profiles.json`:** test hiện tại (`sanitize-html.test.ts`) chưa có case riêng
  cho `data:` URL scheme trong `href` (chỉ test `javascript:`), dù §7.2 yêu cầu chặn cả hai. Hành vi
  chặn đã đúng (default-deny scheme không nằm trong whitelist), chỉ thiếu test xác nhận tường minh.

## Đã xử lý sau review Gate 2 (không còn là điểm mở)

- Đã tạo `packages/builder-components/CLAUDE.md` (thiếu so với §13 yêu cầu CLAUDE.md cho cả hai
  package `builder-renderer` và `builder-components`).
- Đã thêm test cho nhánh FAIL của `checkCrossManifestInvariants`/`checkPropInvariants` trong
  `gen-registry.ts` (`tests/gen-registry.test.ts`, describe "nhánh FAIL của cross-manifest
  invariants") — phủ 3 case: `binding.sources` chứa `"Review"` (#1), `image.preset` ngoài whitelist
  (#2), `type` trùng giữa hai manifest (#10). Test spawn `tsx` thật (vì `fail()` gọi
  `process.exit(1)`, không unit-test in-process được), sửa file thật trong `registry/` rồi khôi phục
  bằng try/finally, chạy tuần tự trong cùng một file test để không đụng test khác cũng spawn
  `gen-registry.ts`.

## Ngoài phạm vi thiết kế của Bước 2 (bug hạ tầng phát hiện giữa chừng, không phải nội dung §07)

- **CRLF/LF trên Windows làm sha256 của `contracts/openapi/sample.v1.json` và `contract.lock` lệch
  nhau:** `core.autocrlf=true` âm thầm convert LF→CRLF trong working tree, không phải nội dung file
  thật đổi (git blob vẫn LF). Fix bằng `.gitattributes` (`* text=auto eol=lf`) + rewrite hai file bị
  ảnh hưởng. Không liên quan Component Manifest Schema, nhưng chặn `pnpm build` nên phải xử lý trong
  cùng phiên làm việc.
