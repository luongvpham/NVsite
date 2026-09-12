import type { PropsWithChildren } from 'react';
import { colorTokens } from '@vsite/theme-engine';
import type { SectionProps } from '../../generated/component-types';

const PADDING_CLASS: Record<string, string> = { sm: 'py-6', lg: 'py-16' };

/** Section02 — tràn viền, không giới hạn bề rộng. */
export default function Section02({ background, paddingY, children }: PropsWithChildren<SectionProps>) {
  const bg = (colorTokens as Record<string, string>)[background ?? 'background'] ?? background;

  return (
    <section className={`px-6 ${PADDING_CLASS[paddingY ?? 'lg']}`} style={{ backgroundColor: bg }}>
      {children}
    </section>
  );
}
