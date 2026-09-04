import { describe, expect, it } from 'vitest';
import { ApiRequestError, getErrorMessage } from './api';

describe('getErrorMessage', () => {
  it('turns fetch connection failures into an actionable message', () => {
    expect(getErrorMessage(new TypeError('Failed to fetch'))).toBe(
      'Failed to connect to the server. Please check your internet connection.'
    );
  });

  it('keeps status-aware API messages', () => {
    expect(getErrorMessage(new ApiRequestError('missing', 404), 'Bitcoin')).toContain(
      'Bitcoin'
    );
    expect(getErrorMessage(new ApiRequestError('limited', 429))).toContain(
      'rate limit'
    );
  });
});
