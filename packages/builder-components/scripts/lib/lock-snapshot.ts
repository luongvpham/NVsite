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
  props?: Record<string, LockPropSnapshot>;
  itemProps?: Record<string, LockPropSnapshot>;
}

export interface LockVariantSnapshot {
  requiresProps: string[];
}

export interface LockTypeSnapshot {
  variants: Record<string, LockVariantSnapshot>;
  props: Record<string, LockPropSnapshot>;
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
    snapshot[manifest.type] = { variants, props: snapshotProps(manifest.props) };
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

    if (prevProp.preset !== undefined && currProp.preset !== undefined && prevProp.preset !== currProp.preset) {
      result.warnings.push(`${where}: đổi preset '${prevProp.preset}' → '${currProp.preset}' (không vỡ dữ liệu, nhưng đổi layout hàng loạt site)`);
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

    diffProps(typeName, '', prevType.props, currType.props, result);
  }

  return result;
}
