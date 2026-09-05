// @vitest-environment jsdom

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getRiskAnalysis } from '../services/api';
import type { RiskAnalysisResponse } from '../types';
import { Dashboard } from './Dashboard';

vi.mock('../services/api', async importOriginal => ({
  ...(await importOriginal<typeof import('../services/api')>()),
  getRiskAnalysis: vi.fn(),
}));

const getAnalysis = vi.mocked(getRiskAnalysis);
const clients: QueryClient[] = [];
const analysis: RiskAnalysisResponse = {
  assetId: 'bitcoin',
  compositeRiskScore: 42,
  volatilityScore: 50,
  trendScore: 40,
  volumeScore: 36,
  downsideRisk: 12.34,
  maxDrawdown: 15,
  sharpeRatio: 1.2,
  valueAtRisk95: 4,
  annualizedVolatility: 55,
  priceHistory: [
    { timestamp: Date.UTC(2026, 8, 3), price: 100 },
    { timestamp: Date.UTC(2026, 8, 4), price: 110 },
  ],
};

function deferredAnalysis() {
  let resolve!: (value: RiskAnalysisResponse) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<RiskAnalysisResponse>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

function renderDashboard() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, staleTime: 60_000 } },
  });
  clients.push(client);
  return {
    client,
    ...render(
      <QueryClientProvider client={client}>
        <Dashboard />
      </QueryClientProvider>
    ),
  };
}

beforeEach(() => {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: true }))
  );
});

afterEach(() => {
  cleanup();
  clients.splice(0).forEach(client => client.clear());
  getAnalysis.mockReset();
  vi.unstubAllGlobals();
});

