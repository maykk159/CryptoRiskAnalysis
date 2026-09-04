# Kripto Risk Analiz Aracı: Özellikler ve Çalışma Mantığı

## Teknolojiler

| Alan | Teknoloji | Kullanım amacı |
|---|---|---|
| Backend | .NET 10 / ASP.NET Core | HTTP API, DI ve asenkron provider erişimi |
| Resilience | Microsoft.Extensions.Http.Resilience | Retry, timeout ve circuit breaker |
| Frontend | React 19 / TypeScript 5.9 / Vite 7 | Tip güvenli ve responsive dashboard |
| Veri yönetimi | TanStack Query ve browser `fetch` | Cache’li istemci sorguları ve iptal desteği |
| Grafik | Recharts | Tarihsel fiyat grafiği |
| Test | xUnit, Moq ve Vitest | Backend, pipeline ve frontend davranış testleri |

## Hibrit veri servisi

Binance, eşlemesi bulunan varlıklarda birincil kaynaktır. CoinGecko eşlemesi olmayan varlıklar ile beklenen Binance sağlayıcı, rate-limit, timeout ve açık circuit hatalarında kullanılır. Beklenmeyen programlama hataları sessizce fallback’e çevrilmez.

İki sağlayıcının verisi ortak kurallarla doğrulanır:

- fiyatlar pozitif olmalıdır;
- hacimler negatif olamaz;
- fiyat ve hacim tarihleri eşleşmelidir;
- yalnız tamamlanmış günlük noktalar kullanılmalıdır;
- dönen nokta sayısı istenen `7`, `30` veya `90` güne eşit olmalıdır.

Binance tarafında taban coin miktarı yerine USDT ciro hacmi kullanılır. Böylece hacim metriği CoinGecko’nun USD hacmiyle aynı para birimi ailesinde değerlendirilir; iki sağlayıcının piyasa kapsamlarının yine de farklı olabileceği unutulmamalıdır.

## Cache ve eşzamanlı istekler

Binance verisi 60 saniye, CoinGecko verisi 180 saniye bellekte tutulur. Cache anahtarında sağlayıcı, varlık/sembol ve gün sayısı yer alır. Aynı anahtara aynı anda gelen cache miss istekleri tek outbound çağrıda birleştirilir.

Cache’in kesin milisaniye yanıt garantisi yoktur. Gerçek süre donanıma, çalışma ortamına ve yük durumuna bağlıdır.

## Risk metrikleri

Motor, doğrulanmış pozitif fiyatlardan günlük log getirileri üretir:

```text
r(t) = ln(P(t) / P(t-1))
```

Hesaplanan metrikler:

- günlük getirilerden `sqrt(365)` ile yıllıklandırılmış volatilite;
- sıfır hedefin altındaki getirilerden downside risk;
- dönem içindeki en büyük peak-to-trough maksimum düşüş;
- yüzde 5 tarihsel getiri kuyruğundan bir günlük VaR 95%;
- yüzde 0 risksiz oran varsayımıyla yıllıklandırılmış Sharpe oranı;
- son bölümü tüm seçili dönemle karşılaştıran trend skoru;
- fiyat hareketiyle birlikte yorumlanan güncel/ortalama hacim oranı.

Kompozit skor volatilite, trend ve hacim için başlangıçta `%40 / %30 / %30` ağırlık kullanır. Yüksek sinyallerde ağırlıklar ve çarpanlar değişebilir. Sonuç `0–100` aralığına sınırlandırılır. UI sınıflandırması `30` ve `70` eşiklerini kullanır.

Bu yöntem doğrulanmış bir tahmin modeli değildir; geçmiş veriye dayalı proje özelinde bir risk sezgisidir.

## Hata yönetimi ve rate limit

Standart resilience pipeline geçici bağlantı hatalarını, `408`, `429` ve `5xx` yanıtlarını ele alır. Varsayılan yapılandırma üç retry, 10 saniyelik attempt timeout, 30 saniyelik total timeout ve circuit breaker içerir.

Global middleware typed hataları uygun HTTP durumlarına ve `ApiResponse<T>` JSON biçimine dönüştürür. Uygulama ayrıca uzak IP başına dakikada 30 istek kabul eder; reddedilen istek de JSON envelope döndürür.

Binance ve CoinGecko’nun dış kotaları sabit kabul edilmez. Limitler sağlayıcı planına, IP’ye, endpoint ağırlığına ve güncel politikalara göre değişebilir.

## Otomatik doğrulama

Backend testleri controller, cancellation aktarımı, risk motoru, sağlayıcı veri doğrulaması, fallback, cache deduplication, retry, timeout, circuit breaker, exception middleware ve rate-limit yanıtını kapsar. Frontend Vitest testleri düşük fiyat formatlama ve kullanıcı dostu ağ hata mesajını doğrular. GitHub Actions her push ve pull request’te backend build/test ile frontend lint/test/build adımlarını çalıştırır.
