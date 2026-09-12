import type { PropsWithChildren } from 'react';
import { colorTokens } from '@vsite/theme-engine';
import type { SectionProps } from '../../generated/component-types';

const PADDING_CLASS: Record<string, string> = { sm: 'py-6', lg: 'py-16' };
const MAX_WIDTH_CLASS: Record<string, string> = { lg: 'max-w-4xl', xl: 'max-w-6xl' };

/** Section01 — một cột, giới hạn bề rộng. Container thật — test acceptsChildren (05 §6 quy tắc 5). */
export default function Section01({ background, paddingY, maxWidth, children }: PropsWithChildren<SectionProps>) {
  const bg = (colorTokens as Record<string, string>)[background ?? 'background'] ?? background;

  return (
    <section className={PADDING_CLASS[paddingY ?? 'lg']} style={{ backgroundColor: bg }}>
      <div className={`mx-auto px-6 ${MAX_WIDTH_CLASS[maxWidth ?? 'xl']}`}>{children}</div>
    </section>
  );
}
