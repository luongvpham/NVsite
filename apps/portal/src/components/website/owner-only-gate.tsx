import type { ReactNode } from 'react';
import { useListShops } from '@vsite/api-sdk';
import { getErrorCode } from '@vsite/shared';
import { getErrorMessage } from '../../lib/error-messages';
import { isShopOwner } from '../../lib/shop-role';

/**
 * #100: Website + Media chỉ Owner. Role khác thấy thông báo thay cho màn — `children` **không mount**,
 * nên không phát request website nào (brief: "FE ẩn UI…, không chờ 403").
 */
export function OwnerOnlyGate({ shopId, children }: { shopId: string; children: ReactNode }) {
  const shopsQuery = useListShops();

  if (shopsQuery.isPending) return <p className="text-muted-foreground">Đang kiểm tra quyền...</p>;

  if (shopsQuery.isError) {
    return (
      <p role="alert" className="text-destructive">
        {getErrorMessage(getErrorCode(shopsQuery.error))}
      </p>
    );
  }

  if (!isShopOwner(shopsQuery.data, shopId)) {
    return <p className="text-muted-foreground">Chỉ chủ shop (Owner) mới quản lý được website của shop này.</p>;
  }

  return <>{children}</>;
}
