import { useMediaQuery } from './useMediaQuery';

export function useWideLayout() {
  return useMediaQuery('(min-width: 1024px)');
}
