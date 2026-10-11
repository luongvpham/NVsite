# TOOLING-001 — changelog

Task 5 của đợt review kiến trúc 2026-10-08 (review FE P0 #1–#4). Lane A: chỉ chạm tooling, CI, test
và vài lỗi lint; không đổi contract, không đổi runtime của app. Trả nợ MEDIA-001 "Chưa làm xong" #8
và #16. Nhánh tách từ `feature/DESIGN-STEP5-PREP`. Theo yêu cầu người duyệt, **chưa commit** —
người duyệt commit một lần.

---

## Đã làm

### 1. Typecheck + lint trong CI (review P0 #1)

- **Script `typecheck`** (`tsc --noEmit -p .`) cho cả 8 package; task turbo `typecheck`;
  root `pnpm typecheck`.
- **CI job Frontend** thêm typecheck, lint, `check-additive`, và
  `git diff --exit-code packages/api-sdk/src/generated` (generated đã commit phải khớp lần sinh lại).
- **Lint trước đây chưa từng chạy trong CI** nên đang đỏ sẵn 12 lỗi ở 4 package:
  - Gỡ các non-null assertion trong `gen-registry.ts`.
  - Sửa test `api-sdk` viết ở REFACTOR-API-001, test setup của `apps/web` và 1 assertion thừa ở
    Portal.
  - `gen-registry.ts` đổi cách sắp xếp nhưng **giữ đúng phép so sánh mặc định**. Đã kiểm: cả 8
    artifact sinh ra giống hệt từng byte trước và sau thay đổi.
- **`packages/api-sdk/tsconfig.json`:**
  - Bỏ `composite`/`rootDir`, vì không package nào dùng project references.
  - Gồm cả `tests/`.
  - Bỏ `tsconfig.test.json` tạm của REFACTOR-API-001.
- **ESLint:** bỏ qua `packages/*/scripts/**/*.mjs` (script Node thuần, không thuộc tsconfig nào),
  cùng cách đang làm với file config.
- **Kiểm chứng:** chèn tạm một lỗi kiểu vào `builder-renderer` → `pnpm typecheck` báo lỗi đúng chỗ.

### 2. Đồ thị turbo (review P0 #2)

- **Vấn đề:** `build` phụ thuộc `"gen:api"`, `"gen:registry"` không có `^`, tức task **của chính
  app**, mà app không có hai task đó. Nên `portal#build` không chờ `api-sdk#gen:api` hay
  `builder-components#gen:registry`. Clone mới có thể build trước khi codegen chạy.
- **Sửa:** `build`/`lint`/`typecheck`/`test` đều phụ thuộc `^gen:api`, `^gen:registry`, `gen:api`,
  `gen:registry`. Dry-run xác nhận `portal#build ← api-sdk#gen:api, builder-components#gen:registry`.
- **`gen:registry`** thêm `inputs: ["$TURBO_DEFAULT$", "$TURBO_ROOT$/config/**"]`. Sửa
  `config/image-presets.json` giờ làm cache miss, không còn phát lại `derivative-presets.json` cũ (BE
  đọc file này).

### 3. Drift check của contract (review P0 #3, nợ MEDIA-001 #8)

- **`tools/contract-sync/lib/diff.mjs`:**
  - Đi **đệ quy** mọi `$ref` mà operation chạm tới: `items.$ref` của response dạng mảng, schema
    lồng, component trỏ component.
  - So thêm `parameters`, `operationId` (đổi tên = đổi tên hook FE, #91), schema inline, và phần
    schema ngoài `properties` (enum, type, items…).
  - **Lưới an toàn:** operation khác ở chỗ không gọi tên được thì vẫn báo BREAKING, không bao giờ ra
    `UNCHANGED` giả.
- **`check-no-drift.mjs`** (CI) còn so **nguyên document** sau normalize. Component không operation
  nào dùng mà đổi cũng fail. Classifier giờ chỉ để báo cáo cho người đọc, không quyết định pass/fail.
- **Luật normalize không đổi** (skill `contract-sync` yêu cầu hỏi trước khi đổi).
- **Test:** `tools/contract-sync/lib/diff.test.mjs` (`node:test`, chạy trong CI job Contract drift),
  14 test (8 cho điểm mù cũ + 6 sau review).
- Drift check thật trên contract hiện tại vẫn OK.

### 4. `registry.lock.json` (review P0 #4, nợ MEDIA-001 #16)

- **Lệnh `pnpm registry:lock`** (`scripts/registry-lock.ts`): ghi lock cho khớp registry, **từ chối
  khi đang vi phạm additive-only**.
- **`gen-registry` không còn tự tạo lock khi thiếu.** Trước đây xoá lock là xoá mọi bảo vệ. Giờ lock
  thiếu hoặc cũ chỉ là cảnh báo ở `gen:registry`.
- **`check-additive`** (CI, pre-commit) **FAIL** khi lock không tồn tại, hoặc khi `findUnlocked` thấy
  phần chưa được bảo vệ: type/variant/prop mới, field snapshot mới. Trước đây lock chỉ ghi một lần
  lúc chưa có file, nên mọi thứ thêm sau không bao giờ được bảo vệ.
- **Snapshot thêm** `acceptsChildren`, `allowedChildTypes`, `link.allowKinds`, `binding.sources`.
  Luật mới, đều FAIL:
  - thu hẹp `allowKinds` hoặc `sources`;
  - `acceptsChildren` từ true sang false;
  - thu hẹp `allowedChildTypes`.
  Lock cũ chưa có các field này thì không báo vi phạm giả.
- **`registry.lock.json` đã cập nhật** bằng `registry:lock`: chỉ thêm 12 mục (field mới), không mất
  mục nào.
- **Hook pre-commit:** `--diff-filter=ACMDR` (trước là `ACM`, bỏ sót **xoá** manifest); chạy cả khi
  `registry.lock.json` đổi (sửa tay lock để lách).
- **Test đột biến của `gen-registry.test.ts`:**
  - Chạy trên **bản sao tạm** của `registry/` + lock + generated, qua biến môi trường
    `VSITE_REGISTRY_DIR`/`VSITE_GENERATED_DIR`/`VSITE_REGISTRY_LOCK` (`scripts/lib/paths.ts`).
  - Không bao giờ sửa `registry/` thật nữa.
  - Thêm test cuối khẳng định hash của manifest thật không đổi.
  - Thêm test FAIL khi thu hẹp `link.allowKinds`.
  - Đặt timeout 60s cho test spawn `tsx` từng bị flaky.
- **`loadManifests`** sắp xếp file trước khi nạp, nên thứ tự không phụ thuộc filesystem (Linux trên
  CI không đảm bảo thứ tự). Artifact sinh ra trên máy dev không đổi.
- **Test:** `lock-snapshot.test.ts` thêm 11 test (luật mới, `findUnlocked`, các ca sau review).

## Sau review độc lập

Review tìm ra 1 Critical (tài liệu) + 5 Important, đã sửa hết:

- **Classifier gắn ADDITIVE cho thay đổi phá vỡ.** Nhãn ADDITIVE dẫn tới tự promote (#89), nên đây
  là lỗi nghiêm trọng nhất.
  - Lưới an toàn giờ chạy **khi chưa có BREAKING**, không chỉ khi chưa có thay đổi nào. Nó so phần
    operation chưa được phân loại (`security`, `deprecated`…), và bắt trường hợp response mất content.
  - Luật "phần schema ngoài `properties`" chạy luôn, nên thêm field cùng lúc siết
    `additionalProperties` vẫn ra BREAKING.
  - Content-type chỉ còn ở bản cũ → BREAKING. `requestBody` thành bắt buộc → BREAKING.
  - `operationId` từ ∅ → X → BREAKING.
  - Thêm 6 test.
- **Lock phải bằng đúng snapshot hiện tại.** Trước đây chỉ kiểm có hay không có key: thêm option hoặc
  nới `maxLength` mà không chạy `registry:lock` thì vẫn lọt.
- **Type lá → container** (`acceptsChildren` false → true) không còn bị báo nhầm là "thu hẹp
  allowedChildTypes".
- **CI kiểm generated bằng `git status --porcelain`** thay vì `git diff`, nên bắt được cả file model
  MỚI quên commit.
- **`documentsEqual` không phụ thuộc thứ tự `required`/`tags`.** Export trên Windows và Ubuntu có thể
  khác thứ tự. Khi lệch, drift check in ra JSON-path đầu tiên khác nhau.
- **Minor:**
  - Cảnh báo khi đường dẫn registry bị ghi đè bằng biến môi trường.
  - Hook pre-commit chạy cả khi `meta/` hoặc `lock-snapshot.ts` đổi.
  - Các test spawn `gen-registry` ghi vào thư mục generated tạm, không đè `generated/` thật mà
    package khác đang đọc.
  - `diff-report.mjs` ghi ra `Docs/` thay vì `docs/` (trên Linux là sai thư mục).
- **Sự cố trong lúc review:** agent review dùng `git worktree` kèm junction `node_modules` và đã xoá
  nhầm một phần `node_modules` của repo, rồi tự cài lại bằng `pnpm install --force`. Không file nguồn
  nào bị ảnh hưởng (vẫn đúng 38 thay đổi), hook `.git/hooks/pre-commit` đúng, và
  `pnpm install --frozen-lockfile` sạch.

## Kiểm chứng (2026-10-11)

Chạy đúng chuỗi lệnh của CI trên máy dev:

| Lệnh | Kết quả |
|---|---|
| `pnpm build` | 4/4 |
| `pnpm typecheck` | 10/10 |
| `pnpm lint` | 10/10 |
| `pnpm check:registry-additive` | OK |
| `pnpm test` | 8/8 package (builder-components 99 test) |
| `git diff --exit-code packages/api-sdk/src/generated` | sạch |
| `node --test tools/contract-sync/lib/diff.test.mjs` | 14/14 |
| `check-no-drift` | OK |

Chưa chạy trên runner CI thật (Ubuntu). Bước `git diff --exit-code` của generated phụ thuộc Orval sinh
giống nhau trên Linux; `.gitattributes` đã ép LF.

## Chưa làm / để task khác

1. **Phần còn lại của review FE (P1/P2) chưa đụng** (đã ghi ở 00-INDEX §4):
   - tách `builder-components` thành package model không phụ thuộc React;
   - theme bằng CSS variables;
   - `oneOf` theo type trong AI tool schema;
   - hằng preset sinh ra thay vì hardcode trong component;
   - test SSR (`renderToString`) cho renderer;
   - lint rule chặn import `apps/**` từ `packages/` và `builder-core` từ renderer;
   - `@vsite/config`;
   - gom error code về `@vsite/shared`;
   - chặn `/dev-registry` ở production;
   - dọn file đã commit nhầm (`.tanstack/tmp`, `.staging`).
2. **CI chưa có job riêng cho script tooling** (`tools/docs`, `tools/contract-sync` ngoài classifier).

## Giả định tôi đã tự đặt

- `gen:registry` khi lock thiếu/cũ chỉ **cảnh báo** (không fail), để dev thêm component vẫn sinh code
  được. Chặn cứng nằm ở `check-additive` (CI + pre-commit).
- Thêm bước `git diff --exit-code` cho `packages/api-sdk/src/generated` vào CI. Mục này ngoài danh
  sách review nhưng cùng loại lỗi "quên chạy codegen".
