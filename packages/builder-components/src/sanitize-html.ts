import sanitizeHtmlLib from 'sanitize-html';
import sanitizeProfilesJson from '../../../config/sanitize-profiles.json';

interface SanitizeProfile {
  tags: string[];
  attrs: Record<string, string[]>;
}

const profiles = sanitizeProfilesJson as Record<string, SanitizeProfile>;

/**
 * Sanitize HTML từ Lexical theo profile whitelist (07 §7.2). Dùng ở FE (UX, lúc nhập/preview)
 * VÀ ở BE (bảo mật, lúc ghi) — đây là bản FE/Node; BE .NET có bản tương đương (2.8).
 *
 * Ràng buộc: không style/class/id/data-*, không img/script/iframe/object/embed/form/input,
 * a[href] chỉ https/http/mailto/tel (chặn javascript:/data:), target=_blank bị ép rel=noopener.
 */
export function sanitizeRichText(html: string, profileName: string): string {
  const profile = profiles[profileName];
  if (!profile) {
    throw new Error(`sanitizeRichText: profile '${profileName}' không có trong config/sanitize-profiles.json`);
  }

  return sanitizeHtmlLib(html, {
    allowedTags: profile.tags,
    allowedAttributes: profile.attrs,
    allowedSchemes: ['https', 'http', 'mailto', 'tel'],
    disallowedTagsMode: 'discard',
    transformTags: {
      a: (tagName, attribs) => {
        if (attribs.target === '_blank') {
          return { tagName, attribs: { ...attribs, rel: 'noopener noreferrer' } };
        }
        return { tagName, attribs };
      },
    },
  });
}
