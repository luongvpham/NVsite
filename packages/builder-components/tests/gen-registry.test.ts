import { execSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { cpSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { afterAll, describe, expect, it } from 'vitest';

const PKG_ROOT = path.resolve(import.meta.dirname, '..');
const REGISTRY_DIR = path.join(PKG_ROOT, 'registry');
const GENERATED_FILES = [
  'component-types.ts',
  'props-schemas.ts',
  'props-schemas.json',
  'property-panel.ts',
  'op-rules.ts',
  'ai-tool-schema.json',
  'registry-map.ts',
  'derivative-presets.json',
  'tree-envelope.schema.json',
  'theme-tokens.schema.json',
  'tree-rules.json',
];

/** Thư mục generated TẠM cho các test chỉ cần chạy codegen — không ghi đè `generated/` thật trong lúc
 * test của package khác (vd. builder-renderer) đang đọc nó song song (review TOOLING-001). */
const TEMP_GENERATED = mkdtempSync(path.join(tmpdir(), 'vsite-generated-'));

function runGenRegistry() {
  execSync('npx tsx scripts/gen-registry.ts', {
    cwd: PKG_ROOT,
    stdio: 'pipe',
    env: { ...process.env, VSITE_GENERATED_DIR: TEMP_GENERATED },
  });
}

/**
 * §6.1 invariant chéo gọi `fail()` → `process.exit(1)`, nên không unit-test in-process được
 * (sẽ kill luôn worker chạy test). Spawn tsx thật, bắt exit code + stderr thay vì throw.
 *
 * TOOLING-001 (trả nợ MEDIA-001 #16): chạy trên BẢN SAO TẠM của `registry/` + lock + generated
 * (biến môi trường của `scripts/lib/paths.ts`) — trước đây test sửa thẳng `registry/*.manifest.ts`
 * đã commit rồi khôi phục bằng `try/finally`; worker bị kill (vd. turbo huỷ task anh em) là file
 * hỏng nằm lại trong repo và reader song song thấy manifest hỏng.
 */
function runGenRegistryOnCopy(file: string, from: string, to: string): { code: number | null; stderr: string } {
  const tmp = mkdtempSync(path.join(tmpdir(), 'vsite-registry-'));
  try {
    const registryCopy = path.join(tmp, 'registry');
    cpSync(REGISTRY_DIR, registryCopy, { recursive: true });
    cpSync(path.join(PKG_ROOT, 'registry.lock.json'), path.join(tmp, 'registry.lock.json'));

    const target = path.join(registryCopy, file);
    const original = readFileSync(target, 'utf8');
    expect(original, `${file} không còn chứa đoạn cần đột biến — cập nhật test`).toContain(from);
    writeFileSync(target, original.replace(from, to));

    execSync('npx tsx scripts/gen-registry.ts', {
      cwd: PKG_ROOT,
      stdio: 'pipe',
      env: {
        ...process.env,
        VSITE_REGISTRY_DIR: registryCopy,
        VSITE_GENERATED_DIR: path.join(tmp, 'generated'),
        VSITE_REGISTRY_LOCK: path.join(tmp, 'registry.lock.json'),
      },
    });
    return { code: 0, stderr: '' };
  } catch (error) {
    const e = error as { status: number | null; stderr?: Buffer };
    return { code: e.status, stderr: e.stderr?.toString('utf8') ?? String(error) };
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
}

/** Như runGenRegistryOnCopy nhưng trả về nội dung một artifact sinh ra trên bản sao (đột biến hợp lệ, gen thành công). */
function generateOnCopyAndRead(file: string, from: string, to: string, artifact: string): string {
  const tmp = mkdtempSync(path.join(tmpdir(), 'vsite-registry-'));
  try {
    const registryCopy = path.join(tmp, 'registry');
    cpSync(REGISTRY_DIR, registryCopy, { recursive: true });
    cpSync(path.join(PKG_ROOT, 'registry.lock.json'), path.join(tmp, 'registry.lock.json'));
    const target = path.join(registryCopy, file);
    const original = readFileSync(target, 'utf8');
    expect(original, `${file} không còn chứa đoạn cần đột biến — cập nhật test`).toContain(from);
    writeFileSync(target, original.replace(from, to));
    execSync('npx tsx scripts/gen-registry.ts', {
      cwd: PKG_ROOT,
      stdio: 'pipe',
      env: {
        ...process.env,
        VSITE_REGISTRY_DIR: registryCopy,
        VSITE_GENERATED_DIR: path.join(tmp, 'generated'),
        VSITE_REGISTRY_LOCK: path.join(tmp, 'registry.lock.json'),
      },
    });
    return readFileSync(path.join(tmp, 'generated', artifact), 'utf8');
  } finally {
    rmSync(tmp, { recursive: true, force: true });
  }
}

/** Hash các manifest thật lúc nạp file test — test cuối khẳng định không test nào đụng vào chúng. */
const REAL_REGISTRY_HASHES = hashRegistry();

function hashRegistry(): Record<string, string> {
  return Object.fromEntries(
    readdirSync(REGISTRY_DIR)
      .sort()
      .map((f) => [f, createHash('sha256').update(readFileSync(path.join(REGISTRY_DIR, f))).digest('hex')]),
  );
}

function readGenerated() {
  return Object.fromEntries(
    GENERATED_FILES.map((f) => [f, readFileSync(path.join(TEMP_GENERATED, f), 'utf8')]),
  );
}

describe('gen-registry — codegen là hàm thuần (§9)', () => {
  it(
    'produces byte-identical output across two runs',
    () => {
      runGenRegistry();
      const first = readGenerated();
      runGenRegistry();
      const second = readGenerated();

      for (const file of GENERATED_FILES) {
        expect(second[file], `${file} khác nhau giữa hai lần chạy`).toBe(first[file]);
      }
    },
    60_000, // spawn 2 tsx process con — chậm hơn 5000ms mặc định; 20_000 từng timeout khi turbo chạy song song
  );
});

describe('gen-registry — nhánh FAIL của cross-manifest invariants (§6.1) không bị bỏ sót', () => {
  it(
    'FAIL khi binding.sources chứa "Review" (#1, 01 §7) — hard-fail bất kể whitelist',
    () => {
      const { code, stderr } = runGenRegistryOnCopy(
        'service-grid.manifest.ts',
        "sources: ['Service', 'ServiceGroup']",
        "sources: ['Service', 'ServiceGroup', 'Review']",
      );
      expect(code).not.toBe(0);
      expect(stderr).toContain('Review');
    },
    30_000,
  );

  it(
    'FAIL khi image.preset không có trong config/image-presets.json (#2)',
    () => {
      const { code, stderr } = runGenRegistryOnCopy('hero.manifest.ts', "preset: '1600x900,cover'", "preset: 'not-a-real-preset'");
      expect(code).not.toBe(0);
      expect(stderr).toContain('image.preset');
    },
    30_000,
  );

  it(
    'FAIL khi richText.profile trỏ vào khoá "dangerous" (khoá danh sách nguy hiểm, không phải profile)',
    () => {
      const { code, stderr } = runGenRegistryOnCopy('rich-text.manifest.ts', "profile: 'basic'", "profile: 'dangerous'");
      expect(code).not.toBe(0);
      expect(stderr).toContain('richText.profile');
    },
    30_000,
  );

  it(
    'FAIL khi type trùng giữa hai manifest (#10)',
    () => {
      // Đổi type của Section trùng với Hero — checkCrossManifestInvariants phải fail trước khi
      // đụng tới việc Section thiếu component src/hero/*.tsx tương ứng.
      const { code, stderr } = runGenRegistryOnCopy('section.manifest.ts', "type: 'Section'", "type: 'Hero'");
      expect(code).not.toBe(0);
      expect(stderr).toContain('khai báo trùng');
    },
    30_000,
  );

  it(
    'FAIL khi thu hẹp link.allowKinds so với lock (TOOLING-001 — trước đây lock không ghi allowKinds)',
    () => {
      const { code, stderr } = runGenRegistryOnCopy(
        'hero.manifest.ts',
        "allowKinds: ['page', 'systemPage', 'external', 'anchor'],",
        "allowKinds: ['page', 'external'],",
      );
      expect(code).not.toBe(0);
      expect(stderr).toContain('allowKinds');
    },
    30_000,
  );
});

describe('gen-registry — binding.imagePresets (#86)', () => {
  it(
    'FAIL khi key của imagePresets không nằm trong sources',
    () => {
      const { code, stderr } = runGenRegistryOnCopy(
        'service-grid.manifest.ts',
        "imagePresets: { Service: ['800x600,cover'], ServiceGroup: ['800x600,cover'] },",
        "imagePresets: { Shop: ['96x96,cover'] },",
      );
      expect(code).not.toBe(0);
      expect(stderr).toContain('imagePresets');
    },
    30_000,
  );

  it(
    'FAIL khi preset trong imagePresets không có trong config/image-presets.json',
    () => {
      const { code, stderr } = runGenRegistryOnCopy(
        'service-grid.manifest.ts',
        "imagePresets: { Service: ['800x600,cover'], ServiceGroup: ['800x600,cover'] },",
        "imagePresets: { Service: ['not-a-real-preset'] },",
      );
      expect(code).not.toBe(0);
      expect(stderr).toContain('imagePresets');
    },
    30_000,
  );

  it('generated/derivative-presets.json hợp theo manifest ∪ surfaces, không trùng, đã sort', () => {
    runGenRegistry();
    const content = JSON.parse(readFileSync(path.join(TEMP_GENERATED, 'derivative-presets.json'), 'utf8')) as Record<
      string,
      string[]
    >;

    // ServiceGrid.source khai imagePresets: { Service: ['800x600,cover'], ServiceGroup: ['800x600,cover'] }
    expect(content.Service).toEqual(['800x600,cover']);
    expect(content.ServiceGroup).toEqual(['800x600,cover']);
    // config/image-presets.json surfaces.Shop = ['320x96,inside', '96x96,cover'] — union, sort
    expect(content.Shop).toEqual(['320x96,inside', '96x96,cover']);

    // key và preset đều sort
    expect(Object.keys(content)).toEqual([...Object.keys(content)].sort());
    for (const presets of Object.values(content)) {
      expect(presets).toEqual([...new Set(presets)].sort());
    }
  }, 60_000); // spawn tsx — cùng lý do timeout với test byte-identical ở trên
});

describe('tree-rules.json (WEBSITE-001 S0-T3, #60) — luật cây cho BE, sinh từ manifest', () => {
  type PropRule = { kind: string; profile?: string; sources?: string[]; preset?: string; itemProps?: Record<string, PropRule> };
  type TypeRule = {
    acceptsChildren: boolean;
    allowedChildTypes: string[] | null;
    allowedInPageKinds: string[];
    maxPerPage: number | null;
    editableInSystemPageProps: string[];
    variants: string[];
    props: Record<string, PropRule>;
  };
  type TreeRules = { schemaVersion: number; types: Record<string, TypeRule> };

  const readTreeRules = (): TreeRules => {
    runGenRegistry();
    return JSON.parse(readFileSync(path.join(TEMP_GENERATED, 'tree-rules.json'), 'utf8')) as TreeRules;
  };

  it('khớp op-rules.ts từng type, từng trường (acceptsChildren, allowedChildTypes, allowedInPageKinds, maxPerPage, editableInSystemPageProps)', async () => {
    const { opRules } = await import('../generated/op-rules');
    const rules = readTreeRules();
    expect(Object.keys(rules.types).sort()).toEqual(Object.keys(opRules).sort());
    for (const [type, op] of Object.entries(opRules)) {
      const rule = rules.types[type];
      expect(rule, type).toBeDefined();
      expect(rule?.acceptsChildren, `${type}.acceptsChildren`).toBe(op.acceptsChildren);
      expect(rule?.allowedChildTypes, `${type}.allowedChildTypes`).toEqual(op.allowedChildTypes);
      expect(rule?.allowedInPageKinds, `${type}.allowedInPageKinds`).toEqual(op.allowedInPageKinds);
      expect(rule?.maxPerPage, `${type}.maxPerPage`).toBe(op.maxPerPage);
      expect(rule?.editableInSystemPageProps, `${type}.editableInSystemPageProps`).toEqual(op.editableInSystemPageProps);
    }
  }, 60_000);

  it('variants[] là key các variant của manifest, props có đủ mọi prop của manifest (khớp props-schemas.json)', async () => {
    const rules = readTreeRules();
    const { default: propsSchemas } = (await import('../generated/props-schemas.json')) as { default: Record<string, { properties: Record<string, unknown> }> };
    expect(rules.types.Hero?.variants).toEqual(['Hero01', 'Hero02']);
    expect(rules.types.Gallery?.variants).toEqual(['Gallery01', 'Gallery02']);
    for (const [type, rule] of Object.entries(rules.types)) {
      expect(Object.keys(rule.props).sort(), `${type}.props`).toEqual(Object.keys(propsSchemas[type]?.properties ?? {}).sort());
    }
  }, 60_000);

  it('bản đồ prop: kind cho mọi prop; richText kèm profile; binding kèm sources; image kèm preset', () => {
    const { types } = readTreeRules();
    expect(types.RichText?.props.content).toEqual({ kind: 'richText', profile: 'basic' });
    expect(types.Hero?.props.subtitle).toEqual({ kind: 'richText', profile: 'inline' });
    expect(types.Hero?.props.title).toEqual({ kind: 'text' });
    expect(types.Hero?.props.image).toEqual({ kind: 'image', preset: '1600x900,cover' });
    expect(types.ServiceGrid?.props.source).toEqual({ kind: 'binding', sources: ['Service', 'ServiceGroup'] });
    expect(types.ServiceGrid?.props.columns).toEqual({ kind: 'number' });
    for (const rule of Object.values(types)) {
      for (const [name, prop] of Object.entries(rule.props)) expect(typeof prop.kind, name).toBe('string');
    }
  });

  it('itemProps đệ quy cho list (Gallery.items) và group (Hero.cta)', () => {
    const { types } = readTreeRules();
    const items = types.Gallery?.props.items;
    expect(items?.kind).toBe('list');
    expect(items?.itemProps?.image?.kind).toBe('image');
    expect(items?.itemProps?.caption?.kind).toBe('text');
    const cta = types.Hero?.props.cta;
    expect(cta?.kind).toBe('group');
    expect(cta?.itemProps?.target?.kind).toBe('link');
  });

  it('không có "Review" ở bất kỳ binding.sources nào (R3)', () => {
    const { types } = readTreeRules();
    const walk = (props: Record<string, PropRule>): void => {
      for (const prop of Object.values(props)) {
        if (prop.kind === 'binding') expect(prop.sources).not.toContain('Review');
        if (prop.itemProps) walk(prop.itemProps);
      }
    };
    for (const rule of Object.values(types)) walk(rule.props);
  });

  it('manifest giả có richText.profile = inline → artifact có profile: inline (đột biến trên bản sao tạm)', () => {
    const rules = JSON.parse(generateOnCopyAndRead('rich-text.manifest.ts', "profile: 'basic'", "profile: 'inline'", 'tree-rules.json')) as TreeRules;
    expect(rules.types.RichText?.props.content).toEqual({ kind: 'richText', profile: 'inline' });
  }, 30_000);

  it('manifest giả thêm binding source (additive) → artifact có source đó', () => {
    const rules = JSON.parse(
      generateOnCopyAndRead('service-grid.manifest.ts', "sources: ['Service', 'ServiceGroup']", "sources: ['Service', 'ServiceGroup', 'Shop']", 'tree-rules.json'),
    ) as TreeRules;
    expect(rules.types.ServiceGrid?.props.source?.sources).toEqual(['Service', 'ServiceGroup', 'Shop']);
  }, 30_000);
});

describe('heroPropsSchema (generated) — validate SHAPE của prop', () => {
  it('rejects a value with the wrong type (overlayOpacity as string)', async () => {
    const { heroPropsSchema } = await import('../generated/props-schemas');
    const result = heroPropsSchema.safeParse({ title: 'x', overlayOpacity: 'nhiều' });
    expect(result.success).toBe(false);
  });

  it('accepts a value with all fields optional (variant-level required is NOT enforced here — see review note)', async () => {
    const { heroPropsSchema } = await import('../generated/props-schemas');
    const result = heroPropsSchema.safeParse({});
    expect(result.success).toBe(true);
  });

  it('accepts a fully valid Hero01 props object', async () => {
    const { heroPropsSchema } = await import('../generated/props-schemas');
    const result = heroPropsSchema.safeParse({
      title: 'Chào mừng',
      image: { imageId: 'media_1' },
      align: 'center',
    });
    expect(result.success).toBe(true);
  });
});

describe('test đột biến không đụng registry/ thật (TOOLING-001)', () => {
  it('manifest đã commit giữ nguyên từng byte sau mọi test ở trên', () => {
    expect(hashRegistry()).toEqual(REAL_REGISTRY_HASHES);
  });
});

afterAll(() => {
  rmSync(TEMP_GENERATED, { recursive: true, force: true });
});
