# BOOTSTRAP-002 — review (Gate 2)

Phạm vi: toàn bộ Bước 2 (Component Manifest Schema + Codegen + 5 component mẫu: Hero, Section, RichText, Gallery, ServiceGrid).

## Tự động

- [x] FE test builder-components: đọc được toàn bộ suite (manifest-schema, config-files, gen-registry, op-rules, schema-equivalence, lock-snapshot, sanitize-html, Inspector) — cấu trúc và assertion khớp yêu cầu §5/§6.1/§9 của tài liệu.
- [x] FE test builder-renderer: renderTree.test.tsx render được home.tree.json (Section > Hero + RichText + Gallery + ServiceGrid), kiểm resolveUrl/resolveImage không hardcode.
- [x] BE test: ComponentSchemaTests (JsonSchema.Net) đối chứng Hero/Gallery với schema-equivalence.test.ts (Ajv2020) — cùng case, cùng verdict.
- [x] additive-only: lock-snapshot.test.ts — đủ 7 rule FAIL + 1 rule CẢNH BÁO (đổi preset) + case "không đổi gì" theo đúng bảng §5, cộng 2 case nested (group, list) — 16 test đúng như khai báo.
- [x] thư mục generated đúng .gitignore (trừ registry.lock.json) — xác nhận bằng git check-ignore, không có artifact generated nào lọt vào git status.
- [x] backend/tests/ComponentSchemaTests/bin và obj — untracked nhưng đúng bị .gitignore chặn, sẽ không bị add nhầm.
- [x] turbo.json/CI wiring: build/test phụ thuộc gen:registry; output path của task gen:registry đã sửa từ đường dẫn sai sang đường dẫn đúng khớp cấu trúc thật.
- [x] guard-write.mjs/guard-bash.mjs cùng sửa theo, không còn trỏ nhầm đường dẫn cũ.

## change-reviewer

Agent tổng-quát (đóng vai change-reviewer, đọc độc lập, không có context phiên trước) đã đọc 07-component-manifest-schema.md, toàn bộ meta-schema, codegen, 5 manifest + component, sanitize, context, renderer, dev harness, backend test, CI/tooling.

Critical: không phát hiện. Không có entity/migration nào được tạo (đúng ranh giới Bước 2), không có vi phạm tenant isolation (chưa có DB ở bước này), không có file generated bị commit nhầm, không có packages import ngược từ apps, builder-renderer không import builder-core (package đó chưa tồn tại) và chỉ import @vsite/builder-components — đúng hướng phụ thuộc một chiều đã định.

Warning (không chặn Gate 2, cần theo dõi hoặc quyết định thêm):

