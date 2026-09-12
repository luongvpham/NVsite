import type { RichTextProps } from '../../generated/component-types';
import { sanitizeRichText } from '../sanitize-html';

/**
 * RichText01 — sanitize LẠI ở render time dù BE đã sanitize lúc ghi (defense in depth, #67).
 * Test invariant #6 (§6.1): allowedInPageKinds có 'System', content editableInSystemPage:true.
 */
export default function RichText01({ content, align }: RichTextProps) {
  const safeHtml = content ? sanitizeRichText(content, 'basic') : '';

  // HTML đã sanitize theo whitelist profile 'basic' (sanitizeRichText) trước khi tới đây.
  return <div className={align === 'justify' ? 'text-justify' : 'text-left'} dangerouslySetInnerHTML={{ __html: safeHtml }} />;
}
