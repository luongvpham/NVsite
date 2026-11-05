#!/usr/bin/env node
/**
 * Runner headless cho skill weekend-burn (Docs/process/weekend-burn-plan.md §7 Giai đoạn 2).
 *
 * Mỗi bước trong "## Hàng đợi" của batch file = một `claude -p "/weekend-burn <batch> --one"` mới:
 * context sạch, crash/rate-limit chỉ mất bước đang dở. Session con tự ghi Log + tick hàng đợi;
 * runner chỉ kiểm lại bằng file + git (Rule 14: không tin lời agent) rồi quyết chạy tiếp hay dừng.
 *
 * Usage: node tools/burn/run.mjs <batch.md> [--budget-per-step 15] [--max-steps 50]
 *                                [--step-timeout-min 180] [--dry-run]
 * Dừng an toàn: tạo file .superpowers/burn/STOP (runner dừng trước bước kế).
 */
import { spawnSync, execSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const STOP_FILE = path.join(REPO_ROOT, '.superpowers', 'burn', 'STOP');
const DISALLOWED = ['Bash(git push *)', 'PowerShell(git push *)'];

// ---------- args ----------
const IS_MAIN = !!process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url);
const argv = IS_MAIN ? process.argv.slice(2) : ['(imported)'];
const opt = (name, def) => {
  const i = argv.indexOf(name);
  return i >= 0 ? argv[i + 1] : def;
};
const VALUE_OPTS = new Set(['--budget-per-step', '--max-steps', '--step-timeout-min']);
const batchArg = argv.find((a, i) => !a.startsWith('--') && !VALUE_OPTS.has(argv[i - 1]));
if (!batchArg) {
  console.error('Usage: node tools/burn/run.mjs <batch.md> [--budget-per-step N] [--max-steps N] [--step-timeout-min N] [--dry-run]');
  process.exit(1);
}
const BATCH = path.resolve(REPO_ROOT, batchArg);
const BATCH_REL = path.relative(REPO_ROOT, BATCH).replace(/\\/g, '/');
const BUDGET = Number(opt('--budget-per-step', '15'));
const MAX_STEPS = Number(opt('--max-steps', '50'));
const STEP_TIMEOUT_MS = Number(opt('--step-timeout-min', '180')) * 60_000;
const DRY = argv.includes('--dry-run');
const LOG_DIR = path.join(REPO_ROOT, '.superpowers', 'burn', path.basename(BATCH, '.md'));

// ---------- batch file ----------
export function parseBatch(text) {
  const section = (title) => {
    const m = text.match(new RegExp(`^## ${title}\\s*$([\\s\\S]*?)(?=^## |(?![\\s\\S]))`, 'm'));
    return m ? m[1] : '';
  };
  const queue = [...section('Hàng đợi').matchAll(/^- \[( |x|X)\] (\S+) (\S+)/gm)].map((m) => ({
    done: m[1] !== ' ',
    id: m[2],
    step: m[3],
  }));
  const log = section('Log')
    .split('\n')
    .filter((l) => l.startsWith('|') && !/^\|\s*-/.test(l) && !/Thời điểm/.test(l))
    .map((l) => l.split('|').slice(1, -1).map((c) => c.trim()))
    .map(([at, id, step, status, commit, note]) => ({ at, id, step, status, commit, note }));
  return { queue, log };
}

export function nextItem({ queue, log }, skip = new Set()) {
  const blocked = new Set(log.filter((r) => r.status === 'BLOCKED').map((r) => r.id));
  return queue.find((q) => !q.done && !blocked.has(q.id) && !skip.has(`${q.id} ${q.step}`));
}

const read = () => readFileSync(BATCH, 'utf8');
const write = (t) => writeFileSync(BATCH, t);
const now = () => {
  const d = new Date();
  const p = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`; // giờ máy, không UTC
};

function setBanner(field, value) {
  const t = read().replace(new RegExp(`(\\*\\*${field}:\\*\\* )\`?[^·\\n]*?\`?(\\s*(·|$))`, 'm'), `$1\`${value}\`$2`);
  write(t);
}

