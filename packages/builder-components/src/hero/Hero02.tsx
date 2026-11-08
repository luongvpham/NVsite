import { colorVar, headingFontVar, radiusVar } from '@vsite/theme-engine';
import type { HeroProps } from '../../generated/component-types';
import { useRenderContext } from '../context';

/** Hero02 — chia đôi: chữ trái, ảnh phải. */
export default function Hero02({ title, subtitle, image, align, cta }: HeroProps) {
  const ctx = useRenderContext();
  const imageUrl = image ? ctx.resolveImage(image.imageId, '1600x900,cover') : undefined;
  const ctaHref = cta?.target ? ctx.resolveUrl(cta.target) : undefined;

  return (
    <section className="flex min-h-[400px] items-stretch gap-8 px-6">
      <div className={`flex flex-1 flex-col justify-center ${align === 'center' ? 'text-center' : 'text-left'}`}>
        <h1 className="text-3xl font-bold" style={{ fontFamily: headingFontVar }}>
          {title}
        </h1>
        {subtitle ? <p className="mt-2 text-lg">{subtitle}</p> : null}
        {cta?.label ? (
          <a
            href={ctaHref ?? '#'}
            className="mt-4 inline-block px-4 py-2"
            style={{ backgroundColor: colorVar('primary'), color: colorVar('background'), borderRadius: radiusVar }}
          >
            {cta.label}
          </a>
        ) : null}
      </div>
      <div
        className="flex-1 bg-cover bg-center"
        style={{ backgroundColor: colorVar('surface'), ...(imageUrl ? { backgroundImage: `url(${imageUrl})` } : {}) }}
        aria-hidden
      />
    </section>
  );
}
