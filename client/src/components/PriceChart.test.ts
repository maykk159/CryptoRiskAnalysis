import { describe, expect, it } from 'vitest';
import { formatUsdPrice } from '../utils/formatUsdPrice';
import { formatUtcAxisDate, formatUtcTooltipDate } from '../utils/formatUtcDate';
import { createChartModel } from '../utils/priceChartModel';

describe('formatUsdPrice', () => {
  it('does not round low-priced assets down to zero', () => {
    expect(formatUsdPrice(0.00001234)).toBe('$0.00001234');
  });

  it('uses a compact number of decimals for regular prices', () => {
    expect(formatUsdPrice(1234.5678)).toBe('$1,234.568');
  });
});

describe('createChartModel', () => {
  it('maps valid prices into bounded SVG coordinates and keeps five readable ticks', () => {
    const data = Array.from({ length: 30 }, (_, index) => ({
      timestamp: Date.UTC(2026, 7, index + 1),
      price: 100 + index,
    }));

    const model = createChartModel(data);

    expect(model?.points).toHaveLength(30);
    expect(model?.xTicks).toHaveLength(5);
    expect(model?.yTicks).toHaveLength(5);
    expect(model?.linePath).toMatch(/^M/);
    expect(model?.areaPath).toMatch(/Z$/);
  });

  it('returns an empty model when no finite observations exist', () => {
    expect(createChartModel([{ timestamp: 1, price: Number.NaN }])).toBeNull();
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
