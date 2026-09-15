import { z } from 'zod';
import { patchShopsShopIdBody, postShopsBody } from '@vsite/api-sdk';
import { reservedRoutes } from './reserved-routes';

/**
 * Ràng buộc slug (Docs/tasks/SHOP-001/brief.md): lowercase, [a-z0-9-], không bắt đầu/kết thúc bằng
 * "-", 3–63 ký tự. Validate ở FE trước để đỡ round-trip 422 vô ích — nhưng 422 từ server vẫn LUÔN
 * là nguồn sự thật cuối (server check thêm reserved-routes đầy đủ, không hardcode danh sách thứ
 * hai — Quyết định #24, đọc qua `reservedRoutes` build-time import có sẵn).
 */
const SLUG_PATTERN = /^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$/;

const reservedSlugs = new Set<string>([...reservedRoutes.reservedPaths, ...reservedRoutes.reservedSubdomains]);

export const slugSchema = z
  .string()
  .min(3, 'Slug phải có ít nhất 3 ký tự')
  .max(63, 'Slug tối đa 63 ký tự')
  .regex(SLUG_PATTERN, 'Slug chỉ gồm chữ thường, số và dấu "-", không bắt đầu/kết thúc bằng "-"')
  .refine((slug) => !reservedSlugs.has(slug), {
    message: 'Slug này thuộc danh sách hệ thống dùng riêng, hãy chọn slug khác',
  });

function requireExternalUrlWhenExternalOnly(data: { kind: string; externalUrl?: string | null }): boolean {
  return data.kind !== 'ExternalOnly' || Boolean(data.externalUrl && data.externalUrl.trim().length > 0);
}

const EXTERNAL_URL_ISSUE = {
  message: 'Vui lòng nhập URL khi chọn "Chỉ liên kết ngoài"',
  path: ['externalUrl'] as (string | number)[],
};

// `postShopsBody`/`patchShopsShopIdBody` sinh từ contract (Orval) — chỉ override field `slug` bằng
// rule chi tiết hơn ở trên, cộng thêm ràng buộc chéo field "ExternalOnly ⇒ externalUrl" mà contract
// không mã hoá được (business rule, không phải type shape).
export const createShopFormSchema = postShopsBody
  .extend({ slug: slugSchema })
  .refine(requireExternalUrlWhenExternalOnly, EXTERNAL_URL_ISSUE);

export const updateShopFormSchema = patchShopsShopIdBody
  .extend({ slug: slugSchema })
  .refine(requireExternalUrlWhenExternalOnly, EXTERNAL_URL_ISSUE);

export type CreateShopFormValues = z.infer<typeof createShopFormSchema>;
export type UpdateShopFormValues = z.infer<typeof updateShopFormSchema>;
