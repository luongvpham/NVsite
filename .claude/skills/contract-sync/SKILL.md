---
name: contract-sync
description: So runtime OpenAPI với contract đã commit, phân loại khác biệt, sinh contract-diff.md cho người duyệt. Chạy ở cuối session BE, trước khi bàn giao FE.
---

# contract-sync

## Không được làm

- KHÔNG ghi vào `contracts/openapi/*.json`. Chỉ ghi `contracts/openapi/.staging/`.
- KHÔNG sửa `contract.lock`.
- KHÔNG tự quyết một khác biệt là "chấp nhận được".

## Các bước

1. Export runtime OpenAPI theo document từng module (xem `backend/CLAUDE.md` — mục "OpenAPI — thư viện và cách xuất document theo module").
2. Ghi ra `contracts/openapi/.staging/{module}.v{n}.json`.
3. Chạy `tools/contract-sync` — normalize cả hai bên (sort key đệ quy · bỏ `servers`/`info.version` · chuẩn hoá whitespace `description`), diff theo từng operation, phân loại `NEW_ENDPOINT` / `ADDITIVE` / `BREAKING` / `REMOVED` / `UNCHANGED`.
4. Sinh `docs/tasks/{TASK-ID}/contract-diff.md` theo template ở `DesignIdeal/ai-agent-development-workflow.md` §9.
5. Có `BREAKING` hoặc `REMOVED` → nêu lên đầu file, ghi rõ nghi ngờ là bug implementation hay là thay đổi có chủ ý. Mặc định coi là bug.
6. Dừng lại. Báo cáo và chờ người duyệt (Gate 1). Không đi tiếp sang `brief.md`.
