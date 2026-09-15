/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { ShopKind } from './shopKind';
import type { ShopStatus } from './shopStatus';

export interface ShopSummaryDto {
  id: string;
  name: string;
  slug: string;
  kind: ShopKind;
  status: ShopStatus;
  roleCode: string;
}
