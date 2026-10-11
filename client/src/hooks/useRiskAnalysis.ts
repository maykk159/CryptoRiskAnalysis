import { useQuery } from '@tanstack/react-query';
import { useRef } from 'react';
import { getRiskAnalysis } from '../services/api';

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
    staleTime: 0,
    refetchInterval: 10_000,
  });
  const paused = fetchStatus === 'paused';
  const retryAnalysis = () => {
    if (isFetching || paused) return;
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
    retryAnalysis,
  };
}
