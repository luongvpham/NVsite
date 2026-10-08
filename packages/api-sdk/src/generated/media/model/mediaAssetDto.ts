/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */

export interface MediaAssetDto {
  id: string;
  storageKey: string;
  mimeType: string;
  width: number;
  height: number;
  sizeBytes: number;
  /** @nullable */
  altText: string | null;
  focalPointX: number;
  focalPointY: number;
  /** @nullable */
  originalFileName: string | null;
  /** @nullable */
  folder: string | null;
  isInLibrary: boolean;
  /** @nullable */
  preset: string | null;
  /** @nullable */
  sourceAssetId: string | null;
  createdAt: string;
}
