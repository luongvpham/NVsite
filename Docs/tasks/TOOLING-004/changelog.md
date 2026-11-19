# TOOLING-004 — changelog

Promote N1–N5 của [`improvement-proposals.md`](../../process/improvement-proposals.md) (kỳ 2026-11-19)
thành P12–P16. Lane A: tooling + tài liệu quy trình, không chạm contract, không đổi runtime của app.
Nhánh `feature/TOOLING-004` tách từ `master` (`ede79b1`).

Người duyệt đã chốt trước khi làm: **N3** = quyết định `burn-auto` có `#N`, trạng thái "đề xuất — chờ
Gate 2"; **N5** = phương án M (codegen barrel); **N1** = sửa 6 lỗi lint làm commit đầu.

## Đã làm

| N → P | Commit | Thay đổi |
|---|---|---|
| N1 → P12 | `89a627c` | `packages/builder-components/meta/envelope-schema.ts`: 6 lỗi `no-unused-vars` (từ commit burn `70e1133`) → helper `withoutSchemaHeader()`. `gen:registry` sinh lại không đổi byte nào |
| N1 → P12 | `8030eb0` | Workflow §9 khuôn "Tự động": `lint + typecheck toàn repo · CI của PR xanh`, test "không lọc". Skill `weekend-burn`: implementer chạy suite đầy đủ trước commit; điều phối tự chạy lại khi dòng `Test:` có lệnh lọc; `PASS-WITH-DEBT` khi suite đỏ từ trước bước |
| N5 → P16 | `cd0b685` | `packages/api-sdk/scripts/lib/barrel.mjs` sinh `src/generated/index.ts` + `mocks.ts` sau mỗi `gen:api`. Model trùng tên giữa module chỉ export bản đầu khi giống từng byte; khác nội dung → `gen:api` fail. `src/index.ts`, `src/mocks.ts` chỉ còn re-export. Tập export trước/sau **giống hệt** (đã so bằng script) |
| N5 → P16 | `69826a8` | Skill `contract-sync` bước 7 (`gen:api` trong cùng nhánh, không sửa barrel tay). `apps/web/CLAUDE.md`: gỡ ngoại lệ "được sửa barrel" — chỉ bớt, file vẫn vượt trần (N18) |
| N2 → P13 | `b4563cb` | `check:docs`: (a) changelog trỏ `DesignIdeal/0X` → banner 0X phải có mã task ở `Tasks:` (`-D{n}` tính theo task cha; tài liệu không đánh số như workflow thì miễn); (b) dòng STATUS backtick lẻ → fail; (c) quyết định ba chữ số — 109 → 122 quyết định được kiểm. Hàm thuần ở `tools/docs/lib/doc-checks.mjs`, test chạy trong CI job `docs-check` |
| N3 → P14 | `716e1ef` | `DesignIdeal/DECISIONS.md`: trạng thái `🟡 đề xuất — chờ Gate 2` + đoạn "Agent cấp số thì chưa phải đã chốt"; dòng #102 viết lại (quyết định `burn-auto` cần số → cấp `#N` ở trạng thái đó). Workflow §6, §6.1. Skill `weekend-burn`: thay câu "không tự sửa `DECISIONS.md`" cho quyết định contract; mẫu prompt D{n} thêm bước cấp số. Plan-template §4 cột "Ai chốt". `change-reviewer`: Warning khi `#N` mới thiếu dấu người duyệt |
| N4 → P15 | `9ff28d7` | Plan-template §6 thành bảng `Ràng buộc \| Giữ bằng \| Ở task` + yêu cầu checkbox ở §9/§10. `change-reviewer`: mở đúng test/rule mà CLAUDE.md/plan nhắc, xác nhận nó fail khi vi phạm |


Cùng nhánh: `improvement-proposals.md` — commit kết quả meta-review kỳ 2026-11-19 (trước đó chưa commit
trên `master`), rồi đánh dấu N1–N5 "Đã promote", thêm P12–P16, cập nhật bảng trần ngân sách.

