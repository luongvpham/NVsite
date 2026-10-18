/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { PageKind } from './pageKind';
import type { NullableOfSystemType } from './nullableOfSystemType';

export interface PageDto {
  id: string;
  kind: PageKind;
  systemType: NullableOfSystemType;
  /** @nullable */
  slug: string | null;
  title: string;
  /** @nullable */
  seoTitle: string | null;
  /** @nullable */
  seoDescription: string | null;
  /** @nullable */
  ogImageId: string | null;
  noIndex: boolean;
  isEnabled: boolean;
  sortOrder: number;
  isHomePage: boolean;
  isDirty: boolean;
  deletable: boolean;
  updatedAt: string;
}
