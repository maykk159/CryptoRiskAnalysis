# Market data persistence and provider resilience

## CoinGecko request budget

The HTTP client sends `User-Agent: CryptoRiskAnalysis/1.0` and accepts JSON.
CoinGecko can reject unidentified clients with HTTP 403; this identifies the
application without impersonating a browser.

`CoinGecko:RequestsPerMinute` defaults to **20 attempts per rolling 60 seconds**.
This is a conservative application setting, not an assertion about the current
CoinGecko plan allowance. Set it to fit the allowance of the deployment/account.
The singleton budget is shared by all assets, HTTP client instances and users.
Each retry consumes a permit; memory-cache hits and rejected local attempts do not.
Permits are not refunded for failures, because the provider may have counted them.

The budget is inside the HTTP resilience pipeline. The pipeline explicitly uses
exponential backoff, jitter, at most three retries, 10-second attempt timeouts and
a 30-second overall timeout. CoinGecko response body buffering is inside the
pipeline as well, so a stalled body cannot escape the timeout. Network failures,
408, 429 and 5xx responses are retryable; ordinary 4xx and invalid parsed market
data are not retried. Caller cancellation propagates.

Both seconds and HTTP-date `Retry-After` headers are honored. A 429 also creates a
shared cooldown; without a header, the cooldown is two seconds. New attempts
during cooldown or quota exhaustion fail promptly without reaching the provider.
They produce HTTP 429 with a rounded-up `Retry-After` header. This can stop a retry
sequence early. Long provider delays are not shortened to force a retry; an
ongoing request can hit its total timeout (504) while the shared cooldown remains.

The existing circuit breaker evaluates a 50% failure ratio after a minimum of
five outcomes within 30 seconds; it opens for 30 seconds. It is not a count of
five consecutive failures. Binance retains the common resilience policy and
the risk endpoint retains its whole-provider fallback to CoinGecko.

This budget is **per running application instance**. Multiple replicas or other
applications sharing the same upstream quota need a shared limiter or divided
budgets. The incoming per-IP API limiter is separate from this outgoing budget.

## Persistent daily history

The application uses SQLite through `Microsoft.Data.Sqlite`. No database server
is required. The database is created on first use at
`CryptoRiskAnalysis.API/Data/market-history.db`, relative to the API content root.
Override `HistoricalData:DatabasePath` (or `HistoricalData__DatabasePath` in the
environment) for a different location. Use a persistent disk/volume in deployment.
The `.db`, WAL and shared-memory files are ignored by Git.

Every successful, validated provider fetch saves its completed daily price and
quote-volume observations **before** populating the in-memory cache. This includes
ordinary risk analysis requests. The unique key is:

```text
(asset_id, source, quote_currency, utc_day)
```

Binance USDT venue turnover and CoinGecko USD market-wide volume are separate
series. They are never spliced into one historical response. Each row preserves
the provider timestamp. Decimal prices and volumes are stored as invariant text,
avoiding SQLite floating-point precision loss. Overlapping fetches upsert the
same dates atomically, including provider corrections. Invalid batches are
rejected before any row is written; cancellation before commit rolls back.

Writes use short transactions and WAL mode. A process-wide semaphore serializes
database operations with cancellation while waiting. SQLite calls themselves are
synchronous, with a five-second lock timeout; this is intended for the current
single-instance workload. Schema version 1 is initialized automatically; newer
schema versions are rejected rather than silently downgraded.

## Historical API

```http
GET /api/marketdata/bitcoin/history?days=30&source=Binance
GET /api/marketdata/bitcoin/history?days=7&source=CoinGecko
GET /api/marketdata/bitcoin/history?days=7&source=Binance&endDate=2026-09-01
```

- `days`: 1–90, default 30. This is an observation count, not a return count.
- `endDate`: optional inclusive UTC date; defaults to yesterday. Today/future dates
  are rejected. This endpoint has the existing incoming API rate limiter.
