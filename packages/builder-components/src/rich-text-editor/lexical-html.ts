import { $generateHtmlFromNodes, $generateNodesFromDOM } from '@lexical/html';
import { LinkNode } from '@lexical/link';
import { ListItemNode, ListNode } from '@lexical/list';
import { HeadingNode, QuoteNode } from '@lexical/rich-text';
import {
  $createParagraphNode,
  $getRoot,
  $isElementNode,
  type Klass,
  type LexicalEditor,
  type LexicalNode,
} from 'lexical';
import { sanitizeRichText } from '../sanitize-html';
import { normalizeEditorHtml, toolbarCapabilities } from './profile';

/** Chỉ đăng ký loại node mà profile có thẻ tương ứng — HTML dán/nạp vào không thể tạo ra khối ngoài profile. */
export function nodesForProfile(profile: string): Klass<LexicalNode>[] {
  const caps = toolbarCapabilities(profile);
  const nodes: Klass<LexicalNode>[] = [];
  if (caps.link) nodes.push(LinkNode);
  if (caps.blocks.some((b) => b === 'h2' || b === 'h3' || b === 'h4')) nodes.push(HeadingNode);
  if (caps.blocks.includes('quote')) nodes.push(QuoteNode);
  if (caps.blocks.includes('ul') || caps.blocks.includes('ol')) nodes.push(ListNode, ListItemNode);
  return nodes;
}

/**
 * Nạp HTML đã lưu vào editor (gọi TRONG `editor.update`). HTML đi qua `sanitizeRichText` TRƯỚC khi vào Lexical:
 * nội dung cũ/độc không thể vào editor, và mọi thứ profile cho phép vẫn còn nguyên (không mất định dạng).
 * Lexical chỉ cho node khối ở gốc → chữ/xuống dòng/link rời được gom vào một đoạn.
 */
export function $importHtml(editor: LexicalEditor, html: string, profile: string): void {
  const root = $getRoot();
  root.clear();
  const clean = sanitizeRichText(html, profile);
  const dom = new DOMParser().parseFromString(clean, 'text/html');
  let loose = $createParagraphNode();
  for (const node of $generateNodesFromDOM(editor, dom)) {
    if ($isElementNode(node) && !node.isInline()) {
      if (loose.getChildrenSize() > 0) {
        root.append(loose);
        loose = $createParagraphNode();
      }
      root.append(node);
    } else {
      loose.append(node);
    }
  }
  if (loose.getChildrenSize() > 0 || root.getChildrenSize() === 0) root.append(loose);
}

/** Editor → giá trị lưu: HTML của Lexical, rồi `normalizeEditorHtml` (sanitize theo profile). */
export function exportHtml(editor: LexicalEditor, profile: string): string {
  return editor.getEditorState().read(() => normalizeEditorHtml($generateHtmlFromNodes(editor, null), profile));
}
