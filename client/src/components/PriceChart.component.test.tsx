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
    expect(screen.getByText('Fri, Sep 4, 2026: $110')).toBeTruthy();

    fireEvent.keyDown(chart, { key: 'ArrowLeft' });
    expect(screen.getByText('Thu, Sep 3, 2026: $100')).toBeTruthy();
  });

  it('shows a useful empty state instead of a broken chart', () => {
    render(<PriceChart data={[]} timeRange={7} />);
    expect(screen.getByRole('status').textContent).toContain('No price history');
  });
});
