# Crypto Risk Analysis API

ASP.NET Core Web API for retrieving validated daily cryptocurrency market data and producing heuristic risk metrics.

The backend targets **.NET 10** and is organized into folders inside a single Web API project. Controllers, services, models, middleware, and provider adapters are separated by responsibility, but they are not separate Domain/Application/Infrastructure assemblies; this repository therefore does not claim strict Clean Architecture.

## Dependencies

The API uses these external NuGet packages:

- `Microsoft.Extensions.Http.Resilience` for retry, timeout, and circuit-breaker handlers.
- `Serilog.AspNetCore` for console and rolling-file logging.
- `Swashbuckle.AspNetCore` for development OpenAPI/Swagger UI.

## Run locally

From the repository root:

```powershell
cd CryptoRiskAnalysis.API
dotnet restore
dotnet run
```

The development API listens on `http://localhost:5058`. Swagger UI is available at `http://localhost:5058/swagger` while the environment is Development.

## Endpoint

```http
GET /api/RiskAnalysis/{assetId}?days={7|30|90}
```

- `assetId` is a CoinGecko asset ID such as `bitcoin` or `ethereum`.
- `days` accepts `7`, `30`, or `90` and defaults to `30`.
- A successful response contains exactly the requested number of completed daily observations.

All success and application-generated error responses use the `ApiResponse<T>` envelope:

```json
{
  "succeeded": true,
  "message": null,
  "data": {
    "assetId": "bitcoin",
    "compositeRiskScore": 31.42,
    "volatilityScore": 38.17,
    "trendScore": 24.86,
    "volumeScore": 29.35,
    "priceHistory": []
  },
  "errors": null
}
```

The values above only demonstrate the response shape; live results depend on provider data and the selected period.

## Data providers and resilience

- Binance is tried first for mapped assets and uses completed daily USDT klines. Quote-asset turnover is used as the volume measure.
- CoinGecko is used for unmapped assets and as fallback for expected Binance provider, timeout, rate-limit, and open-circuit failures.
- Binance results are cached for 60 seconds; CoinGecko results are cached for 180 seconds.
- Concurrent cache misses for the same provider, asset, and period share one outbound request.
- Each attempt has a 10-second timeout and the total resilience pipeline has a 30-second timeout.
- Transient network errors, `408`, `429`, and `5xx` responses are eligible for exponential retry.

Provider quotas are not stated as fixed numbers here because they depend on endpoint weight, account or plan, IP, and current provider policy. Consult the official [Binance Spot API documentation](https://developers.binance.com/docs/binance-spot-api-docs/rest-api/limits) and [CoinGecko rate-limit documentation](https://docs.coingecko.com/reference/common-errors-rate-limit) when configuring production traffic.

## HTTP behavior

| Status | Meaning |
|---|---|
| `200` | Analysis completed |
| `400` | Unsupported analysis period |
| `404` | Asset or market data not found |
| `429` | Application or upstream rate limit reached |
| `502` | Provider returned unsuccessful, malformed, incomplete, or invalid data |
| `503` | Provider circuit is open |
| `504` | Provider request timed out |
| `500` | Unexpected application error |

The application rate limit is 30 requests per minute per remote IP. Its `429` response uses the same JSON envelope.

## Local configuration

- CORS permits `http://localhost:5173` and `http://localhost:5174` for local Vite development.
- Development uses HTTP. Non-development environments enable HSTS and redirect HTTP to HTTPS port 443.
- No authentication is implemented; add it before exposing a restricted deployment.
- Provider API keys are not required by the current public endpoint integration.

## Tests

From the repository root:

```powershell
dotnet build CryptoRiskAnalysis.API.sln --configuration Release --no-restore -warnaserror
dotnet test CryptoRiskAnalysis.Tests/CryptoRiskAnalysis.Tests.csproj --configuration Release --no-build
```

The suite covers controller behavior, cancellation propagation, risk calculations, provider validation and fallback, cache-miss deduplication, the retry/timeout/circuit pipeline, exception middleware, and the JSON rate-limit response.

See the [repository README](../README.md) for complete setup and frontend instructions.
