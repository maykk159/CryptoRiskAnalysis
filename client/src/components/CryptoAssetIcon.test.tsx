// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, expect, it } from 'vitest';
import { CryptoAssetIcon } from './CryptoAssetIcon';

afterEach(cleanup);

it.each(['', '   '])('renders only the fallback for empty icon %j', icon => {
  const { container } = render(
    <CryptoAssetIcon asset={{ icon, name: 'Unknown', ticker: 'UNK' }} />
  );
  expect(screen.getByLabelText('Unknown icon fallback')).toBeTruthy();
  expect(container.querySelector('img')).toBeNull();
});

it('loads an icon when an empty URL is replaced with a valid URL', () => {
  const { container, rerender } = render(
    <CryptoAssetIcon asset={{ icon: '', name: 'Bitcoin', ticker: 'BTC' }} />
  );
  rerender(<CryptoAssetIcon asset={{ icon: '/bitcoin.png', name: 'Bitcoin', ticker: 'BTC' }} />);
  fireEvent.load(container.querySelector('img')!);
  expect(screen.getByRole('img', { name: 'Bitcoin icon' })).toBeTruthy();
  expect(screen.queryByLabelText('Bitcoin icon fallback')).toBeNull();
});
