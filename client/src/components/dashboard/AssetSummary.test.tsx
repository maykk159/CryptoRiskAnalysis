// @vitest-environment jsdom

import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { Asset } from '../../types';
import { AssetSummary } from './AssetSummary';

const btcAsset: Asset = {
  id: 'bitcoin',
  name: 'Bitcoin',
  ticker: 'BTC',
  icon: 'https://example.com/btc.png',
};

const ethAsset: Asset = {
  id: 'ethereum',
  name: 'Ethereum',
  ticker: 'ETH',
  icon: 'https://example.com/eth.png',
};

describe('AssetSummary component', () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    act(() => {
      vi.runOnlyPendingTimers();
    });
    vi.useRealTimers();
    cleanup();
  });

  it('renders asset details and loading skeleton when loading is true', () => {
    render(<AssetSummary asset={btcAsset} loading={true} />);

    expect(screen.getByText('BTC / USD')).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Bitcoin' })).toBeTruthy();
    expect(screen.getByLabelText('Loading current price')).toBeTruthy();
  });

  it('renders formatted current price when available', () => {
    render(<AssetSummary asset={btcAsset} price={64123.5} loading={false} />);

    expect(screen.getByText('$64,123.50')).toBeTruthy();
    expect(screen.queryByLabelText('Loading current price')).toBeNull();
  });

  it('flashes green (price-flash-up) when price increases for the same asset', () => {
    const { rerender } = render(<AssetSummary asset={btcAsset} price={60000} loading={false} />);
    const priceEl = screen.getByText('$60,000.00');
    expect(priceEl.classList.contains('price-flash-up')).toBe(false);
    expect(priceEl.classList.contains('price-flash-down')).toBe(false);

    // Increase price
    rerender(<AssetSummary asset={btcAsset} price={61000} loading={false} />);
    const updatedPriceEl = screen.getByText('$61,000.00');
    expect(updatedPriceEl.classList.contains('price-flash-up')).toBe(true);
    expect(updatedPriceEl.getAttribute('data-flash')).toBe('up');

    // Advance timer past animation duration
    act(() => {
      vi.advanceTimersByTime(1500);
    });
    expect(updatedPriceEl.classList.contains('price-flash-up')).toBe(false);
    expect(updatedPriceEl.getAttribute('data-flash')).toBeNull();
  });

  it('flashes red (price-flash-down) when price decreases for the same asset', () => {
    const { rerender } = render(<AssetSummary asset={btcAsset} price={60000} loading={false} />);

    // Decrease price
    rerender(<AssetSummary asset={btcAsset} price={59000} loading={false} />);
    const updatedPriceEl = screen.getByText('$59,000.00');
    expect(updatedPriceEl.classList.contains('price-flash-down')).toBe(true);
    expect(updatedPriceEl.getAttribute('data-flash')).toBe('down');

    // Advance timer
    act(() => {
      vi.advanceTimersByTime(1500);
    });
    expect(updatedPriceEl.classList.contains('price-flash-down')).toBe(false);
  });

  it('renders a green trend badge for positive price changes', () => {
    render(
      <AssetSummary
        asset={btcAsset}
        price={66000}
        priceHistory={[
          { timestamp: 1, price: 60000 },
          { timestamp: 2, price: 66000 },
        ]}
        loading={false}
      />
    );

    const badge = screen.getByTestId('price-change-badge');
    expect(badge.textContent).toContain('+10.00%');
    expect(badge.classList.contains('text-emerald-400')).toBe(true);
  });

  it('renders a red trend badge for negative price changes', () => {
    render(
      <AssetSummary
        asset={btcAsset}
        price={54000}
        priceHistory={[
          { timestamp: 1, price: 60000 },
          { timestamp: 2, price: 54000 },
        ]}
        loading={false}
      />
    );

    const badge = screen.getByTestId('price-change-badge');
    expect(badge.textContent).toContain('-10.00%');
    expect(screen.getByText('-$6,000.00')).toBeTruthy();
    expect(screen.queryByText('Unavailable')).toBeNull();
    expect(badge.classList.contains('text-rose-400')).toBe(true);
  });

  it('flashes when updatedAt changes even if price remains cached', () => {
    const { rerender } = render(
      <AssetSummary
        asset={btcAsset}
        price={60000}
        priceHistory={[
          { timestamp: 1, price: 50000 },
          { timestamp: 2, price: 60000 },
        ]}
        loading={false}
        updatedAt={1000}
      />
    );

    const priceEl = screen.getByText('$60,000.00');
    expect(priceEl.getAttribute('data-flash')).toBeNull();

    // Query refetches, updatedAt advances, same price
    rerender(
      <AssetSummary
        asset={btcAsset}
        price={60000}
        priceHistory={[
          { timestamp: 1, price: 50000 },
          { timestamp: 2, price: 60000 },
        ]}
        loading={false}
        updatedAt={2000}
      />
    );

    expect(priceEl.classList.contains('price-flash-up')).toBe(true);
    expect(priceEl.getAttribute('data-flash')).toBe('up');
  });

  it('does NOT flash when asset changes to another coin', () => {
    const { rerender } = render(<AssetSummary asset={btcAsset} price={60000} loading={false} />);

    // Switch to Ethereum
    rerender(<AssetSummary asset={ethAsset} price={3000} loading={false} />);
    const ethPriceEl = screen.getByText('$3,000.00');
    expect(ethPriceEl.classList.contains('price-flash-up')).toBe(false);
    expect(ethPriceEl.classList.contains('price-flash-down')).toBe(false);
    expect(ethPriceEl.getAttribute('data-flash')).toBeNull();
  });
});
