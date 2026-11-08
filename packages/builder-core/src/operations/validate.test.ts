import { describe, expect, it } from 'vitest';
import { PageKind } from '../tree/types';
import type { TreeEnvelope, TreeNode } from '../tree/types';
import { envelope, hero, richText, section } from '../test-support/trees';
import { validateOperation, type ValidationContext } from './validate';
import { parseOperation, type Operation } from './types';

/**
 * Bảng phép/cấm `05` §11 + 5 bước kiểm [1]–[5]. Mã lỗi dùng ĐÚNG tên ở `tests/fixtures/tree/README.md` của
 * builder-components; mã chỉ-FE (không gửi BE): TARGET_NOT_FOUND, INDEX_OUT_OF_RANGE, MOVE_INTO_DESCENDANT, INVALID_OPERATION.
 */
const composable = (tree: TreeEnvelope): ValidationContext => ({ tree, pageKind: PageKind.Composable });
const system = (tree: TreeEnvelope): ValidationContext => ({ tree, pageKind: PageKind.System });

const add = (parentId: string, index: number, node: TreeNode): Operation => ({ type: 'add', parentId, index, node });
const code = (op: Operation, ctx: ValidationContext) => {
  const result = validateOperation(op, ctx);
  return result.ok ? 'OK' : result.code;
};

/** Cây System giống trang Contact: Section01 > RichText01 (cây gốc System page seed). */
const contactTree = (): TreeEnvelope =>
  envelope([{ id: 'c_page01', type: 'RichText', variant: 'RichText01', props: { content: '<p>Liên hệ</p>', align: 'left' } }]);

describe('bảng phép/cấm theo PageKind (05 §11)', () => {
  it('Composable: add/remove/move/update/changeTheme đều được', () => {
    const tree = envelope([hero('c_hero1'), richText('c_rt001')]);
    expect(code(add('root', 0, { id: 'c_new01', type: 'Section', variant: 'Section01' }), composable(tree))).toBe('OK');
    expect(code({ type: 'remove', nodeId: 'c_hero1' }, composable(tree))).toBe('OK');
    expect(code({ type: 'move', nodeId: 'c_hero1', toParentId: 'root', toIndex: 1 }, composable(tree))).toBe('OK');
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'Chào' }, composable(tree))).toBe('OK');
  });

  it('System: add/remove/move bị cấm → SYSTEM_PAGE_STRUCTURE_CHANGED', () => {
    const tree = contactTree();
    expect(code(add('root', 0, { id: 'c_new01', type: 'RichText', variant: 'RichText01' }), system(tree))).toBe(
      'SYSTEM_PAGE_STRUCTURE_CHANGED',
    );
    expect(code({ type: 'remove', nodeId: 'c_page01' }, system(tree))).toBe('SYSTEM_PAGE_STRUCTURE_CHANGED');
    expect(code({ type: 'move', nodeId: 'c_page01', toParentId: 'root', toIndex: 0 }, system(tree))).toBe(
      'SYSTEM_PAGE_STRUCTURE_CHANGED',
    );
  });

  it('System: update chỉ prop trong editableInSystemPageProps (RichText.content ✅, align ❌)', () => {
    const tree = contactTree();
    expect(code({ type: 'update', nodeId: 'c_page01', path: ['content'], value: '<p>Mới</p>' }, system(tree))).toBe('OK');
    expect(code({ type: 'update', nodeId: 'c_page01', path: ['align'], value: 'justify' }, system(tree))).toBe(
      'SYSTEM_PAGE_PROP_NOT_EDITABLE',
    );
  });

  it('changeTheme được ở cả hai loại trang', () => {
    const op: Operation = { type: 'changeTheme', tokens: themeDefaults() };
    expect(code(op, composable(envelope()))).toBe('OK');
    expect(code(op, system(contactTree()))).toBe('OK');
  });
});

