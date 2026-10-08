# Khuôn `plan.md`

Rút từ [`Docs/tasks/MEDIA-001/plan.md`](../../../../Docs/tasks/MEDIA-001/plan.md) (lane C đầy đủ) và
[`Docs/tasks/SHOP-001/plan.md`](../../../../Docs/tasks/SHOP-001/plan.md). Lane B: bỏ các mục đánh dấu
*(lane C)*, gộp thành một session.

Đường dẫn trong `plan.md` viết **tương đối với root repo** và là đường dẫn thật đã kiểm.

---

```markdown
# {ID} — {tên ngắn của tính năng}

> **ĐANG LẬP — chưa chốt** ← đổi thành **Đã chốt {YYYY-MM-DD} — chưa code** ở pha 6
>
> **Lane {A/B/C}.** {một câu lý do: module mới? chạm contract? chạm Registry/config?}
> Cổng: {duyệt Registry →} Gate 1 → Gate 2. Tuần tự {S0 →} S1 → S2.
>
> **Người thực thi dùng skill:** `superpowers:subagent-driven-development` hoặc
> `superpowers:executing-plans`. Mỗi bước có checkbox `- [ ]`.

**Mục tiêu:** {2–3 dòng, bằng lời nghiệp vụ: ai làm được gì sau task này}
**Nhu cầu dữ liệu:** [`data-needs.md`](data-needs.md) · **Spec:** {mục `DesignIdeal/…` theo `00-INDEX`}
· **Quyết định liên quan:** {#N, …}

## 1. Yêu cầu (đã chốt với người)
- Người dùng / vai trò:
- Điểm chạm (màn hình, endpoint):
- Xong nghĩa là:
- **Không làm trong task này:**

## 2. Hiện trạng (khảo sát {ngày})
| Khả năng | Trạng thái | Ở đâu | Ghi chú |
|---|---|---|---|

### Phát hiện khi khảo sát
{lệch tài liệu ↔ code, nợ kỹ thuật chạm phạm vi, test thiếu — mỗi dòng có link}

## 3. Flow
### Flow 1: {tên}
{các bước ✅/🟡/❌ kèm link, nhánh lỗi và hành vi đã chốt}

## 4. Đã chốt trong session lập plan
| # | Chốt |
|---|---|
| #N | {quyết định mới đã cấp số ở DECISIONS.md} |
| — | {chi tiết thực thi đã chốt, không cần số} |

## 5. Trình tự session *(lane C)*
{S0/S1/S2 là gì, vì sao thứ tự này, cổng giữa các session}

## 6. Ràng buộc toàn cục
{invariant áp dụng cho mọi task: tenant #21 cụ thể cho task này, #19, generated files, hook…}

## 7. Review focus — lỗi dễ lọt khi spec im lặng
| # | Tình huống | Hành vi đúng | Test ở |
|---|---|---|---|
| R1 | | | T? |

## 8. Sơ đồ file *(lane C)*
{cây file sẽ tạo/sửa, chia theo project/app}

## 9. Session S1: Backend
### T1: {tên}
{mô tả ngắn, chữ ký/khung code nếu giúp người thực thi khỏi đoán}
- [ ] Test trước: `{đường dẫn test}` — {các ca}
- [ ] FAIL → hiện thực → PASS (`dotnet test …`)
- [ ] Commit `{type}({module}): {mô tả} ({#N})`

### T{n}: Kết thúc S1
- [ ] `pnpm contract:export` → skill `contract-sync` → `contract-diff.md` → Gate 1 (#89)
- [ ] `changelog.md` + banner `STATUS` (DoD 9) · nợ Docker → `Docs/DOCKER-TEST-DEBT.md`

## 10. Session S2: Frontend (mở SAU Gate 1)
### F1: {tên}
- [ ] … (MSW trước, loading · error · empty)
- [ ] Smoke chạy app thật sau F1 (workflow §8)

## 11. Đóng task (trước Gate 2)
- [ ] Integration tắt MSW · `change-reviewer` · `review.md` (kèm "Ma sát quy trình")

## 12. Giả định tôi đã tự đặt (chưa hỏi, cần duyệt)
| # | Giả định | Vì sao | Nếu sai thì |
|---|---|---|---|

## 13. Verification tổng
{lệnh + tiêu chí đo được}
```
