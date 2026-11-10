import { useEffect, useMemo, useRef, useState } from 'react';
import { $isLinkNode, TOGGLE_LINK_COMMAND } from '@lexical/link';
import {
  $isListNode,
  INSERT_ORDERED_LIST_COMMAND,
  INSERT_UNORDERED_LIST_COMMAND,
  ListNode,
  REMOVE_LIST_COMMAND,
} from '@lexical/list';
import { LexicalComposer } from '@lexical/react/LexicalComposer';
import { useLexicalComposerContext } from '@lexical/react/LexicalComposerContext';
import { ContentEditable } from '@lexical/react/LexicalContentEditable';
import { LexicalErrorBoundary } from '@lexical/react/LexicalErrorBoundary';
import { HistoryPlugin } from '@lexical/react/LexicalHistoryPlugin';
import { LinkPlugin } from '@lexical/react/LexicalLinkPlugin';
import { ListPlugin } from '@lexical/react/LexicalListPlugin';
import { OnChangePlugin } from '@lexical/react/LexicalOnChangePlugin';
import { RichTextPlugin } from '@lexical/react/LexicalRichTextPlugin';
import { $createHeadingNode, $createQuoteNode, $isHeadingNode, $isQuoteNode } from '@lexical/rich-text';
import { $setBlocksType } from '@lexical/selection';
import { $getNearestNodeOfType } from '@lexical/utils';
import {
  $createParagraphNode,
  $getSelection,
  $isRangeSelection,
  COMMAND_PRIORITY_HIGH,
  COMMAND_PRIORITY_LOW,
  FORMAT_TEXT_COMMAND,
  INSERT_LINE_BREAK_COMMAND,
  KEY_ENTER_COMMAND,
  SELECTION_CHANGE_COMMAND,
  type TextFormatType,
} from 'lexical';
import { isSafeLinkUrl } from '../link-url';
import { $importHtml, exportHtml, nodesForProfile } from './lexical-html';
import { normalizeEditorHtml, toolbarCapabilities, type BlockKind, type ToolbarCapabilities } from './profile';

export interface RichTextEditorProps {
  value: string;
  onChange: (html: string) => void;
  /** Tên profile trong `config/sanitize-profiles.json` (`inline` | `basic`). */
  profile: string;
  label: string;
  maxLength?: number;
  /** Chỉ đọc (vd. prop không sửa được ở trang System). `fieldset disabled` không khoá được contenteditable nên phải tắt ở đây. */
  disabled?: boolean;
}

const theme = {
  paragraph: 'my-1',
  quote: 'my-2 border-l-4 border-border pl-3 italic text-muted-foreground',
  heading: { h2: 'my-2 text-xl font-semibold', h3: 'my-2 text-lg font-semibold', h4: 'my-2 text-base font-semibold' },
  list: { ul: 'my-1 list-disc pl-6', ol: 'my-1 list-decimal pl-6', listitem: 'my-0.5' },
  text: { bold: 'font-bold', italic: 'italic', underline: 'underline', strikethrough: 'line-through' },
  link: 'text-primary underline',
};

const BLOCK_LABELS: Record<BlockKind, string> = {
  paragraph: 'Đoạn văn',
  h2: 'Tiêu đề 2',
  h3: 'Tiêu đề 3',
  h4: 'Tiêu đề 4',
  quote: 'Trích dẫn',
  ul: 'Danh sách chấm',
  ol: 'Danh sách số',
};

/**
 * Control `lexical` của Inspector (07 §7.2). Chỉ mang thứ profile cho phép; mọi thay đổi xuất HTML → `sanitizeRichText` →
 * `onChange` (nơi gọi biến thành op `update`). Tải lười (`React.lazy` ở Inspector) để Lexical không vào bundle `apps/web`.
 */
