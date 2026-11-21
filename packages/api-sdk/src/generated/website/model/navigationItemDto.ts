/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { NavigationChildItemDto } from './navigationChildItemDto';

export interface NavigationItemDto {
  key: string;
  /** @nullable */
  label: string | null;
  /** @nullable */
  url: string | null;
  hidden: boolean;
  children: NavigationChildItemDto[];
}
