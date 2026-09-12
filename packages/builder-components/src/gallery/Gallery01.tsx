import type { GalleryProps } from '../../generated/component-types';
import { useRenderContext } from '../context';

const GRID_COLS_CLASS: Record<number, string> = { 2: 'grid-cols-2', 3: 'grid-cols-3', 4: 'grid-cols-4' };

/** Gallery01 — lưới N cột. */
export default function Gallery01({ heading, items, columns }: GalleryProps) {
  const ctx = useRenderContext();

  return (
    <div>
      {heading ? <h2 className="mb-4 text-2xl font-semibold">{heading}</h2> : null}
      <div className={`grid gap-4 ${GRID_COLS_CLASS[columns ?? 3]}`}>
        {(items ?? []).map((item, i) => (
          <figure key={i}>
            {item.image ? (
              <img
                src={ctx.resolveImage(item.image.imageId, '800x800,cover')}
                alt={item.image.alt ?? item.caption ?? ''}
                className="aspect-square w-full rounded object-cover"
              />
            ) : null}
            {item.caption ? <figcaption className="mt-1 text-sm text-muted-foreground">{item.caption}</figcaption> : null}
          </figure>
        ))}
      </div>
    </div>
  );
}
