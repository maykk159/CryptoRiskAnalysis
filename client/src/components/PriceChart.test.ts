import { describe, expect, it } from 'vitest';
import { formatUsdPrice } from '../utils/formatUsdPrice';
import { formatUtcAxisDate, formatUtcTooltipDate } from '../utils/formatUtcDate';

describe('formatUsdPrice', () => {
  it('does not round low-priced assets down to zero', () => {
    expect(formatUsdPrice(0.00001234)).toBe('$0.00001234');
  });

  it('uses a compact number of decimals for regular prices', () => {
    expect(formatUsdPrice(1234.5678)).toBe('$1,234.568');
  });
});

describe('PriceChart UTC date formatting', () => {
  const utcMidnight = Date.parse('2026-09-04T00:00:00.000Z');

  it('keeps a UTC-midnight point on the same calendar day on the axis', () => {
    expect(formatUtcAxisDate(utcMidnight)).toBe('4/9');
  });

  it('keeps a UTC-midnight point on the same calendar day in the tooltip', () => {
    expect(formatUtcTooltipDate(utcMidnight)).toBe('Fri, Sep 4, 2026');
  });
});