describe('Dashboard loading and recovery', () => {
  it('reserves all three cards while loading and replaces them with the result', async () => {
    const request = deferredAnalysis();
    getAnalysis.mockReturnValueOnce(request.promise);
    const { container } = renderDashboard();

    expect(screen.getByRole('status').textContent).toContain(
      'Loading 30-day risk analysis for Bitcoin.'
    );
    const skeleton = container.querySelector('[aria-busy="true"] > [aria-hidden="true"]');
    expect(skeleton?.children).toHaveLength(3);
    expect(screen.queryByRole('heading', { name: 'Advanced Metrics' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Refresh' })).toBeNull();

    await act(async () => request.resolve(analysis));

    expect(await screen.findByRole('heading', { name: 'Advanced Metrics' })).toBeTruthy();
    expect(container.contains(skeleton)).toBe(false);
    expect(container.querySelector('[aria-busy="true"]')).toBeNull();
    expect(screen.getByRole('status').textContent).toContain('is ready');
    expect(screen.queryByRole('button', { name: 'Refresh' })).toBeNull();
  });

  it('recovers from an initial error through Try again without reloading the page', async () => {
    const user = userEvent.setup();
    const retry = deferredAnalysis();
    getAnalysis.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    getAnalysis.mockReturnValueOnce(retry.promise);
    renderDashboard();

    expect((await screen.findByRole('alert')).textContent).toContain(
      'Failed to connect to the server'
    );
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(getAnalysis).toHaveBeenCalledTimes(2));
    expect(screen.getByRole('status').textContent).toContain('Loading 30-day');
    expect(getAnalysis).toHaveBeenLastCalledWith('bitcoin', 30, expect.any(AbortSignal));

    await act(async () => retry.resolve(analysis));

    expect(await screen.findByRole('heading', { name: 'Advanced Metrics' })).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Try again' })).toBeNull();
  });

  it('keeps the existing cards mounted during a background refresh', async () => {
    const refresh = deferredAnalysis();
    getAnalysis.mockResolvedValueOnce(analysis).mockReturnValueOnce(refresh.promise);
    const { client } = renderDashboard();
    const metricsHeading = await screen.findByRole('heading', { name: 'Advanced Metrics' });

    await act(async () => {
      void client.invalidateQueries({ queryKey: ['risk', 'bitcoin', 30] });
    });
    await waitFor(() =>
      expect(screen.getByRole('status').textContent).toContain('Refreshing 30-day')
    );
    expect(screen.getByRole('heading', { name: 'Advanced Metrics' })).toBe(metricsHeading);
    expect(screen.getByText('12.34%')).toBeTruthy();

    expect(getAnalysis).toHaveBeenCalledTimes(2);
    await act(async () => refresh.resolve({ ...analysis, downsideRisk: 42.75 }));

    expect(await screen.findByText('42.75%')).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Advanced Metrics' })).toBe(metricsHeading);
    expect(screen.queryByText('12.34%')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Refresh' })).toBeNull();
  });

  it('retains the last successful data after a failed refresh and retries the selected query', async () => {
    const user = userEvent.setup();
    const refresh = deferredAnalysis();
    const retry = deferredAnalysis();
    getAnalysis
      .mockResolvedValueOnce(analysis)
      .mockReturnValueOnce(refresh.promise)
      .mockReturnValueOnce(retry.promise);
    const { client } = renderDashboard();
    await screen.findByRole('heading', { name: 'Advanced Metrics' });

    await act(async () => {
      void client.invalidateQueries({ queryKey: ['risk', 'bitcoin', 30] });
    });
    await act(async () => refresh.reject(new Error('Service temporarily unavailable.')));

    expect(await screen.findByText(/Showing the last successfully loaded data/)).toBeTruthy();
    expect(screen.getByText('12.34%')).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Try again' }));

    const retrying = await screen.findByRole<HTMLButtonElement>('button', { name: 'Retrying…' });
    expect(retrying.disabled).toBe(true);
    await user.dblClick(retrying);
    expect(getAnalysis).toHaveBeenCalledTimes(3);
    expect(screen.getByText('12.34%')).toBeTruthy();
    expect(getAnalysis).toHaveBeenLastCalledWith('bitcoin', 30, expect.any(AbortSignal));
    await act(async () => retry.resolve({ ...analysis, downsideRisk: 42.75 }));

    expect(await screen.findByText('42.75%')).toBeTruthy();
    expect(screen.queryByText(/Refresh failed/)).toBeNull();
    expect(getAnalysis).toHaveBeenCalledTimes(3);
  });

  it('shows skeletons for a different period and ignores an abandoned request', async () => {
    const user = userEvent.setup();
    const sevenDays = deferredAnalysis();
    const ninetyDays = deferredAnalysis();
    getAnalysis
      .mockResolvedValueOnce(analysis)
      .mockReturnValueOnce(sevenDays.promise)
      .mockReturnValueOnce(ninetyDays.promise);
    const { container } = renderDashboard();
    await screen.findByRole('heading', { name: 'Advanced Metrics' });

    await user.click(screen.getByRole('button', { name: '7 Days' }));
    expect(screen.getByRole('status').textContent).toContain('Loading 7-day');
    expect(container.querySelector('[aria-busy="true"] > [aria-hidden="true"]')).toBeTruthy();
    expect(screen.queryByText('12.34%')).toBeNull();
    expect(getAnalysis).toHaveBeenLastCalledWith('bitcoin', 7, expect.any(AbortSignal));
    const abandonedSignal = getAnalysis.mock.calls[1][2];

    await user.click(screen.getByRole('button', { name: '90 Days' }));
    expect(abandonedSignal?.aborted).toBe(true);
    expect(getAnalysis).toHaveBeenLastCalledWith('bitcoin', 90, expect.any(AbortSignal));
    await act(async () => ninetyDays.resolve({ ...analysis, downsideRisk: 42.75 }));
    await screen.findByRole('heading', { name: '90-Day Price History' });
    await act(async () => sevenDays.resolve({ ...analysis, downsideRisk: 99.99 }));

    expect(screen.getByText('42.75%')).toBeTruthy();
    expect(screen.queryByText('99.99%')).toBeNull();
    expect(screen.queryByRole('heading', { name: '7-Day Price History' })).toBeNull();
  });

  it('never labels the previous asset data as the newly selected asset', async () => {
    const user = userEvent.setup();
    const ethereum = deferredAnalysis();
    getAnalysis.mockResolvedValueOnce(analysis).mockReturnValueOnce(ethereum.promise);
    renderDashboard();
    await screen.findByRole('heading', { name: 'Advanced Metrics' });

    await user.click(screen.getByRole('button', { name: 'Select Crypto Asset Bitcoin (BTC)' }));
    await user.click(screen.getByRole('option', { name: /Ethereum/ }));

    expect(screen.getByRole('status').textContent).toContain(
      'Loading 30-day risk analysis for Ethereum.'
    );
    expect(screen.queryByRole('heading', { name: /Ethereum/ })).toBeNull();
    expect(screen.queryByText('12.34%')).toBeNull();
    expect(getAnalysis).toHaveBeenLastCalledWith('ethereum', 30, expect.any(AbortSignal));
    await act(async () =>
      ethereum.resolve({ ...analysis, assetId: 'ethereum', downsideRisk: 42.75 })
    );

    expect(await screen.findByRole('heading', { name: /Ethereum/ })).toBeTruthy();
    expect(screen.getByText('42.75%')).toBeTruthy();
  });
});
