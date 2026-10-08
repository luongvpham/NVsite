/**
 * GENERATED — DO NOT EDIT (pnpm gen:api)
 */
import type { IFormFile } from './iFormFile';

export interface UploadToSlotForm {
  file?: IFormFile;
  preset?: string;
  focalX?: number;
  focalY?: number;
  saveToLibrary?: boolean;
  /** @nullable */
  altText?: string | null;
}
