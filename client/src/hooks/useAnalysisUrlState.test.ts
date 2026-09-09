// @vitest-environment jsdom

import { act, cleanup, renderHook } from '@testing-library/react';
import { createElement, StrictMode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { parseUrlParams, useAnalysisUrlState } from './useAnalysisUrlState';

describe('parseUrlParams', () => {
  it('returns bitcoin and 30 by default when query string is empty', () => {
    expect(parseUrlParams('')).toEqual({ assetId: 'bitcoin', days: 30 });
  });

  it('parses valid asset and days from query string', () => {
    expect(parseUrlParams('?asset=ethereum&days=90')).toEqual({
      assetId: 'ethereum',
      days: 90,
    });
  });

  it('preserves unrecognized asset ID explicitly without silent fallback to bitcoin', () => {
    expect(parseUrlParams('?asset=pepe&days=7')).toEqual({
      assetId: 'pepe',
      days: 7,
    });
  });

  it('falls back non-standard days to 30', () => {
    expect(parseUrlParams('?asset=solana&days=15')).toEqual({
      assetId: 'solana',
      days: 30,
    });
  });

  it.each(['7abc', '7.5', '-7', '0', 'NaN', 'Infinity', ''])('rejects malformed days: %s', days => {
    expect(parseUrlParams(`?days=${days}`).days).toBe(30);
  });
});

describe('useAnalysisUrlState', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/');
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
    window.history.replaceState(null, '', '/');
  });

  it('initializes from URL and canonicalizes search params with replaceState', () => {
    const push = vi.spyOn(window.history, 'pushState');
    const replace = vi.spyOn(window.history, 'replaceState');
    const { result } = renderHook(() => useAnalysisUrlState());

    expect(result.current.assetId).toBe('bitcoin');
    expect(result.current.days).toBe(30);
    expect(window.location.search).toBe('?asset=bitcoin&days=30');
    expect(push).not.toHaveBeenCalled();
    expect(replace).toHaveBeenCalledOnce();
  });

  it('reads initial custom asset and days from window.location', () => {
    window.history.replaceState(null, '', '/?asset=solana&days=90');

    const { result } = renderHook(() => useAnalysisUrlState());

    expect(result.current.assetId).toBe('solana');
    expect(result.current.days).toBe(90);
  });

  it('updates assetId and pushes new URL state', () => {
    const { result } = renderHook(() => useAnalysisUrlState());

    act(() => {
      result.current.setAssetId('ethereum');
    });

    expect(result.current.assetId).toBe('ethereum');
    expect(window.location.search).toBe('?asset=ethereum&days=30');
  });

  it('updates days and pushes new URL state', () => {
    const { result } = renderHook(() => useAnalysisUrlState());

    act(() => {
      result.current.setDays(7);
    });

    expect(result.current.days).toBe(7);
    expect(window.location.search).toBe('?asset=bitcoin&days=7');
  });

  it('responds to browser popstate events (Back / Forward navigation)', () => {
    const { result } = renderHook(() => useAnalysisUrlState());

    act(() => {
      result.current.setAssetId('ethereum');
    });
    expect(result.current.assetId).toBe('ethereum');

    // Simulate browser Back button
    act(() => {
      window.history.replaceState(null, '', '/?asset=bitcoin&days=30');
      window.dispatchEvent(new PopStateEvent('popstate'));
    });

    expect(result.current.assetId).toBe('bitcoin');
    expect(result.current.days).toBe(30);
  });

  it('pushes exactly once per selection in StrictMode and ignores unchanged or invalid selections', () => {
    const push = vi.spyOn(window.history, 'pushState');
    const { result } = renderHook(() => useAnalysisUrlState(), {
      wrapper: ({ children }) => createElement(StrictMode, null, children),
    });
    expect(push).not.toHaveBeenCalled();
    act(() => result.current.setDays(7));
    expect(push).toHaveBeenCalledTimes(1);
    act(() => result.current.setDays(7));
    act(() => result.current.setDays(15));
    expect(push).toHaveBeenCalledTimes(1);
    expect(result.current.days).toBe(7);
  });

  it('does not suppress the next selection after popstate with unchanged analysis parameters', () => {
    const { result } = renderHook(() => useAnalysisUrlState());
    const push = vi.spyOn(window.history, 'pushState');
    act(() => {
      window.history.replaceState(
        { retained: true },
        '',
        '/?asset=bitcoin&days=30&view=chart#details'
      );
      window.dispatchEvent(new PopStateEvent('popstate'));
    });
    expect(push).not.toHaveBeenCalled();
    act(() => result.current.setDays(90));
    expect(push).toHaveBeenCalledOnce();
    expect(window.location.search).toBe('?asset=bitcoin&days=90&view=chart');
    expect(window.location.hash).toBe('#details');
    expect(window.history.state).toEqual({ retained: true });
  });
});
