import { describe, expect, it } from 'vitest';
import { formatUsdPrice } from '../utils/formatUsdPrice';

describe('formatUsdPrice', () => {
  it('does not round low-priced assets down to zero', () => {
    expect(formatUsdPrice(0.00001234)).toBe('$0.00001234');
  });

  it('uses a compact number of decimals for regular prices', () => {
    expect(formatUsdPrice(1234.5678)).toBe('$1,234.568');
  });
});
