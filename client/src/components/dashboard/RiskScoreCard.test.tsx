// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { RiskScoreCard } from './RiskScoreCard';

const data = { compositeRiskScore: 80, volatilityScore: 40, trendScore: 60, volumeScore: 80 };
let now: number;
let frameId: number;
let frames: Map<number, FrameRequestCallback>;

function media(wide: boolean, reducedMotion: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      matches: query.includes('prefers-reduced-motion') ? reducedMotion : wide,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }))
  );
}

beforeEach(() => {
  now = 0;
  frameId = 0;
  frames = new Map();
  media(true, false);
  vi.spyOn(performance, 'now').mockImplementation(() => now);
  vi.stubGlobal(
    'requestAnimationFrame',
    vi.fn((callback: FrameRequestCallback) => {
      frames.set(++frameId, callback);
      return frameId;
    })
  );
  vi.stubGlobal(
    'cancelAnimationFrame',
    vi.fn((id: number) => frames.delete(id))
  );
});
afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

function advance(time: number) {
  now = time;
  act(() => {
    const pending = [...frames.values()];
    frames.clear();
    pending.forEach(callback => callback(now));
  });
}

function bar(label: string) {
  return screen
    .getByText(label)
    .parentElement!.parentElement!.querySelector('div[aria-hidden] > div') as HTMLDivElement;
}

describe('RiskScoreCard', () => {
  it.each([
    [0, 20, 120],
    [50, 120, 20],
    [100, 220, 120],
  ])('places the indicator at the expected position for score %i', (score, x, y) => {
    media(true, true);
    const { container } = render(<RiskScoreCard data={{ ...data, compositeRiskScore: score }} />);
    const pointer = container.querySelector('.risk-dial circle');
    expect(pointer).not.toBeNull();
    expect(Number(pointer!.getAttribute('cx'))).toBeCloseTo(x);
    expect(Number(pointer!.getAttribute('cy'))).toBeCloseTo(y);
    expect(container.querySelector('.risk-dial p')?.textContent).toBe(score.toFixed(1));
  });

  it.each([NaN, undefined, Infinity, -1, 101])(
    'hides invalid score %s instead of drawing an invalid indicator',
    score => {
      const { container } = render(
        <RiskScoreCard data={{ ...data, compositeRiskScore: score as number }} />
      );
      expect(container.querySelector('.risk-dial circle')).toBeNull();
      expect(container.querySelector('.risk-dial p')?.textContent).toBe('—');
      expect(screen.getByText('Unavailable')).toBeTruthy();
    }
  );

  it('opens and closes the mobile breakdown with matching accessibility state', () => {
    media(false, true);
    render(<RiskScoreCard data={data} />);
    const button = screen.getByRole('button', { name: 'Risk breakdown' });
    const details = document.getElementById(button.getAttribute('aria-controls')!)!;
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(details.hidden).toBe(true);
    fireEvent.click(button);
    expect(button.getAttribute('aria-expanded')).toBe('true');
    expect(details.hidden).toBe(false);
    fireEvent.click(button);
    expect(button.getAttribute('aria-expanded')).toBe('false');
    expect(details.hidden).toBe(true);
  });

  it('animates the numbers and bars together while keeping the colored dial fixed', () => {
    const { container, rerender } = render(<RiskScoreCard data={data} />);
    const dial = container.querySelector('.risk-dial')!;
    const arc = dial.querySelector('svg g')!.outerHTML;
    expect(dial.querySelector('p')?.textContent).toBe('0.0');
    for (const label of ['Volatility risk', 'Trend risk', 'Volume risk'])
      expect(bar(label).style.width).toBe('0%');
    advance(325);
    expect(dial.querySelector('p')?.textContent).toBe('70.0');
    for (const [label, width] of [
      ['Volatility risk', 35],
      ['Trend risk', 52.5],
      ['Volume risk', 70],
    ] as const) {
      expect(bar(label).style.width).toBe(`${width}%`);
      expect(bar(label).parentElement!.parentElement!.textContent).toContain(width.toFixed(1));
    }
    expect(dial.querySelector('svg g')!.outerHTML).toBe(arc);
    advance(650);
    expect(dial.querySelector('p')?.textContent).toBe('80.0');
    expect(bar('Volatility risk').style.width).toBe('40%');
    expect(bar('Trend risk').style.width).toBe('60%');
    expect(bar('Volume risk').style.width).toBe('80%');
    rerender(<RiskScoreCard data={{ ...data, compositeRiskScore: 40, volumeScore: 40 }} />);
    expect(dial.querySelector('p')?.textContent).toBe('80.0');
    advance(825);
    expect(dial.querySelector('p')?.textContent).toBe('45.0');
    expect(bar('Volume risk').style.width).toBe('45%');
    advance(1000);
    expect(dial.querySelector('p')?.textContent).toBe('40.0');
    expect(bar('Volume risk').style.width).toBe('40%');
  });

  it('leaves invalid component bars empty and shows unavailable values', () => {
    media(true, true);
    render(
      <RiskScoreCard data={{ ...data, volatilityScore: NaN, trendScore: -1, volumeScore: 101 }} />
    );
    for (const label of ['Volatility risk', 'Trend risk', 'Volume risk']) {
      expect(bar(label).style.width).toBe('0%');
      expect(bar(label).parentElement!.parentElement!.textContent).toContain('—');
    }
  });
});
