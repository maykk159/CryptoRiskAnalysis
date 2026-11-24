import { focusManager, useQuery } from '@tanstack/react-query';
import { useCallback, useEffect, useRef, useSyncExternalStore } from 'react';
import { ApiRequestError, getRiskAnalysis } from '../services/api';

function getRetryAt(error: unknown) {
  return error instanceof ApiRequestError && error.status === 429 ? error.retryAt : undefined;
}

function useRetryCountdown(retryAt: number | undefined) {
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (retryAt === undefined) return () => {};
      let timer: ReturnType<typeof setTimeout>;
      const scheduleTick = () => {
        const remaining = retryAt - Date.now();
        if (remaining <= 0) return;
        timer = setTimeout(
          () => {
            onChange();
            scheduleTick();
          },
          remaining % 1000 || 1000
        );
      };
      scheduleTick();
      return () => clearTimeout(timer);
    },
    [retryAt]
  );
  const getSnapshot = useCallback(
    () => Math.max(0, Math.ceil(((retryAt ?? 0) - Date.now()) / 1000)),
    [retryAt]
  );
  return useSyncExternalStore(subscribe, getSnapshot, () => 0);
}

export function useRiskAnalysis(assetId: string, days: number) {
  const refreshRequested = useRef(false);
  const {
    data,
    isPending,
    isFetching,
    fetchStatus,
    error,
    isRefetchError,
    refetch,
    dataUpdatedAt,
  } = useQuery({
    queryKey: ['risk', assetId, days],
    queryFn: ({ signal }) => {
      const refresh = refreshRequested.current;
      refreshRequested.current = false;
      return refresh
        ? getRiskAnalysis(assetId, days, signal, true)
        : getRiskAnalysis(assetId, days, signal);
    },
    enabled: query => (getRetryAt(query.state.error) ?? 0) <= Date.now(),
    staleTime: 0,
    refetchInterval: 10_000,
  });
  const retryAt = getRetryAt(error);
  const retryInSeconds = useRetryCountdown(retryAt);
  useEffect(() => {
    // Enabling after a time-based hold does not itself schedule a stale query fetch.
    if (retryAt !== undefined && retryInSeconds === 0 && focusManager.isFocused())
      void refetch({ cancelRefetch: false });
  }, [refetch, retryAt, retryInSeconds]);

  const paused = fetchStatus === 'paused';
  const retryAnalysis = () => {
    if (isFetching || paused || (retryAt ?? 0) > Date.now()) return;
    refreshRequested.current = true;
    void refetch({ cancelRefetch: false });
  };

  return {
    data,
    isPending,
    isFetching,
    paused,
    error,
    isRefetchError,
    dataUpdatedAt,
    retryInSeconds,
    retryAnalysis,
  };
}
