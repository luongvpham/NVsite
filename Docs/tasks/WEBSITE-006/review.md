# WEBSITE-006 — Review (trước Gate 2)

Lane A, chỉ FE. Quy trình: mỗi task F2–F5 một implementer (subagent) + `change-reviewer` ngữ cảnh mới chỉ nhận diff
của task và mục plan; F1 + F6 controller tự làm, review gộp; F7 controller tự chạy; cuối cùng `change-reviewer`
(model mạnh nhất) trên toàn bộ `master...HEAD`, một đợt sửa, một lần review lại phần sửa.

## Review theo task

| Task | Vòng | Phát hiện chính | Xử lý |
|---|---|---|---|
| F1 + F6 | 1 | Sạch. W: test F6 kiểm hành vi (wrapper `display: contents`) thay vì kiểm import | Chấp nhận — mục tiêu refactor là giữ nguyên hành vi |
| F2 | 4 | V1: thứ tự prepare/finish, phiên sau `dispose`, nuốt bản lưu của tab khác (W3 → lệch G6, xem changelog), rollback ngoài phạm vi. V2: 409 giả khi bản lưu bị sanitize. V3: cờ "diverged" nhận version vô điều kiện → ghi đè âm thầm → thay bằng `serverTree`. V4: **Critical** do chính controller yêu cầu thêm (undo → nạp cây server có thể nạp cây tab khác, xoá redo) → gỡ tính năng | Sạch sau vòng 4 |
| F3 | 1 | W1 dialog đóng được khi đang publish; W2 `onSettled` theo lần gọi bị bỏ khi unmount | Sửa, sạch |
| F4 | 1 | W1 xác nhận rollback treo sau 404; W2 publish/rollback không khoá chéo (R6); W3 thiếu test env rỗng; W4 test retry yếu; S: dùng `Dialog` chung | Sửa, sạch |
| F5 | 1 | W1 thiếu test rời trang/unmount; W2 thiếu test 409 từ editor; S: khe reset dialog, phase "preparing", thông điệp cũ | Sửa, sạch |

## Final review (toàn nhánh, `7f7a30a..017f383`)

Critical 0 · Warning 3 · Suggestion 6. Tenant, contract, ranh giới kiến trúc sạch; không `enum`; không copy Query vào
Zustand; không chạm `backend/` `contracts/` `config/` `generated/`.

