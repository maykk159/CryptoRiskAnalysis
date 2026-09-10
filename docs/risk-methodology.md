# Risk engine methodology — 2.0.0

The engine describes historical market behavior with a deterministic, project-specific
0–100 score. It does not estimate a probability of loss, default, fraud, or future
performance. Thresholds and weights are design choices, not empirically calibrated
investment signals. Compare scores only for the same methodology and lookback window.

## Input contract

- At least seven strictly positive prices on consecutive UTC dates, in ascending order.
- Timestamps are Unix milliseconds. Duplicate dates, gaps, invalid timestamps, null
  observations, and nonpositive prices are rejected. No interpolation or silent sorting.
- Providers supply completed daily observations. The engine validates dates but does
  not consult the clock, so historical replay remains deterministic. Provider selection
  remains responsible for freshness, candle completion, and aligned volume periods.
- Current volume must be nonnegative; average volume must be positive. Both must use
  the same currency, venue definition, and daily measurement period. An observed zero
  current volume is valid; an unavailable/zero average is rejected, not scored as neutral.
- Invalid provider input is returned by the API as a provider failure (HTTP 502).
- N prices yield N−1 daily returns: the 7-day selection has six return intervals.

## Metrics

For price P, daily log return is `r = ln(P[t] / P[t-1])`.

| Metric | Definition and units |
|---|---|
| Annualized volatility | Sample standard deviation of r (denominator N−2 for N prices), multiplied by √365 and 100; percentage |
| Downside risk | `sqrt(sum(min(r, 0)^2) / (N−1)) × sqrt(365) × 100`; all return periods remain in the denominator |
| Maximum drawdown | Largest `(running peak − price) / running peak × 100` in the selected window |
| Sharpe ratio | Log-return variant: `mean(r) / sampleStdDev(r) × sqrt(365)`, zero risk-free return assumption; dimensionless |
| Historical VaR 95% | Sort log returns ascending, select one-based rank `ceil(0.05 × (N−1))`, then `max(0, 1 − exp(selected return)) × 100` |

Sharpe is `null` when daily standard deviation is at most 1e-12. A flat or constant
growth series cannot support a finite return-to-variability ratio. The UI shows
“Unavailable”. This is a log-return Sharpe variant, not the arithmetic-return definition.

VaR is a historical quantile, not a maximum future loss. A price drop from 100 to 50
corresponds to a 50% loss, not the 69.31% magnitude of its log return. With 20 or fewer
returns, nearest rank selects the worst observation; the API includes a warning.
All available 7/30/90-day windows are small samples for estimating tail behavior.
The quantile interpretation follows the [MathWorks historical VaR example](https://www.mathworks.com/help/risk/value-at-risk-estimation-and-backtesting.html);
the nearest-rank convention and short-window policy here are explicit application choices.

## Score components

All mappings below interpolate linearly between the listed points and saturate at 100.

**Volatility:** annualized volatility (fraction) → score:
`0 → 0`, `0.5 → 50`, `1 → 75`, `2 → 100`.

**Trend:** absolute momentum `abs(recentMean / fullWindowMean − 1)` → score:
`0 → 0`, `0.05 → 20`, `0.15 → 50`, `0.30 → 80`, `0.40 → 100`.
Recent mean uses the last seven prices for windows of at least 30 observations;
otherwise the last `max(3, floor(N/3))`. Both rising and falling deviations count.

**Volume:** ratio `q = currentVolume / averageVolume` → baseline score:
`0 → 100`, `0.3 → 65`, `0.5 → 40`, `1 → 30`, `3 → 70`, `6 → 100`.
This measures a relative volume anomaly; it does not measure order-book liquidity,
bid/ask spreads, or absolute trading capacity.

With at least eight prices, weekly simple return is `w = last / price[-8] − 1`.
Add the following continuous context bonuses, then cap the volume score at 100:

```text
sellingPressure = clamp((-w − 0.05) × 300, 0, 60)
                  × clamp((q − 1.5) / 1.5, 0, 1)
weakRally       = clamp((w − 0.05) × 600, 0, 30)
                  × clamp((0.5 − q) / 0.1, 0, 1)
```

Seven prices do not provide a full weekly return, so no weekly bonus is applied.

## Composite, explanation, and compatibility

```text
score = 0.40 × volatilityScore + 0.30 × trendScore + 0.30 × volumeScore
```

No adaptive weight switching or extra amplification is applied. Increasing one component
while holding the others constant cannot decrease the composite. Metrics and final
scores use two decimals with midpoint-to-even rounding. Classification uses the rounded
composite: Low below 30, Medium from 30 to below 70, High from 70.

The API's `methodology` object includes version, dates, observation and return counts,
weights, unrounded weighted contributions, risk level, and human-readable warnings.
Round the sum of contributions to reproduce the composite; recomputing from already
rounded component scores can differ by 0.01. Fewer than 30 prices triggers a short-history
warning. Warnings describe limitations and are not statistical confidence estimates.

Version 2 changes composite/volume scoring and VaR loss units relative to the original
engine. Prior scores must not be mixed with this version without recalculation. Existing
API fields remain, except `sharpeRatio` now permits null; `methodology` is additive.
Drawdown and downside deviation remain reported metrics, not additional weighted inputs.

## Verification and limits

Tests cover known percentage losses, sample volatility, downside deviation, drawdown,
flat and constant-growth prices, nearest-rank behavior, volume boundaries, invalid daily
series, scale invariance, extreme decimal inputs, output isolation, and score contributions.
These establish implementation correctness, not predictive validity. No backtest or
empirical calibration is claimed. Asset/venue coverage and provider currency differences
(for example USD versus USDT) also limit cross-asset comparisons.
