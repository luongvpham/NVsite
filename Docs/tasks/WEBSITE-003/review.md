# WEBSITE-003 — review (change-reviewer, trước Gate 2)

Range `cf4efbd..76d1020`. Reviewer chỉ đọc, không chạy test.

| # | Mức | Phát hiện | Xử lý |
|---|---|---|---|
| C1 | Critical | Banner `07` còn nói `resolveUrl` stub / `vsite.vn/{slug}` | ✅ sửa banner + thân §3/§7.3 |
| C2 | Critical | Banner `05` còn ghi renderer/locale "chưa code" | ✅ sửa banner |
| I1 | Important | Preview chung query key draft với editor nhưng không `gcTime: 0` → cache draft cũ sống 5 phút | ✅ `gcTime: 0` + test |
| I2 | Important | Anchor không cuộn được vì wrapper `display: contents` | ✅ cuộn tới `firstElementChild` + test |
| M1–M11 | Minor | Xem changelog "Chưa làm xong / theo dõi" mục 5 | hoãn |
| W1–W3 | Warning | Follow-up chưa ai theo dõi; banner plan; #11 chưa ghi lệch | W1/W2 ✅ ghi changelog + banner plan; W3 còn mở (DECISIONS #11 chưa ghi lệch `{slug}.vsite.vn`) |

## Ma sát quy trình
| Ma sát | Nguyên cớ gốc | Mức | Bằng chứng | Đề xuất |
|---|---|---|---|---|
| Banner 05/07 bị bỏ sót dù plan T5 nhắc | `check:docs` chỉ kiểm hình thức banner, không kiểm nội dung lệch | B | C1, C2 | Checklist T5: grep symbol bị đổi (`resolveUrl`, `basePath`) trong `DesignIdeal/0*.md` |
| Invariant "không dùng chung cache draft với editor" chỉ nằm trong một comment | Không có test/lint giữ | B | I1 | Test "draft trang khác không còn trong cache" (đã thêm) |
| Danh sách fixture viết tay | Renderer không có `@types/node` | C | M7 | `import.meta.glob` nếu có types vite |
| Hành vi chỉ có ở trình duyệt (scrollIntoView) bị jsdom che | Code dùng `?.()` để vượt jsdom | C | I2 | Ghi vào danh sách nghiệm thu tay |
