import { z } from 'zod';
import { uploadToLibraryBody, uploadToSlotBody } from '@vsite/api-sdk';

/**
 * Ràng buộc client-side cho upload ảnh (MEDIA-001 brief.md §"Việc FE cần làm" F2, §"Ràng buộc").
 * Form multipart sinh từ Orval không có `required` (quirk đã biết trong brief), nên FE tự validate
 * `file`/`preset` — server 413/415/422 vẫn là nguồn sự thật cuối, đây chỉ để đỡ round-trip vô ích.
 */
export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024; // 10 MB
export const ACCEPTED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;
export const ACCEPT_ATTR = ACCEPTED_MIME_TYPES.join(',');

type AcceptedMimeType = (typeof ACCEPTED_MIME_TYPES)[number];

function isAcceptedMimeType(mimeType: string): mimeType is AcceptedMimeType {
  return (ACCEPTED_MIME_TYPES as readonly string[]).includes(mimeType);
}

/** iPhone chụp HEIC/HEIF mặc định — nhận diện để hiện đúng hướng dẫn chuyển định dạng ngay ở client,
 * cùng text với lỗi server `MEDIA_HEIC_UNSUPPORTED` (`HEIC_UNSUPPORTED_GUIDANCE`). */
export function isHeicFile(file: File): boolean {
  const type = file.type.toLowerCase();
  return type === 'image/heic' || type === 'image/heif';
}

/** Export riêng để component gọi thẳng `.safeParse(file)` ngay trong handler chọn file — một nơi
 * duy nhất định nghĩa luật ≤10MB + mime, không để mỗi dialog tự chép lại (Definition of Done:
 * "validation bằng Zod sinh từ contract"). */
export const fileSchema = z
  .instanceof(File, { message: 'Vui lòng chọn một ảnh' })
  .refine((file) => file.size <= MAX_FILE_SIZE_BYTES, 'File vượt quá 10 MB, vui lòng chọn ảnh nhỏ hơn')
  .refine((file) => isAcceptedMimeType(file.type), 'Định dạng không hỗ trợ. Chỉ nhận JPEG, PNG, WEBP');

/** Preset đặt tên theo quy ước `{width}x{height},{fit}` (config/image-presets.json) — suy ra `fit`
 * trực tiếp từ chuỗi preset thay vì chép lại bảng preset→fit lần thứ hai ở FE. */
export function fitOfPreset(preset: string): 'cover' | 'inside' | null {
  const fit = preset.split(',').pop();
  return fit === 'cover' || fit === 'inside' ? fit : null;
}

export const slotUploadClientSchema = uploadToSlotBody.extend({
  file: fileSchema,
  preset: z.string().min(1, 'Thiếu preset'),
});
export type SlotUploadClientValues = z.infer<typeof slotUploadClientSchema>;

export const libraryUploadClientSchema = uploadToLibraryBody.extend({
  file: fileSchema,
});
export type LibraryUploadClientValues = z.infer<typeof libraryUploadClientSchema>;
