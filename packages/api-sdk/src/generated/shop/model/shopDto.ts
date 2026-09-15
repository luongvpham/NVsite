/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { ShopKind } from './shopKind';
import type { ShopStatus } from './shopStatus';

export interface ShopDto {
  id: string;
  name: string;
  slug: string;
  kind: ShopKind;
  /** @nullable */
  externalUrl: string | null;
  status: ShopStatus;
}
