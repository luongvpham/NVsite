import { describe, expect, it } from 'vitest';
import { getPublishedSite, getPublishedSiteResponse } from '@vsite/api-sdk';
import type { PublishedSiteDto } from '@vsite/api-sdk';

describe('api-sdk barrel (WEBSITE-004 site)', () => {
  it('exports getPublishedSite, its zod envelope and DTO types', () => {
    const dto: PublishedSiteDto | undefined = undefined;
    expect(dto).toBeUndefined();
    expect(typeof getPublishedSite).toBe('function');
    expect(typeof getPublishedSiteResponse.parse).toBe('function');
  });
});
