import { useCallback, useSyncExternalStore } from 'react';

export function useMediaQuery(query: string) {
  const subscribe = useCallback(
    (onChange: () => void) => {
      if (typeof window === 'undefined' || !window.matchMedia) return () => {};
      const media = window.matchMedia(query);
      media.addEventListener?.('change', onChange);
      return () => media.removeEventListener?.('change', onChange);
    },
    [query]
  );
  const getSnapshot = useCallback(
    () => typeof window !== 'undefined' && Boolean(window.matchMedia?.(query).matches),
    [query]
  );
  return useSyncExternalStore(subscribe, getSnapshot, () => false);
}
