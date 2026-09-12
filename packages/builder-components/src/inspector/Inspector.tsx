import { colorTokens } from '@vsite/theme-engine';
import { setAtPath, type PathSegment } from './path-utils';
import type { PropertyPanelField } from './types';

/**
 * Bước 2 (§10): chứng minh property-panel.ts đủ metadata để sinh form dùng được. Control cho
 * `richText`/`image`/`link`/`binding` là STUB đơn giản (Lexical thật, media picker thật, v.v.
 * là việc của module Website ở Bước 5+) — mục tiêu ở đây là group/order/kind đủ để không phải
 * sửa meta-schema sau này.
 */
export interface InspectorProps {
  fields: PropertyPanelField[];
  values: Record<string, unknown>;
  onChange: (path: PathSegment[], value: unknown) => void;
  pathPrefix?: PathSegment[];
}

export function Inspector({ fields, values, onChange, pathPrefix = [] }: InspectorProps) {
  const groups = new Map<string, PropertyPanelField[]>();
  for (const field of fields) {
    const list = groups.get(field.group) ?? [];
    list.push(field);
    groups.set(field.group, list);
  }

  return (
    <div className="space-y-4">
      {Array.from(groups.entries()).map(([group, groupFields]) => (
        <fieldset key={group} className="rounded border border-border p-3">
          {group !== '_' ? <legend className="px-1 text-sm font-medium">{group}</legend> : null}
          <div className="space-y-3">
            {groupFields
              .slice()
              .sort((a, b) => a.order - b.order)
              .map((field) => (
                <FieldControl
                  key={field.name}
                  field={field}
                  value={values[field.name]}
                  onChange={(v) => {
                    onChange([...pathPrefix, field.name], v);
                  }}
                />
              ))}
          </div>
        </fieldset>
      ))}
    </div>
  );
}

