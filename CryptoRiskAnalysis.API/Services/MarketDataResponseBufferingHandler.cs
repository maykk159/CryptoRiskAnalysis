namespace CryptoRiskAnalysis.API.Services;

internal sealed class MarketDataResponseBufferingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            // HttpClient normally buffers after the handler chain returns. Read the body
            // here so the resilience pipeline can time out and retry a stalled download.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