describe('add', () => {
  it('đích không tồn tại → TARGET_NOT_FOUND; index ngoài [0..số con] → INDEX_OUT_OF_RANGE (index = số con là được)', () => {
    const tree = envelope([hero('c_hero1')]);
    const node = { id: 'c_new01', type: 'RichText', variant: 'RichText01' };
    expect(code(add('c_nope0', 0, node), composable(tree))).toBe('TARGET_NOT_FOUND');
    expect(code(add('root', 1, node), composable(tree))).toBe('OK');
    expect(code(add('root', 2, node), composable(tree))).toBe('INDEX_OUT_OF_RANGE');
  });

  it('type lạ / variant lạ / thiếu variant → NODE_TYPE_UNKNOWN / NODE_VARIANT_UNKNOWN', () => {
    const tree = envelope();
    expect(code(add('root', 0, { id: 'c_new01', type: 'Carousel', variant: 'Carousel01' }), composable(tree))).toBe(
      'NODE_TYPE_UNKNOWN',
    );
    expect(code(add('root', 0, { id: 'c_new01', type: 'Hero', variant: 'Hero99' }), composable(tree))).toBe(
      'NODE_VARIANT_UNKNOWN',
    );
    expect(code(add('root', 0, { id: 'c_new01', type: 'Hero' }), composable(tree))).toBe('NODE_VARIANT_UNKNOWN');
  });

  it('trùng id (kể cả id nằm sâu trong Section) → NODE_ID_DUPLICATE', () => {
    const tree = envelope([section('c_sec01', [richText('c_rt001')])]);
    expect(code(add('root', 0, { id: 'c_rt001', type: 'RichText', variant: 'RichText01' }), composable(tree))).toBe(
      'NODE_ID_DUPLICATE',
    );
  });

  it('Hero tối đa 1 mỗi trang (kể cả Hero nằm trong Section) → NODE_MAX_PER_PAGE_EXCEEDED', () => {
    const tree = envelope([section('c_sec01', [hero('c_hero1')])]);
    expect(code(add('root', 0, { id: 'c_hero2', type: 'Hero', variant: 'Hero02' }), composable(tree))).toBe(
      'NODE_MAX_PER_PAGE_EXCEEDED',
    );
  });

  it('chỉ Section nhận con: add vào Hero → NODE_CHILDREN_NOT_ACCEPTED, vào Section → OK', () => {
    const tree = envelope([hero('c_hero1'), section('c_sec01')]);
    const node = { id: 'c_new01', type: 'RichText', variant: 'RichText01' };
    expect(code(add('c_hero1', 0, node), composable(tree))).toBe('NODE_CHILDREN_NOT_ACCEPTED');
    expect(code(add('c_sec01', 0, node), composable(tree))).toBe('OK');
  });

  it('node mang khoá i18n → I18N_NOT_ENABLED (chưa bật đa ngôn ngữ, #98)', () => {
    const node = { id: 'c_new01', type: 'RichText', variant: 'RichText01', i18n: { en: { content: 'x' } } };
    expect(code(add('root', 0, node), composable(envelope()))).toBe('I18N_NOT_ENABLED');
  });

  it('props đi kèm cũng bị kiểm: shape sai / prop lạ / link độc / binding Review', () => {
    const base = { id: 'c_new01', type: 'Hero', variant: 'Hero01' };
    const ctx = composable(envelope());
    expect(code(add('root', 0, { ...base, props: { overlayOpacity: 200 } }), ctx)).toBe('PROP_INVALID');
    expect(code(add('root', 0, { ...base, props: { colour: 'red' } }), ctx)).toBe('PROP_UNKNOWN');
    expect(
      code(add('root', 0, { ...base, props: { cta: { label: 'a', target: { kind: 'external', url: 'javascript:alert(1)' } } } }), ctx),
    ).toBe('LINK_URL_UNSAFE');
    expect(
      code(add('root', 0, { id: 'c_new02', type: 'ServiceGrid', variant: 'ServiceGrid01', props: { source: { source: 'Review' } } }), ctx),
    ).toBe('BINDING_SOURCE_NOT_ALLOWED');
  });

  it('vượt độ sâu 20 → TREE_TOO_DEEP; vượt 2000 node → TREE_TOO_MANY_NODES (mã của BE)', () => {
    // Chuỗi Section lồng nhau, Section sâu nhất `c_deep00` ở tầng 20 (con trực tiếp của gốc = 1).
    let chainNode: TreeNode = section('c_deep00');
    for (let i = 19; i >= 1; i--) chainNode = section(`c_t${String(i).padStart(4, '0')}`, [chainNode]);
    const newNode = { id: 'c_new01', type: 'RichText', variant: 'RichText01' };
    expect(code(add('c_deep00', 0, newNode), composable(envelope([chainNode])))).toBe('TREE_TOO_DEEP');
    expect(code(add('c_t0019', 0, newNode), composable(envelope([chainNode])))).toBe('OK'); // tầng 20: còn trong trần

    const full = envelope(Array.from({ length: 2000 }, (_, i) => richText(`c_n${String(i).padStart(5, '0')}`)));
    expect(code(add('root', 0, { id: 'c_new01', type: 'RichText', variant: 'RichText01' }), composable(full))).toBe(
      'TREE_TOO_MANY_NODES',
    );
  });
});

