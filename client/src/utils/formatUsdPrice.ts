/** Full prices retain six significant digits below $1; axes may abbreviate large values. */
export const formatUsdPrice = (
  value: number | null | undefined,
  mode: 'full' | 'axis' = 'full'
) => {
  if (typeof value !== 'number' || !Number.isFinite(value) || value < 0) return 'Unavailable';
  if (mode === 'axis' && value >= 10_000) {
    return '$' + value.toLocaleString('en-US', { notation: 'compact', maximumFractionDigits: 1 });
  }
  if (value > 0 && value < 1e-8) {
    return (
      '$' + value.toLocaleString('en-US', { notation: 'scientific', maximumSignificantDigits: 4 })
    );
  }
  if (value > 0 && value < 1) {
    const formatted = value.toLocaleString('en-US', {
      maximumSignificantDigits: mode === 'axis' ? 3 : 6,
    });
    const parts = formatted.split('.');
    if (parts.length === 1) {
      return '$' + formatted + '.00';
    }
    if (parts[1].length < 2) {
      return '$' + formatted + '0'.repeat(2 - parts[1].length);
    }
    return '$' + formatted;
  }
  return (
    '$' + value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
  );
};
