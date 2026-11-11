// @vitest-environment node
import { renderToString } from 'react-dom/server';
import type { CSSProperties } from 'react';
import { describe, expect, it } from 'vitest';
import type { ComponentNode } from '@vsite/builder-components';
import { defaultThemeVars, themeStyle } from '@vsite/theme-engine';
import { RenderContextProvider } from '../src/context';
import { RenderTree } from '../src/renderTree';
import homeTree from './fixtures/home.tree.json';

/**
 * Theme per-shop đi qua CSS variables ở KHUNG CHỨA trang; component bên trong chỉ đọc `var(--vs-*)`. Render phía server
 * (`renderToString`, không window/document — #23) phải ra đúng biến để apps/web SSR không nháy theme mặc định rồi mới đổi.
 */
const tree = homeTree as ComponentNode;

function page(style: CSSProperties) {
  return renderToString(
    <div style={style}>
      <RenderContextProvider>
        <RenderTree tree={tree} />
      </RenderContextProvider>
    </div>,
  );
}

describe('SSR theme (CSS variables)', () => {
  it('chạy ở môi trường không có window/document', () => {
    expect(typeof globalThis.window).toBe('undefined');
    expect(typeof globalThis.document).toBe('undefined');
  });

  it('theme tuỳ biến → khung chứa mang biến của theme, component bên trong đọc đúng biến đó', () => {
    const style = themeStyle({
      schemaVersion: 1,
      colors: { primary: '#112233', secondary: '#a78bfa', background: '#fafafa', surface: '#eeeeee', text: '#222222', textMuted: '#666666', border: '#dddddd' },
      typography: { headingFont: 'Playfair Display', bodyFont: 'Lora', scale: 'spacious' },
      shape: { radius: 'full', shadow: 'strong' },
      spacing: { sectionGap: 'xl' },
    }) as CSSProperties;
    const html = page(style);

    expect(html).toContain('--vs-color-primary:#112233');
    expect(html).toContain('--vs-radius:9999px');
    expect(html).toContain('--vs-font-heading:&#x27;Playfair Display&#x27;, Georgia, serif');
    expect(html).toContain('var(--vs-');
  });

  it('không đặt theme → vẫn render đẹp nhờ giá trị dự phòng trong var(), không rơi về màu chrome', () => {
    const html = renderToString(
      <RenderContextProvider>
        <RenderTree tree={tree} />
      </RenderContextProvider>,
    );
    expect(html).toContain('var(--vs-color-');
    expect(html).toMatch(new RegExp(`var\\(--vs-color-[a-z-]+, ${defaultThemeVars['--vs-color-primary']}\\)|var\\(--vs-color-[a-z-]+, #`));
    expect(html).not.toMatch(/\b(bg-primary|text-muted-foreground|bg-muted|border-border)\b/);
  });
});
