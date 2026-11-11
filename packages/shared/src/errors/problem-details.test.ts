import { describe, expect, it } from 'vitest';
import {
  getErrorCode,
  getProblemParams,
  isDraftConflictParams,
  isProblemDetails,
  isTreeValidationParams,
} from './problem-details';

describe('problem-details', () => {
  it('isProblemDetails recognizes an object with error_code', () => {
    expect(isProblemDetails({ error_code: 'sample_not_found' })).toBe(true);
    expect(isProblemDetails({ message: 'plain error' })).toBe(false);
    expect(isProblemDetails(null)).toBe(false);
    expect(isProblemDetails('string error')).toBe(false);
  });

  it('getErrorCode extracts error_code when present', () => {
    expect(getErrorCode({ error_code: 'validation_error' })).toBe('validation_error');
    expect(getErrorCode({ message: 'no code' })).toBeUndefined();
    expect(getErrorCode(new Error('network error'))).toBeUndefined();
  });
});

/**
 * `params` của ProblemDetails (WEBSITE-002 brief "Lỗi có cấu trúc"): 422 `TREE_INVALID`/`THEME_INVALID` mang
 * `params.errors[] {path, code, message}` + `params.truncated`; 409 `DRAFT_CONFLICT` mang
 * `params.currentVersion/updatedAt/updatedByUserId`. Hình dạng đối chiếu `WebsiteExceptions.cs`.
 */
describe('getProblemParams', () => {
  const treeProblem = {
    error_code: 'TREE_INVALID',
    status: 422,
    params: {
      errors: [
        { path: '/children/2/props/imageId', code: 'MEDIA_INVALID_IMAGE_REFERENCE', message: 'Ảnh không hợp lệ' },
        { path: '', code: 'TREE_ENVELOPE_INVALID', message: 'Vỏ sai' },
      ],
      truncated: false,
    },
  };

  it('trả params khi guard đúng (cây không hợp lệ)', () => {
    const params = getProblemParams(treeProblem, isTreeValidationParams);
    expect(params?.errors).toHaveLength(2);
    expect(params?.errors[0]).toEqual({
      path: '/children/2/props/imageId',
      code: 'MEDIA_INVALID_IMAGE_REFERENCE',
      message: 'Ảnh không hợp lệ',
    });
    expect(params?.truncated).toBe(false);
  });

  it('trả params khi guard đúng (xung đột draft, updatedByUserId có thể null)', () => {
    const conflict = {
      error_code: 'DRAFT_CONFLICT',
      params: { currentVersion: 7, updatedAt: '2026-11-06T01:00:00+00:00', updatedByUserId: null },
    };
    expect(getProblemParams(conflict, isDraftConflictParams)).toEqual(conflict.params);
    const withUser = { params: { currentVersion: 7, updatedAt: '2026-11-06T01:00:00Z', updatedByUserId: 'u-1' } };
    expect(getProblemParams(withUser, isDraftConflictParams)?.updatedByUserId).toBe('u-1');
  });

  it('undefined khi thiếu params, lỗi mạng, null hay giá trị không phải object', () => {
    expect(getProblemParams({ error_code: 'TREE_INVALID' }, isTreeValidationParams)).toBeUndefined();
    expect(getProblemParams(new Error('network'), isTreeValidationParams)).toBeUndefined();
    expect(getProblemParams(null, isTreeValidationParams)).toBeUndefined();
    expect(getProblemParams('x', isTreeValidationParams)).toBeUndefined();
  });

  it('undefined khi params sai hình dạng — không cast mù', () => {
    const bad = (params: unknown) => getProblemParams({ params }, isTreeValidationParams);
    expect(bad({ errors: 'nope', truncated: false })).toBeUndefined();
    expect(bad({ errors: [], truncated: 'false' })).toBeUndefined();
    expect(bad({ errors: [{ path: '/a', code: 'X' }], truncated: false })).toBeUndefined(); // thiếu message
    expect(bad({ errors: [{ path: 1, code: 'X', message: 'm' }], truncated: false })).toBeUndefined();
    expect(bad({ errors: [null], truncated: false })).toBeUndefined();
    expect(getProblemParams({ params: { currentVersion: '7', updatedAt: 'x', updatedByUserId: null } }, isDraftConflictParams)).toBeUndefined();
    expect(getProblemParams({ params: { currentVersion: 7, updatedAt: 5, updatedByUserId: null } }, isDraftConflictParams)).toBeUndefined();
  });

  it('truncated=true và danh sách rỗng đều hợp lệ (BE tối đa 50 lỗi)', () => {
    expect(getProblemParams({ params: { errors: [], truncated: true } }, isTreeValidationParams)).toEqual({
      errors: [],
      truncated: true,
    });
  });
});
