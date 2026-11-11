import { useState } from 'react';
import { Button } from '@vsite/ui';
import type { ThemeDraftApi } from '../../hooks/use-theme-draft';
import { getThemeValue, isValidThemeColor, type ThemeFieldKey } from '../../lib/theme-edit';
import { THEME_GROUPS, type ThemeField } from '../../lib/theme-fields';

const inputClass = 'w-full rounded-md border border-border bg-background px-2 py-1 text-sm';

/**
 * Tab "Giao diện" (Flow 6): sửa theme của website, canvas đổi ngay (op `changeTheme` → CSS variables).
 * Chưa lưu → "Lưu theme" / "Hoàn tác" (về bản đã lưu). 422 tô ô theo `params.errors[].path`.
 */
export function ThemePanel({ theme }: { theme: ThemeDraftApi }) {
  const { load } = theme;

  if (load.status === 'loading') return <p className="text-sm text-muted-foreground">Đang tải giao diện...</p>;
  if (load.status === 'error') {
    return (
      <div className="space-y-3">
        <p role="alert" className="text-sm text-destructive">
          {load.message}
        </p>
        {/* 404 WEBSITE_NOT_FOUND: chưa có website thì thử lại cũng vô ích — chỉ báo. */}
        {load.code !== 'WEBSITE_NOT_FOUND' && (
          <Button type="button" variant="outline" size="sm" onClick={load.retry}>
            Thử lại
          </Button>
        )}
      </div>
    );
  }
  if (load.status === 'invalid') {
    // Lưu đè một theme ta không đọc được sẽ phá cấu hình của shop → không cho sửa.
    return (
      <p role="alert" className="text-sm text-destructive">
        Cấu hình giao diện không đọc được. Vui lòng liên hệ hỗ trợ.
      </p>
    );
  }

  const fieldErrors = theme.saveError?.mapped?.byField;
  const general = theme.saveError?.mapped?.general ?? [];

  return (
    <form
      aria-label="Giao diện website"
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        theme.save();
      }}
    >
      {theme.saveError && (
        <div role="alert" className="space-y-1 text-sm text-destructive">
          <p>{theme.saveError.message}</p>
          {general.length > 0 && (
            <ul className="list-disc pl-5">
              {general.map((m, i) => (
                <li key={`${m}:${String(i)}`}>{m}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {THEME_GROUPS.map((group) => (
        <fieldset key={group.title} className="space-y-2">
          <legend className="text-xs font-semibold uppercase text-muted-foreground">{group.title}</legend>
          {group.fields.map((field) => (
            <ThemeControl
              key={field.key}
              field={field}
              value={getThemeValue(theme.tokens, field.key)}
              serverError={fieldErrors?.get(field.key)}
              onChange={theme.setValue}
            />
          ))}
        </fieldset>
      ))}

      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={!theme.dirty || theme.saving}>
          {theme.saving ? 'Đang lưu...' : 'Lưu theme'}
        </Button>
        <Button type="button" variant="outline" size="sm" disabled={!theme.dirty || theme.saving} onClick={theme.revert}>
          Hoàn tác
        </Button>
      </div>
      <p className="text-xs text-muted-foreground">
        {theme.dirty ? 'Có thay đổi chưa lưu — canvas đang hiện bản xem trước.' : 'Giao diện đã lưu.'}
      </p>
    </form>
  );
}

function ThemeControl({
  field,
  value,
  serverError,
  onChange,
}: {
  field: ThemeField;
  value: unknown;
  serverError: string | undefined;
  onChange: (key: ThemeFieldKey, value: string) => string | null;
}) {
  const id = `theme-${field.key}`;
  const current = typeof value === 'string' ? value : '';
  const [localError, setLocalError] = useState<string | null>(null);

  return (
    <div className="space-y-1">
      <label htmlFor={id} className="block text-sm text-foreground">
        {field.label}
      </label>
      {field.kind === 'color' ? (
        <ColorInput
          id={id}
          label={field.label}
          value={current}
          invalid={Boolean(serverError ?? localError)}
          onCommit={(v) => {
            setLocalError(onChange(field.key, v));
          }}
        />
      ) : (
        <select
          id={id}
          className={inputClass}
          value={current}
          aria-invalid={serverError || localError ? true : undefined}
          onChange={(e) => {
            setLocalError(onChange(field.key, e.target.value));
          }}
        >
          {/* Giá trị hiện tại không thuộc tập (không nên xảy ra sau Zod) vẫn hiện để không đổi ngầm. */}
          {!field.options.some((o) => o.value === current) && <option value={current}>{current}</option>}
          {field.options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      )}
      {(serverError ?? localError) && (
        <p role="alert" className="text-xs text-destructive">
          {serverError ?? localError}
        </p>
      )}
    </div>
  );
}

/**
 * Ô màu: ô chọn màu của trình duyệt (luôn ra `#rrggbb`) + ô gõ hex. Gõ dở/sai khuôn thì **không** gửi op nào
 * (chặn ở client) — canvas giữ màu hợp lệ gần nhất.
 */
function ColorInput({
  id,
  label,
  value,
  invalid,
  onCommit,
}: {
  id: string;
  label: string;
  value: string;
  invalid: boolean;
  onCommit: (value: string) => void;
}) {
  // `typed` chỉ có khi người dùng đang gõ; null = theo `value` của theme (undo/hoàn tác cập nhật ô ngay).
  const [typed, setTyped] = useState<string | null>(null);
  const shown = typed ?? value;
  const badFormat = typed !== null && !isValidThemeColor(typed);

  return (
    <>
    <div className="flex items-center gap-2">
      <input
        type="color"
        aria-label={`${label} (bảng màu)`}
        className="h-8 w-10 shrink-0 cursor-pointer rounded border border-border bg-background p-0"
        value={isValidThemeColor(value) ? value : '#000000'}
        onChange={(e) => {
          setTyped(null);
          onCommit(e.target.value);
        }}
      />
      <input
        id={id}
        type="text"
        className={`${inputClass} font-mono`}
        value={shown}
        spellCheck={false}
        maxLength={7}
        aria-invalid={badFormat || invalid ? true : undefined}
        aria-describedby={badFormat ? `${id}-hint` : undefined}
        onChange={(e) => {
          const next = e.target.value;
          // Hợp lệ → theme là nguồn của ô (undo/hoàn tác cập nhật được); chỉ chữ gõ dở/sai mới giữ cục bộ.
          if (isValidThemeColor(next)) {
            setTyped(null);
            onCommit(next);
          } else {
            setTyped(next);
          }
        }}
        onBlur={() => {
          setTyped(null);
        }}
      />
    </div>
    {badFormat && (
      <p id={`${id}-hint`} role="alert" className="text-xs text-destructive">
        Màu phải có dạng #RRGGBB (vd. #7c3aed).
      </p>
    )}
    </>
  );
}
