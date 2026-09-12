# vsite

Nền tảng SaaS Việt Nam kết hợp **danh bạ dịch vụ địa phương** (tìm kiếm theo bản đồ, shop tự nguyện đăng tin) và **trình dựng website** cho shop nhỏ (spa, salon, phòng khám).

vsite **không xử lý giao dịch**. Đây là kênh khám phá và dẫn khách. Hai luồng doanh thu độc lập: phí duy trì website, và phí đăng tin/quảng cáo.

---

## Đọc tài liệu ở đâu

Toàn bộ thiết kế nằm trong `DesignIdeal/`. **Đừng đoán — tra bảng này rồi đọc đúng file.**

| Câu hỏi thuộc về | Đọc |
|---|---|
| Sản phẩm, tính năng, lộ trình Phase | `DesignIdeal/01-project-ideal.md` |
| **Mọi quyết định kiến trúc (#1–#68)**, tech stack, ràng buộc dependency | `DesignIdeal/02-tech-stack-and-decision.md` |
| `User`, `ExternalLogin`, `UserShop`, `Role`, `PendingRegistration` | `DesignIdeal/03-identity-entity-design.md` |
| `Shop` (đầy đủ), `ServiceCategory`, `Listing`, `Review`, `Lead` | `DesignIdeal/04-listing-and-review-design.md` |
| Website builder, `Page`, Component Tree, `MediaAsset`, `Product` | `DesignIdeal/05-website-builder-and-product-design.md` |
| `Service`, `ShopServiceGroup` | `DesignIdeal/06-service-design.md` |
| Component manifest schema, codegen registry | `DesignIdeal/07-component-manifest-schema.md` |
| **Quy trình làm việc, contract, cổng duyệt** | `DesignIdeal/ai-agent-development-workflow.md` |

**Khi hai tài liệu mô tả cùng một entity, tài liệu thiết kế chi tiết thắng** (Quyết định #39.5). `02` là nguồn sự thật cho quyết định kiến trúc; `03`–`07` là nguồn sự thật cho entity tương ứng.

**Nhưng tài liệu `DesignIdeal/` mô tả Ý ĐỊNH lúc thiết kế, không phải bản đã chạy được.** Sau khi một
task hoàn thành, thực thi thật có thể lệch so với đúng file đã đọc ở trên — vì lý do kỹ thuật phát
sinh lúc code mà lúc viết tài liệu chưa thấy được. Trước khi dựa vào một file `DesignIdeal/*.md` để
sửa/mở rộng code đã có, **kiểm tra `docs/tasks/<TASK-ID>/changelog.md` có tồn tại không** (tên
`TASK-ID` tra ở đầu file tài liệu tương ứng, nếu có dòng "⚠️ Đã hiện thực hoá") — file đó liệt kê
từng điểm lệch với nguyên nhân, chia rõ "lệch có chủ đích, giữ nguyên" và "chưa làm xong, còn nợ".
Đừng "sửa lại cho đúng tài liệu" một chỗ mà changelog đã ghi là lệch có chủ đích.

---

## Quy trình

Chi tiết ở `DesignIdeal/ai-agent-development-workflow.md`. Tóm tắt:

**Tuần tự BE → sync contract → FE.** Không chạy song song.

| Lane | Khi nào | Cổng |
|---|---|---|
| **A — Fast** (mặc định) | Không chạm contract: sửa bug, đổi UI, refactor trong một module | Gate 2 |
| **B — Contract, một tay** | Chạm contract nhưng nhỏ | Gate 1 + Gate 2 |
| **C — Tuần tự đầy đủ** | Module mới, màn hình lớn, chạm nhiều module | Gate 1 + Gate 2, có `brief.md` |

**Chọn nhầm lane thì dừng và báo**, không âm thầm đi tiếp.

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
3. **BREAKING mặc định là bug implementation**, không phải lý do tạo `v2`.
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
| **Shop Profile** (`vsite.vn/shop/{slug}`) vs **Shop Site** (`spa-abc.com`, `{slug}.vsite.vn`) | Profile do vsite render bằng mẫu thống nhất, **có hiển thị `Review`**. Site là output của `builder-renderer`, shop kiểm soát nội dung, **tuyệt đối không hiển thị `Review`**. Cả hai đều nằm trong `apps/web`. Shop Profile **không** dùng `builder-renderer` |
| **`ServiceCategory`** vs **`ShopProductCategory`** | Cái đầu là taxonomy toàn cục do vsite quản trị, dùng cho tìm kiếm marketplace, shop chỉ được **chọn** node lá. Cái sau là cây do shop tự vẽ, chỉ dùng để điều hướng trên website riêng |
| **`Listing`** vs **`Service`** | `Listing` là tin đăng marketplace (Phase 1). `Service` là dịch vụ hiển thị trên website shop (Phase 2). **Không auto-map giữa hai cái** (#38) |
| **`Review`** vs **Testimonials** | `Review` là đánh giá thật của khách, neo vào `Listing`. Testimonials là nội dung shop tự nhập trong builder. Không trộn |

---

## Anti-pattern cấm kế thừa từ VSite .NET Framework 4.8 (#36)

Hệ cũ được dùng làm tham chiếu. Đọc code cũ để hiểu nghiệp vụ thì được; **sao chép những pattern dưới đây thì không.**

| Cấm | Thay bằng |
|---|---|
| Một cột JSON `SiteConfig` chứa category + attribute + menu + home + contact | Tách bảng + `RowVersion`. Component Tree vẫn là JSON nhưng **per-page, có version** |
| `CategoryID` kiểu int không FK, match bằng giá trị | UUID + FK thật, composite FK khi cần kiểm ownership |
| Bitmask `Int64` cho tags/promotions | Bảng nối hoặc `text[]` + GIN index |
| Cache toàn bộ dữ liệu tenant trong memory rồi filter bằng LINQ | Elasticsearch (#26) |
| DTO kiểu `{ object Item; object Item2; }` | DTO có kiểu rõ ràng từng trang; SEO là field `seo` lồng bên trong |
| Không validate server-side khi lưu dữ liệu động | FluentValidation theo schema (#19) |
| Tham số chết trong chữ ký controller | Dọn ngay khi phát hiện — agent sẽ giả định tham số có tác dụng và viết code dựa trên đó |
| Một action gánh nhiều nhánh rẽ theo enum | Mỗi loại trang một route/handler riêng |

---

## Khi không chắc

**Hỏi, đừng đoán.** Đặc biệt với: hình dạng API, tên field, ngữ nghĩa nullable, ranh giới module, và bất cứ thứ gì chạm tới tenant isolation.

Nếu phải tự quyết một điều gì đó không hỏi được, **ghi lại nó vào mục "Giả định tôi đã tự đặt"** trong báo cáo cuối. Thứ tự quyết mà không nghĩ đến việc hỏi mới là chỗ hay sai.
