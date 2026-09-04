// @vitest-environment jsdom

import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AssetSelector } from './AssetSelector';

afterEach(cleanup);

describe('AssetSelector keyboard interaction', () => {
  it('includes the selected asset in the trigger accessible name', () => {
    render(<AssetSelector selectedAsset="bitcoin" onSelectAsset={vi.fn()} />);

    const trigger = screen.getByRole('button', {
      name: 'Select Crypto Asset Bitcoin (BTC)',
    });
    expect(trigger.getAttribute('aria-expanded')).toBe('false');
  });

  it('moves one roving tab stop with arrow keys and restores trigger focus after selection', async () => {
    const user = userEvent.setup();
    const onSelectAsset = vi.fn();
    render(<AssetSelector selectedAsset="bitcoin" onSelectAsset={onSelectAsset} />);
    const trigger = screen.getByRole('button', {
      name: 'Select Crypto Asset Bitcoin (BTC)',
    });

    trigger.focus();
    await user.keyboard('{ArrowDown}');

    const options = screen.getAllByRole('option');
    expect(document.activeElement).toBe(options[0]);
    expect(options.filter(option => option.tabIndex === 0)).toHaveLength(1);

    await user.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(options[1]);
    expect(options[0].tabIndex).toBe(-1);
    expect(options[1].tabIndex).toBe(0);

    await user.keyboard('{Enter}');
    expect(onSelectAsset).toHaveBeenCalledWith('ethereum');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });

  it('closes with Escape and returns focus to the trigger', async () => {
    const user = userEvent.setup();
    render(<AssetSelector selectedAsset="bitcoin" onSelectAsset={vi.fn()} />);
    const trigger = screen.getByRole('button', {
      name: 'Select Crypto Asset Bitcoin (BTC)',
    });

    trigger.focus();
    await user.keyboard('{ArrowDown}{ArrowDown}{Escape}');

    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(trigger);
  });
});
