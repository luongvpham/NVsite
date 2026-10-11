import { describe, expect, it } from 'vitest';
import { checkAdditive, findUnlocked, type LockSnapshot, type LockTypeSnapshot } from './lock-snapshot';

/**
 * §9 test #5 — "Viết test cho chính cái checker": test trực tiếp checkAdditive() với fixture
 * tổng hợp, bao phủ đúng 7 FAIL + các case OK/CẢNH BÁO ở bảng §5.
 */
function baseHero(): LockTypeSnapshot {
  return {
    variants: { Hero01: { requiresProps: ['title', 'image'] } },
    props: {
      title: { kind: 'text', maxLength: 120 },
      image: { kind: 'image', preset: '1600x900,cover' },
      overlayOpacity: { kind: 'number', min: 0, max: 80 },
      align: { kind: 'select', options: ['left', 'center'] },
      cta: { kind: 'group', props: { label: { kind: 'text', maxLength: 40 } } },
      items: { kind: 'list', minItems: 1, maxItems: 24, itemProps: { caption: { kind: 'text', maxLength: 120 } } },
      source: { kind: 'binding', imagePresets: { Service: ['800x600,cover', '160x160,cover'] } },
    },
  };
}

function baseSnapshot(): LockSnapshot {
  return { Hero: baseHero() };
}

function withHeroPatch(patch: (hero: LockTypeSnapshot) => void): LockSnapshot {
  const hero = baseHero();
  patch(hero);
  return { Hero: hero };
}

describe('checkAdditive — 7 luật FAIL của §5', () => {
  it('#1 đổi kind của prop → FAIL', () => {
    const current = withHeroPatch((hero) => {
      hero.props.title = { kind: 'richText', maxLength: 120 };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('đổi kind'))).toBe(true);
  });

  it('#2 xoá prop → FAIL', () => {
    const current = withHeroPatch((hero) => {
      delete hero.props.overlayOpacity;
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('overlayOpacity') && v.includes('xoá'))).toBe(true);
  });

  it('#3 đổi tên prop (= xoá + thêm) → FAIL vì phía "xoá" bị bắt', () => {
    const current = withHeroPatch((hero) => {
      const title = hero.props.title;
      if (title) hero.props.heading = title;
      delete hero.props.title;
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('title') && v.includes('xoá'))).toBe(true);
  });

  it('#4 xoá variant → FAIL', () => {
    const current = withHeroPatch((hero) => {
      delete hero.variants.Hero01;
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('Hero01') && v.includes('variant bị xoá'))).toBe(true);
  });

  it('#5 xoá option khỏi select → FAIL', () => {
    const current = withHeroPatch((hero) => {
      hero.props.align = { kind: 'select', options: ['left'] };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('center') && v.includes('xoá option'))).toBe(true);
  });

  it('#6a siết maxLength → FAIL', () => {
    const current = withHeroPatch((hero) => {
      hero.props.title = { kind: 'text', maxLength: 50 };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('siết maxLength'))).toBe(true);
  });

  it('#6b siết min (nới cận dưới lên) → FAIL', () => {
    const current = withHeroPatch((hero) => {
      hero.props.overlayOpacity = { kind: 'number', min: 10, max: 80 };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('siết min'))).toBe(true);
  });

  it('#7 thêm vào requiresProps → FAIL', () => {
    const current = withHeroPatch((hero) => {
      hero.variants.Hero01 = { requiresProps: ['title', 'image', 'overlayOpacity'] };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('requiresProps'))).toBe(true);
  });

  it('nested: xoá prop trong group (cta.label) cũng bị bắt', () => {
    const current = withHeroPatch((hero) => {
      hero.props.cta = { kind: 'group', props: {} };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('cta.label') && v.includes('xoá'))).toBe(true);
  });

  it('nested: siết maxLength trong list item (items[].caption) cũng bị bắt', () => {
    const current = withHeroPatch((hero) => {
      hero.props.items = { kind: 'list', minItems: 1, maxItems: 24, itemProps: { caption: { kind: 'text', maxLength: 10 } } };
    });
    const { violations } = checkAdditive(baseSnapshot(), current);
    expect(violations.some((v) => v.includes('items[].caption') && v.includes('siết maxLength'))).toBe(true);
  });
});