| # | Phát hiện | Xử lý |
|---|---|---|
| W1 | `keepMine` không cập nhật `savedTree` ⇒ sau "Giữ bản của tôi" + PUT lỗi + undo về cây ban đầu: báo "Đã lưu", publish xuất bản nội dung tab khác (lỗi có từ trước, publish làm nó có hậu quả) | **Sửa** `0f41032` — `savedTree = serverTree = latest.tree`; 2 test mới (FAIL trước sửa) | |
| W2 | `packages/builder-renderer/CLAUDE.md` còn ghi Portal dùng `decorateWithId` | **Sửa** `38378fe` | |
| W3 | Plan §6 nói lint chặn `enum` (#19) nhưng `eslint.config.mjs` không có rule | **Sửa** `f2ec31e` — `no-restricted-syntax` `TSEnumDeclaration`, đột biến thử thấy lint fail; kèm `turbo.json` `lint.inputs` thêm `eslint.config.mjs` gốc (không có thì cache turbo bỏ qua rule mới) | |
| S1 | `publish-errors` hiện `message` của BE thay vì map theo `code` | **Giữ** — message BE nêu đúng prop thiếu ("Prop bắt buộc 'title' bị thiếu."), map theo mã sẽ mất thông tin; cùng cách với 422 theme |
| S2 | Plan còn ghi "chưa code", checkbox chưa tick | **Sửa** `7da99cd` | |
| S3 | Thiếu `review.md` | File này |
| S4 | Banner `05` có thể thêm WEBSITE-006 | Bỏ qua — `05` không mô tả UI publish Portal |
| S5 | Dòng 00-INDEX §4 nên có shop smoke còn trong DB + `:3000` 502 | **Sửa** `7da99cd` | |
| S6 | Lần publish đầu từ tổng quan, dialog thành công chưa có "Mở site" tới khi refetch | **Sửa** `f50064f` + test | |

Mục hoãn trong lúc làm: reviewer phân loại — chỉ F2(a) (= W1) phải sửa; F2(e) đã đóng nhờ F7 bước 4; còn lại để nợ
(ghi ở changelog "Chưa làm xong / nợ / theo dõi"). F5-S3 (test mở lại dialog thì reset) "nên thêm sớm" → làm luôn (`df62ac9`, đột biến bỏ `key` theo phiên ⇒ FAIL).

## Re-review đợt sửa (`017f383..7da99cd`)

`change-reviewer` ngữ cảnh mới: 6/6 mục + `turbo.json` **ADDRESSED / đúng**; không Critical, không Warning mới. Bất biến
autosave kiểm lại: chỉ "Đã lưu" khi cây local trùng cây server; không hai PUT song song; không ghi đè tab khác; không mất
dữ liệu local. Ba gợi ý → **nợ** (không có đợt sửa thứ hai):
- Test S6 (`-website.test.tsx` ~520–542) ghi đè resolver mỗi lần refetch — nên giữ mảng và thả hết.
- Comment bất biến `autosave-controller.ts:70–72` gộp keepMine vào nhóm "cây GET đã nhận version" — nên ghi rõ cây GET
  của keepMine chỉ là mốc so, không nằm trong store.
- `turbo.json`: `typecheck`/`test` có thể trúng cache khi đổi config gốc — cân nhắc `globalDependencies` (không phải hồi quy).

Sau đợt sửa: `pnpm lint && pnpm typecheck && pnpm test && pnpm build` exit 0 @ `7da99cd` (portal 385, web 269,
builder-components 535, builder-core 137, builder-renderer 36, theme-engine 49, api-sdk 12, shared 10); `pnpm check:docs`
OK; **F7 chạy lại @ `7da99cd`: 6/6 ĐẠT**.

## Kết luận

**SẴN SÀNG** cho Gate 2.

## Ma sát quy trình

1. **Quyền đọc file env/appsettings bị chặn** (classifier auto-mode) cho cả controller lẫn subagent: không dọn được
   dòng trống thừa trong `apps/portal/.env.*`, không đọc được cấu hình Redis của BE nên phải dừng hỏi người. Đề xuất:
   cho phép đọc `.env.development`/`.env.test` (không chứa bí mật) hoặc ghi cổng dịch vụ dev vào `backend/CLAUDE.md`.
2. **Hook `guard-bash` chặn nhầm heredoc** chỉ vì *nội dung văn bản* nhắc tới đường dẫn được bảo vệ (`contracts/**`…)
   trong file luật cho subagent. Phải chuyển sang công cụ Write.
3. **Plan dùng tiêu đề `### F1..F7`** nên script `task-brief` của skill SDD không tách được — brief phải dựng tay. Đề xuất
   template plan dùng tiêu đề khớp script, hoặc ghi chú trong skill `plan-task`.
4. **F2 mất 4 vòng review, một vòng do controller tự gây ra:** vòng 3 tôi yêu cầu thêm tính năng ngoài phạm vi (undo về
   bản đã lưu thì nạp cây sanitize) → sinh Critical ở vòng 4 → gỡ. Bài học: đợt sửa chỉ sửa phát hiện, không mở rộng.
5. **CRLF + prettier:** file ghi bằng Python ra CRLF; chạy `prettier --write` reflow dòng không liên quan (repo chưa
   format prettier). Đã đưa vào luật chung cho subagent: chỉ Edit/Write, không prettier trên file có sẵn.
6. **"Subagent chạy foreground"** không làm được đúng chữ: công cụ Agent luôn chạy subagent nền và báo khi xong. Controller
   chờ thông báo, không kết thúc lượt giữa chừng — hiệu quả tương đương, nhưng luật nên ghi theo hành vi thật.
7. **F7 cần môi trường mà session không tự dựng được trọn:** BE/Portal/web của người dùng đang chạy (không phải của
   agent); dev server `:3000` trả 502 mọi shop; email verify chỉ ra console của BE ⇒ phải bật một instance BE tạm để lấy
   link. Không có công cụ trình duyệt sẵn ⇒ cài `playwright-core` vào scratchpad, lái Edge hệ thống. Đề xuất: (a) seed
   Owner dev đã verify hoặc endpoint dev đọc email cuối; (b) đưa kịch bản smoke trình duyệt (playwright-core + msedge)
   thành công cụ dùng lại trong `tools/`.
8. **Reviewer chỉ-đọc không ghi được file báo cáo** (`change-reviewer` từ chối ghi `.md`) — controller phải chép tay. Đề
   xuất: brief reviewer chỉ yêu cầu trả về văn bản.
9. **Plan §6 khẳng định một lá chắn không tồn tại** (lint chặn `enum`) — chỉ final review mới bắt được. Đề xuất: plan
   ghi "lint hiện có" thì phải dẫn tên rule.
