/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { ShopKind } from './shopKind';
import type { ShopStatus } from './shopStatus';

export interface UpdateShopRequest {
  name: string;
  slug: string;
  kind: ShopKind;
  /** @nullable */
  externalUrl: string | null;
  status: ShopStatus;
}