describe('checkAdditive — 6 case OK không được fail (§5)', () => {
  it('thêm prop mới (optional) → OK', () => {
    const current = withHeroPatch((hero) => {
      hero.props.subtitle = { kind: 'text', maxLength: 300 };
    });
    expect(checkAdditive(baseSnapshot(), current).violations).toEqual([]);
  });

  it('thêm variant mới → OK', () => {
    const current = withHeroPatch((hero) => {
      hero.variants.Hero02 = { requiresProps: ['title'] };
    });
    expect(checkAdditive(baseSnapshot(), current).violations).toEqual([]);
  });

  it('thêm option vào select → OK', () => {
    const current = withHeroPatch((hero) => {
      hero.props.align = { kind: 'select', options: ['left', 'center', 'right'] };
    });
    expect(checkAdditive(baseSnapshot(), current).violations).toEqual([]);
  });

  it('nới maxLength / max → OK', () => {
    const current = withHeroPatch((hero) => {
      hero.props.title = { kind: 'text', maxLength: 200 };
      hero.props.overlayOpacity = { kind: 'number', min: 0, max: 100 };
    });
    expect(checkAdditive(baseSnapshot(), current).violations).toEqual([]);
  });

  it('không đổi gì → OK, 0 violation 0 warning', () => {
    const result = checkAdditive(baseSnapshot(), baseSnapshot());
    expect(result.violations).toEqual([]);
    expect(result.warnings).toEqual([]);
  });

  it('đổi preset của image → CẢNH BÁO, KHÔNG fail', () => {
    const current = withHeroPatch((hero) => {
      hero.props.image = { kind: 'image', preset: '1200x630,cover' };
    });
    const result = checkAdditive(baseSnapshot(), current);
    expect(result.violations).toEqual([]);
    expect(result.warnings.some((w) => w.includes('đổi preset'))).toBe(true);
  });

  it('thêm imagePresets / thêm source mới vào binding → OK', () => {
    const current = withHeroPatch((hero) => {
      hero.props.source = {
        kind: 'binding',
        imagePresets: { Service: ['800x600,cover', '160x160,cover'], ServiceGroup: ['800x600,cover'] },
      };
    });
    expect(checkAdditive(baseSnapshot(), current).violations).toEqual([]);
  });

  it('bỏ một preset khỏi binding.imagePresets → CẢNH BÁO, KHÔNG fail (#86)', () => {
    const current = withHeroPatch((hero) => {
      hero.props.source = { kind: 'binding', imagePresets: { Service: ['800x600,cover'] } };
    });
    const result = checkAdditive(baseSnapshot(), current);
    expect(result.violations).toEqual([]);
    expect(result.warnings.some((w) => w.includes('imagePresets') && w.includes('160x160,cover'))).toBe(true);
  });
});