export default function RichTextEditor({ value, onChange, profile, label, maxLength, disabled = false }: RichTextEditorProps) {
  const caps = useMemo(() => toolbarCapabilities(profile), [profile]);
  const lastEmitted = useRef<string>(normalizeEditorHtml(value, profile));
  const [length, setLength] = useState(() => value.length);

  const initialConfig = useMemo(
    () => ({
      namespace: 'vsite-richtext',
      editable: !disabled,
      nodes: nodesForProfile(profile),
      theme,
      onError: (error: Error) => {
        throw error;
      },
      editorState: (editor: Parameters<typeof $importHtml>[0]) => {
        $importHtml(editor, value, profile);
      },
    }),
    // Giá trị ban đầu chỉ dùng lúc mở; thay đổi sau đó (undo ở cây…) do SyncValuePlugin xử lý.
    [profile],
  );

  const over = maxLength !== undefined && length > maxLength;

  return (
    <div>
      {/* eslint-disable-next-line @typescript-eslint/no-deprecated -- LexicalComposer còn dùng được ở 0.53 (pin cứng); chuyển sang LexicalExtensionComposer khi nâng major (nợ ghi ở changelog WEBSITE-002 F10). */}
      <LexicalComposer initialConfig={initialConfig}>
        <div className="mt-1 rounded border border-border">
          <Toolbar caps={caps} />
          <RichTextPlugin
            contentEditable={
              <ContentEditable
                aria-label={label}
                aria-disabled={disabled || undefined}
                className="min-h-24 px-2 py-1 text-sm outline-none"
                data-testid="richtext-editable"
              />
            }
            ErrorBoundary={LexicalErrorBoundary}
          />
        </div>
        <HistoryPlugin />
        {caps.link && <LinkPlugin validateUrl={isSafeLinkUrl} />}
        {(caps.blocks.includes('ul') || caps.blocks.includes('ol')) && <ListPlugin />}
        {caps.enterInsertsLineBreak && <EnterAsLineBreakPlugin />}
        <EditablePlugin editable={!disabled} />
        <SyncValuePlugin value={value} profile={profile} lastEmitted={lastEmitted} />
        <OnChangePlugin
          onChange={(_state, editor) => {
            const html = exportHtml(editor, profile);
            setLength(html.length);
            if (html === lastEmitted.current) return;
            lastEmitted.current = html;
            onChange(html);
          }}
        />
      </LexicalComposer>
      {maxLength !== undefined && (
        <p className={`mt-1 text-xs ${over ? 'text-destructive' : 'text-muted-foreground'}`} data-testid="richtext-count">
          {length}/{maxLength} ký tự
          {over ? ' — quá dài, thay đổi sẽ không được áp dụng' : ''}
        </p>
      )}
    </div>
  );
}

/** `disabled` đổi sau khi mở (vd. đổi sang trang System) → bật/tắt chế độ sửa của Lexical. */
function EditablePlugin({ editable }: { editable: boolean }) {
  const [editor] = useLexicalComposerContext();
  useEffect(() => {
    editor.setEditable(editable);
  }, [editor, editable]);
  return null;
}

/** Giá trị đổi từ bên ngoài (undo/redo của cây khi đang chọn khối này) → nạp lại editor; không phát lại `onChange`. */
function SyncValuePlugin({
  value,
  profile,
  lastEmitted,
}: {
  value: string;
  profile: string;
  lastEmitted: React.RefObject<string>;
}) {
  const [editor] = useLexicalComposerContext();
  useEffect(() => {
    const incoming = normalizeEditorHtml(value, profile);
    if (incoming === lastEmitted.current) return;
    lastEmitted.current = incoming;
    // `history-merge`: không thành bước undo riêng của editor, và OnChangePlugin bỏ qua.
    editor.update(() => { $importHtml(editor, value, profile); }, { tag: 'history-merge' });
  }, [value, profile, editor, lastEmitted]);
  return null;
}

