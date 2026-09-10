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
  currentPrice: 111.25,
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
  window.history.replaceState(null, '', '/');
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() }))
  );
});

afterEach(() => {
  cleanup();
  clients.splice(0).forEach(client => client.clear());
  getAnalysis.mockReset();
  window.history.replaceState(null, '', '/');
  vi.unstubAllGlobals();
});

describe('Dashboard loading and recovery', () => {
  it('displays limitations returned by the risk engine', async () => {
    getAnalysis.mockResolvedValueOnce({
      ...analysis,
      methodology: {
        version: '2.0.0',
        riskLevel: 'Medium',
        observationCount: 7,
        returnCount: 6,
        periodStart: 0,
        periodEnd: 518400000,
        volatilityWeight: 0.4,
        trendWeight: 0.3,
        volumeWeight: 0.3,
        volatilityContribution: 20,
        trendContribution: 12,
        volumeContribution: 10,
        warnings: ['Short history: fewer than 30 daily prices; annualized estimates are unstable.'],
      },
    });
    renderDashboard();
    expect(
      (await screen.findByRole('complementary', { name: 'Analysis limitations' })).textContent
    ).toContain('Short history');
  });

  it('reserves the dashboard layout while loading and replaces them with the result', async () => {
    const request = deferredAnalysis();
    getAnalysis.mockReturnValueOnce(request.promise);
    const { container } = renderDashboard();

    expect(screen.getByRole('status').textContent).toContain(
      'Loading 30-day risk analysis for Bitcoin.'
    );
    const skeleton = container.querySelector('[aria-busy="true"] > [aria-hidden="true"]');
    expect(skeleton).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Advanced Metrics' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Refresh' })).toBeTruthy();

    await act(async () => request.resolve(analysis));

    expect(await screen.findByRole('heading', { name: 'Advanced Metrics' })).toBeTruthy();
    const formattedCurrentPrice = analysis.currentPrice.toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 8,
    });
    expect(container.textContent).toContain(`$${formattedCurrentPrice}`);
    expect(container.contains(skeleton)).toBe(false);
    expect(container.querySelector('[aria-busy="true"]')).toBeNull();
    expect(screen.getByRole('status').textContent).toContain('is ready');
    expect(screen.getAllByRole('heading', { name: 'Risk overview' })).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Refresh' })).toBeTruthy();
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
    expect(screen.getByRole('status').textContent).toBe('Unable to fetch');
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
    expect(screen.getByRole('button', { name: 'Refresh' })).toBeTruthy();
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

    await user.click(screen.getByRole('combobox', { name: 'Select Crypto Asset' }));
    await user.click(screen.getByRole('option', { name: /Ethereum/ }));

    expect(screen.getByRole('status').textContent).toContain(
      'Loading 30-day risk analysis for Ethereum.'
    );
    expect(screen.getByRole('heading', { name: /Ethereum/ })).toBeTruthy();
    expect(screen.queryByText('12.34%')).toBeNull();
    expect(getAnalysis).toHaveBeenLastCalledWith('ethereum', 30, expect.any(AbortSignal));
    await act(async () =>
      ethereum.resolve({ ...analysis, assetId: 'ethereum', downsideRisk: 42.75 })
    );

    expect(await screen.findByRole('heading', { name: /Ethereum/ })).toBeTruthy();
    expect(screen.getByText('42.75%')).toBeTruthy();
  });
});

