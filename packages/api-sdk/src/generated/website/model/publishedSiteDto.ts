/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { WebsiteStatus } from './websiteStatus';
import type { PublishedShopDto } from './publishedShopDto';
import type { PublishedSiteDtoPublication } from './publishedSiteDtoPublication';

export interface PublishedSiteDto {
  status: WebsiteStatus;
  shop: PublishedShopDto;
  /** @nullable */
  publication: PublishedSiteDtoPublication;
}