function FieldControl({
  field,
  value,
  onChange,
}: {
  field: PropertyPanelField;
  value: unknown;
  onChange: (value: unknown) => void;
}) {
  const label = (
    <label className="block text-sm font-medium">
      {field.label}
      {field.help ? <span className="ml-1 text-xs text-muted-foreground">({field.help})</span> : null}
    </label>
  );

  const stringValue = (value as string | undefined) ?? '';

  switch (field.control) {
    case 'text':
      return (
        <div>
          {label}
          {field.multiline ? (
            <textarea
              className="mt-1 w-full rounded border border-border px-2 py-1"
              value={stringValue}
              maxLength={field.maxLength}
              onChange={(e) => {
                onChange(e.target.value);
              }}
            />
          ) : (
            <input
              className="mt-1 w-full rounded border border-border px-2 py-1"
              value={stringValue}
              maxLength={field.maxLength}
              onChange={(e) => {
                onChange(e.target.value);
              }}
            />
          )}
        </div>
      );

    case 'lexical':
      // STUB — Lexical editor thật là việc của Bước 5+. Textarea đủ để chứng minh pipeline metadata.
      return (
        <div>
          {label}
          <textarea
            className="mt-1 w-full rounded border border-border px-2 py-1 font-mono text-xs"
            value={stringValue}
            maxLength={field.maxLength}
            onChange={(e) => {
              onChange(e.target.value);
            }}
          />
        </div>
      );

    case 'number':
      return (
        <div>
          {label}
          <input
            type="number"
            className="mt-1 w-full rounded border border-border px-2 py-1"
            value={(value as number | undefined) ?? ''}
            min={field.min}
            max={field.max}
            step={field.step}
            onChange={(e) => {
              onChange(e.target.value === '' ? undefined : Number(e.target.value));
            }}
          />
        </div>
      );

    case 'switch':
      return (
        <label className="flex items-center gap-2 text-sm font-medium">
          <input
            type="checkbox"
            checked={Boolean(value)}
            onChange={(e) => {
              onChange(e.target.checked);
            }}
          />
          {field.label}
        </label>
      );

    case 'select':
      return (
        <div>
          {label}
          <select
            className="mt-1 w-full rounded border border-border px-2 py-1"
            value={stringValue}
            onChange={(e) => {
              onChange(e.target.value);
            }}
          >
            <option value="" disabled>
              — chọn —
            </option>
            {field.options?.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </select>
        </div>
      );

    case 'color-token':
      return (
        <div>
          {label}
          <select
            className="mt-1 w-full rounded border border-border px-2 py-1"
            value={stringValue}
            onChange={(e) => {
              onChange(e.target.value);
            }}
          >
            {Object.keys(colorTokens).map((token) => (
              <option key={token} value={token}>
                {token}
              </option>
            ))}
          </select>
        </div>
      );

    case 'media-picker': {
      // STUB — media picker thật cần MediaAsset (Bước 4). Nhập imageId trực tiếp để test pipeline.
      const imageValue = (value as { imageId?: string; alt?: string } | undefined) ?? {};
      return (
        <div>
          {label}
          <input
            className="mt-1 w-full rounded border border-border px-2 py-1"
            placeholder={`imageId (preset: ${field.preset ?? ''})`}
            value={imageValue.imageId ?? ''}
            onChange={(e) => {
              onChange({ ...imageValue, imageId: e.target.value });
            }}
          />
        </div>
      );
    }

    case 'icon-picker':
      return (
        <div>
          {label}
          <input
            className="mt-1 w-full rounded border border-border px-2 py-1"
            placeholder="tên icon lucide-react"
            value={stringValue}
            onChange={(e) => {
              onChange(e.target.value);
            }}
          />
        </div>
      );

    case 'link-picker': {
      // STUB đơn giản — chỉ hỗ trợ 'external' (đủ để test pipeline). page/systemPage/productCategory
      // cần Page/Product tồn tại (Bước 5+).
      const linkValue = (value as { kind?: string; url?: string } | undefined) ?? {};
      return (
        <div>
          {label}
          <input
            className="mt-1 w-full rounded border border-border px-2 py-1"
            placeholder="https://..."
            value={linkValue.kind === 'external' ? (linkValue.url ?? '') : ''}
            onChange={(e) => {
              onChange({ kind: 'external', url: e.target.value });
            }}
          />
        </div>
      );
    }

    case 'repeater': {
      const items: unknown[] = Array.isArray(value) ? value : [];
      return (
        <div>
          {label}
          <div className="space-y-2">
            {items.map((item, i) => (
              <div key={i} className="rounded border border-border p-2">
                <Inspector
                  fields={field.itemFields ?? []}
                  values={item as Record<string, unknown>}
                  onChange={(path, v) => {
                    const next: unknown[] = [...items];
                    next[i] = setAtPath(item as Record<string, unknown>, path, v);
                    onChange(next);
                  }}
                />
                <button
                  type="button"
                  className="mt-2 text-xs text-destructive"
                  onClick={() => {
                    onChange(items.filter((_, idx) => idx !== i));
                  }}
                >
                  Xoá
                </button>
              </div>
            ))}
          </div>
          <button
            type="button"
            className="mt-2 rounded border border-border px-2 py-1 text-sm"
            onClick={() => {
              const next: unknown[] = [...items, {}];
              onChange(next);
            }}
          >
            + Thêm {field.name}
          </button>
        </div>
      );
    }

    case 'fieldset':
      return (
        <div>
          {label}
          <Inspector
            fields={field.fields ?? []}
            values={(value as Record<string, unknown> | undefined) ?? {}}
            onChange={(path, v) => {
              onChange(setAtPath((value as Record<string, unknown> | undefined) ?? {}, path, v));
            }}
          />
        </div>
      );

    case 'binding-builder':
      // STUB — Binding Resolver thật là Bước 9. Chỉ hiện whitelist source để xác nhận metadata đúng.
      return (
        <div>
          {label}
          <p className="mt-1 text-sm text-muted-foreground">Nguồn cho phép: {field.sources?.join(', ')}</p>
        </div>
      );

    default:
      return null;
  }
}