describe('Dashboard data integrity', () => {
  it('uses currentPrice only, preserving tiny prices and showing unavailable for missing values', async () => {
    getAnalysis.mockResolvedValueOnce({ ...analysis, currentPrice: 0.00001234 });
    const { client } = renderDashboard();
    await screen.findByRole('heading', { name: 'Advanced Metrics' });
    const summary = screen.getByRole('region', { name: 'Selected asset and current price' });
    expect(summary.textContent).toContain('$0.00001234');
    expect(summary.textContent).not.toContain('$110');
    await act(async () =>
      client.setQueryData(['risk', 'bitcoin', 30], { ...analysis, currentPrice: undefined })
    );
    await waitFor(() => expect(summary.textContent).toContain('Unavailable'));
    expect(summary.textContent).not.toContain('$110');
  });

  it('keeps the successful fetch time after refresh failure and clears it for a new selection', async () => {
    const user = userEvent.setup();
    const refresh = deferredAnalysis();
    const next = deferredAnalysis();
    getAnalysis
      .mockResolvedValueOnce(analysis)
      .mockReturnValueOnce(refresh.promise)
      .mockReturnValueOnce(next.promise);
    const { container } = renderDashboard();
    await screen.findByRole('heading', { name: 'Advanced Metrics' });
    const timestamp = container.querySelector('time')?.dateTime;
    expect(timestamp).toBeTruthy();
    await user.click(screen.getByRole('button', { name: 'Refresh' }));
    await act(async () => refresh.reject(new Error('Temporarily unavailable')));
    await screen.findByText(/Showing the last successfully loaded data/);
    expect(container.querySelector('time')?.dateTime).toBe(timestamp);
    await user.click(screen.getByRole('button', { name: '7 Days' }));
    expect(container.querySelector('time')).toBeNull();
    expect(screen.queryByText('$111.25')).toBeNull();
    await act(async () => next.resolve(analysis));
  });

  it('does not request data while typing and prevents parallel manual refreshes', async () => {
    const user = userEvent.setup();
    const refresh = deferredAnalysis();
    getAnalysis.mockResolvedValueOnce(analysis).mockReturnValueOnce(refresh.promise);
    renderDashboard();
    await screen.findByRole('heading', { name: 'Advanced Metrics' });
    await user.click(screen.getByRole('combobox'));
    await user.type(screen.getByRole('combobox'), 'ethereum');
    expect(getAnalysis).toHaveBeenCalledTimes(1);
    await user.keyboard('{Escape}');
    await user.dblClick(screen.getByRole('button', { name: 'Refresh' }));
    expect(getAnalysis).toHaveBeenCalledTimes(2);
    expect(screen.getByRole<HTMLButtonElement>('button', { name: 'Refresh' }).disabled).toBe(true);
    await act(async () => refresh.resolve(analysis));
  });
});

describe('Dashboard URL state and deep linking', () => {
  it('loads the asset and time range directly specified in the URL query string', async () => {
    window.history.replaceState(null, '', '/?asset=ethereum&days=90');
    getAnalysis.mockResolvedValueOnce({
      ...analysis,
      assetId: 'ethereum',
    });

    renderDashboard();

    expect(screen.getByRole('status').textContent).toContain(
      'Loading 90-day risk analysis for Ethereum.'
    );
    expect(getAnalysis).toHaveBeenCalledWith('ethereum', 90, expect.any(AbortSignal));
    expect(await screen.findByRole('heading', { name: '90-Day Price History' })).toBeTruthy();
  });

  it('updates the URL search params when the user changes asset or period', async () => {
    const user = userEvent.setup();
    getAnalysis.mockResolvedValue(analysis);
    renderDashboard();

    await screen.findByRole('heading', { name: 'Advanced Metrics' });
    expect(window.location.search).toBe('?asset=bitcoin&days=30');

    await user.click(screen.getByRole('button', { name: '7 Days' }));
    expect(window.location.search).toBe('?asset=bitcoin&days=7');

    await user.click(screen.getByRole('combobox', { name: 'Select Crypto Asset' }));
    await user.click(screen.getByRole('option', { name: /Ethereum/ }));
    expect(window.location.search).toBe('?asset=ethereum&days=7');
  });

  it('preserves an unknown asset from the URL and displays the error without falling back to bitcoin', async () => {
    window.history.replaceState(null, '', '/?asset=unsupportedcoin&days=30');
    const { ApiRequestError } = await import('../services/api');
    getAnalysis.mockRejectedValueOnce(
      new ApiRequestError(
        'Crypto asset "unsupportedcoin" not found. Please select a different asset.',
        404
      )
    );

    renderDashboard();

    expect(getAnalysis).toHaveBeenCalledWith('unsupportedcoin', 30, expect.any(AbortSignal));
    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Crypto asset "Unsupportedcoin" not found');
    expect(screen.getByRole('heading', { name: 'Analysis unavailable' })).toBeTruthy();
  });
});