1. Thiếu packages/builder-components/CLAUDE.md. Tài liệu (§13) yêu cầu CLAUDE.md invariant cho cả hai package (builder-renderer và builder-components), nhưng diff chỉ tạo packages/builder-renderer/CLAUDE.md. builder-components là nơi chứa prop-kinds.ts, sanitize-html.ts, và là package agent nhiều khả năng chạm tới nhất khi thêm component mới — thiếu CLAUDE.md ở đây nghĩa là 8 invariant của §13 chỉ neo được ở một nửa số nơi cần.
2. Thiếu lint rule thứ hai mà §9 yêu cầu. Tài liệu liệt kê hai test rẻ mà đáng: (a) codegen idempotent — đã có (gen-registry.test.ts); (b) ESLint no-restricted-imports chặn component import resolveImage/resolveUrl từ nơi khác ngoài context — không có trong eslint.config.mjs. Rule đã thêm (no-restricted-globals cho window/document) là một rule khác, enforce đúng #23 (isomorphic) nhưng không thay thế được rule (b) vốn enforce #11/#53. Cả hai đều nên tồn tại độc lập.
3. Thiếu test snapshot SSR và CSR (§9 test #6 — chứng minh invariant isomorphic #23, tài liệu tự ghi chú đây là lỗi agent vi phạm liên tục). Không tìm thấy renderToString/renderToStaticMarkup nào trong packages/builder-renderer hay builder-components. Test hiện tại (renderTree.test.tsx) chỉ chạy qua @testing-library/react (jsdom, tức CSR), không đối chứng với output server-side.
4. config/image-presets.json mới có 6 preset, trong khi chính tài liệu (§15 điểm #2) đánh dấu cần liệt kê đủ (khoảng 12) trước 2.1 vì codegen fail cứng nếu preset không tồn tại trong whitelist. Với 5 manifest hiện tại, 6 preset là đủ để build xanh, nhưng điểm mở này trong tài liệu chưa thấy ghi nhận là đã được xác nhận hay đóng — nên chốt lại trước khi module Website thật (Bước 5+) cần thêm preset mà không có quy trình duyệt rõ ràng.
5. Không có test tự động cho chính checkCrossManifestInvariants và checkPropInvariants trong gen-registry.ts (bao gồm invariant #1 Review hard-fail, #2 preset whitelist, #5 đến #10 ở §6.1). Đọc code xác nhận logic đúng: checkPropInvariants kiểm tra sources chứa Review tách biệt khỏi việc check whitelist, nên dù ai đó có nhét Review vào config/binding-sources.json thì vẫn fail cứng. Nhưng vì hàm fail gọi process.exit trực tiếp nên các hàm này không thể unit-test in-process; gen-registry.test.ts hiện chỉ test idempotency, không test đường FAIL. Đây là invariant §6.1 tự nhận là danh sách đắt giá nhất — nên có ít nhất một test spawn-process xác nhận exit code khác 0 khi một manifest tạm thời chứa nguồn Review.
6. sanitize-html.test.ts chưa test data URL scheme trong href — tài liệu §7.2 yêu cầu chặn cả javascript và data. Test hiện tại chỉ assert javascript. allowedSchemes trong sanitize-html.ts (https, http, mailto, tel) implicit chặn data scheme (default-deny), nhưng không có test xác nhận hành vi đó.
7. Fixture theo đúng tên file ở §9 không tồn tại như file JSON riêng (bad-prop-type, bad-required, bad-nesting, bad-unknown-variant, bad-maxlength, xss-richtext, system-page.tree). Coverage tương đương có tồn tại nhưng nằm rải rác dưới dạng object JS inline trong các file test. Chấp nhận được vì bản chất test đã phủ đúng case, nhưng lệch cấu trúc tests/fixtures mà tài liệu mô tả.

Suggestion:

- RenderContextValue được đặt tại packages/builder-components/src/context.tsx thay vì packages/builder-renderer/src/context.tsx như §3 vẽ, với lý do tránh phụ thuộc vòng (Hero01.tsx sống trong builder-components cần gọi ctx.resolveImage, trong khi builder-renderer đã import builder-components để lấy registryMap). builder-renderer/src/context.tsx chỉ re-export lại để giữ đúng bề mặt API tài liệu mô tả. Đánh giá: hợp lý và đúng là bắt buộc phải làm vậy — nếu context sống ở builder-renderer, builder-components phải import ngược lại để component lá dùng type và hook, tạo vòng phụ thuộc thật, không phải giả định. Đặt ở builder-components (package thấp hơn trong cây phụ thuộc) là hướng đúng về nguyên tắc phân lớp, không vi phạm invariant kiến trúc nào. Duy nhất cần lưu ý: đây là lệch có chủ đích so với bản vẽ thư mục ở §3 tài liệu, nên ghi rõ vào changelog khi báo cáo lại người duyệt tài liệu gốc.
- Hero01.tsx và Gallery01.tsx hardcode preset ảnh (1600x900 cover và 800x800 cover), RichText01.tsx hardcode profile basic trực tiếp trong component, tách biệt khỏi giá trị khai trong manifest tương ứng. Không có codegen hay check nào buộc hai nơi này khớp nhau. Là hệ quả tất yếu của quyết định #61, nhưng đáng ghi vào CLAUDE.md của builder-components như một điểm agent dễ quên đồng bộ.
- apps/web/src/styles/app.css đã thêm @source cho packages/builder-components/src dù apps/web chưa có dependency nào tới builder-components hay builder-renderer trong package.json — chuẩn bị trước cho Bước 5+, vô hại nhưng hiện tại là code chết.

Đã kiểm và đạt:
- 12 PropKind đúng là tập đóng, không có kind thứ 13 lén vào — đối chiếu prop-kinds.ts, manifest-schema.ts và CONTROL_BY_KIND trong gen-registry.ts, cả ba khớp nhau.
- Zod và JSON Schema sinh độc lập trực tiếp từ manifest (hai hàm riêng trong gen-registry.ts, không convert qua nhau) — đúng #60. schema-equivalence.test.ts và PropsSchemaEquivalenceTests.cs chạy cùng bộ case cho Hero và Gallery và đối chứng verdict.
- Invariant Review hard-fail được implement tại đúng lớp cứng nhất có thể, tách biệt khỏi việc check whitelist file.
- additive-only đủ 7 rule FAIL và 1 rule CẢNH BÁO, có test riêng cho chính checker.
- Không có vi phạm ranh giới module: builder-renderer chỉ phụ thuộc builder-components; không package nào trong packages import từ apps.
- ESLint no-restricted-globals cho window và document, scope đúng, loại trừ file test.
- File generated của builder-components không bị commit; gitignore có ngoại lệ đúng cho registry.lock.json.
- turbo.json sửa đúng lỗi output path và wiring build/test phụ thuộc gen:registry.
- gitattributes mới giải quyết đúng vấn đề CRLF/LF làm lệch sha256 trên Windows.
- Sanitize richText đúng theo whitelist: không style, class, id, data thuộc tính, không img, script, iframe, object, embed, form, input, ép rel noopener noreferrer khi target _blank.

## Nợ kỹ thuật cố ý và task theo dõi

- resolveImage/resolveUrl chỉ được enforce đi qua context bằng convention, chưa có lint rule no-restricted-imports.
- Preset ảnh và sanitize profile bị hardcode trùng lặp giữa manifest và component.
- image-presets.json mới có 6 trên khoảng 12 preset dự kiến.
- Chưa có test snapshot SSR và CSR cho RenderTree.
- gen-registry.ts cross-manifest invariant checks không có test tự động cho nhánh FAIL.

## Điểm cần quyết trước khi merge

Không có Critical nào chặn Gate 2. Đề nghị xử lý trước khi merge:
1. ~~Tạo packages/builder-components/CLAUDE.md.~~ **Đã xử lý** — `packages/builder-components/CLAUDE.md` tạo mới, 9 invariant (bao gồm ranh giới `RenderContextValue` và cảnh báo preset/profile hardcode trùng lặp ở mục 7).
2. ~~Thêm ít nhất 1 test cho nhánh FAIL của checkPropInvariants và checkCrossManifestInvariants trong gen-registry.ts.~~ **Đã xử lý** — `packages/builder-components/tests/gen-registry.test.ts`, describe mới "nhánh FAIL của cross-manifest invariants (§6.1) không bị bỏ sót", 4 test: `Review` hard-fail (#1), preset ngoài whitelist (#2), type trùng (#10), và một test xác nhận `registry/` được khôi phục sạch sau các test mutate-rồi-restore. Test spawn `tsx` thật (vì `fail()` gọi `process.exit`), chạy tuần tự trong cùng file để tránh đụng test khác cũng spawn `gen-registry.ts`. `pnpm --filter @vsite/builder-components run lint` và `pnpm test` (67 test builder-components, 7/7 task) đều xanh sau khi thêm.

Các Warning còn lại (mục 2–7 ở trên) hợp lý để ghi vào nợ kỹ thuật và xử lý ở các Sub-phase sau — đối chiếu đầy đủ với thiết kế gốc và nguyên nhân từng điểm lệch nằm ở `docs/tasks/BOOTSTRAP-002/changelog.md`.
