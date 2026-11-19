# Improvement Proposals

> **Nguồn:** mục "Ma sát quy trình" cuối các `Docs/tasks/*/review.md`, tổng hợp bởi agent `meta-reviewer`.
> **Cách chạy:** skill `meta-review`. Xem `DesignIdeal/ai-agent-development-workflow.md` §9 mục
> "Sau Gate 2 — bước học".
>
> **File này là đề xuất, không phải quyết định.** Không đề xuất nào có hiệu lực cho tới khi người
> duyệt sửa thẳng file quy trình và đánh dấu `Đã promote` ở đây.

---

## Đã promote — theo dõi hiệu quả

Bảng **thường trực**, không xoá theo kỳ. Đây là bước đóng vòng: thiếu nó thì quy trình chỉ biết cộng
thêm rule mà không bao giờ biết bớt.

Mỗi lần chạy `meta-review`, agent phải rà mọi dòng `⏳` đã tới hạn và kết luận: giữ, gỡ, hay chờ tiếp.

| # | Đề xuất | Ngày promote | Sửa ở file nào | Rà lại sau | Kết quả |
|---|---|---|---|---|---|
| P1 | **Gate 1 gọn trước production (#89):** bỏ phân tích nhãn `BREAKING`; chỉ thêm + không câu hỏi → promote ngay. Bằng chứng: MEDIA-001 có 4 lần Gate 1, mỗi lần chờ người 4.7–6.2 giờ, phần diff không bắt được lỗi nào | 2026-10-08 | workflow §5 §6 §10 (Rule 3, 5) · skill `contract-sync` · `CLAUDE.md` · `backend/CLAUDE.md` DoD 8 | Sau 2 task lane B/C — đo: số lần dừng Gate 1, lỗi contract lọt sang FE | ✅ có tác dụng — giữ (rà 2026-11-19). Sau promote có 3 lần Gate 1: WEBSITE-001 dừng đúng (6 câu hỏi, đổi hành vi auth media), IDENTITY-002 duyệt ngay trong session plan, WEBSITE-004 promote ngay. 0 lỗi contract lọt sang FE: WEBSITE-002/005 không có vòng D, WEBSITE-005 `data-needs.md` Q5 "không phát hiện". Mặt trái: một giả định của agent thành #110 mà không ai duyệt (WEBSITE-004 I3), xem N3 |
| P2 | **Checklist Gate 1:** chốt mọi "Câu hỏi mở" của `data-needs.md` + chạy thử Orval trên staging. Bằng chứng: MEDIA-001 phải mở lại contract 3 lần (D2 `MediaAssetDto2`, D3/D4 logo) vì hai việc này | 2026-10-08 | workflow §6 Checklist · skill `contract-sync` bước 4 | Sau 2 task lane B/C — đo: số lần promote lại contract trong cùng task | ✅ có tác dụng — giữ (rà 2026-11-19). Có chạy thử Orval trên staging ở WEBSITE-001 và WEBSITE-004, không có `…Dto2`. Số lần promote lại trong cùng task: 0 ở cả 3 task có contract (MEDIA-001 là 3) |
| P3 | **Lane C cần Docker** trước session BE. Bằng chứng: MEDIA-001 phải viết hai bộ test; 13 test fail chỉ lộ khi Docker mới có | 2026-10-08 | workflow §4 | Task lane C kế tiếp | ✅ có tác dụng — giữ (rà 2026-11-19). WEBSITE-001 và WEBSITE-004 đều chạy với Docker thật; `DOCKER-TEST-DEBT.md` rỗng; không task nào phải viết hai bộ test. Không có ca đối chứng, nhưng rule chỉ tốn 2 dòng |
| P4 | **Smoke chạy app thật sau task FE đầu tiên.** Bằng chứng: hai lỗi FE của MEDIA-001 (proxy `apps/web`, không có đường tới `/dev-registry`) chỉ lộ khi soạn hướng dẫn nghiệm thu | 2026-10-08 | workflow §8 | Task có FE kế tiếp — đo: lỗi chỉ lộ ở nghiệm thu tay | ✅ có tác dụng — giữ (rà 2026-11-19). Smoke sau F1 của WEBSITE-002 bắt sớm 4 lỗi môi trường (thiếu DB `vsite_dev`, thiếu Redis, process `Vsite.Api` cũ, compose lệch cổng). Lỗ hổng: smoke gửi HTML viết tay nên M1 lọt tới review, xem N6 |
| P5 | **Subagent chạy lệnh foreground; review task theo rủi ro.** Bằng chứng: D1 treo 4 lần, D2 treo tới lúc được đánh thức | 2026-10-08 | workflow Rule 14 · `CLAUDE.md` §Quy trình | Sau 2 task dùng subagent — đo: số lần agent treo | ✅ có tác dụng — giữ (rà 2026-11-19). 0 lần agent treo trong 5 task sau promote. Review theo rủi ro làm đúng (model mạnh nhất cho WEBSITE-001, 002, 004, 005) |
| P6 | **Cho phép trước các lệnh an toàn** (`pnpm` script, `node tools/*`, `git` chỉ đọc, `dotnet ef migrations list`). Bằng chứng: ~57 giờ lệnh nằm chờ duyệt quyền qua đêm (6 lần) trong MEDIA-001 | 2026-10-08 | `.claude/settings.json` | Sau 2 task — đo: khoảng chờ > 1 giờ giữa lúc phát lệnh và lúc có kết quả | ✅ giữ — đề nghị đóng theo dõi, gộp vào P11 (rà 2026-11-19). Thước đo gốc ("chờ > 1 giờ") không đo được vì §9 cấm ước lượng thời gian. Không review.md nào sau promote nêu prompt quyền chặn tiến độ. TOOLING-002 vẫn phải thêm 22 allow, nên P6 một mình không đủ |
| P7 | **Gỡ `session-retro`** (skill + template ~9 KB) → mục "Ma sát quy trình" cuối `review.md`. Bằng chứng: 0 retro sau 8 task; P1–P6 đều rút trực tiếp, không qua retro | 2026-10-08 | workflow §2 §9 · `meta-review` · `meta-reviewer` · `00-INDEX` §5 | Kỳ `meta-review` đầu tiên — đo: số `review.md` có mục ma sát điền thật | ✅ có tác dụng — giữ (rà 2026-11-19). 5/5 review.md sau promote có mục ma sát điền thật (0/3 trước promote). Chỉ WEBSITE-003 dùng đúng bảng có mức A/B/C; 4 task còn lại viết gạch đầu dòng không mức, nên meta-reviewer phải tự xếp |
| P8 | **Vòng mở lại contract `-D{n}`** (workflow §6.1) + chốt Task ID/nhánh (§14 mục 5, 7). Bằng chứng: MEDIA-001 mở lại 3 lần (D2–D4), mỗi lần một folder riêng, workflow không mô tả đường quay lại | 2026-10-08 | workflow §2 §6.1 §10 Rule 1 §14 · `00-INDEX` §5 | Task lane C kế tiếp — đo: số vòng D{n}, artifact có nằm trong folder task cha không | ⏳ chưa đủ dữ liệu — rà lại sau 3 task lane B/C (rà 2026-11-19). Không task người chạy nào sau promote mở vòng D (WEBSITE-001…005, IDENTITY-002); chỉ BURN-SMOKE mở D2 theo #102 |
| P9 | **Lane C: Bước 0 + `plan.md` bắt buộc.** Bằng chứng: IDENTITY-001, SHOP-001 không có `data-needs.md`; MEDIA-001/SHOP-001 thực tế đã chạy theo `plan.md` mà workflow không nhắc | 2026-10-08 | workflow §1 §3 §4 | Task lane C kế tiếp | ✅ có tác dụng — giữ (rà 2026-11-19). 5/5 task lane C (WEBSITE-001…005) có `data-needs.md` + `plan.md`. WEBSITE-005: `data-needs.md` Q5/Q6 và plan §2 mục 1, 4 chặn được hai lần suýt mở `-D1` không cần |
| P10 | **Quyền lệnh cho tool PowerShell** + sửa pattern `pnpm gen:*` (`:*` = ` *`, không khớp `gen:api`) + bỏ tự cho phép `git push`. Bằng chứng: `Bash(...)` không áp dụng cho tool PowerShell (docs permissions §PowerShell) | 2026-10-08 | `.claude/settings.json` | Sau 2 task — đo như P6 | ✅ giữ — đề nghị đóng theo dõi cùng P6 (rà 2026-11-19), lý do như P6 |
| P11 | **Burn không người trông** (TOOLING-002): skill `weekend-burn` + runner `tools/burn/run.mjs` + `permissions.deny` + Rule 15. Gate 1 tự promote theo #89 với `approvedBy = burn-auto`; contract thiếu giữa chừng → tự mở D{n} (#102). Bằng chứng: ma sát quyền của P6 (~57 giờ chờ trong MEDIA-001) + ~497 rule một-lần trong `settings.local.json`. Plan + review: `Docs/process/weekend-burn-plan.md` | 2026-11-06 | `.claude/skills/weekend-burn/` · `tools/burn/` · `.claude/settings.json` · workflow Rule 15 §13 · `.gitignore` | Sau 2 lần burn — đo: số prompt treo, `BLOCKED` đúng/sai lý do, Critical lọt sang Gate 2, số `ABORTED`, số quyết định `burn-auto` người bác ở Gate 2 | ⏳ chưa đủ dữ liệu — rà lại sau 2 lần burn nữa, kèm N1 (rà 2026-11-19). Đã có 2 burn (2026-11-06-smoke, 2026-11-13): 0 `BLOCKED`, 0 `ABORTED`, 1 Critical bị bắt ngay trong burn (S0-T2). Nhưng 2 lỗi lọt khỏi burn: T3 chạy test đã lọc nên làm đỏ 4 test cũ; lỗi lint từ `70e1133` còn trên master qua 2 lần merge. Log burn 2026-11-13 vẫn `RUNNING`, không có Tóm tắt, không ghi lý do dừng |
| P12 | **Gate 2 và burn chạy kiểm đầy đủ** (N1): khuôn "Tự động" §9 thêm `lint + typecheck toàn repo · CI của PR xanh`, test không lọc; skill `weekend-burn`: implementer chạy `dotnet test backend/vsite.sln` / `pnpm lint && pnpm typecheck && pnpm test` trước commit, điều phối tự chạy lại khi dòng `Test:` có lệnh lọc. Sửa 6 lỗi lint `envelope-schema.ts` (`89a627c`). Bằng chứng: WEBSITE-004 `70e1133` (lint đỏ qua PR #25, #26), burn T3 chạy test lọc | 2026-11-19 | workflow §9 · `.claude/skills/weekend-burn/SKILL.md` · `packages/builder-components/meta/envelope-schema.ts` | Sau 2 task + 1 lần burn — đo: lỗi lint/typecheck/test có trên master sau merge, bước burn commit khi suite đỏ | ⏳ chưa tới hạn. Lúc promote, master còn đỏ `pnpm typecheck` (`apps/portal`, từ WEBSITE-004 `d592df9`) và 3 test `gen-registry` timeout — đã sửa trong TOOLING-004 |
| P13 | **`check:docs` kiểm nội dung banner** (N2): (a) changelog trỏ `DesignIdeal/0X` → banner 0X phải có mã task ở `Tasks:` (`-D{n}` tính theo task cha); (b) dòng STATUS backtick lẻ → fail; (c) regex quyết định ba chữ số (109 → 122 quyết định được kiểm). Bằng chứng: lỗi banner ở 5/5 task WEBSITE + BURN-SMOKE | 2026-11-19 | `tools/docs/check-docs.mjs` · `tools/docs/lib/doc-checks.mjs` (+ test, CI job docs-check) | Sau 3 task — đo: số lỗi banner reviewer còn phải bắt tay | ⏳ chưa tới hạn |
| P14 | **Ghi rõ ai chốt từng quyết định** (N3): trạng thái `🟡 đề xuất — chờ Gate 2` cho `#N` do agent cấp; quyết định `burn-auto` cần số thì cấp `#N` ở trạng thái đó (#102 viết lại); plan-template §4 cột "Ai chốt"; `change-reviewer` Warning khi `#N` mới thiếu dấu người duyệt. Bằng chứng: WEBSITE-004 I3 (#110), MEDIA-001 #88, BURN-SMOKE | 2026-11-19 | `DesignIdeal/DECISIONS.md` (đầu file + #102) · workflow §6 §6.1 · skill `weekend-burn` · `plan-template.md` · `change-reviewer.md` | Sau 2 task có `#N` mới — đo: `#N` agent cấp mà ghi như đã chốt; số dòng 🟡 còn treo sau Gate 2 | ⏳ chưa tới hạn |
| P15 | **Mỗi ràng buộc có chốt kiểm tự động** (N4): plan-template §6 thành bảng `Ràng buộc \| Giữ bằng \| Ở task`; `change-reviewer` mở đúng test/rule mà CLAUDE.md hay plan nhắc và xác nhận nó fail khi vi phạm. Bằng chứng: WEBSITE-005 I2, WEBSITE-003 I1, BOOTSTRAP-002 W2 W3 | 2026-11-19 | `plan-template.md` · `change-reviewer.md` | Sau 2 task lane C — đo: ràng buộc chỉ nằm ở tài liệu mà reviewer phát hiện | ⏳ chưa tới hạn |
| P16 | **Barrel `api-sdk` sinh bằng codegen** (N5, phương án M): `gen:api` sinh `src/generated/index.ts` + `mocks.ts` từ output Orval; model trùng tên chỉ export khi giống từng byte, khác thì fail. Skill `contract-sync` bước 7: `gen:api` trong cùng nhánh. Gỡ ngoại lệ barrel ở `apps/web/CLAUDE.md`. Bằng chứng: WEBSITE-001, WEBSITE-005 Q6, MEDIA-001 | 2026-11-19 | `packages/api-sdk/scripts/` · `src/index.ts` · `src/mocks.ts` · skill `contract-sync` · `apps/web/CLAUDE.md` | Task kế tiếp thêm module/tag vào contract — đo: có phải sửa barrel tay không, `gen:api` có fail vì model trùng không | ⏳ chưa tới hạn |

Ký hiệu cột **Kết quả**: `⏳ chưa tới hạn` · `✅ có tác dụng — giữ` · `❌ không cứu được gì — đã gỡ`

---

## Kỳ gần nhất — 2026-11-19 (kỳ đầu tiên)

### Tóm tắt nhanh

- Số task đã đọc: 8. Có mục ma sát: WEBSITE-001…005. Rút gián tiếp (có trước P7): BOOTSTRAP-001,
  BOOTSTRAP-002, MEDIA-001. Bằng chứng phụ: 2 log burn, changelog TOOLING-002/003, `git log`/`git blame`.
- Số đề xuất thô: khoảng 41 (30 từ mục ma sát, khoảng 6 rút gián tiếp, 5 từ nháp burn).
- Số đề xuất đưa lên nhóm ưu tiên cao: 4 (N1–N4).
- Số dòng `⏳` đã rà: 11/11. Kết quả: 9 ✅ giữ (P6, P10 đề nghị đóng theo dõi), 2 ⏳ chờ tiếp (P8, P11), 0 ❌.

**Ba điểm cần biết trước:**

1. **Master đang có lỗi lint.** `packages/builder-components/meta/envelope-schema.ts:231-232` có 6 lỗi
   `no-unused-vars`, vào từ commit burn `70e1133` (WEBSITE-004 S0-T2). Lỗi vẫn còn sau khi merge PR #25
   và #26, dù `ci.yml` có bước `pnpm lint`. Chưa xem được CI của hai PR đó (máy không có `gh`).
2. **Hai file vượt trần ngân sách mà không ai ghi lý do:** `backend/CLAUDE.md` và `apps/web/CLAUDE.md`
   (xem mục Trần ngân sách).
3. **`check:docs` bỏ qua mọi quyết định từ #100 trở lên:** `tools/docs/check-docs.mjs` dòng 53, 62
   (regex `#(\d{1,2}…)`) và dòng 67 (`base > 99`).

### Ưu tiên cao — nên làm ngay

| # | Đề xuất | Bằng chứng (Task ID) | Mức | Tần suất | Effort | File sẽ sửa | Đụng quyết định | Trạng thái |
|---|---|---|---|---|---|---|---|---|
| N1 | **Gate 2 và burn phải chạy kiểm đầy đủ.** Khuôn "Tự động" ở §9 thêm `- [ ] lint + typecheck toàn repo · CI của PR xanh`. Mỗi bước burn chạy suite đầy đủ trước khi commit (`dotnet test backend/vsite.sln` không lọc, `pnpm lint && pnpm test`). Đưa luật này lên skill, hiện chỉ nằm cục bộ ở plan WEBSITE-004 §10. Sửa lỗi lint hiện tại bằng một task lane A | WEBSITE-004: commit burn `70e1133` gây 6 lỗi lint, vẫn còn trên master. WEBSITE-005: reviewer ghi lỗi này "có từ trước". WEBSITE-004 changelog: burn T3 chạy test lọc, làm đỏ 4 test cũ | **A** (merge khi lint đỏ, qua 2 PR) | 1 lần (một nguyên cớ, 2 biểu hiện) | S | workflow §9 (+~110 B) · `.claude/skills/weekend-burn/SKILL.md` · có thể `tools/burn/run.mjs` | không (bổ sung P11/#102) | **Đã promote** 2026-11-19 → P12 (TOOLING-004) |
| N2 | **`check:docs` kiểm nội dung banner.** (a) Changelog của task trỏ tới `DesignIdeal/0X` thì banner 0X phải có mã task ở `Tasks:`. (b) Backtick lẻ trong banner thì fail. (c) Sửa regex để kiểm cả #100 trở lên | Lần nào cũng phải làm lại vì banner: WEBSITE-001 C1, WEBSITE-002 Critical + Warning, WEBSITE-003 C1 C2 (dù plan T5 đã nhắc), WEBSITE-004 T1 Warning, WEBSITE-005 C1, thêm BURN-SMOKE. Nhắc bằng tài liệu đã thất bại (WEBSITE-003), nên phải chuyển lên công cụ (#17) | **B** | ≥ 3 lần (5/5 task WEBSITE) | M | `tools/docs/check-docs.mjs` (+ test) | không (thực thi #17) | **Đã promote** 2026-11-19 → P13 (TOOLING-004) |
| N3 | **Ghi rõ ai chốt từng quyết định.** Plan-template §4 thêm cột "Ai chốt" (`người` / `agent-uỷ-quyền` / `burn-auto`). Agent cấp `#N` thì dòng mới ở `DECISIONS.md` mang trạng thái "đề xuất — chờ Gate 2". `change-reviewer` báo Warning khi diff có `#N` mới mà không có dấu người duyệt. Trả lời luôn câu mở của BURN-SMOKE: quyết định `burn-auto` có cần `#N` không | WEBSITE-004 I3: #110 là giả định của agent mà được ghi như đã chốt. MEDIA-001 (rút gián tiếp): agent tự cấp #88, người viết đè; W9 về A1–A11. BURN-SMOKE log "Cần người quyết" | **A** (#110 định nghĩa ngữ nghĩa response chưa ai duyệt; biên A/B) | 2 lần | S | `.claude/skills/plan-task/references/plan-template.md` · đầu `DesignIdeal/DECISIONS.md` · `.claude/agents/change-reviewer.md` | **#102** · workflow §6 "Người duyệt đã quyết" | **Đã promote** 2026-11-19 → P14 (TOOLING-004) |
| N4 | **Mỗi ràng buộc phải có chốt kiểm tự động.** Plan-template §6 đổi thành bảng `Ràng buộc \| Giữ bằng (test/lint/type) \| Ở task`, mỗi dòng có checkbox tương ứng ở §9/§10. `change-reviewer` kiểm chốt mà CLAUDE.md hay plan nhắc tới có thật không | WEBSITE-005 I2: plan §6 ghi "test cấm import draft", §9 không có checkbox, `apps/web/CLAUDE.md` lại ghi như đã có. WEBSITE-003 I1: invariant cache draft chỉ nằm trong comment. BOOTSTRAP-002 W2 W3 (rút gián tiếp): luật lint và test SSR không được làm | **B** | ≥ 3 lần | S | `plan-template.md` · `.claude/agents/change-reviewer.md` | không (thực thi #17) | **Đã promote** 2026-11-19 → P15 (TOOLING-004) |

### Ưu tiên trung bình

| # | Đề xuất | Bằng chứng (Task ID) | Mức | Tần suất | Effort | File sẽ sửa | Đụng quyết định | Trạng thái |
|---|---|---|---|---|---|---|---|---|
| N5 | **Sau promote: chạy `gen:api` và cập nhật barrel viết tay.** Skill contract-sync thêm bước 7: trong cùng nhánh chạy `pnpm gen:api` và thêm export module/tag mới vào `api-sdk/src/index.ts` và `mocks.ts`. Phương án M: sinh barrel bằng codegen | WEBSITE-001 (plan cấm Orval gen nhưng CI đòi khớp, người phải quyết giữa chừng). WEBSITE-005 (barrel thiếu export của WEBSITE-004). MEDIA-001 (rút gián tiếp, turbo khôi phục nhầm barrel) | B | 2 lần | S / M | `.claude/skills/contract-sync/SKILL.md` · (M) `packages/api-sdk/scripts/` | không | **Đã promote** 2026-11-19 → P16 (TOOLING-004) |
| N6 | **Round-trip output thật của FE qua BE** cho mọi giá trị BE có chuẩn hoá (HTML, slug…), dùng fixture chung kiểu `be-canonical.json`. Thêm một dòng mặc định ở §7 "Review focus" của plan-template | WEBSITE-002 M1: lệch `<br />`/`<br>` và U+00A0/`&nbsp;`, mỗi lần lưu đều `sanitized: true` và mất undo. Smoke F8 gửi HTML viết tay nên không bắt được | B | 1 lần | S | `plan-template.md` | không | Chờ duyệt |
| N7 | **Đường dẫn chết mới thì fail.** `check:docs` lưu baseline các cảnh báo hiện có; cảnh báo mới ngoài baseline thì fail. Plan-template §11 thêm: "xoá/đổi symbol, route, công cụ dev thì grep `DesignIdeal/` và `Docs/tasks/*/acceptance.md`". Làm cùng đợt với N2 được | WEBSITE-002 (xoá `/dev-registry` gãy banner 07 + `MEDIA-001/acceptance.md`). WEBSITE-003 (đổi `resolveUrl`/`basePath`). WEBSITE-004 (21 cảnh báo cũ che cảnh báo mới) | B | ≥ 3 lần | S | `tools/docs/check-docs.mjs` · `plan-template.md` | không | Chờ duyệt |
| N8 | **Ghi SHA mà review và kiểm chứng đã chạy trên.** Khuôn review.md ở §9 thêm: "Reviewer đọc tới `<sha>`; HEAD khác thì liệt kê các commit sau và lý do không re-review" | MEDIA-001 (2 commit cuối không reviewer nào đọc). WEBSITE-005 (N1 sửa sau re-review; không biết `.output` dựng từ commit nào) | B | 2 lần | S | workflow §9 (+~130 B) | không | Chờ duyệt |
| N9 | **Vệ sinh môi trường cho subagent.** `permissions.deny` cho `git stash*` (Bash + PowerShell); subagent dọn tiến trình nó mở, hoặc dùng cổng/thư mục output riêng | WEBSITE-005 (`git stash pop` áp nhầm stash cũ; server bỏ lại ở cổng 3011–3013, 4597–4599). WEBSITE-004 (dev server khoá DLL; `gen-registry` timeout khi chạy song song dotnet) | B / C | 2 lần | S | `.claude/settings.json` · `plan-template.md` mục Subagent | không | Chờ duyệt |
| N10 | **`guard-bash` bớt chặn nhầm.** Bỏ thân heredoc và chuỗi trong ngoặc trước khi khớp `WRITE_COMMANDS`; thêm ca test. Không nới phần kiểm đích của redirect | WEBSITE-001, TOOLING-002 (sửa một phần ở TOOLING-003), BURN-SMOKE log, WEBSITE-004 (sau TOOLING-003). Nguyên cớ còn trong code: `/\b(copy\|move\|…)\b/i` và `\bcp\b` khớp ở bất kỳ đâu | C | ≥ 3 lần | S | `.claude/hooks/guard-bash.mjs` · `guard-bash.test.mjs` | không | Chờ duyệt |
| N11 | **Changelog bớt trùng (đề xuất bớt).** Gộp "Giả định" vào "Lệch có chủ đích" (thêm cột Nguồn); "Theo dõi" chỉ một danh sách ở cuối; nhật ký từng T để trong commit message; mặc định viết changelog một lần lúc đóng task | WEBSITE-002 (header và "Chưa làm xong" không được dọn). WEBSITE-004 (511 dòng). WEBSITE-005 (344 dòng, B1/B3/B5 chồng lệch 1/4/5) | C (WEBSITE-002 là B) | ≥ 3 lần | S | `plan-template.md` §11 · `backend/CLAUDE.md` DoD 9 (**đang vượt trần**, đi cùng N19) | không | Chờ duyệt |
| N12 | **Việc người phải làm trước S0 là cổng cứng.** Plan-template §5 thêm checklist "Việc của người trước S0" (file bị hook bảo vệ như `config/reserved-routes.json`); chưa tick thì không mở S0 | WEBSITE-001 (`reservedShopSitePaths` bị bỏ qua tới Gate 2, test để `Skip`/`it.todo`). MEDIA-001 (rút gián tiếp, route `media`) | B | 2 lần | S | `plan-template.md` · `.claude/skills/plan-task/SKILL.md` | không | Chờ duyệt |
| N13 | **Test trình duyệt (Playwright) sớm hơn** cho kéo-thả, IME, scroll, layout | WEBSITE-002 (F7–F11 dồn sang nghiệm thu tay). WEBSITE-003 I2 (`scrollIntoView` bị jsdom che) | B | 2 lần | L | `apps/portal`, `apps/web` (hạ tầng test) · workflow §8 (1 dòng) | không | Chờ duyệt |
| N14 | **Triage chéo các minor đã hoãn trước đợt cuối.** Một dòng ở plan-template §11 | WEBSITE-001 I1 chỉ lộ khi đặt cạnh nhau hai minor của hai đợt | B | 1 lần | S | `plan-template.md` | không | Chờ duyệt |
| N15 | **Banner gọn lại (đề xuất bớt).** `Changelog:` nhận nhiều đường dẫn; bỏ thói nhồi lịch sử vào `Stale`; danh sách "chưa code" chuyển sang changelog hoặc 00-INDEX §3; banner ≤ khoảng 400 ký tự, `check:docs` cảnh báo khi vượt | WEBSITE-002 (banner chỉ nhận một changelog). WEBSITE-004 (banner 05 = 3 286 B, 08 = 2 156 B, 07 = 1 320 B). BURN-SMOKE | C | ≥ 3 lần | M | `DesignIdeal/00-INDEX.md` §5 · `tools/docs/gen-doc-index.mjs` · `check-docs.mjs` · banner `0*.md` | không | Chờ duyệt |
| N16 | **Đóng §14 mục 6** (ngưỡng lane bằng con số): để cảm tính, giữ Rule 11 "dừng và báo" (đề xuất bớt) | 6 task sau promote (WEBSITE-001…005, IDENTITY-002) không báo chọn nhầm lane. Chỉ BURN-SMOKE ghi sai, #102 đã xử lý | C | 1 lần | S | workflow §14 (−~250 B) | không | Chờ duyệt |
| N17 | **`check:docs` đo trần ngân sách**, vượt trần thì cảnh báo | Mục ngân sách dưới: 2 file vượt trần im lặng | C | 2 lần | S | `tools/docs/check-docs.mjs` | không | Chờ duyệt |
| N18 | **Gỡ bớt `apps/web/CLAUDE.md`** (8.8/6 KB). Mục "Shop Site: chọn shop theo Host" (3 479 B) rút còn khoảng 6 dòng invariant trỏ vào code; bỏ danh sách tên SDK draft ở dòng 43 (trùng `WEBSITE_DRAFT_SDK_NAMES` trong `eslint.config.mjs`); lệnh chạy dev chuyển sang `.env.example`/README. Phương án khác: nâng trần riêng cho `apps/web`, ghi lý do kèm ngày | WEBSITE-005 (`c4b5dfd`, `16050df`) | C | 1 lần | S | `apps/web/CLAUDE.md` | không | Chờ duyệt — **điều kiện tiên quyết** trước khi sửa file này vì việc khác |
| N19 | **Gỡ bớt `backend/CLAUDE.md`** (16.3/16 KB). Mục "Cấu trúc thư mục" (dòng 37–110, 4 939 B) rút còn khoảng 1 KB, chi tiết chuyển sang `backend/docs/structure.md` | Phình từ 12.1 lên 16.3 KB qua REFACTOR-BE/DB/AUTHZ/API-001 (2026-10-08 đến 10-09) | C | 1 lần | S | `backend/CLAUDE.md` · tạo `backend/docs/structure.md` | không | Chờ duyệt — **điều kiện tiên quyết** cho N11 |

### Tạm gác — cần thêm dữ liệu

Chỉ dành cho đề xuất **mức C** xuất hiện một lần. Đề xuất **mức A** dù chỉ một lần vẫn phải lên nhóm
ưu tiên cao — một lần suýt để lọt vi phạm tenant isolation không cần lặp lại mới đáng sửa.

- **Helper test được đưa lên code production thì phải kèm test input hỏng.** WEBSITE-002 m1
  (`isSafeLinkUrl` gặp `RangeError`). Hiện là nợ sản phẩm, đang theo dõi.
- **Danh sách fixture viết tay, nên đổi sang `import.meta.glob`.** WEBSITE-003 M7.
- **Kịch bản nghiệm thu HTTP viết bằng Node thay vì curl+bash** (escape lỗi, cần đặt `Host`). WEBSITE-004.
- **`IAppDbContext`: thêm `DbSet` kéo theo sửa 4 fake decorator.** Log burn 2026-11-13 (nháp).
- **`diff-report.mjs` tạo folder nhầm theo taskId; sửa banner kéo theo phải chạy `gen:doc-index`.**
  Log BURN-SMOKE (nháp).
- **Nới khuôn mục ma sát: cho gạch đầu dòng kèm tag `[A/B/C]` thay cho bảng.** Từ kết quả rà P7: 4/5
  task không dùng bảng. Không có thiệt hại, chỉ để meta-reviewer khỏi tự xếp mức.
- **Reviewer báo nhầm vì thiếu context** (EF InMemory đúng theo skill Bước 1). BOOTSTRAP-001, rút gián
  tiếp, chuyện cũ.

### Bị loại — và lý do

Ghi lại để kỳ sau không đề xuất lại cùng một thứ.

- **"Giữ quy ước S0 spike có quyền dừng plan"** (WEBSITE-005): xác nhận cách làm đang chạy tốt, không
  phải ma sát; plan-template §5 đã có S0.
- **`.superpowers/` không được ignore** (WEBSITE-001): đã sửa ngay trong task (`.gitignore`), không tái diễn.
- **`contract:diff` mù với endpoint trả về mảng** (MEDIA-001): đã sửa ở TOOLING-001 (classifier đệ quy,
  skill contract-sync bước 3).

### Giả định meta-reviewer đã tự đặt

1. Mức A/B/C cho WEBSITE-001, 002, 004, 005 và 3 task rút gián tiếp là do meta-reviewer tự xếp.
2. N1 mức A dựa trên lỗi lint có thật trên master và CI có bước `pnpm lint`; chưa xem được CI của PR #25/#26.
3. N3 mức A vì #110 định nghĩa ngữ nghĩa response của contract; nằm ở biên A/B.
4. Dùng log burn và changelog TOOLING-002/003 làm bằng chứng phụ cho tần suất (N10, N16, P11).
5. P6/P10 "✅ giữ, đóng theo dõi" dùng thước đo thay thế, vì thước đo gốc mâu thuẫn với luật "không
   ước lượng thời gian" ở §9.

---

## Trần ngân sách tài liệu

Mỗi rule mới bắt **mọi session sau** trả giá bằng token, mãi mãi. Trần này là cái phanh duy nhất.

| File | Trần | Hiện tại (2026-11-19) | Còn trống |
|---|---|---|---|
| `DesignIdeal/ai-agent-development-workflow.md` | 22 KB | 19.7 KB (sau TOOLING-004) | 2.3 KB |
| `CLAUDE.md` (root) | 8 KB | 7.5 KB | 0.5 KB |
| `backend/CLAUDE.md` | 16 KB | **16.3 KB — VƯỢT** | **−0.3 KB** |
| `CLAUDE.md` mỗi thư mục khác | 6 KB | `apps/web` **8.7 KB — VƯỢT** (TOOLING-004 bớt 0.1) · `builder-components` 5.4 · `builder-core` 5.0 · `apps/portal` 4.7 · `builder-renderer` 2.5 | `apps/web` **−2.7 KB**; còn lại ≥ 0.6 KB |

**Luật:** đề xuất làm vượt trần phải **kèm một đề xuất gỡ bớt tương ứng**, hoặc tự xuống nhóm ưu tiên
thấp. Nâng trần là một quyết định riêng, phải ghi lý do ở đây kèm ngày.

> 2026-10-08: workflow từ 21.3 xuống 18.1 KB nhờ bỏ phần tự chép (§10 Rules, #89 lặp 5 chỗ, §12, §14
> mục đã xong) dù thêm §6.1. Muốn nâng trần thì ghi lý do ở đây kèm ngày, đừng nâng im lặng.
>
> 2026-11-19: hai file vượt trần mà không có ghi chú nâng trần. `backend/CLAUDE.md` phình từ 12.1 lên
> 16.3 KB qua 4 task REFACTOR-* (2026-10-08 đến 10-09); `apps/web/CLAUDE.md` phình qua WEBSITE-005.
> Đề xuất gỡ: N18, N19; chặn tái diễn: N17. Tác động của đề xuất kỳ này lên workflow: N1 +110, N8 +130,
> N13 +80, N16 −250 → khoảng +70 B, vẫn dưới trần. N6, N12, N14 cố ý đặt vào `plan-template.md` thay
> vì workflow. N11 đụng `backend/CLAUDE.md` nên chỉ làm được khi tổng ≤ 0 byte hoặc sau N19.

Đo bằng `wc -c` (1 KB = 1000 byte). Con số "hiện tại" cập nhật mỗi kỳ meta-review.
