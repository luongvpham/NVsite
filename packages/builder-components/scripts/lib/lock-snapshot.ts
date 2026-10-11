import type { ComponentManifest } from '../../meta/manifest-schema';
import type { PropDef } from '../../meta/prop-kinds';

/**
 * Snapshot tối giản của manifest — chỉ giữ đủ thông tin để check-additive so sánh được 7 luật
 * FAIL ở §5. KHÔNG phải bản sao đầy đủ manifest (label/help/group/order không ảnh hưởng
 * additive-only nên không cần lưu).
 */
export interface LockPropSnapshot {
  kind: string;
  maxLength?: number;
  min?: number;
  max?: number;
  minItems?: number;
  maxItems?: number;
  options?: string[];
  preset?: string;
  imagePresets?: Record<string, string[]>;
  /** `link` — thu hẹp làm link đã lưu trỏ kiểu không còn được phép (TOOLING-001). */
  allowKinds?: string[];
  /** `binding` — bỏ source làm binding đã lưu trỏ nguồn không còn được phép (TOOLING-001). */
  sources?: string[];
  props?: Record<string, LockPropSnapshot>;
  itemProps?: Record<string, LockPropSnapshot>;
}

export interface LockVariantSnapshot {
  requiresProps: string[];
}

export interface LockTypeSnapshot {
  variants: Record<string, LockVariantSnapshot>;
  props: Record<string, LockPropSnapshot>;
  /** TOOLING-001 — lock cũ không có hai field này; vắng mặt = chưa ghi nhận, không so. */
  acceptsChildren?: boolean;
  /** `null` = nhận mọi type con. Sort để diff ổn định. */
  allowedChildTypes?: string[] | null;
}

export type LockSnapshot = Record<string, LockTypeSnapshot>;

function snapshotProp(prop: PropDef): LockPropSnapshot {
  const base: LockPropSnapshot = { kind: prop.kind };

  switch (prop.kind) {
    case 'text':
    case 'richText':
      if (prop.maxLength !== undefined) base.maxLength = prop.maxLength;
      return base;
    case 'number':
      if (prop.min !== undefined) base.min = prop.min;
      if (prop.max !== undefined) base.max = prop.max;
      return base;
    case 'select':
      base.options = prop.options.map((o) => o.value);
      return base;
    case 'image':
      base.preset = prop.preset;
      return base;
    case 'group':
      base.props = snapshotProps(prop.props);
      return base;
    case 'list':
      if (prop.minItems !== undefined) base.minItems = prop.minItems;
      if (prop.maxItems !== undefined) base.maxItems = prop.maxItems;
      base.itemProps = snapshotProps(prop.itemProps);
      return base;
    case 'link':
      base.allowKinds = [...prop.allowKinds].sort();
      return base;
    case 'binding':
      base.sources = [...prop.sources].sort();
      if (prop.imagePresets) {
        const sorted: Record<string, string[]> = {};
        for (const source of Object.keys(prop.imagePresets).sort()) {
          const presets = prop.imagePresets[source];
          if (presets) sorted[source] = [...new Set(presets)].sort();
        }
        base.imagePresets = sorted;
      }
      return base;
    default:
      return base;
  }
}

function snapshotProps(props: Record<string, PropDef>): Record<string, LockPropSnapshot> {
  const result: Record<string, LockPropSnapshot> = {};
  for (const [name, prop] of Object.entries(props)) {
    result[name] = snapshotProp(prop);
  }
  return result;
}

export function buildLockSnapshot(manifests: ComponentManifest[]): LockSnapshot {
  const snapshot: LockSnapshot = {};
  for (const manifest of manifests) {
    const variants: Record<string, LockVariantSnapshot> = {};
    for (const variant of manifest.variants) {
      variants[variant.key] = { requiresProps: [...variant.requiresProps].sort() };
    }
    snapshot[manifest.type] = {
      variants,
      props: snapshotProps(manifest.props),
      acceptsChildren: manifest.acceptsChildren,
      allowedChildTypes: manifest.allowedChildTypes === null ? null : [...manifest.allowedChildTypes].sort(),
    };
  }
  return snapshot;
}

export interface CheckAdditiveResult {
  violations: string[];
  warnings: string[];
}