describe('remove', () => {
  it('node không tồn tại → TARGET_NOT_FOUND', () => {
    expect(code({ type: 'remove', nodeId: 'c_nope0' }, composable(envelope([hero()])))).toBe('TARGET_NOT_FOUND');
  });
});

describe('move (toIndex tính trong danh sách con của đích SAU khi gỡ node)', () => {
  const tree = envelope([richText('c_aaaaa'), richText('c_bbbbb'), richText('c_ccccc'), section('c_sec01')]);

  it('cùng cha: toIndex ∈ [0..n-1]', () => {
    expect(code({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'root', toIndex: 3 }, composable(tree))).toBe('OK');
    expect(code({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'root', toIndex: 4 }, composable(tree))).toBe('INDEX_OUT_OF_RANGE');
  });

  it('sang Section khác: toIndex ∈ [0..số con của đích]', () => {
    expect(code({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'c_sec01', toIndex: 0 }, composable(tree))).toBe('OK');
    expect(code({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'c_sec01', toIndex: 1 }, composable(tree))).toBe('INDEX_OUT_OF_RANGE');
  });

  it('thiếu node hoặc thiếu đích → TARGET_NOT_FOUND', () => {
    expect(code({ type: 'move', nodeId: 'c_nope0', toParentId: 'root', toIndex: 0 }, composable(tree))).toBe('TARGET_NOT_FOUND');
    expect(code({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'c_nope0', toIndex: 0 }, composable(tree))).toBe('TARGET_NOT_FOUND');
  });

  it('không thả vào chính nó hay hậu duệ → MOVE_INTO_DESCENDANT', () => {
    const nested = envelope([section('c_outer', [section('c_inner')])]);
    expect(code({ type: 'move', nodeId: 'c_outer', toParentId: 'c_outer', toIndex: 0 }, composable(nested))).toBe('MOVE_INTO_DESCENDANT');
    expect(code({ type: 'move', nodeId: 'c_outer', toParentId: 'c_inner', toIndex: 0 }, composable(nested))).toBe('MOVE_INTO_DESCENDANT');
  });

  it('đích không nhận con (Hero) → NODE_CHILDREN_NOT_ACCEPTED', () => {
    const withHero = envelope([hero('c_hero1'), richText('c_aaaaa')]);
    expect(code({ type: 'move', nodeId: 'c_aaaaa', toParentId: 'c_hero1', toIndex: 0 }, composable(withHero))).toBe('NODE_CHILDREN_NOT_ACCEPTED');
  });

  it('chuyển cả nhánh xuống sâu quá 20 → TREE_TOO_DEEP', () => {
    let bottom: TreeNode = section('c_deep00');
    for (let i = 19; i >= 1; i--) bottom = section(`c_t${String(i).padStart(4, '0')}`, [bottom]);
    const tree20 = envelope([bottom, section('c_movd01', [richText('c_leaf01')])]);
    expect(code({ type: 'move', nodeId: 'c_movd01', toParentId: 'c_deep00', toIndex: 0 }, composable(tree20))).toBe('TREE_TOO_DEEP');
  });
});

