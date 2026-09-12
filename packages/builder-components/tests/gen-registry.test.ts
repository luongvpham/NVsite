import { execSync } from 'node:child_process';
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

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
];

function runGenRegistry() {
  execSync('npx tsx scripts/gen-registry.ts', { cwd: PKG_ROOT, stdio: 'pipe' });
}

/**
 * §6.1 invariant chéo gọi `fail()` → `process.exit(1)`, nên không unit-test in-process được
 * (sẽ kill luôn worker chạy test). Spawn tsx thật, bắt exit code + stderr thay vì throw.
 */
function runGenRegistryExpectFailure(): { code: number | null; stderr: string } {
  try {
    execSync('npx tsx scripts/gen-registry.ts', { cwd: PKG_ROOT, stdio: 'pipe' });
    return { code: 0, stderr: '' };
  } catch (error) {
    const e = error as { status: number | null; stderr: Buffer };
    return { code: e.status, stderr: e.stderr.toString('utf8') };
  }
}

function readGenerated() {
  return Object.fromEntries(
    GENERATED_FILES.map((f) => [f, readFileSync(path.join(PKG_ROOT, 'generated', f), 'utf8')]),
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
    20_000, // spawn 2 tsx process con — chậm hơn 5000ms mặc định khi máy đang bận
  );
});

describe('gen-registry — nhánh FAIL của cross-manifest invariants (§6.1) không bị bỏ sót', () => {
  // Không dùng test.concurrent — các test dưới đây sửa file thật trong registry/ rồi khôi phục,
  // cần chạy tuần tự trong CÙNG file test này để không đụng runGenRegistry() ở describe khác.

  it(
    'FAIL khi binding.sources chứa "Review" (#1, 01 §7) — hard-fail bất kể whitelist',
    () => {
      const file = path.join(REGISTRY_DIR, 'service-grid.manifest.ts');
      const original = readFileSync(file, 'utf8');
      expect(original).toContain("sources: ['Service', 'ServiceGroup']");
      writeFileSync(file, original.replace("sources: ['Service', 'ServiceGroup']", "sources: ['Service', 'ServiceGroup', 'Review']"));

      try {
        const { code, stderr } = runGenRegistryExpectFailure();
        expect(code).not.toBe(0);
        expect(stderr).toContain('Review');
      } finally {
        writeFileSync(file, original);
      }
    },
    20_000,
  );

  it(
    'FAIL khi image.preset không có trong config/image-presets.json (#2)',
    () => {
      const file = path.join(REGISTRY_DIR, 'hero.manifest.ts');
      const original = readFileSync(file, 'utf8');
      expect(original).toContain("preset: '1600x900,cover'");
      writeFileSync(file, original.replace("preset: '1600x900,cover'", "preset: 'not-a-real-preset'"));

      try {
        const { code, stderr } = runGenRegistryExpectFailure();
        expect(code).not.toBe(0);
        expect(stderr).toContain('image.preset');
      } finally {
        writeFileSync(file, original);
      }
    },
    20_000,
  );

  it(
    'FAIL khi type trùng giữa hai manifest (#10)',
    () => {
      const file = path.join(REGISTRY_DIR, 'section.manifest.ts');
      const original = readFileSync(file, 'utf8');
      expect(original).toContain("type: 'Section'");
      // Đổi type của Section trùng với Hero — checkCrossManifestInvariants phải fail trước khi
      // đụng tới việc Section thiếu component src/hero/*.tsx tương ứng.
      writeFileSync(file, original.replace("type: 'Section'", "type: 'Hero'"));

      try {
        const { code, stderr } = runGenRegistryExpectFailure();
        expect(code).not.toBe(0);
        expect(stderr).toContain('khai báo trùng');
      } finally {
        writeFileSync(file, original);
      }
    },
    20_000,
  );

  it('registry/ đã được khôi phục nguyên vẹn — gen:registry lại chạy sạch', () => {
    expect(() => {
      runGenRegistry();
    }).not.toThrow();
  });
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
