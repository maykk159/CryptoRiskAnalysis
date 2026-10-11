// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PriceChart } from './PriceChart';

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

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

  it.each(['touch', 'mouse'])('selects the nearest point on %s pointer move', pointerType => {
    render(<PriceChart data={data} timeRange={30} />);
    const chart = screen.getByRole('img', { name: /30-day price chart/i });
    // jsdom has no layout; zero-width bounds otherwise always select the first point.
    vi.spyOn(chart, 'getBoundingClientRect').mockReturnValue({
      x: 100,
      y: 0,
      left: 100,
      top: 0,
      right: 600,
      bottom: 310,
      width: 500,
      height: 310,
      toJSON: () => ({}),
    });

    fireEvent.pointerMove(chart, { clientX: 550, pointerType });
    expect(screen.getByText('Fri, Sep 4, 2026')).toBeTruthy();
    expect(screen.getByText('$110.00')).toBeTruthy();
    expect(screen.queryByText('Thu, Sep 3, 2026')).toBeNull();

    fireEvent.pointerMove(chart, { clientX: 150, pointerType });
    expect(screen.getByText('Thu, Sep 3, 2026')).toBeTruthy();
    expect(screen.getByText('$100.00')).toBeTruthy();
    expect(screen.queryByText('Fri, Sep 4, 2026')).toBeNull();

    fireEvent.pointerLeave(chart, { pointerType });
    if (pointerType === 'touch') {
      expect(screen.getByText('$100.00')).toBeTruthy();
    } else {
      expect(screen.queryByText('$100.00')).toBeNull();
    }
  });
});

it('resizes without losing inspection and disconnects its observer on unmount', () => {
  const observe = vi.fn();
  const disconnect = vi.fn();
  let notifyResize!: (width: number, height: number) => void;
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(
        callback: (entries: { contentRect: { width: number; height: number } }[]) => void
      ) {
        notifyResize = (width, height) => callback([{ contentRect: { width, height } }]);
      }
      observe = observe;
      disconnect = disconnect;
    }
  );
  const { container, unmount } = render(
    <PriceChart
      data={[
        { timestamp: Date.UTC(2026, 8, 3), price: 100 },
        { timestamp: Date.UTC(2026, 8, 4), price: 110 },
      ]}
      timeRange={30}
    />
  );
  const chart = screen.getByRole('img');
  const oldPath = container.querySelector('.price-chart-line')?.getAttribute('d');
  fireEvent.focus(chart);
  expect(chart.getAttribute('viewBox')).toBe('0 0 1000 310');
  expect(observe).toHaveBeenCalledExactlyOnceWith(container.querySelector('.chart-canvas'));

  act(() => notifyResize(500, 260));
  expect(chart.getAttribute('viewBox')).toBe('0 0 500 260');
  expect(container.querySelector('.price-chart-line')?.getAttribute('d')).not.toBe(oldPath);
  expect(screen.getByText('Fri, Sep 4, 2026: $110.00')).toBeTruthy();

  act(() => notifyResize(50, 120));
  expect(chart.getAttribute('viewBox')).toBe('0 0 200 200');
  expect(screen.getByText('Fri, Sep 4, 2026: $110.00')).toBeTruthy();
  unmount();
  expect(disconnect).toHaveBeenCalledOnce();
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
