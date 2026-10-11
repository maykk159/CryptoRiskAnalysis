import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiRequestError, getErrorMessage, getRiskAnalysis } from './api';

afterEach(() => vi.unstubAllGlobals());

it('manual refresh explicitly asks the backend for a fresh quote', async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(
      new Response(JSON.stringify({ succeeded: true, data: { currentPrice: 100 } }))
    );
  vi.stubGlobal('fetch', fetchMock);
  await getRiskAnalysis('bitcoin', 90, undefined, true);
  expect(fetchMock).toHaveBeenCalledWith(
    expect.stringContaining('/RiskAnalysis/bitcoin?days=90&refresh=true'),
    expect.any(Object)
  );
});

describe('getRiskAnalysis', () => {
  it('encodes asset IDs, sends the selected range and forwards the abort signal', async () => {
    const data = {
      assetId: 'coin/with space',
      currentPrice: 100,
      compositeRiskScore: 50,
      volatilityScore: 40,
      trendScore: 50,
      volumeScore: 60,
      downsideRisk: 1,
      maxDrawdown: 2,
      sharpeRatio: 1,
      valueAtRisk95: 3,
      annualizedVolatility: 40,
      priceHistory: [{ timestamp: 1, price: 100 }],
    };
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ succeeded: true, data }));
    vi.stubGlobal('fetch', fetchMock);
    const controller = new AbortController();
    expect(await getRiskAnalysis('coin/with space', 90, controller.signal)).toEqual(data);
    expect(fetchMock).toHaveBeenCalledExactlyOnceWith(
      expect.stringMatching(/\/RiskAnalysis\/coin%2Fwith%20space\?days=90$/),
      { headers: { Accept: 'application/json' }, signal: controller.signal }
    );
  });

  it('defaults to a 30-day request', async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ succeeded: true, data: {} }));
    vi.stubGlobal('fetch', fetchMock);
    await getRiskAnalysis('bitcoin');
    expect(fetchMock.mock.calls[0][0]).toMatch(/\?days=30$/);
  });

  it.each([404, 500])('preserves the HTTP %i status and server error message', async status => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(
          Response.json({ succeeded: false, message: 'provider failure' }, { status })
        )
    );
    await expect(getRiskAnalysis('bitcoin')).rejects.toMatchObject({
      name: 'ApiRequestError',
      status,
      message: 'provider failure',
    });
  });

  it.each([200, 500])('turns malformed JSON at status %i into an API error', async status => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(new Response('<html>error</html>', { status }))
    );
    await expect(getRiskAnalysis('bitcoin')).rejects.toMatchObject({
      name: 'ApiRequestError',
      status,
    });
  });

  it.each([{ succeeded: false }, { succeeded: true }, null])(
    'rejects unsuccessful or missing data envelopes: %j',
    async payload => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(payload)));
      await expect(getRiskAnalysis('bitcoin')).rejects.toBeInstanceOf(ApiRequestError);
    }
  );

  it.each([new TypeError('Failed to fetch'), new DOMException('Aborted', 'AbortError')])(
    'preserves network and abort errors for the caller',
    async error => {
      vi.stubGlobal('fetch', vi.fn().mockRejectedValue(error));
      await expect(getRiskAnalysis('bitcoin')).rejects.toBe(error);
    }
  );
});

describe('getErrorMessage', () => {
  it('turns fetch connection failures into an actionable message', () => {
    expect(getErrorMessage(new TypeError('Failed to fetch'))).toBe(
      'Failed to connect to the server. Please check your internet connection.'
    );
  });

  it('keeps status-aware API messages', () => {
    expect(getErrorMessage(new ApiRequestError('missing', 404), 'Bitcoin')).toContain('Bitcoin');
    expect(getErrorMessage(new ApiRequestError('limited', 429))).toContain('rate limit');
  });
});