describe('update', () => {
  it('node không tồn tại → TARGET_NOT_FOUND', () => {
    expect(code({ type: 'update', nodeId: 'c_nope0', path: ['title'], value: 'x' }, composable(envelope([hero()])))).toBe('TARGET_NOT_FOUND');
  });

  it('prop lạ ở cấp 1 hoặc lồng nhau → PROP_UNKNOWN', () => {
    const tree = envelope([hero('c_hero1', { cta: { label: 'a' } })]);
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['colour'], value: 'red' }, composable(tree))).toBe('PROP_UNKNOWN');
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['cta', 'colour'], value: 'red' }, composable(tree))).toBe('PROP_UNKNOWN');
  });

  it('shape sai → PROP_INVALID (overlayOpacity ≤ 80, align chỉ left|center)', () => {
    const tree = envelope([hero('c_hero1')]);
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['overlayOpacity'], value: 200 }, composable(tree))).toBe('PROP_INVALID');
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['align'], value: 'right' }, composable(tree))).toBe('PROP_INVALID');
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['overlayOpacity'], value: 40 }, composable(tree))).toBe('OK');
  });

  it('link: url độc bị chặn ở mọi độ sâu (R7) — cả khi sửa riêng khoá url', () => {
    const tree = envelope([hero('c_hero1', { cta: { label: 'a', target: { kind: 'external', url: 'https://vsite.vn' } } })]);
    const bad = (value: unknown, path: (string | number)[]) =>
      code({ type: 'update', nodeId: 'c_hero1', path, value }, composable(tree));
    expect(bad({ kind: 'external', url: 'javascript:alert(1)' }, ['cta', 'target'])).toBe('LINK_URL_UNSAFE');
    expect(bad('javascript:alert(1)', ['cta', 'target', 'url'])).toBe('LINK_URL_UNSAFE');
    expect(bad('//evil.example', ['cta', 'target', 'url'])).toBe('LINK_URL_UNSAFE');
    expect(bad('/dich-vu', ['cta', 'target', 'url'])).toBe('OK');
    expect(bad('mailto:a@b.vn', ['cta', 'target', 'url'])).toBe('OK');
  });

  it('binding: Review bị cấm (ranh giới cứng 01 §7), nguồn khai báo được phép, source thiếu bị chặn', () => {
    const tree = envelope([{ id: 'c_grid01', type: 'ServiceGrid', variant: 'ServiceGrid01' }]);
    const upd = (value: unknown) => code({ type: 'update', nodeId: 'c_grid01', path: ['source'], value }, composable(tree));
    expect(upd({ source: 'Review' })).toBe('BINDING_SOURCE_NOT_ALLOWED');
    expect(upd({ source: 'Product' })).toBe('BINDING_SOURCE_NOT_ALLOWED'); // hợp lệ toàn cục nhưng ServiceGrid không khai
    expect(upd({ filters: {} })).toBe('BINDING_SOURCE_NOT_ALLOWED');
    expect(upd({ source: null })).toBe('BINDING_SOURCE_NOT_ALLOWED');
    expect(upd({ source: 'Service' })).toBe('OK');
    expect(upd({ source: 'ServiceGroup', filters: { a: 1 } })).toBe('OK');
  });

  it('prop đã sai sẵn không chặn việc sửa prop KHÁC (chỉ kiểm phần đang sửa)', () => {
    const tree = envelope([hero('c_hero1', { overlayOpacity: 999 })]);
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: 'Chào' }, composable(tree))).toBe('OK');
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['overlayOpacity'], value: 999 }, composable(tree))).toBe('PROP_INVALID');
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['overlayOpacity'], value: 30 }, composable(tree))).toBe('OK');
  });

  it('value undefined = xoá prop (Inspector xoá trắng một ô) → OK', () => {
    const tree = envelope([hero('c_hero1', { title: 'x' })]);
    expect(code({ type: 'update', nodeId: 'c_hero1', path: ['title'], value: undefined }, composable(tree))).toBe('OK');
  });
});