/** Profile không có `<p>`: Enter = xuống dòng trong cùng một dòng văn bản, không tách đoạn. */
function EnterAsLineBreakPlugin() {
  const [editor] = useLexicalComposerContext();
  useEffect(
    () =>
      editor.registerCommand(
        KEY_ENTER_COMMAND,
        (event) => {
          event?.preventDefault();
          return editor.dispatchCommand(INSERT_LINE_BREAK_COMMAND, false);
        },
        COMMAND_PRIORITY_HIGH,
      ),
    [editor],
  );
  return null;
}

interface SelectionState {
  bold: boolean;
  italic: boolean;
  underline: boolean;
  strikethrough: boolean;
  block: BlockKind;
  link: boolean;
}
const EMPTY_SELECTION: SelectionState = {
  bold: false,
  italic: false,
  underline: false,
  strikethrough: false,
  block: 'paragraph',
  link: false,
};

function Toolbar({ caps }: { caps: ToolbarCapabilities }) {
  const [editor] = useLexicalComposerContext();
  const [sel, setSel] = useState<SelectionState>(EMPTY_SELECTION);
  const [linkOpen, setLinkOpen] = useState(false);

  useEffect(() => {
    const read = () => {
      const selection = $getSelection();
      if (!$isRangeSelection(selection)) return;
      const anchor = selection.anchor.getNode();
      // Root rỗng (vừa xoá hết) thì selection nằm ở gốc, chưa có khối nào: bỏ qua, không ném lỗi.
      const top = anchor.getTopLevelElement();
      if (!top) return;
      let block: BlockKind = 'paragraph';
      if ($isHeadingNode(top)) block = top.getTag() as BlockKind;
      else if ($isQuoteNode(top)) block = 'quote';
      else if ($isListNode(top) || $getNearestNodeOfType(anchor, ListNode)) {
        const list = $isListNode(top) ? top : $getNearestNodeOfType(anchor, ListNode);
        block = list?.getListType() === 'number' ? 'ol' : 'ul';
      }
      setSel({
        bold: selection.hasFormat('bold'),
        italic: selection.hasFormat('italic'),
        underline: selection.hasFormat('underline'),
        strikethrough: selection.hasFormat('strikethrough'),
        block,
        link: anchor.getParents().some((p) => $isLinkNode(p)) || $isLinkNode(anchor),
      });
    };
    const offUpdate = editor.registerUpdateListener(({ editorState }) => {
      editorState.read(read);
    });
    const offSelection = editor.registerCommand(
      SELECTION_CHANGE_COMMAND,
      () => {
        editor.getEditorState().read(read);
        return false;
      },
      COMMAND_PRIORITY_LOW,
    );
    return () => {
      offUpdate();
      offSelection();
    };
  }, [editor]);

  const mark = (format: TextFormatType) => {
    editor.dispatchCommand(FORMAT_TEXT_COMMAND, format);
  };

  const setBlock = (kind: BlockKind) => {
    if (kind === 'ul' || kind === 'ol') {
      if (sel.block === kind) editor.dispatchCommand(REMOVE_LIST_COMMAND, undefined);
      else editor.dispatchCommand(kind === 'ul' ? INSERT_UNORDERED_LIST_COMMAND : INSERT_ORDERED_LIST_COMMAND, undefined);
      return;
    }
    // Từ danh sách sang khối khác: gỡ danh sách trước.
    if (sel.block === 'ul' || sel.block === 'ol') editor.dispatchCommand(REMOVE_LIST_COMMAND, undefined);
    editor.update(() => {
      const selection = $getSelection();
      if (!$isRangeSelection(selection)) return;
      $setBlocksType(selection, () => {
        if (kind === 'h2' || kind === 'h3' || kind === 'h4') return $createHeadingNode(kind);
        if (kind === 'quote') return $createQuoteNode();
        return $createParagraphNode();
      });
    });
  };

  const markButtons: { format: TextFormatType; on: boolean; text: string; name: string; active: boolean }[] = [
    { format: 'bold', on: caps.marks.bold, text: 'B', name: 'In đậm', active: sel.bold },
    { format: 'italic', on: caps.marks.italic, text: 'I', name: 'In nghiêng', active: sel.italic },
    { format: 'underline', on: caps.marks.underline, text: 'U', name: 'Gạch chân', active: sel.underline },
    { format: 'strikethrough', on: caps.marks.strikethrough, text: 'S', name: 'Gạch ngang', active: sel.strikethrough },
  ];

  return (
    <div role="toolbar" aria-label="Định dạng văn bản" className="border-b border-border">
      <div className="flex flex-wrap items-center gap-1 p-1">
        {caps.blocks.length > 0 && (
          <select
            aria-label="Kiểu khối"
            className="rounded border border-border bg-background px-1 py-0.5 text-xs"
            value={sel.block}
            onChange={(e) => { setBlock(e.target.value as BlockKind); }}
          >
            {caps.blocks.map((b) => (
              <option key={b} value={b}>
                {BLOCK_LABELS[b]}
              </option>
            ))}
          </select>
        )}
        {markButtons
          .filter((b) => b.on)
          .map((b) => (
            <button
              key={b.format}
              type="button"
              aria-label={b.name}
              aria-pressed={b.active}
              className={`h-7 w-7 rounded border text-xs ${b.active ? 'border-primary bg-muted font-bold' : 'border-border'}`}
              onMouseDown={(e) => { e.preventDefault(); }}
              onClick={() => { mark(b.format); }}
            >
              {b.text}
            </button>
          ))}
        {caps.link && (
          <button
            type="button"
            aria-label="Liên kết"
            aria-pressed={sel.link}
            aria-expanded={linkOpen}
            className={`h-7 rounded border px-2 text-xs ${sel.link ? 'border-primary bg-muted' : 'border-border'}`}
            onMouseDown={(e) => { e.preventDefault(); }}
            onClick={() => { setLinkOpen((o) => !o); }}
          >
            Liên kết
          </button>
        )}
      </div>
      {caps.link && linkOpen && (
        <LinkForm
          onApply={(url) => {
            editor.dispatchCommand(TOGGLE_LINK_COMMAND, url);
            setLinkOpen(false);
          }}
          onRemove={() => {
            editor.dispatchCommand(TOGGLE_LINK_COMMAND, null);
            setLinkOpen(false);
          }}
          onCancel={() => { setLinkOpen(false); }}
          canRemove={sel.link}
        />
      )}
    </div>
  );
}

