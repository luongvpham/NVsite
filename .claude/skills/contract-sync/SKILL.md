---
name: contract-sync
description: So runtime OpenAPI với contract đã commit, phân loại khác biệt, sinh contract-diff.md cho người duyệt. Chạy ở cuối session BE, trước khi bàn giao FE.
---

# contract-sync

## Không được làm

- KHÔNG ghi vào `contracts/openapi/*.json`. Chỉ ghi `contracts/openapi/.staging/`.
- KHÔNG sửa `contract.lock`.
- KHÔNG tự quyết một khác biệt là "chấp nhận được" — trừ trường hợp promote ngay ở bước 6 (#89).

## Các bước

1. Export runtime OpenAPI theo document từng module: `pnpm contract:export [module...]` (cách đăng ký document: `backend/CLAUDE.md` — mục "OpenAPI").
2. Ghi ra `contracts/openapi/.staging/{module}.v{n}.json`.
3. Chạy `tools/contract-sync` — normalize cả hai bên (sort key đệ quy · bỏ `servers`/`info.version` · chuẩn hoá whitespace `description`), diff theo từng operation, phân loại `NEW_ENDPOINT` / `ADDITIVE` / `BREAKING` / `REMOVED` / `UNCHANGED`.
   Không tự thêm luật normalize khác mà không hỏi — lỏng quá bỏ lọt breaking change thật, chặt quá báo diff giả liên tục.
   ⚠️ Tool **mù với response dạng mảng** (`{type: array, items: {$ref}}`): đổi schema ở endpoint trả danh sách vẫn hiện `UNCHANGED`. Tự đối chiếu file staging cho các endpoint đó.
4. Chạy thử Orval trên file **staging** vào thư mục tạm (không đụng `packages/api-sdk/src/generated`): không được có type trùng kiểu `…Dto2`.
5. Sinh `Docs/tasks/{TASK-ID}/contract-diff.md` theo `DesignIdeal/ai-agent-development-workflow.md` §6. **Trước production (#89):** chỉ ghi endpoint/field mới · auth policy · giả định tự đặt · câu hỏi · bảng "Người duyệt đã quyết". Nhãn `BREAKING` của tool chỉ để tham khảo — không viết đoạn giải thích. `REMOVED` thì vẫn dừng và hỏi: gần như luôn là bug.
6. **Có câu hỏi, hoặc có `REMOVED`, hoặc đã deploy production** → dừng, chờ người duyệt (Gate 1), chưa viết `brief.md`. **Ngược lại** (chỉ thêm, không câu hỏi, trước production) → promote ngay, báo người duyệt đọc lại `contract-diff.md` sau.
