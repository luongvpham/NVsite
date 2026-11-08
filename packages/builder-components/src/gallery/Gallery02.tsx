import { colorVar, headingFontVar, radiusVar } from '@vsite/theme-engine';
import type { GalleryProps } from '../../generated/component-types';
import { useRenderContext } from '../context';

/** Gallery02 — băng cuộn ngang. */
export default function Gallery02({ heading, items }: GalleryProps) {
  const ctx = useRenderContext();

  return (
    <div>
      {heading ? (
        <h2 className="mb-4 text-2xl font-semibold" style={{ fontFamily: headingFontVar }}>
          {heading}
        </h2>
      ) : null}
      <div className="flex gap-4 overflow-x-auto">
        {(items ?? []).map((item, i) => (
          <figure key={i} className="w-40 flex-none">
            {item.image ? (
              <img
                src={ctx.resolveImage(item.image.imageId, '800x800,cover')}
                alt={item.image.alt ?? item.caption ?? ''}
                className="aspect-square w-full object-cover"
                style={{ borderRadius: radiusVar }}
              />
            ) : null}
            {item.caption ? (
              <figcaption className="mt-1 text-sm" style={{ color: colorVar('textMuted') }}>
                {item.caption}
              </figcaption>
            ) : null}
          </figure>
        ))}
      </div>
    </div>
  );
}
