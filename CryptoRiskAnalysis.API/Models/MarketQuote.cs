namespace CryptoRiskAnalysis.API.Models;

public sealed record MarketQuote(decimal Price, string Source, string Currency,
    DateTimeOffset FetchedAt, DateTimeOffset? SourceUpdatedAt);
