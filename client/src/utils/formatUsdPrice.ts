export const formatUsdPrice = (value: number) => {
  if (!Number.isFinite(value)) {
    return '$—';
  }

  const absoluteValue = Math.abs(value);

  if (absoluteValue !== 0 && absoluteValue < 1e-20) {
    return `$${value.toLocaleString('en-US', {
      notation: 'scientific',
      maximumSignificantDigits: 6,
    })}`;
  }

  const maximumFractionDigits =
    absoluteValue > 0 && absoluteValue < 1
      ? Math.min(20, Math.max(3, 5 - Math.floor(Math.log10(absoluteValue))))
      : 3;

  return `$${value.toLocaleString('en-US', { maximumFractionDigits })}`;
};
