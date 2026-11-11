import type { PropsWithChildren } from 'react';
import type { SectionProps } from '../../generated/component-types';
import { sectionStyle } from './section-style';

/** Section02 — tràn viền, không giới hạn bề rộng. */
export default function Section02({ background, paddingY, children }: PropsWithChildren<SectionProps>) {
  return (
    <section className="px-6" style={sectionStyle(background, paddingY)}>
      {children}
    </section>
  );
}
