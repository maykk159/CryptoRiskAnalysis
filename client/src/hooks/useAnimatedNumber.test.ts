// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { useAnimatedNumber } from './useAnimatedNumber';

let tick: FrameRequestCallback;
let now: number;
beforeEach(() => {
  now = 0;
  vi.spyOn(performance, 'now').mockImplementation(() => now);
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: false }))
  );
  vi.stubGlobal(
    'requestAnimationFrame',
    vi.fn(callback => {
      tick = callback;
      return 1;
    })
  );
  vi.stubGlobal('cancelAnimationFrame', vi.fn());
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});
function advance(time: number) {
  now = time;
  act(() => tick(now));
}

it('reacts to live reduced-motion changes and unsubscribes on unmount', () => {
  const media = new EventTarget() as EventTarget & { matches: boolean };
  media.matches = false;
  const remove = vi.spyOn(media, 'removeEventListener');
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => media)
  );
  const { result, unmount } = renderHook(() => useAnimatedNumber(80));
  advance(100);
  expect(result.current).toBeLessThan(80);
  act(() => {
    media.matches = true;
    media.dispatchEvent(new Event('change'));
  });
  expect(result.current).toBe(80);
  expect(cancelAnimationFrame).toHaveBeenCalled();
  act(() => {
    media.matches = false;
    media.dispatchEvent(new Event('change'));
  });
  advance(450);
  expect(result.current).toBe(80);
  unmount();
  expect(remove).toHaveBeenCalledWith('change', expect.any(Function));
});

it('counts up on entry and reaches the exact target after 650 ms', () => {
  const { result } = renderHook(() => useAnimatedNumber(80));
  expect(result.current).toBe(0);
  advance(500);
  expect(result.current).toBeGreaterThan(0);
  expect(result.current).toBeLessThan(80);
  advance(650);
  expect(result.current).toBe(80);
});

it('continues from the displayed value when an update interrupts the animation', () => {
  const { result, rerender } = renderHook(({ score }) => useAnimatedNumber(score), {
    initialProps: { score: 80 },
  });
  advance(500);
  const current = result.current;
  rerender({ score: 40 });
  expect(result.current).toBe(current);
  advance(675);
  expect(result.current).toBeLessThan(current);
  expect(result.current).toBeGreaterThan(40);
  advance(850);
  expect(result.current).toBe(40);
  expect(cancelAnimationFrame).toHaveBeenCalled();
});

it('shows the final value immediately with reduced motion', () => {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches: true }))
  );
  const { result, rerender } = renderHook(({ score }) => useAnimatedNumber(score), {
    initialProps: { score: 80 },
  });
  expect(result.current).toBe(80);
  rerender({ score: 40 });
  expect(result.current).toBe(40);
});
