#!/usr/bin/env node
/**
 * Export runtime OpenAPI theo document module (backend/CLAUDE.md).
 * Build backend/src/Api (kích hoạt Microsoft.Extensions.ApiDescription.Server),
 * rồi copy từng document sinh ra vào contracts/openapi/.staging/{module}.v1.json.
 *
 * Usage: node tools/contract-sync/export.mjs [moduleName ...]
 *        (không truyền module nào → export tất cả document tìm thấy)
 */
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, copyFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const API_PROJECT = path.join(REPO_ROOT, 'backend', 'src', 'Api');
const OPENAPI_OUTPUT_DIR = path.join(API_PROJECT, 'obj', 'openapi');
const STAGING_DIR = path.join(REPO_ROOT, 'contracts', 'openapi', '.staging');

function buildApiProject() {
  console.log(`[export] dotnet build ${API_PROJECT}`);
  execFileSync('dotnet', ['build', API_PROJECT], { stdio: 'inherit' });
}

function exportDocuments(moduleFilter) {
  if (!existsSync(OPENAPI_OUTPUT_DIR)) {
    throw new Error(`Không tìm thấy thư mục OpenAPI output: ${OPENAPI_OUTPUT_DIR}. Build có sinh document không?`);
  }

  mkdirSync(STAGING_DIR, { recursive: true });

  const files = readdirSync(OPENAPI_OUTPUT_DIR).filter((f) => f.endsWith('.json'));
  const exported = [];

  for (const file of files) {
    // Quy ước Microsoft.Extensions.ApiDescription.Server: {Project}_{documentName}.json
    const match = file.match(/^Api_(.+)\.json$/);
    if (!match) continue;

    const moduleName = match[1];
    if (moduleFilter.length > 0 && !moduleFilter.includes(moduleName)) continue;

    const targetFile = path.join(STAGING_DIR, `${moduleName}.v1.json`);
    copyFileSync(path.join(OPENAPI_OUTPUT_DIR, file), targetFile);
    exported.push(targetFile);
  }

  if (exported.length === 0) {
    throw new Error(
      `Không export được document nào (filter: ${moduleFilter.join(',') || '(all)'}). ` +
        `Kiểm tra tên document đăng ký trong Api/Program.cs (AddOpenApi(...)).`,
    );
  }

  return exported;
}

const moduleFilter = process.argv.slice(2);
buildApiProject();
const exported = exportDocuments(moduleFilter);
for (const file of exported) {
  console.log(`[export] -> ${path.relative(REPO_ROOT, file)}`);
}
