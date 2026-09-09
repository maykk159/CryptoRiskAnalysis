import { useCallback, useEffect, useState } from 'react';

export function parseUrlParams(
  search = typeof window !== 'undefined' ? window.location.search : ''
): {
  assetId: string;
  days: number;
} {
  const params = new URLSearchParams(search);
  const rawAsset = params.get('asset');
  const rawDays = params.get('days');

  const assetId = rawAsset !== null ? rawAsset.trim().toLowerCase() : 'bitcoin';
  const parsedDays = rawDays !== null ? Number(rawDays) : 30;
  const days = parsedDays === 7 || parsedDays === 30 || parsedDays === 90 ? parsedDays : 30;

  return { assetId: assetId || 'bitcoin', days };
}

export function useAnalysisUrlState() {
  const [state, setState] = useState(() => ({
    ...parseUrlParams(),
    navigation: 'replace' as 'replace' | 'push',
  }));

  // Listen to browser Back/Forward (popstate)
  useEffect(() => {
    const handlePopState = () => {
      setState({ ...parseUrlParams(), navigation: 'replace' });
    };

    window.addEventListener('popstate', handlePopState);
    return () => window.removeEventListener('popstate', handlePopState);
  }, []);

  // Synchronize state changes to the URL.
  // This replaces the previous side-effect inside setState updater callbacks.
  useEffect(() => {
    if (typeof window === 'undefined') return;

    const url = new URL(window.location.href);
    url.searchParams.set('asset', state.assetId);
    url.searchParams.set('days', String(state.days));

    if (url.search !== window.location.search) {
      const method = state.navigation === 'push' ? 'pushState' : 'replaceState';
      window.history[method](window.history.state, '', url.toString());
    }
  }, [state]);

  const setAssetId = useCallback((newAssetId: string) => {
    setState(prev => {
      if (prev.assetId === newAssetId) return prev;
      return { ...prev, assetId: newAssetId, navigation: 'push' };
    });
  }, []);

  const setDays = useCallback((newDays: number) => {
    if (![7, 30, 90].includes(newDays)) return;
    setState(prev => {
      if (prev.days === newDays) return prev;
      return { ...prev, days: newDays, navigation: 'push' };
    });
  }, []);

  return {
    assetId: state.assetId,
    days: state.days,
    setAssetId,
    setDays,
  };
}
