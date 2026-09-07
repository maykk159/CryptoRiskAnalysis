import { describe, expect, it } from 'vitest';
import { formatMetric, riskLevel } from './riskPresentation';

describe('risk presentation', () => {
  it.each([
    [0, 'Low risk'],
    [29.9, 'Low risk'],
    [30, 'Medium risk'],
    [69.9, 'Medium risk'],
    [70, 'High risk'],
    [100, 'High risk'],
  ])('uses the existing threshold for %s', (score, label) => {
    expect(riskLevel(score as number).label).toBe(label);
  });
  it('does not label invalid scores as high risk', () => {
    expect(riskLevel(NaN).valid).toBe(false);
    expect(riskLevel(undefined).label).toBe('Unavailable');
  });
  it('formats losses without negative zero and keeps ratios distinct from percentages', () => {
    expect(formatMetric(0, 'loss')).toBe('0.00%');
    expect(formatMetric(-0.001, 'loss')).toBe('0.00%');
    expect(formatMetric(5, 'loss')).toBe('-5.00%');
    expect(formatMetric(-5, 'loss')).toBe('-5.00%');
    expect(formatMetric(1.2, 'ratio')).toBe('1.20');
    expect(formatMetric(undefined)).toBe('Unavailable');
  });
});