describe('changeTheme', () => {
  it('tokens sai (màu không đúng pattern, giá trị ngoài tập) → THEME_TOKEN_INVALID', () => {
    const tokens = themeDefaults();
    expect(code({ type: 'changeTheme', tokens: { ...tokens, colors: { ...tokens.colors, primary: 'tím' } } }, composable(envelope()))).toBe(
      'THEME_TOKEN_INVALID',
    );
    expect(code({ type: 'changeTheme', tokens: { ...tokens, shape: { ...tokens.shape, radius: 'huge' } } }, composable(envelope()))).toBe(
      'THEME_TOKEN_INVALID',
    );
    expect(code({ type: 'changeTheme', tokens: 'xanh' }, composable(envelope()))).toBe('THEME_TOKEN_INVALID');
  });

  it('theme quá 64 KB → THEME_BODY_TOO_LARGE', () => {
    const tokens = { ...themeDefaults(), extra: 'a'.repeat(70_000) };
    // Quá lớn được báo trước khi kiểm shape để không phải duyệt khối khổng lồ.
    expect(code({ type: 'changeTheme', tokens }, composable(envelope()))).toBe('THEME_BODY_TOO_LARGE');
  });
});

describe('parseOperation (hình dạng op)', () => {
  it('op sai hình dạng → INVALID_OPERATION, không ném lỗi', () => {
    for (const bad of [
      null,
      'add',
      {},
      { type: 'explode' },
      { type: 'add', parentId: 'root', index: -1, node: { id: 'c_new01', type: 'Hero', variant: 'Hero01' } },
      { type: 'add', parentId: 'root', index: 0, node: { id: 'root', type: 'Hero', variant: 'Hero01' } },
      { type: 'add', parentId: 'root', index: 0, node: { id: 'x1', type: 'Hero', variant: 'Hero01' } },
      { type: 'add', parentId: 'root', index: 0, node: { id: 'c_new01', type: 'Hero', variant: 'Hero01', children: [] } },
      { type: 'update', nodeId: 'c_hero1', path: [], value: 1 },
      { type: 'update', nodeId: 'c_hero1', path: [{}], value: 1 },
      { type: 'move', nodeId: 'c_hero1', toParentId: 'root', toIndex: 1.5 },
    ]) {
      const parsed = parseOperation(bad);
      expect(parsed.ok, JSON.stringify(bad)).toBe(false);
      if (!parsed.ok) expect(parsed.code).toBe('INVALID_OPERATION');
    }
  });

  it('op đúng hình dạng → ok và trả op đã chuẩn hoá', () => {
    const parsed = parseOperation({ type: 'remove', nodeId: 'c_hero1' });
    expect(parsed).toEqual({ ok: true, op: { type: 'remove', nodeId: 'c_hero1' } });
  });
});

function themeDefaults() {
  return {
    schemaVersion: 1,
    colors: {
      primary: '#7c3aed',
      secondary: '#a78bfa',
      background: '#ffffff',
      surface: '#f9fafb',
      text: '#111827',
      textMuted: '#6b7280',
      border: '#e5e7eb',
    },
    typography: { headingFont: 'Be Vietnam Pro', bodyFont: 'Inter', scale: 'comfortable' },
    shape: { radius: 'md', shadow: 'soft' },
    spacing: { sectionGap: 'lg' },
  };
}
