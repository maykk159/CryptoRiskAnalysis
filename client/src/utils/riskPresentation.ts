export function riskLevel(score: number | undefined) {
  if (typeof score !== 'number' || !Number.isFinite(score) || score < 0 || score > 100)
    return { label: 'Unavailable', color: 'text-muted', fill: 'bg-control', valid: false };
  if (score < 30)
    return { label: 'Low risk', color: 'text-positive', fill: 'bg-positive', valid: true };
  if (score < 70)
    return { label: 'Medium risk', color: 'text-warning', fill: 'bg-warning', valid: true };
  return { label: 'High risk', color: 'text-negative', fill: 'bg-negative', valid: true };
}

export function formatMetric(
  value: number | null | undefined,
  kind: 'percent' | 'loss' | 'ratio' = 'percent'
) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return 'Unavailable';
  const normalized = Math.abs(value) < 0.005 ? 0 : kind === 'loss' ? -Math.abs(value) : value;
  return (
    normalized.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) +
    (kind === 'ratio' ? '' : '%')
  );
}