## Đã kiểm

- `pnpm lint`: 11/11 package xanh (trước task: `builder-components` đỏ 6 lỗi).
- `node --test packages/api-sdk/scripts/lib/barrel.test.mjs`: 5/5 (6/6 sau review). `pnpm --filter @vsite/api-sdk test`: 12/12 vitest + 5/5 node.
- `pnpm gen:api`: chỉ thêm `src/generated/index.ts` + `mocks.ts`, mọi file generated khác không đổi.
- `node --test tools/docs/lib/doc-checks.test.mjs`: 10/10 (13/13 sau review). `check:docs` xanh trên repo hiện tại
  (122 quyết định). Thử thật rule (a) bằng một changelog tạm → fail đúng thông báo, đã xoá.
- `pnpm turbo run test --force`: 10/10 package xanh, 2/2 lần (sau khi sửa lỗi có sẵn, xem dưới). `pnpm typecheck` + `pnpm lint` xanh.
- Không chạm `backend/` → không có nợ Docker (`Docs/DOCKER-TEST-DEBT.md` không đổi).

## Lỗi có sẵn trên `master` — đã trace và sửa trong nhánh này

Người yêu cầu sửa luôn (2026-11-19) để session sau khỏi tốn token dò lại nguyên cớ.

1. **`pnpm typecheck` đỏ ở `apps/portal`** — nguyên cớ: WEBSITE-004 (`d592df9`) thêm 4 `MediaReferenceKind`
   (`SitePublication`, `PageOgImage`, `WebsiteOgImage`, `WebsiteFavicon`) và `WebsiteDto.currentPublicationId`
   bắt buộc, portal không theo; CI không bắt vì lint đã đỏ sẵn (N1).
   - `media-library-picker.tsx` `describeReference`: phủ đủ 6 loại → trước đây 4 loại mới in **dòng trống**
     trong cảnh báo xoá ảnh. Giữ switch không `default` để kind mới sau này làm `typecheck` đỏ ngay.
   - `-website.test.tsx`: fixture thêm `currentPublicationId: null`.
   - Test mới `media-library-picker.test.tsx` "4 loại tham chiếu của WEBSITE-004": đỏ trước sửa, xanh sau.
2. **`gen-registry.test.ts` timeout khi `turbo test` chạy song song** — nguyên cớ: `readTreeRules()` spawn
   `npx tsx scripts/gen-registry.ts` ở **mỗi** test (7 lần, 2–7 s/lần khi máy bận), 3 test cuối không khai
   timeout → vượt 5 s mặc định. Sửa: sinh một lần trong `beforeAll` (60 s); output chỉ phụ thuộc manifest.
   `pnpm turbo run test --force`: trước đỏ 3 ca, sau xanh 2/2 lần.

Còn lại (không sửa): chạy `typecheck lint test` của mọi package **cùng lúc** (nặng hơn CI, vốn chạy tuần tự)
thì 2 test portal chạm trần `findBy` 4 s (4073 ms) và test byte-identical của `gen-registry` vượt 60 s. Chỉ
lộ ở mức tải đó; chạy như CI thì xanh.

## Giả định tôi đã tự đặt

1. **Rule (a) của N2 bắt theo tên file**, kể cả khi changelog chỉ *trích* tài liệu chứ không sửa. Trên repo
   hiện tại chỉ có 2 ca, đều là vòng `MEDIA-001-D3/D4` → xử bằng cách tính `-D{n}` theo task cha. Nếu sau này
   chặn nhầm nhiều, đổi sang chỉ bắt link trong mục "Đã làm".
2. **Rule (a) chỉ áp cho tài liệu đánh số `0X-…`**; workflow (banner `PROCESS`, `Tasks: —`) được miễn —
   proposal ghi "`DesignIdeal/0X`".
