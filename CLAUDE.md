# vsite

Nền tảng SaaS Việt Nam kết hợp **danh bạ dịch vụ địa phương** (tìm kiếm theo bản đồ, shop tự nguyện đăng tin) và **trình dựng website** cho shop nhỏ (spa, salon, phòng khám).

vsite **không xử lý giao dịch** — đây là kênh khám phá và dẫn khách. Hai luồng doanh thu độc lập: phí duy trì website, và phí đăng tin/quảng cáo.

---

## Cần nền thiết kế thì đọc ở đâu

**Cần** (hình dạng API, tên field, ngữ nghĩa nullable, ranh giới module, một quyết định `#N`):
→ đọc [`DesignIdeal/00-INDEX.md`](DesignIdeal/00-INDEX.md) **trước**. Nó có bảng tra, và §2 nói file nào còn tin được.

**KHÔNG mở thẳng `DesignIdeal/0[1-7]*.md`.** Chúng nặng 23–75 KB và nhiều chỗ đã lệch code — mở nhầm `02` là mất ~26k token. Tra quyết định `#N` thì vào [`DesignIdeal/DECISIONS.md`](DesignIdeal/DECISIONS.md), đừng quét `02`.

**Không cần** (sửa CSS, đổi text, refactor trong một hàm, fix lỗi có stack trace rõ):
→ **không đọc gì cả.** Vào thẳng code.

`CLAUDE.md` của từng thư mục (`backend/`, `apps/*/`, `packages/*/`) trỏ tiếp tới đúng tài liệu của phạm vi đó — chúng tự nạp khi bạn chạm file trong thư mục, không cần tìm.

---

## Quy trình

Chi tiết ở `DesignIdeal/ai-agent-development-workflow.md`. **Tuần tự BE → sync contract → FE.** Không chạy song song.

| Lane | Khi nào | Cổng |
|---|---|---|
| **A — Fast** (mặc định) | Không chạm contract: sửa bug, đổi UI, refactor trong một module | Gate 2 |
| **B — Contract, một tay** | Chạm contract nhưng nhỏ | Gate 1 + Gate 2 |
| **C — Tuần tự đầy đủ** | Module mới, màn hình lớn, chạm nhiều module | Gate 1 + Gate 2, có `brief.md` |

**Chọn nhầm lane thì dừng và báo**, không âm thầm đi tiếp.

**Task chạm code chưa có `Docs/tasks/{ID}/changelog.md` là task CHƯA XONG** — kể cả khi code chạy và test xanh. Xem Definition of Done ở `backend/CLAUDE.md`.

**⚠️ Trước khi báo task backend "xong":** đọc `Docs/DOCKER-TEST-DEBT.md`. Không có Docker daemon thì test cần Testcontainers **không** được coi là "bỏ qua" — ghi vào file đó theo đúng quy ước. Máy CÓ Docker thì đọc file đó trước, có thể đang có nợ chờ bạn chạy giúp.

---

## Contract — ba loại, đều phải người duyệt

| Contract | File | Bảo vệ bằng |
|---|---|---|
| API boundary | `contracts/openapi/{module}.v{n}.json` | `contracts/contract.lock` (sha256) |
| Component Registry | `packages/builder-components/registry/*.manifest.ts` | `registry.lock.json` + `check-additive.ts` |
| Reserved routes | `config/reserved-routes.json` | Test khẳng định FE và BE đọc cùng file |

### Quy tắc tuyệt đối

1. **Không bao giờ ghi thẳng vào `contracts/openapi/*.json`.** Script sync chỉ ghi `contracts/openapi/.staging/`. Promote là hành động riêng, sau khi người duyệt.
2. **Không bao giờ overwrite contract bằng runtime swagger.** Swagger *đề xuất*, người *duyệt*, contract *chốt*.
3. **BREAKING: chưa deploy production thì cứ sửa** — sửa BE, chạy lại `pnpm gen:api`, sửa lỗi compile FE. Không tạo `v2`. **Từ lần deploy production đầu tiên**, luật đảo lại: BREAKING mặc định là bug implementation. Khung cảnh báo đầy đủ ở `DesignIdeal/ai-agent-development-workflow.md` §6.
4. **Contract sai hoặc thiếu → DỪNG và báo.** Không tự sửa, không làm tạm rồi sửa sau.
5. **File generated không sửa tay**: `packages/api-sdk/src/generated/**`, `packages/builder-components/generated/**`.

---

## Invariant toàn hệ

Vi phạm những điều này là lỗi thật, không phải chuyện code style.

- **Tenant isolation (#21)** — xem `backend/CLAUDE.md`. Mọi entity tenant-scoped có `ShopId`; mọi query qua Global Query Filter; ownership validate **trong câu query**; **không bao giờ** nhận `ShopId` từ request body.
- **Module không reference project của module khác (#1).** Cross-module qua Integration Event hoặc Public Contract interface.
- **`packages/` không import từ `apps/`.** Chiều phụ thuộc một hướng.
- **`builder-renderer` không import `builder-core`**, và phải **isomorphic** — không đụng `window`/`document` trong logic render chính (#23).
- **Reserved routes có đúng một nguồn** là `config/reserved-routes.json` (#24). Không viết tay danh sách thứ hai ở FE hay BE.
- **Enum serialize dạng string** ở BE; TS dùng `const object + union type`, **không** dùng `enum` của TypeScript (#19).

Nguyên tắc nền: **codegen > skill > CLAUDE.md > hy vọng agent nhớ** (#17). Chỗ nào đẩy được lên codegen hoặc lock file thì đừng để ở tầng tài liệu.

---

## Từ vựng dễ nhầm — đọc kỹ trước khi code

| Cặp khái niệm | Khác nhau ở đâu |
|---|---|
| **Shop Profile** (`vsite.vn/shop/{slug}`) vs **Shop Site** (`spa-abc.com`, `{slug}.vsite.vn`) | Profile do vsite render bằng mẫu thống nhất, **có `Review`**. Site là output của `builder-renderer`, shop kiểm soát nội dung, **tuyệt đối không có `Review`**. Cả hai ở trong `apps/web`; Profile **không** dùng `builder-renderer` |
| **`ServiceCategory`** vs **`ShopProductCategory`** | Taxonomy toàn cục do vsite quản trị, dùng cho tìm kiếm marketplace, shop chỉ được **chọn** node lá — vs — cây do shop tự vẽ, chỉ để điều hướng trên website riêng |
| **`Listing`** vs **`Service`** | Tin đăng marketplace (Phase 1) — vs — dịch vụ trên website shop (Phase 2). **Không auto-map** (#38) |
| **`Review`** vs **Testimonials** | Đánh giá thật của khách, neo vào `Listing` — vs — nội dung shop tự nhập trong builder. Không trộn |

---

## Khi không chắc

**Hỏi, đừng đoán.** Đặc biệt với: hình dạng API, tên field, ngữ nghĩa nullable, ranh giới module, và bất cứ thứ gì chạm tới tenant isolation.

Nếu phải tự quyết một điều không hỏi được, **ghi vào mục "Giả định tôi đã tự đặt"** trong báo cáo cuối. Thứ tự quyết mà không nghĩ đến việc hỏi mới là chỗ hay sai.
