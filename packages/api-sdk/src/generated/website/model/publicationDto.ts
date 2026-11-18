/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */

export interface PublicationDto {
  id: string;
  version: number;
  publishedAt: string;
  /** @nullable */
  publishedByUserId: string | null;
  /** @nullable */
  note: string | null;
  isCurrent: boolean;
}
