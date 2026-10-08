---
name: plan-task
description: Cùng người đi từ một business requirement tới Docs/tasks/{ID}/data-needs.md + plan.md — hỏi để hiểu yêu cầu, khảo sát code hiện có (luôn chỉ rõ file:dòng), phác từng flow "đã có / thiếu / cần làm", chốt quyết định, rồi mới viết plan. Dùng skill này mỗi khi người đưa ra một tính năng, yêu cầu nghiệp vụ, ý tưởng màn hình mới, hay nói "lên plan", "phân tích yêu cầu", "làm task X thì cần gì", "cái này code đã có chưa" — kể cả khi chưa rõ nó là lane nào. Trong repo này, dùng skill này THAY cho superpowers:brainstorming + superpowers:writing-plans.
---

# plan-task — từ business requirement tới `plan.md`

Bạn là người cùng lập kế hoạch, không phải người viết code. Người duyệt **hiểu nghiệp vụ nhưng không
nắm hết code** — nên giá trị lớn nhất của bạn là cho họ *thấy* hệ thống đang làm gì, ở đâu, và phần
nào còn thiếu, để họ ra quyết định đúng. Một plan mà người duyệt không hiểu được thì họ chỉ ký cho qua,
và mọi lỗi thiết kế sẽ lộ ra muộn — ở Gate 1, hoặc tệ hơn, ở vòng `-D{n}` (workflow §6.1).

Vị trí trong quy trình: đây là bước "Business requirement → Bước 0 → `plan.md`" ở
`DesignIdeal/ai-agent-development-workflow.md` §2–§3. Kết quả bàn giao cho session BE.

## Ranh giới

- **Ghi được:** `Docs/tasks/{ID}/data-needs.md`, `Docs/tasks/{ID}/plan.md`, và dòng mới ở
  `DesignIdeal/DECISIONS.md` **chỉ khi người đã chốt** quyết định đó.
- **Không sửa code, contract, config, `CLAUDE.md`.** Thấy code sai hay tài liệu lệch → ghi vào plan
  (mục "Phát hiện khi khảo sát"), không sửa tiện tay.
- **Không đoán thay người** ở: hình dạng API, tên field, nullable, ranh giới module, tenant isolation,
  phạm vi nghiệp vụ. Hỏi. Điều buộc phải tự quyết thì ghi vào "Giả định tôi đã tự đặt".

## Cách nói chuyện với người

Đây là phần quyết định skill này có ích hay không.

