// Sinh barrel cho src/generated (N5 phương án M, TOOLING-004).
//
// Trước đây src/index.ts và src/mocks.ts liệt kê tay từng file model — thêm module/tag mới mà quên
// sửa barrel thì FE không import được (WEBSITE-005 thiếu export của WEBSITE-004). Giờ barrel suy ra
// từ chính output của Orval, nên `pnpm gen:api` là đủ.
//
// Bố cục Orval mode 'tags-split' mà hàm này dựa vào:
//   {module}/{tag}/{tag}.ts       → client react-query  → index.ts
//   {module}/{tag}/{tag}.msw.ts   → handler MSW         → mocks.ts (tách riêng: không kéo msw/faker vào bundle)
//   {module}/model/*.ts           → type; bỏ model/index.ts của Orval
//   {module}/{module}.zod.ts      → schema zod          → index.ts
//
// Model trùng tên giữa các module (ProblemDetails… mọi document backend đều tham chiếu) gây TS2308
// khi cùng `export *`. Chỉ export bản đầu tiên — và CHỈ KHI các bản giống hệt nhau từng byte. Trùng
// tên mà khác nội dung là hai type khác nhau mang cùng tên: dừng, không chọn hộ.

import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

export const BARREL_FILES = ['index.ts', 'mocks.ts'];

const HEADER = '// GENERATED — DO NOT EDIT (pnpm gen:api → scripts/lib/barrel.mjs)\n';

const tsFiles = (dir) =>
  readdirSync(dir, { withFileTypes: true })
    .filter((e) => e.isFile() && e.name.endsWith('.ts'))
    .map((e) => e.name)
    .sort();

const subdirs = (dir) =>
  readdirSync(dir, { withFileTypes: true })
    .filter((e) => e.isDirectory())
    .map((e) => e.name)
    .sort();

const spec = (...parts) => `export * from './${parts.join('/').replace(/\.ts$/, '')}';`;

/**
 * @param {string} generatedDir thư mục src/generated
 * @returns {{ index: string, mocks: string }} nội dung hai barrel
 */
export function buildBarrels(generatedDir) {
  const index = [];
  const mocks = [];
  /** tên file model → { module, content } của bản đã export */
  const exportedModels = new Map();

  for (const module of subdirs(generatedDir)) {
    const moduleDir = path.join(generatedDir, module);
    index.push('', `// ${module}`);

    for (const tag of subdirs(moduleDir).filter((d) => d !== 'model')) {
      for (const file of tsFiles(path.join(moduleDir, tag))) {
        (file.endsWith('.msw.ts') ? mocks : index).push(spec(module, tag, file));
      }
    }

    const modelDir = path.join(moduleDir, 'model');
    const models = subdirs(moduleDir).includes('model') ? tsFiles(modelDir) : [];
    // Tag OpenAPI tên "model" sẽ rơi client vào chung thư mục với type — barrel không phân biệt được.
    if (models.some((f) => f.endsWith('.msw.ts'))) {
      throw new Error(`[barrel] '${module}/model/' có file .msw — một tag OpenAPI tên "model" trùng thư mục type. Đổi tên tag.`);
    }
    // Chỉ dedupe model. Trùng tên operation/zod giữa module thì `pnpm typecheck` báo TS2308.
    for (const file of models.filter((f) => f !== 'index.ts')) {
      const content = readFileSync(path.join(modelDir, file), 'utf8');
      const first = exportedModels.get(file);
      if (first) {
        if (first.content !== content) {
          throw new Error(
            `[barrel] model '${file}' có ở cả '${first.module}' và '${module}' nhưng nội dung khác nhau — ` +
              'hai type khác nhau trùng tên. Đổi tên schema ở backend; không chọn hộ một bản.',
          );
        }
        continue;
      }
      exportedModels.set(file, { module, content });
      index.push(spec(module, 'model', file));
    }

    // Orval sinh zod RỖNG (chỉ comment) cho module không có body/param để validate (vd. platform) — `export *`
    // từ file không có export là TS2306 "is not a module". Bỏ file không có câu `export` nào.
    const hasExport = (f) => /^export\s/m.test(readFileSync(path.join(moduleDir, f), 'utf8'));
    for (const file of tsFiles(moduleDir).filter((f) => !f.endsWith('.msw.ts') && hasExport(f))) {
      index.push(spec(module, file));
    }
  }

  return {
    index: `${HEADER}${index.join('\n')}\n`,
    mocks: `${HEADER}\n${mocks.join('\n')}\n`,
  };
}