- `source`: `Binance` or `CoinGecko`. If omitted, mapped assets select Binance,
  others select CoinGecko. An explicitly selected source never silently falls back.
- Response uses the existing `{ succeeded, data, ... }` envelope. Data includes
  `assetId`, `source`, `quoteCurrency`, `from`, `to` and ordered `observations` with
  `date`, `timestamp`, `price`, and `volume`.

The endpoint reads SQLite first. A complete stored window needs **no external
request**, including after application restart. If dates are missing, the service
fetches a recent window beginning at the earliest missing date through yesterday,
using the selected provider's existing adapter. It re-reads the database and
requires the entire requested window to be present before responding.

Provider cache entries retain every daily price and volume. A cache hit also
upserts these observations for the requested asset ID before returning, so an
empty archive can be repaired without another HTTP call. Binance aliases sharing
a symbol (for example `matic-network` and `polygon-ecosystem-token`) share the
provider cache but each receives its own archive rows. This adds a small SQLite
batch write on provider cache hits; reads of complete historical windows remain
read-only.

Backfill is limited to the last 90 UTC days. This implementation requests a
covering recent window, not an individual HTTP call for every missing date.
Stored older windows remain readable; missing older dates produce HTTP 422.
Provider errors propagate as 429/502/503/504, and an incomplete result after
backfill is 502. Missing prices are never interpolated or replaced with zero.
Provider access/plan restrictions can still prevent a requested backfill.

Concurrent fills for the same asset/source are coalesced and database keys prevent
duplicates across overlapping windows. The existing risk endpoint continues to
reuse daily history using its 60-second cache, while retrieving the current quote
through a separate provider price endpoint. Collection is on demand. A periodic
background collector is a separate next step, not included here.

## Current quotes and explicit refresh

`GET /api/RiskAnalysis/{assetId}?days=7|30|90` returns a quote from
`/api/v3/ticker/price` (Binance) or `/api/v3/simple/price` (CoinGecko). The
historical adapter's last candle value is not used as the displayed current price.
The historical result carries its provider so a Binance history is paired with
a Binance/USDT quote, and a CoinGecko fallback history with a CoinGecko/USD quote.

The response retains `currentPrice` and adds `currentQuote` with `price`, `source`,
`currency`, `fetchedAt`, and nullable `sourceUpdatedAt`. `fetchedAt` is when this
server received the quote, not when the exchange executed a trade. Binance's
price ticker does not return that timestamp, so `sourceUpdatedAt` remains null.
CoinGecko is requested with `include_last_updated_at=true&precision=full`; missing,
malformed, more than five-minute-old, or over one-minute-future timestamps fail
validation rather than being represented as a fresh quote.

The quote cache is shared across periods, keyed by provider, currency, and asset
(Binance symbol for aliases), and lasts 10 seconds for Binance or 30 seconds for
CoinGecko. Concurrent cache fills are coalesced. Add `refresh=true` to bypass this
cache and contact the provider; this does not bypass rate limits or change daily
history caching. A failed forced fetch returns the provider error, not the old
quote with a new fetch timestamp. HTTP responses use `Cache-Control: no-store`;
provider caching remains controlled inside the application.

The dashboard polls every 10 seconds while active, revalidates on period changes,
and displays the quote's source, currency and fetch time. CoinGecko requests share
the existing history/retry request budget. No scheduled process is introduced;
when the application is idle no quote requests are made. A deploy still requires
an accessible backend and persistent storage for the SQLite archive.

## Verification

Backend tests use stub HTTP handlers and temporary, real SQLite databases. They
exercise retry quotas, cooldowns, response-body and total timeouts, cancellation,
restart persistence, decimal round trips, source isolation, overlapping writes,
gap backfill, cached historical reads, and API input/response behavior.

Technical references: [HTTP resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience),
[client-side rate limiting](https://learn.microsoft.com/en-us/dotnet/core/extensions/http-ratelimiter),
[SQLite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions),
[SQLite async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async).
