import { useEffect, useRef, useState } from 'react';
import { useMediaQuery } from './useMediaQuery';

/** Animate from the displayed value, including when a refresh interrupts a transition. */
export function useAnimatedNumber(target: number, duration = 650, updateDuration = 350) {
  const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)');
  const [value, setValue] = useState(reducedMotion ? target : 0);
  const displayed = useRef(value);
  const animation = useRef({ target, started: false });

  useEffect(() => {
    const activeDuration =
      !animation.current.started && Object.is(animation.current.target, target)
        ? duration
        : updateDuration;
    animation.current.target = target;
    if (reducedMotion) {
      displayed.current = target;
      animation.current.started = true;
      return;
    }
    const from = Number.isFinite(displayed.current) ? displayed.current : 0;
    const start = performance.now();
    let frame: number;
    const tick = (now: number) => {
      animation.current.started = true;
      const progress = reducedMotion ? 1 : Math.min(Math.max((now - start) / activeDuration, 0), 1);
      const next = progress === 1 ? target : from + (target - from) * (1 - (1 - progress) ** 3);
      displayed.current = next;
      setValue(next);
      if (progress < 1) frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [target, duration, updateDuration, reducedMotion]);

  return reducedMotion || !Number.isFinite(target) ? target : value;
}
