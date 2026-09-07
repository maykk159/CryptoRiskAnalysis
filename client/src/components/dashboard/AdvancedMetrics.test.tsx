// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import { AdvancedMetrics } from './AdvancedMetrics';

afterEach(cleanup);

const metrics = {
  downsideRisk: 12.34,
  maxDrawdown: 15,
  sharpeRatio: 1.2,
  valueAtRisk95: 4,
  annualizedVolatility: 55,
};

describe('AdvancedMetrics help', () => {
  it('closes an open explanation when the user clicks outside it', async () => {
    const user = userEvent.setup();
    const { container } = render(<AdvancedMetrics data={metrics} />);

    const helpButton = container.querySelector<HTMLElement>(
      'summary[aria-label="About Downside Risk"]'
    );
    const help = helpButton?.closest('details');

    await user.click(helpButton!);
    expect(help?.hasAttribute('open')).toBe(true);

    await user.click(screen.getByRole('heading', { name: 'Advanced Metrics' }));
    expect(help?.hasAttribute('open')).toBe(false);
  });

  it('keeps an explanation open when the user clicks inside it', async () => {
    const user = userEvent.setup();
    const { container } = render(<AdvancedMetrics data={metrics} />);

    const helpButton = container.querySelector<HTMLElement>(
      'summary[aria-label="About Downside Risk"]'
    );
    const help = helpButton?.closest('details');

    await user.click(helpButton!);
    await user.click(screen.getByText(/relative to a 0% daily target/i));

    expect(help?.hasAttribute('open')).toBe(true);
  });
});
