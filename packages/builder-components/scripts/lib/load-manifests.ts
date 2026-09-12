import fg from 'fast-glob';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { componentManifestSchema, type ComponentManifest } from '../../meta/manifest-schema';

export function fail(message: string): never {
  console.error(`[gen-registry] FAIL: ${message}`);
  process.exit(1);
}

export async function loadManifests(pkgRoot: string): Promise<ComponentManifest[]> {
  const files = await fg('registry/*.manifest.ts', { cwd: pkgRoot, absolute: true });
  const manifests: ComponentManifest[] = [];

  for (const file of files) {
    const mod = (await import(pathToFileURL(file).href)) as { default: unknown };
    const result = componentManifestSchema.safeParse(mod.default);
    if (!result.success) {
      fail(
        `${path.relative(pkgRoot, file)} không hợp lệ:\n${result.error.issues.map((i) => `  - ${i.path.join('.')}: ${i.message}`).join('\n')}`,
      );
    }
    // componentManifestSchema validate đúng runtime shape; ép kiểu về ComponentManifest (không
    // dùng z.infer — xem ghi chú trong meta/manifest-schema.ts).
    manifests.push(result.data as unknown as ComponentManifest);
  }

  return manifests;
}
