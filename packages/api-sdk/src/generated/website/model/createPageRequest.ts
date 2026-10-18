/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */

export interface CreatePageRequest {
  slug: string;
  title: string;
  /** @nullable */
  seoTitle: string | null;
  /** @nullable */
  seoDescription: string | null;
  /** @nullable */
  ogImageId: string | null;
  noIndex?: boolean;
  isEnabled?: boolean;
}