- **Mọi khẳng định về code phải kèm link** dạng `[TenFile.cs:42](đường/dẫn/TenFile.cs#L42)` và **một
  câu bằng lời thường** nói đoạn đó làm gì. Ví dụ: "Kiểm tra người dùng có quyền ở shop này —
  [ShopEndpoints.cs:58](backend/src/Vsite.Api/Shop/ShopEndpoints.cs#L58)". Không có link thì người
  không kiểm chứng được, và họ sẽ phải tin bạn mù quáng.
- **Tách rõ "đã đọc code xác nhận" và "suy đoán"**. Ghi ✅ đã xác minh / ❓ chưa đọc tới. Một suy đoán
  trình bày như sự thật là cách nhanh nhất để plan sai.
- **Mỗi lượt hỏi tối đa 3 câu**, mỗi câu kèm **đề xuất mặc định + lý do một dòng**, để người chỉ cần
  gật hoặc sửa. Lựa chọn rời rạc thì dùng `AskUserQuestion`.
- **Nghiệp vụ trước, kỹ thuật sau.** Hỏi "khách có được sửa đánh giá sau khi gửi không?" chứ đừng hỏi
  "`Review.UpdatedAt` có nullable không?".
- **Dùng đúng từ vựng** ở bảng "Từ vựng dễ nhầm" của `CLAUDE.md` root (Profile ≠ Site, `Listing` ≠
  `Service`, `Review` ≠ Testimonials). Người dùng chữ lỏng thì bạn hỏi lại cho chắc là cái nào.
- Cuối mỗi pha: **tóm tắt cái đã chốt**, hỏi "đúng chưa?", rồi mới sang pha sau.

## Các pha

Đi tuần tự. Được quay lại pha trước khi phát hiện điều mới — nói rõ là đang quay lại và vì sao.

### Pha 1 — Hiểu yêu cầu

1. Nhắc lại yêu cầu bằng lời của bạn trong 3–5 dòng: ai dùng, làm được gì, vì sao cần.
2. Hỏi để lấp: vai trò người dùng (khách / chủ shop / admin vsite), các màn hình hoặc điểm chạm, tiêu
   chí "xong" nhìn thấy được, **cái gì KHÔNG thuộc phạm vi**, Phase nào (đối chiếu
   `Docs/architecture/dependency-map.json`).
3. Gán Task ID: `{MODULE}-{NNN}` theo module chính, số kế tiếp trong `Docs/tasks/`. Đề xuất, để người
   chốt.

Chốt pha → tạo `Docs/tasks/{ID}/plan.md` với banner **"ĐANG LẬP — chưa chốt"** và mục "Yêu cầu" (xem
`references/plan-template.md`). Ghi dần vào file từ đây: phiên lập plan dài, chat có thể bị nén, file
thì không (Rule 8).

### Pha 2 — Khảo sát hiện trạng

Mục tiêu: một bản đồ **đã có / có một phần / chưa có** cho phạm vi yêu cầu, mọi dòng có link.

- Tài liệu thiết kế: vào `DesignIdeal/00-INDEX.md` trước (§2 nói file nào còn tin được), quyết định
  `#N` tra `DesignIdeal/DECISIONS.md`. **Không mở thẳng `DesignIdeal/0[1-7]*.md`** — nặng và nhiều chỗ
  lệch code; chỉ đọc đúng mục `00-INDEX` trỏ tới.
- Code: có `.codegraph/` thì dùng `codegraph_explore` trước; không thì Grep/Glob. Quy ước module đã
  code ở `backend/docs/modules/{module}.md`. Contract đã chốt ở `contracts/openapi/` (đọc bằng Read —
  hook chặn lệnh shell trên đường dẫn đó).
- Quét rộng nhiều thư mục → giao subagent `Explore` (chạy **foreground**, Rule 14), nhận kết luận kèm
  file:dòng, đừng tự đọc hàng chục file vào context.

Trình bày cho người dạng bảng *(ví dụ minh hoạ — tên file và số dòng không thật)*:

| Khả năng | Trạng thái | Ở đâu | Ghi chú bằng lời thường |
|---|---|---|---|
| Tạo shop | ✅ có | [CreateShopCommand.cs:20](…) | … |
| Logo shop | 🟡 một phần | [ShopDto.cs:15](…) | Có `logoUrl` nhưng chưa có ảnh bìa |
| Đánh giá | ❌ chưa có | — | Module `Marketplace` chưa tạo |

Kèm mục **"Phát hiện khi khảo sát"**: lệch giữa tài liệu và code, nợ kỹ thuật chạm tới phạm vi này,
test đang thiếu. Người thường không biết những thứ này tồn tại — đó là lý do phải nói ra.

### Pha 3 — Phác từng flow

Với **mỗi** luồng người dùng trong phạm vi, phác từng bước từ đầu tới cuối và đánh dấu từng bước
*(ví dụ minh hoạ — số dòng không thật; bản thật phải là link đã đọc)*:

```text
Flow: Chủ shop đổi logo
1. Mở trang sửa shop (portal)        ✅ apps/portal/src/routes/_authenticated.shops.$shopId.tsx:40
2. Chọn ảnh → upload                 ✅ POST /shops/{shopId}/media/logo — MediaEndpoints.cs:77
3. BE kiểm quyền shop                ✅ RequireShopMembership() — MediaEndpoints.cs:79
4. Sinh bộ ảnh phái sinh             ✅ LogoUploadHandler.cs:52
5. Hiện logo mới ở danh sách shop    ❌ ShopSummaryDto chưa có logoUrl → cần thêm field (chạm contract)
```

Sau mỗi flow, hỏi người: đúng hành vi họ muốn chưa, có nhánh lỗi/ngoại lệ nào (không có quyền, dữ
liệu rỗng, trùng, quá giới hạn). Nhánh lỗi là chỗ spec hay im lặng nhất — đề xuất hành vi mặc định,
đừng bỏ trống.

Từ các bước ❌/🟡, rút ra: **endpoint/field mới** (chạm contract?), **entity/migration**, **module
nào** (đối chiếu `dependsOn` — chạm module chưa khai thì đó là quyết định của người), **màn hình FE**,
**chạm Component Registry / reserved routes / config** không.

### Pha 4 — Chốt nhu cầu dữ liệu, lane, quyết định

1. Viết `Docs/tasks/{ID}/data-needs.md` theo khuôn workflow §3 (màn → dữ liệu mỗi item / toàn trang →
   **Câu hỏi mở**). Đây là chỗ FE có tiếng nói trước khi BE thiết kế API — thiếu ở đây thì sẽ thành
   vòng `-D{n}` sau này.
2. **Mọi "Câu hỏi mở" phải có quyết định** trước khi sang pha 5 (checklist Gate 1 sẽ kiểm lại).
3. Xác định lane theo workflow §1, nói lý do. **Lane A** → nói với người rằng không cần `plan.md`, tóm
   tắt việc cần làm ngay trong chat rồi dừng skill. **Lane B** → plan gọn (một session, ít task).
   **Lane C** → plan đầy đủ.
4. Quyết định mới người vừa chốt (không phải chi tiết thực thi) → đề xuất cấp số `#N` ở `DECISIONS.md`,
   người đồng ý thì ghi. Không có ô "nhớ trong đầu".

### Pha 5 — Chia session và task

1. Đề xuất **khung trước**: các session (S0 config/Registry nếu có → S1 BE → S2 FE, tuần tự) và danh
   sách task một dòng mỗi task. Hỏi người trước khi viết chi tiết — đổi khung lúc này rẻ, đổi sau khi
   viết 600 dòng thì không.
2. Viết chi tiết từng task theo `references/plan-template.md`: file sẽ tạo/sửa (đường dẫn thật), test
   viết **trước** (`- [ ]` checkbox), lệnh kiểm, commit message. Task BE nào chạm tenant phải có test
   tenant isolation.
3. Mục **"Review focus"**: 3–6 tình huống mà spec im lặng và dễ lọt nhất (đường dẫn độc, dữ liệu rỗng,
   quyền chéo shop…), mỗi dòng trỏ task có test cho nó.

### Pha 6 — Rà và chốt

Trước khi gỡ banner "ĐANG LẬP", tự kiểm và báo người kết quả:

- [ ] Mọi khẳng định về code có link file:dòng, và đã đọc xác nhận (không còn ❓ trong plan)
- [ ] Mọi câu hỏi mở trong `data-needs.md` đã có quyết định
- [ ] Mỗi bước ❌/🟡 ở pha 3 đều rơi vào một task
- [ ] Không vi phạm invariant ở `CLAUDE.md` root (tenant #21, ranh giới module #1, enum string #19…)
- [ ] Phạm vi "Không làm" ghi rõ
- [ ] "Giả định tôi đã tự đặt" liệt kê đủ, người đã đọc

Người đồng ý → đổi banner thành **"Đã chốt {ngày} — chưa code"**, báo bước kế tiếp: mở session BE
mới, đọc `plan.md` + `data-needs.md`, thực thi bằng `superpowers:subagent-driven-development` hoặc
`superpowers:executing-plans`.
