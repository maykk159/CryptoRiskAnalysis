import { useState, useEffect, useRef } from 'react';

/**
 * Animates a numeric value from its current value to `target` using cubic ease-out.
 * Extracted from RiskScoreCard to be reusable and comply with React Rules of Hooks
 * (hooks must be called at the top level of a component, not inside JSX expressions).
 *
 * @param target  - The final value to animate to
 * @param duration - Animation duration in milliseconds (default: 800ms)
 * @param delay   - Optional delay before animation starts in milliseconds (default: 0)
 */
export function useAnimatedNumber(
  target: number,
  duration: number = 800,
  delay: number = 0
): number {
  const [value, setValue] = useState(0);
  const valueRef = useRef(0);

  useEffect(() => {
    let startTime: number | null = null;
    let animationFrameId: number | undefined;
    const startValue = valueRef.current;
    const change = target - startValue;
    const shouldSkipAnimation =
      window.matchMedia('(prefers-reduced-motion: reduce)').matches || duration <= 0;

    if (change === 0) return;

    const easeOut = (t: number) => 1 - Math.pow(1 - t, 3); // Cubic ease-out

    const animate = (currentTime: number) => {
      if (!startTime) startTime = currentTime;
      const elapsed = currentTime - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const currentVal = startValue + change * easeOut(progress);

      valueRef.current = currentVal;
      setValue(currentVal);

      if (progress < 1) {
        animationFrameId = requestAnimationFrame(animate);
      }
    };

    const timeoutId = window.setTimeout(
      () => {
        if (shouldSkipAnimation) {
          valueRef.current = target;
          setValue(target);
          return;
        }

        animationFrameId = requestAnimationFrame(animate);
      },
      shouldSkipAnimation ? 0 : delay
    );

    return () => {
      clearTimeout(timeoutId);
      if (animationFrameId !== undefined) cancelAnimationFrame(animationFrameId);
    };
  }, [target, duration, delay]);

  return value;
}
