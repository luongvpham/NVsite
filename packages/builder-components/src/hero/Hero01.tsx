import type { HeroProps } from '../../generated/component-types';
import { useRenderContext } from '../context';

/** Hero01 — ảnh nền toàn khối. */
export default function Hero01({ title, subtitle, image, overlayOpacity, align, cta }: HeroProps) {
  const ctx = useRenderContext();
  const imageUrl = image ? ctx.resolveImage(image.imageId, '1600x900,cover') : undefined;
  const ctaHref = cta?.target ? ctx.resolveUrl(cta.target) : undefined;

  return (
    <section
      className={`relative flex min-h-[400px] items-center bg-cover bg-center ${align === 'center' ? 'justify-center text-center' : 'justify-start text-left'}`}
      style={imageUrl ? { backgroundImage: `url(${imageUrl})` } : undefined}
    >
      <div className="absolute inset-0 bg-black" style={{ opacity: (overlayOpacity ?? 35) / 100 }} />
      <div className="relative z-10 max-w-xl px-6 text-white">
        <h1 className="text-3xl font-bold">{title}</h1>
        {subtitle ? <p className="mt-2 text-lg">{subtitle}</p> : null}
        {cta?.label ? (
          <a href={ctaHref ?? '#'} className="mt-4 inline-block rounded bg-white px-4 py-2 text-black">
            {cta.label}
          </a>
        ) : null}
      </div>
    </section>
  );
}
