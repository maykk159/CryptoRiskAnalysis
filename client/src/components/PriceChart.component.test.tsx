// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { PriceChart } from './PriceChart';

afterEach(cleanup);

describe('PriceChart interaction', () => {
  const data = [
    { timestamp: Date.UTC(2026, 8, 3), price: 100 },
    { timestamp: Date.UTC(2026, 8, 4), price: 110 },
  ];

  it('exposes the latest value and supports keyboard inspection', () => {
    render(<PriceChart data={data} timeRange={30} />);
    const chart = screen.getByRole('img', { name: /30-day price chart/i });

    fireEvent.focus(chart);
    expect(screen.getByText('Fri, Sep 4, 2026: $110.00')).toBeTruthy();

    fireEvent.keyDown(chart, { key: 'ArrowLeft' });
    expect(screen.getByText('Thu, Sep 3, 2026: $100.00')).toBeTruthy();
  });

  it('shows a useful empty state instead of a broken chart', () => {
    render(<PriceChart data={[]} timeRange={7} />);
    expect(screen.getByRole('status').textContent).toContain('No price history');
  });

  it('updates inspection on touch pointer move', () => {
    render(<PriceChart data={data} timeRange={30} />);
    const chart = screen.getByRole('img', { name: /30-day price chart/i });

    fireEvent.pointerMove(chart, { clientX: 100, pointerType: 'touch' });
    expect(screen.getByText('Thu, Sep 3, 2026')).toBeTruthy();
    expect(screen.getByText('$100.00')).toBeTruthy();
  });
});

it('clears old inspection when the series changes and preserves it for the same values', () => {
  const data = [
    { timestamp: Date.UTC(2026, 8, 3), price: 100 },
    { timestamp: Date.UTC(2026, 8, 4), price: 110 },
  ];
  const { rerender } = render(<PriceChart data={data} timeRange={30} />);
  const chart = screen.getByRole('img');
  fireEvent.focus(chart);
  expect(screen.getByText('Fri, Sep 4, 2026: $110.00')).toBeTruthy();
  rerender(<PriceChart data={data.map(point => ({ ...point }))} timeRange={30} />);
  expect(screen.getByText('Fri, Sep 4, 2026: $110.00')).toBeTruthy();
  rerender(<PriceChart data={[{ timestamp: Date.UTC(2026, 8, 5), price: 120 }]} timeRange={30} />);
  expect(screen.queryByText('Fri, Sep 4, 2026: $110.00')).toBeNull();
});

it('updates the existing chart paths on refresh without restarting the entry animation', () => {
  const data = [
    { timestamp: Date.UTC(2026, 8, 3), price: 100 },
    { timestamp: Date.UTC(2026, 8, 4), price: 110 },
    { timestamp: Date.UTC(2026, 8, 5), price: 105 },
  ];
  const { container, rerender } = render(<PriceChart data={data} timeRange={30} />);
  const line = container.querySelector('.price-chart-line');
  const area = container.querySelector('.price-chart-area');
  const oldPath = line?.getAttribute('d');
  rerender(
    <PriceChart
      data={data.map((point, index) => ({ ...point, price: index === 1 ? 102 : point.price }))}
      timeRange={30}
    />
  );
  expect(container.querySelector('.price-chart-line')).toBe(line);
  expect(container.querySelector('.price-chart-area')).toBe(area);
  expect(line?.getAttribute('d')).not.toBe(oldPath);
});