function LinkForm({
  onApply,
  onRemove,
  onCancel,
  canRemove,
}: {
  onApply: (url: string) => void;
  onRemove: () => void;
  onCancel: () => void;
  canRemove: boolean;
}) {
  const [url, setUrl] = useState('');
  const [error, setError] = useState<string | null>(null);

  const apply = () => {
    const trimmed = url.trim();
    if (!isSafeLinkUrl(trimmed)) {
      setError('Liên kết không hợp lệ: chỉ nhận https, http, mailto, tel, đường dẫn nội bộ (/…) hoặc #neo.');
      return;
    }
    onApply(trimmed);
  };

  return (
    <div className="space-y-1 border-t border-border p-1">
      <div className="flex gap-1">
        <input
          type="text"
          aria-label="Địa chỉ liên kết"
          aria-invalid={error ? true : undefined}
          placeholder="https://…"
          className="min-w-0 flex-1 rounded border border-border px-2 py-0.5 text-xs"
          value={url}
          onChange={(e) => {
            setUrl(e.target.value);
            setError(null);
          }}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              apply();
            } else if (e.key === 'Escape') {
              onCancel();
            }
          }}
        />
        <button type="button" className="rounded border border-border px-2 text-xs" onClick={apply}>
          Áp dụng
        </button>
        {canRemove && (
          <button type="button" className="rounded border border-border px-2 text-xs" onClick={onRemove}>
            Gỡ liên kết
          </button>
        )}
        <button type="button" className="rounded border border-border px-2 text-xs" onClick={onCancel}>
          Đóng
        </button>
      </div>
      {error && (
        <p role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  );
}