function diffProps(
  typeName: string,
  pathPrefix: string,
  previous: Record<string, LockPropSnapshot>,
  current: Record<string, LockPropSnapshot>,
  result: CheckAdditiveResult,
): void {
  for (const [name, prevProp] of Object.entries(previous)) {
    const where = `${typeName}.${pathPrefix}${name}`;
    const currProp = current[name];

    if (!currProp) {
      result.violations.push(`${where}: prop bị xoá`);
      continue;
    }

    if (currProp.kind !== prevProp.kind) {
      result.violations.push(`${where}: đổi kind '${prevProp.kind}' → '${currProp.kind}'`);
      continue;
    }

    if ((currProp.maxLength ?? Infinity) < (prevProp.maxLength ?? Infinity)) {
      result.violations.push(`${where}: siết maxLength ${prevProp.maxLength ?? '∞'} → ${currProp.maxLength ?? '∞'}`);
    }
    if ((currProp.min ?? -Infinity) > (prevProp.min ?? -Infinity)) {
      result.violations.push(`${where}: siết min ${prevProp.min ?? '-∞'} → ${currProp.min ?? '-∞'}`);
    }
    if ((currProp.max ?? Infinity) < (prevProp.max ?? Infinity)) {
      result.violations.push(`${where}: siết max ${prevProp.max ?? '∞'} → ${currProp.max ?? '∞'}`);
    }
    if ((currProp.minItems ?? 0) > (prevProp.minItems ?? 0)) {
      result.violations.push(`${where}: siết minItems ${prevProp.minItems ?? 0} → ${currProp.minItems}`);
    }
    if ((currProp.maxItems ?? Infinity) < (prevProp.maxItems ?? Infinity)) {
      result.violations.push(`${where}: siết maxItems ${prevProp.maxItems ?? '∞'} → ${currProp.maxItems ?? '∞'}`);
    }

    if (prevProp.options) {
      const removedOptions = prevProp.options.filter((o) => !(currProp.options ?? []).includes(o));
      if (removedOptions.length > 0) {
        result.violations.push(`${where}: xoá option [${removedOptions.join(', ')}] khỏi select`);
      }
    }

    if (prevProp.allowKinds) {
      const removed = prevProp.allowKinds.filter((k) => !(currProp.allowKinds ?? []).includes(k));
      if (removed.length > 0) {
        result.violations.push(`${where}: thu hẹp link.allowKinds, bỏ [${removed.join(', ')}] — link đã lưu thành không hợp lệ`);
      }
    }
    if (prevProp.sources) {
      const removed = prevProp.sources.filter((src) => !(currProp.sources ?? []).includes(src));
      if (removed.length > 0) {
        result.violations.push(`${where}: bỏ binding.sources [${removed.join(', ')}] — binding đã lưu thành không hợp lệ`);
      }
    }

    if (prevProp.preset !== undefined && currProp.preset !== undefined && prevProp.preset !== currProp.preset) {
      result.warnings.push(`${where}: đổi preset '${prevProp.preset}' → '${currProp.preset}' (không vỡ dữ liệu, nhưng đổi layout hàng loạt site)`);
    }

    // #86 — bỏ preset khỏi binding.imagePresets (kể cả bỏ nguyên source key) là CẢNH BÁO, không FAIL
    // (cùng khuôn với đổi preset của image ở trên): ảnh cũ không mất, chỉ mất derivative sẵn có.
    if (prevProp.imagePresets) {
      for (const [source, prevPresets] of Object.entries(prevProp.imagePresets)) {
        const currPresets = currProp.imagePresets?.[source] ?? [];
        const removed = prevPresets.filter((p) => !currPresets.includes(p));
        if (removed.length > 0) {
          result.warnings.push(
            `${where}: bỏ preset [${removed.join(', ')}] khỏi imagePresets.${source} (không vỡ dữ liệu cũ, nhưng mất derivative đã sinh)`,
          );
        }
      }
    }

    if (prevProp.props) {
      diffProps(typeName, `${pathPrefix}${name}.`, prevProp.props, currProp.props ?? {}, result);
    }
    if (prevProp.itemProps) {
      diffProps(typeName, `${pathPrefix}${name}[].`, prevProp.itemProps, currProp.itemProps ?? {}, result);
    }
  }
}

