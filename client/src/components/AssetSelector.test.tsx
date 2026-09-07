// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AssetSelector } from './AssetSelector';

afterEach(cleanup);

describe('AssetSelector editable combobox', () => {
  it('labels the input and exposes the selected asset', () => {
    render(<AssetSelector selectedAsset="bitcoin" onSelectAsset={vi.fn()} />);
    const input = screen.getByRole<HTMLInputElement>('combobox', { name: 'Select Crypto Asset' });
    expect(input.value).toBe('Bitcoin (BTC)');
    expect(input.getAttribute('aria-expanded')).toBe('false');
  });
  it('keeps focus in the input while arrows move the active option and Enter selects', async () => {
    const user = userEvent.setup();
    const select = vi.fn();
    render(<AssetSelector selectedAsset="bitcoin" onSelectAsset={select} />);
    const input = screen.getByRole('combobox');
    input.focus();
    await user.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(input);
    expect(input.getAttribute('aria-activedescendant')).toBe(screen.getAllByRole('option')[0].id);
    await user.keyboard('{ArrowDown}');
    expect(input.getAttribute('aria-activedescendant')).toBe(screen.getAllByRole('option')[1].id);
    await user.keyboard('{Enter}');
    expect(select).toHaveBeenCalledWith('ethereum');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(input);
  });
  it('filters names, tickers and IDs with case and whitespace tolerance; clears no results', async () => {
    const user = userEvent.setup();
    const select = vi.fn();
    render(<AssetSelector selectedAsset="bitcoin" onSelectAsset={select} />);
    const input = screen.getByRole('combobox');
    await user.click(input);
    await user.type(input, '  sHiB  ');
    expect(screen.getAllByRole('option')).toHaveLength(1);
    expect(screen.getByRole('option').textContent).toContain('Shiba Inu');
    expect(select).not.toHaveBeenCalled();
    await user.clear(input);
    await user.type(input, 'the-open-network');
    expect(screen.getByRole('option').textContent).toContain('Toncoin');
    await user.clear(input);
    await user.type(input, 'nothing-matches');
    expect(screen.getByRole('status').textContent).toContain('No assets found');
    expect(input.getAttribute('aria-activedescendant')).toBeNull();
    await user.click(screen.getByRole('button', { name: 'Clear asset search' }));
    expect(screen.getAllByRole('option').length).toBeGreaterThan(1);
    expect(document.activeElement).toBe(input);
  });
  it('preserves space and text editing keys, closes on Escape and leaves normally with Tab', async () => {
    const user = userEvent.setup();
    render(
      <>
        <AssetSelector selectedAsset="bitcoin" onSelectAsset={vi.fn()} />
        <button>Next</button>
      </>
    );
    const input = screen.getByRole<HTMLInputElement>('combobox');
    await user.click(input);
    await user.type(input, 'Bitcoin Cash');
    await user.keyboard('{Home}{ArrowRight}{End}');
    expect(input.value).toBe('Bitcoin Cash');
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(input);
    await user.keyboard('{ArrowDown}{Tab}');
    expect(screen.queryByRole('listbox')).toBeNull();
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Next' }));
  });
});
