/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { NavigationGrandchildItemDto } from './navigationGrandchildItemDto';

export interface NavigationChildItemDto {
  key: string;
  /** @nullable */
  label: string | null;
  /** @nullable */
  url: string | null;
  hidden: boolean;
  children: NavigationGrandchildItemDto[];
}
