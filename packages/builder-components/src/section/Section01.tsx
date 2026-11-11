import type { PropsWithChildren } from 'react';
import type { SectionProps } from '../../generated/component-types';
import { sectionStyle } from './section-style';

const MAX_WIDTH_CLASS: Record<string, string> = { lg: 'max-w-4xl', xl: 'max-w-6xl' };

/** Section01 — một cột, giới hạn bề rộng. Container thật — test acceptsChildren (05 §6 quy tắc 5). */
export default function Section01({ background, paddingY, maxWidth, children }: PropsWithChildren<SectionProps>) {
  return (
    <section style={sectionStyle(background, paddingY)}>
      <div className={`mx-auto px-6 ${MAX_WIDTH_CLASS[maxWidth ?? 'xl']}`}>{children}</div>
    </section>
  );
}
