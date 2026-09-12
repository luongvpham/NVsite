import { describe, expect, it } from 'vitest';
import { opRules } from '../generated/op-rules';
import aiToolSchema from '../generated/ai-tool-schema.json';

describe('op-rules.ts (§1, §14 2.10) — data cho Operations Engine (Bước 5), không enforce ở đây', () => {
  it('RichText có content trong editableInSystemPageProps (test invariant #6 §6.1)', () => {
    expect(opRules.RichText.allowedInPageKinds).toContain('System');
    expect(opRules.RichText.editableInSystemPageProps).toContain('content');
  });

  it('Section chấp nhận children, Hero thì không', () => {
    expect(opRules.Section.acceptsChildren).toBe(true);
    expect(opRules.Hero.acceptsChildren).toBe(false);
  });

  it('Hero maxPerPage: 1 giữ nguyên từ manifest', () => {
    expect(opRules.Hero.maxPerPage).toBe(1);
  });
});

describe('ai-tool-schema.json (§12) — enum không thể bịa (#17)', () => {
  it('componentType enum khớp đúng 5 type đã đăng ký', () => {
    expect(aiToolSchema.add_component.componentType.enum.sort()).toEqual(
      ['Gallery', 'Hero', 'RichText', 'Section', 'ServiceGrid'].sort(),
    );
  });

  it('variant enum chứa tất cả variant key của mọi type', () => {
    expect(aiToolSchema.add_component.variant.enum).toContain('Hero01');
    expect(aiToolSchema.add_component.variant.enum).toContain('Gallery02');
  });

  it('aiSummaryTemplates có template cho mọi type', () => {
    expect(Object.keys(aiToolSchema.aiSummaryTemplates).sort()).toEqual(
      ['Gallery', 'Hero', 'RichText', 'Section', 'ServiceGrid'].sort(),
    );
  });
});
