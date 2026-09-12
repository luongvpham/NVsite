import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const REPO_ROOT = path.resolve(import.meta.dirname, '..', '..', '..');

function readJson(relativePath: string): unknown {
  return JSON.parse(readFileSync(path.join(REPO_ROOT, relativePath), 'utf8'));
}

describe('config whitelist files (§2.1)', () => {
  it('image-presets.json is valid JSON with at least one entry', () => {
    const presets = readJson('config/image-presets.json') as Record<string, unknown>;
    expect(Object.keys(presets).length).toBeGreaterThan(0);
    expect(presets['1600x900,cover']).toBeDefined();
  });

  it('binding-sources.json is a non-empty array and never contains "Review"', () => {
    const sources = readJson('config/binding-sources.json') as string[];
    expect(Array.isArray(sources)).toBe(true);
    expect(sources.length).toBeGreaterThan(0);
    expect(sources).not.toContain('Review');
  });

  it('sanitize-profiles.json has inline and basic profiles', () => {
    const profiles = readJson('config/sanitize-profiles.json') as Record<string, { tags: string[] }>;
    expect(profiles.inline).toBeDefined();
    expect(profiles.basic).toBeDefined();
    expect(profiles.inline?.tags).toContain('a');
    expect(profiles.basic?.tags).toContain('blockquote');
  });
});
