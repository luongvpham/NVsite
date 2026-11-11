// @vitest-environment node
import type { ComponentType, ReactNode } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { registryMap } from '../generated/registry-map';

/**
 * Component đọc màu/bo góc/font/khoảng cách từ CSS variable của theme per-shop (`--vs-*`, TOOLING-001 #1), KHÔNG từ token
 * tĩnh của chrome (`bg-primary`, `text-muted-foreground`…). Render bằng `renderToStaticMarkup` ở môi trường Node: không có
 * jsdom để parse CSS nên chuỗi `style` được kiểm nguyên văn, và đồng thời chứng minh render được khi SSR (#23).
 */
const CASES: Record<string, Record<string, unknown>> = {
  'Hero/Hero01': { title: 'Chào', subtitle: 'Phụ', cta: { label: 'Đặt lịch', target: { kind: 'external', url: 'https://vsite.vn' } } },
  'Hero/Hero02': { title: 'Chào', subtitle: 'Phụ', cta: { label: 'Đặt lịch', target: { kind: 'external', url: 'https://vsite.vn' } } },
  'Gallery/Gallery01': { heading: 'Ảnh', items: [{ caption: 'Một' }, { caption: 'Hai' }] },
  'Gallery/Gallery02': { heading: 'Ảnh', items: [{ caption: 'Một' }] },
  'ServiceGrid/ServiceGrid01': { heading: 'Dịch vụ', showPrice: true },
  'Section/Section01': { background: 'primary' },
  'Section/Section02': { background: 'primary' },
  'RichText/RichText01': { content: '<p>Xin chào</p>' },
};

const render = (key: string, props: Record<string, unknown> = CASES[key] ?? {}): string => {
  const Component = registryMap[key] as ComponentType<Record<string, unknown> & { children?: ReactNode }> | undefined;
  if (!Component) throw new Error(`registryMap thiếu ${key}`);
  return renderToStaticMarkup(<Component {...props}>{key.startsWith('Section') ? <p>nội dung</p> : null}</Component>);
};

/** Class Tailwind lấy màu từ preset TĨNH của chrome — component builder không được dùng nữa. */
const STATIC_CHROME_CLASSES = /\b(bg-primary|text-primary-foreground|text-muted-foreground|bg-muted|border-border|bg-white|text-black|bg-background|text-foreground)\b/;

describe('mọi variant: không còn màu của chrome tĩnh', () => {
  it('đủ 8 variant được phủ (thêm variant mới → phải thêm ca ở đây)', () => {
    expect(Object.keys(registryMap).sort()).toEqual(Object.keys(CASES).sort());
  });

  it.each(Object.keys(CASES))('%s', (key) => {
    const html = render(key);
    expect(html).not.toMatch(STATIC_CHROME_CLASSES);
    // Không còn mã hex cứng của theme tĩnh (chrome) trong style.
    expect(html).not.toMatch(/#2563eb|#0a0a0a|#f4f4f5|#71717a|#e4e4e7/i);
  });

  it.each(Object.keys(CASES).filter((key) => key !== 'RichText/RichText01'))('%s đọc biến --vs-*', (key) => {
    expect(render(key)).toContain('var(--vs-');
  });
});

describe('Section — background là KEY màu của theme', () => {
  it.each(['Section/Section01', 'Section/Section02'])('%s: key hợp lệ → biến của key đó, kèm màu mặc định làm dự phòng', (key) => {
    expect(render(key, { background: 'primary' })).toContain('background-color:var(--vs-color-primary, #7c3aed)');
    expect(render(key, { background: 'textMuted' })).toContain('background-color:var(--vs-color-text-muted, #6b7280)');
  });

  it.each(['Section/Section01', 'Section/Section02'])('%s: thiếu/key lạ/key cũ của theme tĩnh → background', (key) => {
    const background = 'background-color:var(--vs-color-background, #ffffff)';
    expect(render(key, {})).toContain(background);
    expect(render(key, { background: 'muted' })).toContain(background);
    expect(render(key, { background: 'foreground' })).toContain(background);
    expect(render(key, { background: '#ff0000' })).toContain(background); // không nhận màu tự do (allowCustom:false)
  });

  it.each(['Section/Section01', 'Section/Section02'])('%s: khoảng cách trên/dưới theo --vs-section-gap (sm nhỏ hơn lg)', (key) => {
    const lg = render(key, { paddingY: 'lg' });
    const sm = render(key, { paddingY: 'sm' });
    expect(lg).toContain('padding-top:var(--vs-section-gap, 4rem)');
    expect(lg).toContain('padding-bottom:var(--vs-section-gap, 4rem)');
    expect(sm).toContain('padding-top:calc(var(--vs-section-gap, 4rem) * 0.4)');
    expect(sm).toContain('padding-bottom:calc(var(--vs-section-gap, 4rem) * 0.4)');
    expect(lg).not.toMatch(/\bpy-(6|16)\b/);
  });

  it('Section01 vẫn giới hạn bề rộng theo maxWidth', () => {
    expect(render('Section/Section01', { maxWidth: 'lg' })).toContain('max-w-4xl');
    expect(render('Section/Section01', {})).toContain('max-w-6xl');
  });
});

describe('Hero / Gallery / ServiceGrid dùng biến cho bo góc, font tiêu đề, màu chữ phụ', () => {
  it('Hero: nút CTA dùng màu primary + bo góc theo theme; tiêu đề dùng font heading', () => {
    for (const key of ['Hero/Hero01', 'Hero/Hero02']) {
      const html = render(key);
      expect(html, key).toContain('background-color:var(--vs-color-primary, #7c3aed)');
      expect(html, key).toContain('border-radius:var(--vs-radius, 0.5rem)');
      expect(html, key).toContain('font-family:var(--vs-font-heading, ');
    }
  });

  it('Gallery: chú thích dùng màu chữ phụ, ảnh bo góc theo theme, tiêu đề dùng font heading', () => {
    for (const key of ['Gallery/Gallery01', 'Gallery/Gallery02']) {
      const html = render(key, { heading: 'Ảnh', items: [{ caption: 'Một', image: { imageId: 'img-1' } }] });
      expect(html, key).toContain('color:var(--vs-color-text-muted, #6b7280)');
      expect(html, key).toContain('border-radius:var(--vs-radius, 0.5rem)');
      expect(html, key).toContain('font-family:var(--vs-font-heading, ');
    }
  });

  it('ServiceGrid: thẻ dùng viền/nền/bo góc/bóng theo theme', () => {
    const html = render('ServiceGrid/ServiceGrid01');
    expect(html).toContain('border-color:var(--vs-color-border, #e5e7eb)');
    expect(html).toContain('background-color:var(--vs-color-surface, #f9fafb)');
    expect(html).toContain('border-radius:var(--vs-radius, 0.5rem)');
    expect(html).toContain('box-shadow:var(--vs-shadow, ');
  });
});
