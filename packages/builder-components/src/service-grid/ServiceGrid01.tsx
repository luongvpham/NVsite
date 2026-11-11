import { colorVar, headingFontVar, radiusVar, shadowVar } from '@vsite/theme-engine';
import type { ServiceGridProps } from '../../generated/component-types';

// Khối giữ chỗ (skeleton) dùng màu viền của theme để nổi lên trên nền thẻ.
const placeholder = { backgroundColor: colorVar('border'), borderRadius: radiusVar };

const GRID_COLS_CLASS: Record<number, string> = { 2: 'grid-cols-2', 3: 'grid-cols-3', 4: 'grid-cols-4' };

/**
 * ServiceGrid01 — CHỈ render skeleton, không nối dữ liệu thật (07 §8.5). Binding Resolver
 * thật là Bước 9. `source` (kind: binding) chỉ mô tả cách lấy, không chứa dữ liệu materialize.
 */
export default function ServiceGrid01({ heading, showPrice, columns }: ServiceGridProps) {
  const placeholderCount = columns ?? 3;

  return (
    <div>
      {heading ? (
        <h2 className="mb-4 text-2xl font-semibold" style={{ fontFamily: headingFontVar }}>
          {heading}
        </h2>
      ) : null}
      <div className={`grid gap-4 ${GRID_COLS_CLASS[placeholderCount]}`}>
        {Array.from({ length: placeholderCount }, (_, i) => (
          <div
            key={i}
            className="border p-4"
            style={{
              borderColor: colorVar('border'),
              backgroundColor: colorVar('surface'),
              borderRadius: radiusVar,
              boxShadow: shadowVar,
            }}
          >
            <div className="aspect-square w-full" style={placeholder} aria-hidden />
            <div className="mt-2 h-4 w-3/4" style={placeholder} aria-hidden />
            {showPrice ? <div className="mt-1 h-4 w-1/3" style={placeholder} aria-hidden /> : null}
          </div>
        ))}
      </div>
    </div>
  );
}
