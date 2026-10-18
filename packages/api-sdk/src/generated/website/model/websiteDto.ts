/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { WebsiteStatus } from './websiteStatus';
import type { JsonElement } from './jsonElement';

export interface WebsiteDto {
  id: string;
  status: WebsiteStatus;
  defaultLocale: string;
  locales: string[];
  /** @nullable */
  defaultSeoTitle: string | null;
  /** @nullable */
  defaultSeoDescription: string | null;
  /** @nullable */
  defaultOgImageId: string | null;
  /** @nullable */
  faviconAssetId: string | null;
  trackingSnippets: JsonElement;
  homePageId: string;
}