function appendLog(id, step, status, note) {
  const t = read();
  const row = `| ${now()} | ${id} | ${step} | ${status} | — | ${note.replace(/\|/g, '/')} |`;
  const m = t.match(/^## Log\s*$[\s\S]*?(?=^## |(?![\s\S]))/m);
  if (!m) throw new Error('Batch file thiếu mục "## Log"');
  const block = m[0].replace(/\s*$/, '');
  write(t.replace(m[0], `${block}\n${row}\n\n`));
}

function writeSummary() {
  const { queue, log } = parseBatch(read());
  const ids = [...new Set(queue.map((q) => q.id))];
  const rows = ids.map((id) => {
    const r = log.filter((l) => l.id === id);
    const c = (s) => r.filter((l) => l.status === s).length;
    const left = queue.filter((q) => q.id === id && !q.done).length;
    return `| ${id} | ${c('PASS')} | ${c('PASS-WITH-DEBT')} | ${c('BLOCKED')} | ${left} | [review.md](../../tasks/${id}/review.md) |`;
  });
  const table = ['| Task | PASS | DEBT | BLOCKED | Chưa chạy | Review |', '|---|---|---|---|---|---|', ...rows].join('\n');
  let t = read();
  t = /^## Tóm tắt\s*$/m.test(t)
    ? t.replace(/^## Tóm tắt\s*$[\s\S]*?(?=^## |(?![\s\S]))/m, `## Tóm tắt\n\n${table}\n\n`)
    : `${t.replace(/\s*$/, '')}\n\n## Tóm tắt\n\n${table}\n`;
  write(t);
}

// ---------- git / env ----------
const sh = (cmd) => execSync(cmd, { cwd: REPO_ROOT, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
const treeClean = () => sh('git status --porcelain') === '';
const branchExists = (id) => {
  try { sh(`git rev-parse --verify --quiet refs/heads/feature/${id}`); return true; } catch { return false; }
};
const dockerOk = () => {
  try { execSync('docker info', { stdio: 'ignore', timeout: 20_000 }); return true; } catch { return false; }
};

function runClaude(item, n) {
  const args = [
    '-p',
    '--permission-mode', 'auto',
    '--output-format', 'json',
    '--max-budget-usd', String(BUDGET),
    '--disallowedTools', ...DISALLOWED,
  ];
  const prompt = `/weekend-burn ${BATCH_REL} --one`;
  const win = process.platform === 'win32';
  const shown = `claude ${args.map((a) => (/[\s()*]/.test(a) ? `"${a}"` : a)).join(' ')}  <<< "${prompt}"`;
  if (DRY) {
    console.log(`[dry-run] ${item.id} ${item.step}: ${shown}`);
    return { dry: true };
  }
  console.log(`[burn] ${now()} → ${item.id} ${item.step}`);
  // Windows: claude là claude.cmd → cần shell; tự ghép + quote thành một chuỗi lệnh cho cmd.exe
  // (truyền mảng args kèm shell:true bị Node cảnh báo DEP0190). Prompt đi qua stdin.
  // Gỡ biến CLAUDECODE / CLAUDE_CODE_* của session cha: runner gọi từ trong Claude Code vẫn chạy như terminal thường.
  const env = Object.fromEntries(Object.entries(process.env).filter(([k]) => k !== 'CLAUDECODE' && !k.startsWith('CLAUDE_CODE_')));
  const res = spawnSync(win ? ['claude', ...args.map((a) => `"${a}"`)].join(' ') : 'claude', win ? [] : args, {
    cwd: REPO_ROOT,
    env,
    input: prompt,
    encoding: 'utf8',
    shell: win,
    timeout: STEP_TIMEOUT_MS,
    maxBuffer: 64 * 1024 * 1024,
  });
  mkdirSync(LOG_DIR, { recursive: true });
  const file = path.join(LOG_DIR, `${String(n).padStart(2, '0')}-${item.id}-${item.step}.json`);
  writeFileSync(file, res.stdout || JSON.stringify({ stderr: res.stderr, error: String(res.error ?? '') }));
  let json = {};
  try { json = JSON.parse(res.stdout); } catch { /* không phải JSON — crash/timeout */ }
  return { code: res.status, signal: res.signal, json, file };
}

// ---------- main ----------
function main() {
  if (!existsSync(BATCH)) throw new Error(`Không thấy batch file: ${BATCH_REL}`);
  const first = parseBatch(read());
  if (first.queue.length === 0) throw new Error('Hàng đợi rỗng hoặc sai định dạng ("- [ ] {ID} {Fn}")');

  // Pre-flight (bản headless của skill §Pre-flight mục 1–3)
  if (!treeClean()) throw new Error('Working tree không sạch — commit/stash trước khi burn.');
  const missing = [...new Set(first.queue.filter((q) => !q.done).map((q) => q.id))].filter((id) => !branchExists(id));
  if (missing.length) throw new Error(`Thiếu nhánh: ${missing.map((id) => `feature/${id}`).join(', ')}`);
  if (!DRY) {
    setBanner('Docker', dockerOk() ? 'có' : 'không');
    setBanner('STATUS', 'RUNNING');
    if (/\*\*Bắt đầu:\*\* `?—/.test(read())) setBanner('Bắt đầu', now());
  }

  let ran = 0;
  let totalCost = 0;
  const dryDone = new Set(); // --dry-run không ghi file → nhớ trong RAM để không lặp vô hạn
  for (;;) {
    if (existsSync(STOP_FILE)) { console.log('[burn] Thấy file STOP — dừng.'); if (!DRY) setBanner('STATUS', 'STOPPED'); break; }
    const state = parseBatch(read());
    if (state.log.some((r) => r.status === 'HALT')) { console.log('[burn] Có HALT trong Log — dừng.'); setBanner('STATUS', 'HALTED'); break; }
    const item = nextItem(state, dryDone);
    if (!item) { console.log('[burn] Hết hàng đợi.'); if (!DRY) setBanner('STATUS', 'DONE'); break; }
    if (ran >= MAX_STEPS) { console.log(`[burn] Chạm --max-steps ${MAX_STEPS}.`); if (!DRY) setBanner('STATUS', 'STOPPED'); break; }

    const r = runClaude(item, ran + 1);
    ran++;
    if (r.dry) { dryDone.add(`${item.id} ${item.step}`); continue; }
    totalCost += Number(r.json.total_cost_usd ?? 0);

    const after = parseBatch(read());
    const ticked = after.queue.find((q) => q.id === item.id && q.step === item.step)?.done;
    if (!treeClean()) {
      appendLog(item.id, item.step, 'HALT', `runner: tree bẩn sau bước (exit ${r.code}). Log: ${path.relative(REPO_ROOT, r.file)}`);
      setBanner('STATUS', 'HALTED');
      break;
    }
    if (!ticked) {
      const why = r.json.subtype || r.signal || `exit ${r.code}`;
      appendLog(item.id, item.step, 'ABORTED', `runner: session kết thúc mà chưa ghi Log (${why}) — rate-limit/budget/crash? Log: ${path.relative(REPO_ROOT, r.file)}`);
      setBanner('STATUS', 'STOPPED');
      console.log(`[burn] ${item.id} ${item.step} không hoàn tất (${why}) — dừng. Chạy lại cùng lệnh để tiếp.`);
      break;
    }
    const last = after.log.filter((l) => l.id === item.id && l.step === item.step).pop();
    console.log(`[burn] ${item.id} ${item.step}: ${last?.status ?? '?'} · cost $${Number(r.json.total_cost_usd ?? 0).toFixed(2)}`);
  }
  if (!DRY) writeSummary();
  console.log(`[burn] Đã chạy ${ran} bước · tổng cost ước tính $${totalCost.toFixed(2)}`);
}

if (IS_MAIN) {
  try { main(); } catch (e) { console.error(`[burn] ${e.message}`); process.exit(1); }
}
