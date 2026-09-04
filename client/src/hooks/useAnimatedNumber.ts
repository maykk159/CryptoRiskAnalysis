import { useState, useEffect } from 'react';

/**
 * Animates a numeric value from 0 to `target` using cubic ease-out.
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

  useEffect(() => {
    let startTime: number | null = null;
    let animationFrameId: number;

    const easeOut = (t: number) => 1 - Math.pow(1 - t, 3); // Cubic ease-out

    const animate = (currentTime: number) => {
      if (!startTime) startTime = currentTime;
      const elapsed = currentTime - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const currentVal = target * easeOut(progress);

      setValue(currentVal);

      if (progress < 1) {
        animationFrameId = requestAnimationFrame(animate);
      } else {
        setValue(target);
      }
    };

    const timeoutId = window.setTimeout(() => {
      setValue(0);
      animationFrameId = requestAnimationFrame(animate);
    }, delay + 50);

    return () => {
      clearTimeout(timeoutId);
      if (animationFrameId) cancelAnimationFrame(animationFrameId);
    };
  }, [target, duration, delay]);

  return value;
}
