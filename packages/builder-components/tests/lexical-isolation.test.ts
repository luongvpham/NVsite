import { readFileSync } from 'node:fs';
import path from 'node:path';
import fg from 'fast-glob';
import { describe, expect, it } from 'vitest';

/**
 * Lexical chỉ được vào bundle qua `import()` động của Inspector (WEBSITE-002 F10): `apps/web` import package này để render
 * component, nên một import tĩnh lọt vào là kéo cả Lexical vào bundle khách xem. Test này giữ ranh giới bằng cấu trúc
 * (nhanh, chạy ở mọi lần test); kiểm bundle thật là bước `pnpm --filter @vsite/web build` + tìm "lexical" trong output.
 */
const srcDir = path.resolve(import.meta.dirname, '..', 'src');
const files = fg
  .sync('**/*.{ts,tsx}', { cwd: srcDir })
  .filter((f) => !f.startsWith('rich-text-editor/') && !/\.test\.tsx?$/.test(f))
  .map((f) => ({ f, text: readFileSync(path.join(srcDir, f), 'utf8') }));

describe('Lexical chỉ vào qua import động', () => {
  it('không file nào ngoài rich-text-editor/ import lexical hay @lexical/*', () => {
    const offenders = files.filter(({ text }) => /from\s+['"](?:@?lexical)(?:\/[^'"]*)?['"]/.test(text)).map((x) => x.f);
    expect(offenders).toEqual([]);
  });

  it('RichTextEditor chỉ được tham chiếu bằng import() động, không import tĩnh (kể cả index.ts)', () => {
    for (const { f, text } of files) {
      expect(text, f).not.toMatch(/^\s*import\s[^;]*from\s+['"][^'"]*rich-text-editor\/RichTextEditor['"]/m);
      expect(text, f).not.toMatch(/^\s*export\s[^;]*from\s+['"][^'"]*rich-text-editor\/(?:RichTextEditor|lexical-html)['"]/m);
    }
    const inspector = files.find((x) => x.f === 'inspector/Inspector.tsx')?.text ?? '';
    expect(inspector).toMatch(/lazy\(\(\)\s*=>\s*import\(['"]\.\.\/rich-text-editor\/RichTextEditor['"]\)\)/);
  });
});
