import { describe, expect, it } from 'vitest';
import { formatUsdPrice } from '../utils/formatUsdPrice';
import { formatUtcAxisDate, formatUtcTooltipDate } from '../utils/formatUtcDate';
import { createChartModel } from '../utils/priceChartModel';

describe('formatUsdPrice', () => {
  it('does not round low-priced assets down to zero', () => {
    expect(formatUsdPrice(0.00001234)).toBe('$0.00001234');
  });

  it('uses a compact number of decimals for regular prices', () => {
    expect(formatUsdPrice(1234.5678)).toBe('$1,234.57');
  });

  it('preserves minimum two decimals for sub-dollar prices without dropping trailing zeros', () => {
    expect(formatUsdPrice(0.5)).toBe('$0.50');
    expect(formatUsdPrice(0.1)).toBe('$0.10');
    expect(formatUsdPrice(0.05)).toBe('$0.05');
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

describe('price edge cases', () => {
  it('distinguishes zero and invalid values, and keeps tiny positive prices', () => {
    expect(formatUsdPrice(0)).toBe('$0.00');
    for (const value of [undefined, null, NaN, Infinity, -1])
      expect(formatUsdPrice(value)).toBe('Unavailable');
    expect(formatUsdPrice(1e-12)).not.toBe('$0.00');
    expect(formatUsdPrice(123456789, 'axis')).toBe('$123.5M');
  });
  it.each([0, 0.00001234, 100, 1e12])('bounds flat series at %s', price => {
    const model = createChartModel(
      [
        { timestamp: 1, price },
        { timestamp: 2, price },
      ],
      280,
      280
    )!;
    for (const point of model.points) {
      expect(Number.isFinite(point.y)).toBe(true);
      expect(point.y).toBeGreaterThanOrEqual(model.bounds.top);
      expect(point.y).toBeLessThanOrEqual(model.bounds.height - model.bounds.bottom);
    }
    expect(model.yTicks.every(tick => tick.value >= 0)).toBe(true);
  });
  it('centers a single point and filters invalid timestamps and negative prices', () => {
    const model = createChartModel(
      [
        { timestamp: 1, price: 10 },
        { timestamp: 1e20, price: 20 },
        { timestamp: 2, price: -1 },
      ],
      280,
      280
    )!;
    expect(model.points).toHaveLength(1);
    expect(model.points[0].x).toBeGreaterThan(model.bounds.left);
    expect(createChartModel([])).toBeNull();
  });
});
