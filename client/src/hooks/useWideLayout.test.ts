// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, expect, it, vi } from 'vitest';
import { useWideLayout } from './useWideLayout';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

it('falls back to the narrow layout when matchMedia is unavailable', () => {
  vi.stubGlobal('matchMedia', undefined);
  expect(renderHook(() => useWideLayout()).result.current).toBe(false);
});

it('updates on breakpoint changes', () => {
  const media = new EventTarget() as EventTarget & { matches: boolean };
  media.matches = false;
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => media)
  );
  const { result } = renderHook(() => useWideLayout());
  act(() => {
    media.matches = true;
    media.dispatchEvent(new Event('change'));
  });
  expect(result.current).toBe(true);
});