/** So `previous` (registry.lock.json) với `current` (manifest hiện tại) — 7 luật FAIL ở §5. */
export function checkAdditive(previous: LockSnapshot, current: LockSnapshot): CheckAdditiveResult {
  const result: CheckAdditiveResult = { violations: [], warnings: [] };

  for (const [typeName, prevType] of Object.entries(previous)) {
    const currType = current[typeName];
    if (!currType) {
      result.violations.push(`${typeName}: type bị xoá`);
      continue;
    }

    for (const [variantKey, prevVariant] of Object.entries(prevType.variants)) {
      const currVariant = currType.variants[variantKey];
      if (!currVariant) {
        result.violations.push(`${typeName}.${variantKey}: variant bị xoá`);
        continue;
      }
      const addedRequired = currVariant.requiresProps.filter((p) => !prevVariant.requiresProps.includes(p));
      if (addedRequired.length > 0) {
        result.violations.push(`${typeName}.${variantKey}: thêm vào requiresProps [${addedRequired.join(', ')}] — tree cũ hợp lệ bỗng thành không hợp lệ`);
      }
    }

    if (prevType.acceptsChildren === true && currType.acceptsChildren === false) {
      result.violations.push(`${typeName}: acceptsChildren true → false — node đã lưu có children thành không hợp lệ`);
    }
    // Chỉ so khi trước đó ĐÃ là container: lá (acceptsChildren=false) → container là NỚI, không phải thu hẹp.
    if (prevType.allowedChildTypes !== undefined && prevType.acceptsChildren !== false && currType.acceptsChildren !== false) {
      const prev = prevType.allowedChildTypes;
      const curr = currType.allowedChildTypes ?? null;
      if (prev === null && curr !== null) {
        result.violations.push(`${typeName}: allowedChildTypes từ "mọi type" → [${curr.join(', ')}] — thu hẹp`);
      } else if (prev !== null && curr !== null) {
        const removed = prev.filter((t) => !curr.includes(t));
        if (removed.length > 0) {
          result.violations.push(`${typeName}: allowedChildTypes bỏ [${removed.join(', ')}] — thu hẹp`);
        }
      }
    }

    diffProps(typeName, '', prevType.props, currType.props, result);
  }

  return result;
}

function unlockedProps(where: string, locked: Record<string, LockPropSnapshot>, current: Record<string, LockPropSnapshot>, out: string[]): void {
  for (const [name, prop] of Object.entries(current)) {
    const path = `${where}${name}`;
    const lockedProp = locked[name];
    if (!lockedProp) {
      out.push(`${path} (prop)`);
      continue;
    }
    if (prop.allowKinds && !lockedProp.allowKinds) out.push(`${path}.allowKinds`);
    if (prop.sources && !lockedProp.sources) out.push(`${path}.sources`);
    if (prop.props) unlockedProps(`${path}.`, lockedProp.props ?? {}, prop.props, out);
    if (prop.itemProps) unlockedProps(`${path}[].`, lockedProp.itemProps ?? {}, prop.itemProps, out);
  }
}

/**
 * TOOLING-001 — phần nào của registry hiện tại CHƯA được lock bảo vệ (type/variant/prop mới, hoặc
 * field snapshot mới mà lock cũ chưa có). Trước đây lock chỉ ghi lúc chưa tồn tại, nên mọi thứ thêm
 * sau lần đầu không bao giờ được bảo vệ. Khác rỗng → `pnpm registry:lock`.
 */
export function findUnlocked(locked: LockSnapshot, current: LockSnapshot): string[] {
  const out = findMissing(locked, current);
  // Lock phải BẰNG ĐÚNG snapshot hiện tại, không chỉ "có đủ key": thêm option vào select, nới
  // maxLength, thêm source… mà không cập nhật lock thì lần sau gỡ/siết lại sẽ không bị chặn (review TOOLING-001).
  for (const path of valueDrift(locked, current, '')) {
    if (!out.some((o) => path.startsWith(o.replace(/ \((type|variant|prop)\)$/, '')))) out.push(`${path} (giá trị đổi)`);
  }
  return out;
}

const canonical = (value: unknown): string =>
  JSON.stringify(value, (_k, v: unknown) =>
    v !== null && typeof v === 'object' && !Array.isArray(v)
      ? Object.fromEntries(Object.entries(v as Record<string, unknown>).sort(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)))
      : v,
  );

/** Đường dẫn có mặt ở cả hai nhưng giá trị khác — chỉ đi vào object, so lá (mảng coi là lá). */
function valueDrift(locked: unknown, current: unknown, path: string): string[] {
  if (canonical(locked) === canonical(current)) return [];
  const isObj = (v: unknown): v is Record<string, unknown> => v !== null && typeof v === 'object' && !Array.isArray(v);
  if (!isObj(locked) || !isObj(current)) return [path];
  return Object.keys(current)
    .filter((k) => k in locked)
    .flatMap((k) => valueDrift(locked[k], current[k], path ? `${path}.${k}` : k));
}

function findMissing(locked: LockSnapshot, current: LockSnapshot): string[] {
  const out: string[] = [];
  for (const [typeName, type] of Object.entries(current)) {
    const lockedType = locked[typeName];
    if (!lockedType) {
      out.push(`${typeName} (type)`);
      continue;
    }
    for (const variant of Object.keys(type.variants)) {
      if (!(variant in lockedType.variants)) out.push(`${typeName}.${variant} (variant)`);
    }
    if (lockedType.acceptsChildren === undefined) out.push(`${typeName}.acceptsChildren`);
    if (lockedType.allowedChildTypes === undefined) out.push(`${typeName}.allowedChildTypes`);
    unlockedProps(`${typeName}.`, lockedType.props, type.props, out);
  }
  return out;
}
