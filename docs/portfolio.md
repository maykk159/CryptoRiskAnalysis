# Crypto Risk Analysis — English portfolio kit

**Project:** Crypto Risk Analysis (CIPHER)  
**Focus:** Full-stack engineering, numerical correctness, API reliability, and transparent analytical output  
**Stack:** C#, .NET 10, ASP.NET Core, React 19, TypeScript, TanStack Query, xUnit, and Vitest  
**Repository:** [maykk159/CryptoRiskAnalysis](https://github.com/maykk159/CryptoRiskAnalysis)

## Positioning

This project is strongest as a software-engineering portfolio piece for .NET backend and full-stack roles. Its distinguishing contribution is the handling of mathematical edge cases, data quality, API semantics, and user-facing limitations across the application. The code and tests support that story. The repository does not establish predictive accuracy or a calibrated investment model.

### Ready-to-use project summary

> I built Crypto Risk Analysis, a full-stack dashboard that transforms daily cryptocurrency market data into historical risk metrics and an explainable composite score. The backend combines Binance and CoinGecko integrations with a deterministic C# calculation engine, while the React and TypeScript client presents the results, limitations, and recovery states. In methodology 2.0.0, I made score contributions traceable, corrected historical VaR loss units, represented undefined Sharpe ratios explicitly, and tested those behaviors across the engine, API, and UI.

### Thirty-second introduction

> Crypto Risk Analysis is my .NET and React portfolio project. The most interesting part is making analytical output trustworthy: validating daily observations, exposing how the score is calculated, and handling metrics that cannot be meaningfully computed. For example, a zero-variability return series produces a null Sharpe ratio, which the UI displays as unavailable. I also test known-loss examples and API failure behavior, so the engineering decisions are backed by reproducible checks.

## What methodology 2.0.0 actually changed

The reference is [commit 210d2f2, dated September 10, 2026](https://github.com/maykk159/CryptoRiskAnalysis/commit/210d2f290da37635cfd057c985038b2ac6dbe56a). Here, **2.0.0 refers to the risk methodology**, rather than a verified repository release tag or the frontend package version.

The advanced metrics and hybrid provider integration already existed before this commit. Describe this change as a methodology revision that improves correctness, explanation, validation, and API/UI behavior. SQLite persistence, separate current quotes, and shared CoinGecko quota handling were added after the reference commit and belong to the broader current project.

### Strongest technical contributions, ranked

| Rank | Contribution | Concrete behavior | Engineering significance | Evidence |
| --- | --- | --- | --- | --- |
| 1 | Explicit numerical edge cases | Daily standard deviation at or below `1e-12` produces `sharpeRatio: null`; the UI displays “Unavailable.” A known 100 → 50 drop produces 50% historical VaR. | Preserves mathematical meaning through C#, JSON, TypeScript, and presentation. | [Engine](../CryptoRiskAnalysis.API/Services/RiskAnalysisEngine.cs), [methodology tests](../CryptoRiskAnalysis.Tests/Services/RiskMethodologyTests.cs), [UI tests](../client/src/components/dashboard/AdvancedMetrics.test.tsx) |
| 2 | Versioned, explainable scoring | Fixed volatility/trend/volume weights of 40/30/30, unrounded weighted contributions, dates, counts, risk level, and warnings are returned by the API. | Makes a result inspectable and reproducible; records methodology compatibility explicitly. | [Metadata contract](../CryptoRiskAnalysis.API/Models/RiskMethodologyDetails.cs), [formulas and compatibility](risk-methodology.md) |
| 3 | Strict input validation and error semantics | Consecutive UTC dates and positive prices are required. A missing volume baseline is rejected; observed zero volume remains a valid input. Unsuitable upstream data maps to HTTP 502. | Prevents incomplete data from silently generating plausible-looking analysis. | [Controller](../CryptoRiskAnalysis.API/Controllers/RiskAnalysisController.cs), [API pipeline tests](../CryptoRiskAnalysis.Tests/Integration/ApiPipelineTests.cs) |
| 4 | Tests of mathematical properties and boundaries | Known losses, sample volatility, downside deviation, flat and constant-growth series, volume boundaries, scale invariance, extreme decimals, and output isolation are checked. | Tests meaning and invariants beyond a single happy-path output. | [Methodology tests](../CryptoRiskAnalysis.Tests/Services/RiskMethodologyTests.cs), [risk engine tests](../CryptoRiskAnalysis.Tests/Services/RiskAnalysisEngineTests.cs) |
| 5 | Limitations carried into the user experience | Short history, worst-observation VaR, and undefined Sharpe generate warnings; the dashboard renders the warnings and null metric state. | Communicates when the output needs cautious interpretation through the same contract used for calculations. | [Dashboard tests](../client/src/components/Dashboard.test.tsx), [TypeScript contract](../client/src/types/index.ts), [methodology](risk-methodology.md) |

The file links above refer to the working repository. The [reference commit tree](https://github.com/maykk159/CryptoRiskAnalysis/tree/210d2f290da37635cfd057c985038b2ac6dbe56a) preserves the exact September 10 implementation.

### Supporting strengths of the broader project

- Hybrid provider routing, cancellation propagation, caching, and concurrent cache-miss deduplication demonstrate external API integration. Retry, timeout, circuit-breaking, typed errors, and an API rate limiter provide reliability mechanisms. See [provider routing](../CryptoRiskAnalysis.API/Services/HybridCryptoDataService.cs) and [service configuration](../CryptoRiskAnalysis.API/Extensions/ServiceCollectionExtensions.cs).
- Subsequent development separates current quotes from completed daily risk history and includes source-separated SQLite persistence and historical gap backfill. See [current quotes](../CryptoRiskAnalysis.API/Services/CurrentQuoteService.cs), [historical service](../CryptoRiskAnalysis.API/Services/HistoricalMarketDataService.cs), and [market-data documentation](market-data.md).
- Responsive SVG charts, keyboard/touch interaction, loading states, and retained results after failed refreshes demonstrate UI behavior beyond the successful request. See the [archived visual QA report](../.qa/cipher/README.md) and [current dashboard tests](../client/src/components/Dashboard.test.tsx).
- [GitHub Actions](../.github/workflows/ci.yml) defines formatting, backend warning-as-error build/test, frontend lint/test/build, and Windows setup checks. Workflow configuration alone does not establish the latest remote CI result.

## Existing visual evidence

The screenshot gallery and short implementation report are in [`.qa/cipher/README.md`](../.qa/cipher/README.md). These are **September 7, 2026 captures**, predating methodology 2.0.0. Use them to show the interface and earlier recovery behavior; they do not verify the current methodology, nullable Sharpe display, or current quote provenance.

| Visual | Ready-to-use caption | Provenance |
| --- | --- | --- |
| [Desktop dashboard](../.qa/cipher/bitcoin-1440.png) | “Desktop analysis view with historical prices, a component risk breakdown, and five advanced metrics.” | Earlier local-API capture; figures reflect the earlier methodology. |
| [Mobile dashboard](../.qa/cipher/bitcoin-390.png) | “Responsive mobile layout with stacked analysis cards and an expandable risk breakdown.” | Earlier local-API capture. |
| [Refresh recovery](../.qa/cipher/refresh-error-1366.png) | “A failed refresh preserves the last successful analysis and offers an explicit retry action.” | Browser-intercepted HTTP 429 scenario, as documented in the QA report. |
| [Keyboard chart inspection](../.qa/cipher/keyboard-first-1440.png) | “Historical chart inspection through keyboard focus.” | Earlier interaction QA capture. |

The gallery is now linked from the main README. Fresh captures of methodology warnings and unavailable Sharpe are the next visual improvement.

## Prioritized improvements

This is a recommended portfolio backlog, not a list of implemented features. Priority reflects value for a general backend/full-stack application. Quantitative research roles would give empirical validation a higher priority.

| Priority | Gap | Why it matters | Concrete completion criterion |
| --- | --- | --- | --- |
| 1 — Before sharing | Current-version demo evidence | Existing screenshots are useful but predate 2.0.0; no hosted demo or video link is advertised in the README. | Add a short current-version recording or hosted demo; capture 7-day warnings and null Sharpe with clearly labeled fixtures, alongside a normal desktop/mobile view. |
| 2 — Before sharing | Older explanatory reports disagree with the code | Root-level reports still mention Recharts, 180-second CoinGecko caching, or adaptive score weights. These conflict with the current implementation and can confuse reviewers. | Update or clearly archive [projectfirstreport.md](../projectfirstreport.md), [proje_mimari_ve_isleyis.md](../proje_mimari_ve_isleyis.md), and [ozellikler_nedenler_ve_calisma_mantigi.md](../ozellikler_nedenler_ve_calisma_mantigi.md); keep the historical QA report explicitly dated. |
| 3 — High | Full methodology explanation in the UI | The API returns contributions and input provenance, but the dashboard shows component scores and warnings without the complete metadata. | Add an accessible explanation panel showing version, input dates/counts, weights, and contributions whose rounded sum matches the composite. |
| 4 — High | Portable browser regression coverage | Browser QA scripts already exist, but use a machine-specific runtime path and are not a frontend CI job. Component tests do not exercise a browser-to-API journey. | Make fixture-based browser checks reproducible in a clean environment and run them in CI: normal analysis, 7-day warnings, null Sharpe, failure/retry, and rapid period changes. |
| 5 — Medium | API compatibility checks | Allowing null Sharpe changes the client contract. TypeScript annotations alone do not validate incoming JSON. | Add contract/schema checks for nullable metrics and methodology fields; document client migration expectations and test malformed successful responses. |
| 6 — Medium | Measured reliability and performance | Caching and resilience are implemented, but this review establishes no throughput, latency, availability, or cache-hit figures. | Publish a reproducible load scenario with environment, concurrency, cold/warm behavior, p50/p95 latency, errors, and provider-call counts; expose corresponding telemetry. |
| 7 — Role-dependent | Empirical model validation | Score thresholds and weights are design choices; implementation tests establish correctness rather than predictive usefulness. | If pursuing quantitative claims, define an evaluation target, use longer historical windows and held-out periods, compare baselines, and report limitations and VaR exceptions. |

## Ready-to-use README copy

The main [README](../README.md) already contains the portfolio introduction, engineering highlights, and linked visual evidence. This shorter description can also be used on a personal portfolio page:

> **Crypto Risk Analysis — Explainable Market-Risk Dashboard**  
> A full-stack application built with ASP.NET Core, React, and TypeScript that analyzes completed daily cryptocurrency observations from Binance and CoinGecko. Methodology 2.0.0 exposes the inputs and weighted contributions behind its composite score, handles undefined Sharpe ratios explicitly, and reports historical VaR as percentage loss. Automated engine, API, and UI tests verify numerical edge cases, data validation, serialization, and recovery behavior.

## Three-minute demo script

### Preparation

Start the API and frontend using the [README setup instructions](../README.md). Open `http://localhost:5173/?asset=bitcoin&days=30` and Swagger at `http://localhost:5058/swagger`. Keep the methodology tests and a successful test run available. Use current API output for the main walkthrough; do not promise a fixed live price or score.

Use the archived recovery screenshot for the failure example if needed and identify it as a simulated, earlier QA capture. Null Sharpe is demonstrated by the deterministic test case unless a current fixture-based UI demo has been prepared. The old captures do not include that case.

### 0:00–0:25 — Problem and product

**Show:** Bitcoin, 30-day view, chart, and risk overview.

> “This is Crypto Risk Analysis, a full-stack dashboard built with .NET, React, and TypeScript. It summarizes historical market behavior using completed daily observations. My focus was to make the calculation, data assumptions, and failure states understandable. The composite is a project-specific historical indicator, and its individual metrics provide additional context.”

### 0:25–0:55 — Explain the score

**Show:** Component breakdown, then the current API response's `methodology` object in Swagger.

> “Methodology 2.0.0 uses fixed weights: 40 percent volatility, 30 percent trend, and 30 percent volume. The API returns the weighted contributions, methodology version, input dates, and observation counts. Rounding the sum of contributions reproduces the composite. Drawdown and downside risk are reported separately; they are not extra weighted inputs.”

### 0:55–1:25 — Show limitations

**Show:** Switch to 7 days, inspect the warnings, and open a metric explanation.

> “A seven-price window contains six return intervals. The engine warns that short-history annualized estimates are unstable and that this VaR estimate uses the worst observed return. The same warnings travel from the engine to the API and dashboard. This keeps sample limitations visible alongside the numbers.”

### 1:25–2:00 — Demonstrate correctness

**Show:** `FlatSeries_HasZeroPriceRiskAndUndefinedSharpe` and `KnownLoss_UsesPercentageLossRatherThanLogLoss` in the methodology tests; point to the API/UI null tests.

> “Two edge cases explain the methodology revision. With effectively zero return variability, Sharpe is undefined, so the engine returns null and the UI displays ‘Unavailable.’ For a price drop from 100 to 50, the actual loss is 50 percent. The historical VaR implementation converts the log return back into percentage loss, and a known-loss test verifies that result.”

### 2:00–2:35 — Demonstrate recovery

**Show:** Current recovery behavior if prepared; otherwise the labeled archived refresh-error screenshot. Open the invalid-input API pipeline test.

> “Provider failures have explicit HTTP semantics. Daily-series gaps and missing volume baselines produce a provider error rather than an analysis based on fabricated data. The client preserves the last successful result when a refresh fails, tells the user what happened, and provides a retry action. This archived screenshot demonstrates that recovery pattern with a simulated rate-limit response.”

### 2:35–3:00 — Close with engineering evidence

**Show:** Successful test output and the CI workflow.

> “The test suites cover mathematical examples, scale invariance, numerical boundaries, API serialization, and dashboard behavior. My next steps are a current-version demo, an in-app methodology panel, and portable browser regression checks. The project demonstrates how I connect backend correctness, API contracts, and frontend behavior in one application.”

## Ready-to-use CV bullets

**Crypto Risk Analysis (CIPHER) | Personal Project**  
**C# · ASP.NET Core · React · TypeScript · xUnit · Vitest**  
[github.com/maykk159/CryptoRiskAnalysis](https://github.com/maykk159/CryptoRiskAnalysis)

Choose one set to suit the vacancy.

### Full-stack roles

- Built a cryptocurrency risk dashboard with ASP.NET Core, React, and TypeScript, integrating Binance and CoinGecko daily market data across 7-, 30-, and 90-day analysis windows.
- Revised risk methodology 2.0.0 to expose weighted score contributions and data limitations, correct historical VaR loss units, and support nullable Sharpe ratios across the engine, API, and UI.
- Added xUnit and Vitest regression coverage for numerical edge cases, invalid market data, JSON serialization, and user-facing methodology warnings.

### .NET backend roles

- Implemented a deterministic C# risk engine with daily-series validation, fixed-weight composite scoring, and versioned metadata for reproducible analysis.
- Corrected historical VaR percentage-loss calculations and modeled undefined Sharpe ratios as nullable API values, with tests for known losses, scale invariance, and numerical extremes.
- Integrated hybrid market-data providers with caching, concurrent request deduplication, cancellation, retry/timeout/circuit-breaking policies, and typed HTTP error handling.

### React / TypeScript roles

- Built a responsive React and TypeScript risk dashboard with interactive SVG charts, keyboard/touch controls, asset selection, and 7-, 30-, and 90-day analysis views.
- Extended the UI and TypeScript contract to display undefined Sharpe ratios as unavailable and render methodology warnings from the API.
- Implemented loading and retry states that preserve the last successful analysis after failed refreshes, with Vitest coverage for recovery and abandoned requests.

### Optional current-project bullet

Use this for the later implementation, separately from the September 10 methodology contribution:

- Added source-separated SQLite daily market-data persistence and historical gap backfill, alongside independently cached current quotes and shared CoinGecko request-budget handling.

## Interview talking points

**Why fixed weights?** They make contributions easy to inspect and avoid the prior adaptive weight switching. Holding other component scores constant, increasing one component cannot reduce the composite. The specific weights remain uncalibrated design choices.

**Why null Sharpe?** Zero return variability cannot support a finite mean-to-variability ratio. Null carries that fact through the API contract instead of substituting a numerical score. The implementation uses log returns, a zero risk-free assumption, and `sqrt(365)` annualization.

**Why reject missing days?** Interpolating prices would invent observations and alter measured return variability. Provider completeness is part of the input contract. The engine does not consult the clock, so replay of the same inputs remains deterministic.

**How are results reproduced?** Use the same validated prices, aligned volume inputs, lookback, and methodology version. Sum the unrounded API contributions and round once; recomputing from rounded component scores may differ by 0.01.

**What remains unproven?** Empirical calibration, predictive accuracy, and production scale. Historical VaR is a sample quantile rather than a maximum future loss. Provider venue and USD/USDT differences also constrain cross-asset comparisons.

## Verification record

Reviewed on **October 11, 2026** against local commit [`4f68c84`](https://github.com/maykk159/CryptoRiskAnalysis/tree/4f68c84e752d1764090b2b6d932bb3856ce438d5), with the methodology contribution checked against `210d2f2` and its parent diff. The risk engine and methodology tests are unchanged between the reference commit and this local HEAD.

| Check | Observed result |
| --- | --- |
| Backend test assembly, xUnit in-process runner | 167 passed; no failed or skipped tests |
| `npm.cmd test` in `client/` | 110 passed across 13 files |
| `npm.cmd run build` in `client/` | TypeScript and Vite production build passed |

The initial `dotnet test --project ... --configuration Release --no-restore` invocation failed before executing tests because the sandbox denied the Microsoft.Testing.Platform named-pipe connection. After that command built the test assembly, it was executed successfully with `dotnet CryptoRiskAnalysis.Tests/bin/Release/net10.0/CryptoRiskAnalysis.Tests.dll`. The 167/110 counts describe the current local HEAD, not the September 10 snapshot. This review did not rerun the old browser QA or verify a hosted deployment or remote CI status.

Keep CV claims tied to the implementation and observed checks. This evidence supports correctness and engineering quality; it does not establish commercial adoption, revenue impact, prediction accuracy, latency improvements, or production availability.
