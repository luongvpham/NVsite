/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { ShopKind } from './shopKind';

export interface CreateShopRequest {
  name: string;
  slug: string;
  kind: ShopKind;
  /** @nullable */
  externalUrl: string | null;
}
