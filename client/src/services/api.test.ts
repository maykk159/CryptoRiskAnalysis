import { afterEach, describe, expect, it, vi } from 'vitest';
import type { RiskAnalysisResponse } from '../types';
import { ApiRequestError, getErrorMessage, getRiskAnalysis } from './api';

const legacyAnalysis = {
  assetId: 'bitcoin',
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
} satisfies RiskAnalysisResponse;

const currentAnalysis = {
  ...legacyAnalysis,
  sharpeRatio: null,
  currentQuote: {
    price: 100,
    source: 'Binance',
    currency: 'USDT',
    fetchedAt: '2026-11-23T18:00:45+00:00',
    sourceUpdatedAt: null,
  },
  methodology: {
    version: '2.0.0',
    riskLevel: 'Medium',
    observationCount: 7,
    returnCount: 6,
    periodStart: 1,
    periodEnd: 2,
    volatilityWeight: 0.4,
    trendWeight: 0.3,
    volumeWeight: 0.3,
    volatilityContribution: 16,
    trendContribution: 15,
    volumeContribution: 18,
    warnings: ['Short history'],
  },
} satisfies RiskAnalysisResponse;

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

it('manual refresh explicitly asks the backend for a fresh quote', async () => {
  const fetchMock = vi
    .fn()
    .mockResolvedValue(Response.json({ succeeded: true, data: legacyAnalysis }));
  vi.stubGlobal('fetch', fetchMock);
  await getRiskAnalysis('bitcoin', 90, undefined, true);
  expect(fetchMock).toHaveBeenCalledWith(
    expect.stringContaining('/RiskAnalysis/bitcoin?days=90&refresh=true'),
    expect.any(Object)
  );
});

describe('getRiskAnalysis', () => {
  it('encodes asset IDs, sends the selected range and forwards the abort signal', async () => {
    const data = { ...legacyAnalysis, assetId: 'coin/with space' };
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
    const fetchMock = vi
      .fn()
      .mockResolvedValue(Response.json({ succeeded: true, data: legacyAnalysis }));
    vi.stubGlobal('fetch', fetchMock);
    await getRiskAnalysis('bitcoin');
    expect(fetchMock.mock.calls[0][0]).toMatch(/\?days=30$/);
  });

  it.each([
    ['current metadata and null Sharpe', currentAnalysis],
    ['legacy metadata omissions', legacyAnalysis],
    ['nullable metadata', { ...legacyAnalysis, currentQuote: null, methodology: null }],
  ])('accepts valid risk analysis with %s', async (_, data) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json({ succeeded: true, data })));
    await expect(getRiskAnalysis('bitcoin')).resolves.toEqual(data);
  });

  it.each([
    ['empty data', {}],
    ['wrong asset', { ...legacyAnalysis, assetId: 'ethereum' }],
    ['out-of-range score', { ...legacyAnalysis, compositeRiskScore: 101 }],
    ['string metric', { ...legacyAnalysis, annualizedVolatility: '40' }],
    ['missing Sharpe', { ...legacyAnalysis, sharpeRatio: undefined }],
    ['missing series', { ...legacyAnalysis, priceHistory: undefined }],
    ['empty series', { ...legacyAnalysis, priceHistory: [] }],
    ['null price point', { ...legacyAnalysis, priceHistory: [null] }],
    ['invalid timestamp', { ...legacyAnalysis, priceHistory: [{ timestamp: 9e15, price: 100 }] }],
    [
      'invalid warnings',
      { ...currentAnalysis, methodology: { ...currentAnalysis.methodology, warnings: null } },
    ],
    [
      'invalid quote date',
      {
        ...currentAnalysis,
        currentQuote: { ...currentAnalysis.currentQuote, fetchedAt: 'unknown' },
      },
    ],
  ])('rejects a successful envelope with %s', async (_, data) => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(Response.json({ succeeded: true, message: 'OK', data }))
    );

    await expect(getRiskAnalysis('bitcoin')).rejects.toMatchObject({
      name: 'ApiRequestError',
      status: 200,
      message: 'The server returned invalid risk analysis data.',
    });
  });

  it.each([400, 404, 500])(
    'preserves the HTTP %i status and server error message',
    async status => {
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
    }
  );

  it.each([
    [
      {
        title: 'One or more validation errors occurred.',
        errors: {
          days: ["The value 'abc' is not valid."],
          refresh: ["The value 'maybe' is not valid."],
        },
      },
      "days — The value 'abc' is not valid. refresh — The value 'maybe' is not valid.",
    ],
    [
      {
        title: 'Validation error.',
        detail: 'Enter a valid date.',
        errors: { endDate: [null, ' '] },
      },
      'Enter a valid date.',
    ],
    [{ title: 'Invalid request.' }, 'Invalid request.'],
  ])('uses field messages, detail, or title from Problem Details', async (problem, message) => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        Response.json(problem, {
          status: 400,
          headers: { 'Content-Type': 'application/problem+json' },
        })
      )
    );

    await expect(getRiskAnalysis('bitcoin')).rejects.toMatchObject({
      name: 'ApiRequestError',
      status: 400,
      message,
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

  it.each([
    ['60', 60_000],
    ['Mon, 23 Nov 2026 18:00:45 GMT', 45_000],
    ['Mon, 23 Nov 2026 17:59:45 GMT', undefined],
    ['unexpected', undefined],
    ['0.5', undefined],
    ['0', undefined],
    [null, undefined],
  ])('uses only a valid future Retry-After value on HTTP 429 (%s)', async (header, delay) => {
    const now = Date.UTC(2026, 10, 23, 18);
    vi.spyOn(Date, 'now').mockReturnValue(now);
    const headers = header === null ? undefined : { 'Retry-After': header };
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue(
        new Response('<html>limited</html>', {
          status: 429,
          headers,
        })
      )
    );

    await expect(getRiskAnalysis('bitcoin')).rejects.toMatchObject({
      status: 429,
      retryAt: delay === undefined ? undefined : now + delay,
    });
  });

  it.each([
    { succeeded: false },
    { succeeded: true },
    { succeeded: 'true', data: legacyAnalysis },
    null,
  ])('rejects unsuccessful or missing data envelopes: %j', async payload => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(Response.json(payload)));
    await expect(getRiskAnalysis('bitcoin')).rejects.toBeInstanceOf(ApiRequestError);
  });

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
