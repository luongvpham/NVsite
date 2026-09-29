import type { ShopSummaryDto } from '@vsite/api-sdk';

/**
 * Signal role hiện có duy nhất ở FE: `ShopSummaryDto.roleCode` từ `GET /shops` (SHOP-001 brief.md
 * §"Việc FE cần làm" #5, đã hiện ở `_authenticated.shops.index.tsx`). Không có endpoint/DTO nào
 * khác trả role cho một shop cụ thể (`ShopDto` — `GET /shops/{shopId}` — không có field role).
 *
 * `roleCode` là chuỗi tự do trong contract (không phải enum), nhưng SHOP-001 brief.md dùng nhất
 * quán chữ "Owner" (PATCH permission table, error message `SHOP_OWNER_REQUIRED`) và MEDIA-001
 * brief.md lặp lại đúng "Owner" cho `MEDIA_OWNER_REQUIRED`/upload logo — suy ra literal string BE
 * dùng là `"Owner"`, theo đúng quy ước PascalCase các enum-as-string khác (`ShopKind.Hosted`...).
 * Đây là suy luận từ tài liệu đã duyệt, không phải bịa signal mới (#19 vẫn giữ: không dùng TS enum).
 */
export const SHOP_OWNER_ROLE_CODE = 'Owner';

export function isShopOwner(shops: ShopSummaryDto[] | undefined, shopId: string): boolean {
  return shops?.find((shop) => shop.id === shopId)?.roleCode === SHOP_OWNER_ROLE_CODE;
}