describe('TOOLING-001 — ràng buộc trước đây không có trong lock', () => {
  const withType = (patch: (hero: LockTypeSnapshot) => void): LockSnapshot => {
    const hero: LockTypeSnapshot = {
      ...baseHero(),
      acceptsChildren: true,
      allowedChildTypes: ['Hero', 'RichText'],
    };
    hero.props.target = { kind: 'link', allowKinds: ['external', 'page'] };
    hero.props.source = { kind: 'binding', sources: ['Service', 'ServiceGroup'] };
    patch(hero);
    return { Hero: hero };
  };
  const previous = withType(() => {});

  it('thu hẹp link.allowKinds → FAIL', () => {
    const current = withType((h) => { h.props.target = { kind: 'link', allowKinds: ['page'] }; });
    expect(checkAdditive(previous, current).violations.some((v) => v.includes('allowKinds'))).toBe(true);
  });

  it('bỏ binding.sources → FAIL; thêm source → OK', () => {
    const removed = withType((h) => { h.props.source = { kind: 'binding', sources: ['Service'] }; });
    expect(checkAdditive(previous, removed).violations.some((v) => v.includes('binding.sources'))).toBe(true);
    const added = withType((h) => { h.props.source = { kind: 'binding', sources: ['Product', 'Service', 'ServiceGroup'] }; });
    expect(checkAdditive(previous, added).violations).toEqual([]);
  });

  it('acceptsChildren true → false → FAIL', () => {
    const current = withType((h) => { h.acceptsChildren = false; h.allowedChildTypes = null; });
    expect(checkAdditive(previous, current).violations.some((v) => v.includes('acceptsChildren'))).toBe(true);
  });

  it('allowedChildTypes: bỏ type → FAIL; "mọi type" → danh sách → FAIL; danh sách → "mọi type" → OK', () => {
    const narrowed = withType((h) => { h.allowedChildTypes = ['Hero']; });
    expect(checkAdditive(previous, narrowed).violations.some((v) => v.includes('allowedChildTypes'))).toBe(true);

    const anyPrev = withType((h) => { h.allowedChildTypes = null; });
    expect(checkAdditive(anyPrev, previous).violations.some((v) => v.includes('mọi type'))).toBe(true);

    expect(checkAdditive(previous, anyPrev).violations).toEqual([]);
  });

  it('lock cũ chưa có các field mới → không báo vi phạm giả', () => {
    expect(checkAdditive(baseSnapshot(), previous).violations).toEqual([]);
  });
});

describe('findUnlocked — lock phải phủ hết registry hiện tại', () => {
  it('lock khớp → rỗng', () => {
    const snap: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } };
    expect(findUnlocked(snap, snap)).toEqual([]);
  });

  it('type / variant / prop (kể cả lồng) mới, field snapshot mới → liệt kê đủ', () => {
    const locked: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } };
    const hero = { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } as LockTypeSnapshot;
    hero.variants.Hero02 = { requiresProps: [] };
    hero.props.subtitle = { kind: 'text' };
    hero.props.cta = { kind: 'group', props: { label: { kind: 'text', maxLength: 40 }, href: { kind: 'link', allowKinds: ['page'] } } };
    const current: LockSnapshot = { Hero: hero, Gallery: { variants: {}, props: {} } };

    expect(findUnlocked(locked, current).sort()).toEqual(
      ['Gallery (type)', 'Hero.Hero02 (variant)', 'Hero.cta.href (prop)', 'Hero.subtitle (prop)'].sort(),
    );
  });

  it('lock cũ thiếu acceptsChildren/allowedChildTypes → cần cập nhật', () => {
    const current: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } };
    expect(findUnlocked(baseSnapshot(), current)).toEqual(['Hero.acceptsChildren', 'Hero.allowedChildTypes']);
  });
});

describe('Sau review TOOLING-001', () => {
  it('lá → container (acceptsChildren false → true + allowedChildTypes) là NỚI, không báo thu hẹp', () => {
    const prev: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } };
    const curr: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: true, allowedChildTypes: ['Hero'] } };
    expect(checkAdditive(prev, curr).violations).toEqual([]);
  });

  it('findUnlocked bắt cả GIÁ TRỊ đổi (thêm option, nới maxLength) — lock phải bằng đúng snapshot', () => {
    const locked: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } };
    const hero = { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } as LockTypeSnapshot;
    hero.props.align = { kind: 'select', options: ['left', 'center', 'right'] };
    hero.props.title = { kind: 'text', maxLength: 200 };
    const unlocked = findUnlocked(locked, { Hero: hero });
    expect(unlocked).toContain('Hero.props.align.options (giá trị đổi)');
    expect(unlocked).toContain('Hero.props.title.maxLength (giá trị đổi)');
  });

  it('lock khớp từng giá trị (khác thứ tự key) → rỗng', () => {
    const a: LockSnapshot = { Hero: { ...baseHero(), acceptsChildren: false, allowedChildTypes: null } };
    const reordered = JSON.parse(JSON.stringify({ Hero: { allowedChildTypes: null, acceptsChildren: false, props: baseHero().props, variants: baseHero().variants } })) as LockSnapshot;
    expect(findUnlocked(reordered, a)).toEqual([]);
  });
});