3. **N3 — quyết định ngoài contract của burn** vẫn đi `Cần người quyết` và **không** được cấp số. Câu chốt
   "burn-auto có `#N`" được hiểu cho quyết định contract (#102), vì burn không có quyền tự quyết ngoài đó.
4. **N3 — không gắn lại trạng thái 🟡 cho các `#N` cũ**: #110 đã có "Người duyệt chốt 2026-11-18".
5. **N1 — không sửa `tools/burn/run.mjs`** (proposal ghi "có thể"): luật đặt ở skill và bước kiểm của điều phối là đủ để đo ở lần rà P12.
6. **N5 — barrel đặt trong `src/generated/`** (thay vì ghi đè `src/index.ts`): giữ quy ước "file generated
   chỉ nằm trong `generated/`" và hook `guard-write` sẵn có.
7. Thứ tự commit: lint (N1) → meta-review → N5 → N1 tài liệu → N2 → N3 → N4 → đánh dấu promote + changelog.

## Review (`change-reviewer`, một vòng) — 0 Critical, 5 Warning

| # | Vấn đề | Xử lý |
|---|---|---|
| W1 | Regex 3 chữ số nhận nhầm số PR (sẽ vượt #112), `&#123;`, anchor `a.md#105-x`, `color: #333` → `check:docs` fail giả | Sửa: lookbehind `(?<![\w&/.])` + bỏ tiền tố `PR`/`issue`/thuộc tính màu. +2 ca test, gồm ca vẫn phải bắt `(#21)`, `#89:` |
| W2 | Không chỗ nào nhắc Gate 2 duyệt các dòng 🟡 | Sửa: khuôn §9 "Điểm cần anh quyết" thêm dòng `#N 🟡`; plan-template §11 thêm checkbox; burn ghi `#N` 🟡 vào `review.md` "Điểm cần anh quyết" |
| W3 | `plan-task/SKILL.md` "chỉ khi người đã chốt" mâu thuẫn cột "Ai chốt"; ví dụ §4 lẫn | Sửa: viết lại ranh giới (người chốt → số thường; người uỷ quyền → số 🟡; không ai → hỏi); ví dụ §4 cho `#N` nhận cả ba giá trị |
| W4 | Hai phát hiện ngoài phạm vi không ai theo dõi; CI của PR này sẽ đỏ ở `pnpm typecheck` | Sau đó người yêu cầu sửa luôn → đã sửa cả hai (mục "Lỗi có sẵn trên `master`") |
| W5 | Rule (a) bắt theo tên file → có thể nhồi Tasks vào banner, ngược hướng N15 | Giữ (giả định 1); rà ở mốc P13 |
| S1 | Tag OpenAPI tên "model" rơi vào thư mục type | Sửa: `barrel.mjs` throw khi `model/` có `.msw`; +1 ca test |
| S2 | Số burn cấp không trỏ nơi định nghĩa | Sửa: mẫu D{n} ghi "Định nghĩa tại" = `contract-diff-D{n}.md`, trả về "Số đã cấp" |
| S3 | Nhánh song song cấp trùng số, `Set` không báo | Sửa: `duplicateDecisions()` → `check:docs` fail `decision-dup`; +1 ca test |
| S4 | Ô trạng thái #102 chỉ nhắc TOOLING-002 | Sửa |
| S5 | Brief cũ ở `.superpowers/sdd/plan-WEBSITE-005/` còn nói sửa barrel tay | Không sửa — artifact cũ, không tái dùng |

Sau sửa: `doc-checks.test.mjs` 13/13, `barrel.test.mjs` 6/6, `check:docs` + `gen-doc-index --check` xanh,
`pnpm gen:api` không đổi `generated/`.

## Theo dõi

- P12–P16 ở bảng "Đã promote" của `improvement-proposals.md`, mỗi dòng có mốc rà lại.
- Hai lỗi có sẵn trên `master` đã sửa trong nhánh (mục trên) — đã gỡ khỏi `00-INDEX` §4.
