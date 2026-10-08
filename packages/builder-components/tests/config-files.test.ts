import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const REPO_ROOT = path.resolve(import.meta.dirname, '..', '..', '..');

function readJson(relativePath: string): unknown {
  return JSON.parse(readFileSync(path.join(REPO_ROOT, relativePath), 'utf8'));
}

type ImagePreset = { width: number; height: number; fit: string; usage: string };
type ImagePresetsFile = { presets: Record<string, ImagePreset>; surfaces: Record<string, string[]> };

const EXPECTED_PRESET_KEYS = [
  '1600x900,cover',
  '1600x600,cover',
  '1200x630,cover',
  '1200x1200,inside',
  '800x800,cover',
  '800x600,cover',
  '320x96,inside',
  '160x160,cover',
  '96x96,cover',
];

describe('config whitelist files (§2.1)', () => {
  it('image-presets.json has shape { presets, surfaces } with exactly the 9 expected presets', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    expect(Object.keys(file.presets).sort()).toEqual([...EXPECTED_PRESET_KEYS].sort());
  });

  it('image-presets.json presets all use fit ∈ {cover, inside} and a numeric height (no null)', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    for (const [key, preset] of Object.entries(file.presets)) {
      expect(['cover', 'inside'], `${key}.fit`).toContain(preset.fit);
      expect(typeof preset.height, `${key}.height`).toBe('number');
    }
  });

  it('image-presets.json no longer has the "600xR,cover" preset', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    expect(file.presets['600xR,cover']).toBeUndefined();
  });

  it('image-presets.json surfaces reference only presets that exist in presets', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    for (const [surface, presetKeys] of Object.entries(file.surfaces)) {
      for (const presetKey of presetKeys) {
        expect(file.presets[presetKey], `surfaces.${surface} -> ${presetKey}`).toBeDefined();
      }
    }
  });

  it('image-presets.json surfaces keys are all valid binding sources', () => {
    const file = readJson('config/image-presets.json') as ImagePresetsFile;
    const sources = readJson('config/binding-sources.json') as string[];
    for (const surface of Object.keys(file.surfaces)) {
      expect(sources, `surfaces key '${surface}'`).toContain(surface);
    }
  });

  it('binding-sources.json is a non-empty array and never contains "Review"', () => {
    const sources = readJson('config/binding-sources.json') as string[];
    expect(Array.isArray(sources)).toBe(true);
    expect(sources.length).toBeGreaterThan(0);
    expect(sources).not.toContain('Review');
  });

  it('binding-sources.json contains "Shop" (#73)', () => {
    const sources = readJson('config/binding-sources.json') as string[];
    expect(sources).toContain('Shop');
  });

  it('sanitize-profiles.json has inline and basic profiles', () => {
    const profiles = readJson('config/sanitize-profiles.json') as Record<string, { tags: string[] }>;
    expect(profiles.inline).toBeDefined();
    expect(profiles.basic).toBeDefined();
    expect(profiles.inline?.tags).toContain('a');
    expect(profiles.basic?.tags).toContain('blockquote');
  });

  // `media` là route phục vụ ảnh trên mọi domain (08 §5) — không shop nào được lấy slug này (#24).
  it('reserved-routes.json reservedPaths contains media (#24)', () => {
    const config = readJson('config/reserved-routes.json') as { reservedPaths: string[] };
    expect(config.reservedPaths).toContain('media');
  });
});
