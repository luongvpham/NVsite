/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { JsonElement } from './jsonElement';
import type { PageDraftPageDto } from './pageDraftPageDto';

export interface PageDraftDto {
  tree: JsonElement;
  version: number;
  isDirty: boolean;
  updatedAt: string;
  page: PageDraftPageDto;
}
