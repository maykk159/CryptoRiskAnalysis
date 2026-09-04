# Summary of the work carried out during the semester

## 1. System architecture and backend

The project is a full-stack application with a **.NET 10 ASP.NET Core Web API** and a React client. The backend uses a pragmatic layered organization inside one Web API assembly: controllers handle HTTP concerns, services contain provider routing and risk calculations, models/DTOs define contracts, and middleware standardizes failures. Interfaces and dependency injection make the main services independently testable. Because these responsibilities are not split into separate Domain, Application, and Infrastructure projects, the repository does not claim strict Clean Architecture.

## 2. Market-data integration

`HybridCryptoDataService` routes mapped assets to Binance first and falls back to CoinGecko for expected provider failures or unsupported mappings. Provider data is normalized into completed daily price and quote-currency volume observations. The requested window is consistently 7, 30, or 90 days; price metrics and the volume baseline use that same window.

Binance responses are cached for 60 seconds and CoinGecko responses for 180 seconds. Concurrent cache misses for the same key are deduplicated. Provider quotas are dynamic and provider-controlled, so this report does not state fixed request-per-minute guarantees.

## 3. Risk analysis

The risk engine calculates daily log returns and derives annualized volatility, downside risk, maximum drawdown, annualized Sharpe ratio, and historical one-day VaR at 95%. It also produces volatility, trend, and contextual volume scores and combines them into a bounded 0–100 project-specific heuristic.

Dashboard labels classify scores below 30 as Low, scores from 30 through 69.99 as Medium, and scores of 70 or more as High. These scores are analytical outputs, not accuracy claims, forecasts, or investment advice.

## 4. Frontend

The client uses **React 19**, **TypeScript 5.9**, **Vite 7**, Tailwind CSS, Recharts, Lucide React, and TanStack Query. Network requests use the browser `fetch` API. The UI supports 7-, 30-, and 90-day analysis, responsive layouts, accessible time-range controls, risk summaries, advanced metrics, and a historical price chart.

## 5. Reliability and verification

Outbound provider clients use the .NET standard resilience handler with retry, attempt timeout, total timeout, and circuit-breaker behavior. Global middleware maps typed failures to JSON `ApiResponse<T>` envelopes. The API also applies a per-IP fixed-window application limit of 30 requests per minute.

The automated suite includes backend unit and pipeline integration tests plus frontend Vitest tests. GitHub Actions restores, builds with warnings treated as errors, runs backend and frontend tests, lints the client, and creates a production frontend build.

No benchmark, memory-consumption, prediction-accuracy, Docker, or license claim is made without a corresponding checked-in artifact or reproducible measurement.
