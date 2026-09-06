import { describe, expect, it } from 'vitest';
import { getErrorCode, isProblemDetails } from './problem-details';

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
