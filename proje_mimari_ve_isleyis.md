# Kripto Risk Analiz Aracı: Mimari ve İşleyiş

## Proje yapısı

Uygulama iki ana parçadan oluşur:

- `CryptoRiskAnalysis.API`: .NET 10 ASP.NET Core Web API.
- `client`: React 19, TypeScript ve Vite tabanlı kullanıcı arayüzü.

Backend tek bir Web API assembly’sidir. Controller, servis, model/DTO, middleware ve provider adaptörleri sorumluluklarına göre klasörlere ayrılmıştır. Interface ve dependency injection kullanılmaktadır; ancak ayrı Domain/Application/Infrastructure projeleri bulunmadığı için yapı “Strict Clean Architecture” olarak tanımlanmaz.

## Backend bileşenleri

- `RiskAnalysisController.cs`: `7`, `30` veya `90` günlük analiz isteğini doğrular ve standart API yanıtını üretir.
- `HybridCryptoDataService.cs`: Uygun varlıklarda Binance’i dener, beklenen sağlayıcı hatalarında CoinGecko’ya geçer.
- `BinanceSpotService.cs`: Tamamlanmış günlük USDT mumlarını ve USDT ciro hacmini işler.
- `CoinGeckoService.cs`: Günlük USD fiyat ve toplam hacim serisini işler.
- `MarketDataValidator.cs`: Fiyatların pozitif, hacimlerin negatif olmayan, tarihlerinin eşleşen ve nokta sayısının istenen pencereye eşit olmasını doğrular.
- `RiskAnalysisEngine.cs`: Risk skorlarını ve ileri metrikleri yerel olarak hesaplar.
- `ExceptionHandlingMiddleware.cs`: Hataları `ApiResponse<T>` biçimine dönüştürür.

## Frontend bileşenleri

- `Dashboard.tsx`: veri sorgusunu ve görünüm durumlarını yönetir.
- `AssetSelector.tsx`: desteklenen varlık seçimini sunar.
- `dashboard/RiskScoreCard.tsx`: kompozit, volatilite, trend ve hacim risklerini gösterir.
- `dashboard/AdvancedMetrics.tsx`: downside risk, maksimum düşüş, Sharpe, VaR ve yıllık volatiliteyi gösterir.
- `PriceChart.tsx`: seçilen dönemin fiyat geçmişini çizer.
- `services/api.ts`: browser `fetch` API’si ile backend’e bağlanır.

Arayüzde Tether bulunmaz. Güncel varlık listesi `client/src/constants/assets.ts` dosyasındadır.

## İstek akışı

1. Kullanıcı bir varlık ve `7`, `30` veya `90` günlük dönem seçer.
2. Frontend `/api/RiskAnalysis/{assetId}?days={days}` endpoint’ini çağırır.
3. Hibrit servis, sembol eşlemesi varsa Binance’i dener; eşleme yoksa veya beklenen sağlayıcı hatası oluşursa CoinGecko’yu kullanır.
4. Sağlayıcı servisi tamamlanmış günlük fiyat ve hacim serilerini doğrular. Eksik veya tutarsız veri geçerli analiz olarak kabul edilmez.
5. Risk motoru seçilen dönemin tamamını kullanarak metrikleri hesaplar.
6. API sonucu standart JSON envelope içinde döndürür ve React dashboard’u günceller.

## Skor gösterimi

- `0–29.99`: Low Risk
- `30–69.99`: Medium Risk
- `70–100`: High Risk

Kompozit skor proje özelinde sezgisel bir göstergedir; yatırım tavsiyesi veya gelecek performansı tahmini değildir.

## Dayanıklılık

- Binance cache süresi: 60 saniye.
- CoinGecko cache süresi: 180 saniye.
- Aynı anahtardaki eşzamanlı cache miss istekleri tek sağlayıcı çağrısında birleştirilir.
- Geçici ağ, `408`, `429` ve `5xx` hataları retry kapsamındadır.
- Her denemenin timeout’u 10 saniye, toplam istek timeout’u 30 saniyedir.
- Provider limitleri endpoint ağırlığına, IP’ye, hesaba/plana ve sağlayıcının güncel politikasına bağlıdır; sabit kota varsayılmaz.

Yanıt süresi ve bellek kullanımı için ölçülmüş benchmark bulunmadığından kesin performans rakamı verilmez.
