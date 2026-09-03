# Kripto Risk Analiz Aracı - Proje Mimarisi, Özellikler ve İşleyiş

Bu doküman, projede bulunan tüm dosyaların genel taraması sonucunda sistemin özelliklerini, arkaplan yapılarını ve uygulamanın çalışma mantığını detaylandırmak üzere hazırlanmıştır.

## 1. Proje Yapısı ve Dosya Taraması

Proje temel olarak iki ana bileşenden (Backend ve Frontend) oluşan tam yığın (Full-Stack) bir uygulamadır.

### Backend (.NET 10 Web API - `CryptoRiskAnalysis.API/` klasörü)
*   **Controllers (`RiskAnalysisController.cs`)**: Kullanıcıdan ve arayüzden gelen HTTP (REST) taleplerini karşılayan ve sistemin dışa açılan tek kapısıdır.
*   **Services (Servis Katmanı)**:
    *   `HybridCryptoDataService.cs`: Ana veri akışını yöneten yapıdır. Binance ve CoinGecko API'lerini birleştirerek hataya dayanıklı bir "hibrit" mimari sunar.
    *   `BinanceSpotService.cs`: Binance Spot API'si ile iletişim kurup (OHLCV - Açılış, En Yüksek, En Düşük, Kapanış, Hacim) 30 günlük fiyat geçmişi ve hacim verilerini yüksek hızda çeken servistir.
    *   `CoinGeckoService.cs`: Binance verisine ulaşılamadığında veya bazı spesifik varlıklar için yedek veri kaynağı olarak görev yapan servistir.
    *   `BinanceSymbolMapper.cs`: CoinGecko formatındaki kripto isimlerini (örneğin 'ethereum') Binance formatındaki işlem çifti sembollerine ('ETHUSDT') çeviren mantık sınıfıdır.
    *   `RiskAnalysisEngine.cs`: Projenin beyni konumundadır. Servislerden gelen veriyi istatistiksel modellerden geçirerek bir "Risk Skoru" üreten hesaplama motorudur.
*   **Models / DTOs**: Sistem içerisindeki katmanlar arası veri taşıma şablonlarıdır (`CryptoAsset.cs`, `PriceData.cs`, `RiskScoreResult.cs`).
*   **Interfaces**: Bağımlılıkların tersine çevrilmesi prensibi (Dependency Injection) için kullanılan `ICryptoDataService` ve `IRiskEngine` şablonlarıdır. Sistemin parçalarının kolayca değiştirilip test edilebilmesini sağlar.

### Frontend (React 19, TypeScript, Vite - `client/` klasörü)
*   **Components (Arayüz Bileşenleri)**:
    *   `Dashboard.tsx`: Bütün görsel parçaları bir araya getiren ana analiz paneli.
    *   `AssetSelector.tsx`: Kullanıcının hangi kripto parayı (Örn: BTC, SOL) analiz etmek istediğini seçtiği ve logoları barındıran menü.
    *   `PriceChart.tsx`: `Recharts` kütüphanesi aracılığıyla elde edilen fiyat değişimlerini ve hacim verilerini çizgi grafik olarak çizen bileşen.
    *   `RiskCard.tsx`: Backend'den gelen karmaşık risk skorlarını ve finansal metrikleri, riskin büyüklüğüne göre renk kodlu kartlar halinde (yeşil, sarı, kırmızı) sunan bileşen.
*   **Services (`api.ts`)**: Frontend'in Axios kütüphanesini kullanarak Backend'e (C# API'ye) istek attığı bağlantı noktasıdır.

---

## 2. Sistemin Özellikleri (Görünür Özellikler)

*   **Dinamik Kripto Risk Analizi**: Seçilen kripto para biriminin anlık ve geçmiş verilerine bakılarak karmaşık bir risk skorunun (0-100 arası) tek bir tuşla üretilmesi.
*   **Modern ve Görsel Veri Analizi**: Risk faktörlerinin, tarihsel fiyat hareketlerinin anlaşılır, modern bir panelde sunulması. Tasarımda koyu mod (Dark-mode) ile estetik bir ticaret/analiz arayüzü sağlanması.
*   **Anlık Varlık Geçişi**: Zengin kripto havuzu arasından (Bitcoin, Ethereum, Solana, Tether vb.) hızlıca geçiş yapılarak aynı sayfa içerisinde yükleme beklemeden analiz sonuçlarının alınması.
*   **Detaylı Finansal İstatistikler**: Sadece "riskli/risksiz" demek yerine; **Maksimum Düşüş (Max Drawdown)**, **Sharpe Oranı** ve **Volatilite** yüzdesi gibi profesyonel finansal ölçütlerin kullanıcıya şeffafça sunulması.

---

## 3. Arka Planda Çalışan Özellikler (Mimari ve Performans)

*   **Smart Routing (Akıllı Veri Yönlendirme & Hibrit Veri)**: Sistem dışa bağımlılığını (API kısıtlamalarını) zekice çözer. Öncelikle en hızlı sonuç veren **Binance API** kullanılır. Eğer Binance çökükse veya limite takılmışsak sistem saniyesinde otomatik olarak **CoinGecko API**'ye geçer (Yedeklilik / Graceful Degradation).
*   **In-Memory Caching (Bellek İçi Önbellekleme)**: Sürekli aynı kripto paranın verisi istendiğinde, uygulamamız tekrar tekrar internetten veri çekmez. Çektiği veriyi kısa süreliğine (60 saniye ile 180 saniye arası) RAM (bellek) üzerinde saklar. Bu sistemin tepki süresini saniyenin altına indirirken, API şirketlerinden banlanmamızı engeller.
*   **Clean Architecture (Temiz Mimari)**: Frontend, hesaplama motorundan; hesaplama motoru ise veri çeken servislerden tamamen izole edilmiştir. Kodlar spagetti olmak yerine "Dependency Injection (Bağımlılık Enjeksiyonu)" kullanılarak modüler şekilde birbirine bağlanır. Bu da projenin gelecekte kolayca büyütülebilmesini sağlar.
*   **Ağırlıklı Risk Katlaması (Risk Amplification)**: Arka plandaki matematiksel model, risk hesaplarken faktörlerin sadece ortalamasını almaz. Eğer bir kripto parada aynı anda hem inanılmaz bir dalgalanma hem de çok düşük bir alım-satım hacmi varsa, risk motoru bu kriptonun puanına x1.1 veya x1.2 **ceza çarpanı** ekler.

---

## 4. Neyi, Nasıl Yapıyoruz? (Adım Adım Sistemin İşleyişi)

Kullanıcı arayüzde bir işlem yaptığında arka planda sistem şu şekilde çalışır:

1.  **Talep (Frontend):** Kullanıcı "AssetSelector" üzerinden örneğin "Ethereum (ETH)" seçer. React uygulaması (client), `.NET 10` Backend'inin `RiskAnalysisController`'ına bir istek gönderir.
2.  **Veri Toplama (Services):** `HybridCryptoDataService` devreye girer. 
    *   Önce belleğe bakar: "Son 1 dakika içinde ETH verisi çekilmiş mi?" Çekilmişse direkt onu alır.
    *   Çekilmemişse `BinanceSpotService`'e gider ve Binance'den son 30 günün mum verilerini (`OHLCV`) çeker. Hata olursa CoinGecko'ya geçer.
3.  **Matematiksel Analiz (Engine):** Elde edilen 30 günlük fiyat listesi `RiskAnalysisEngine`'e (Risk Motoruna) yollanır. Risk Motoru şunları yapar:
    *   **Volatilite (Oynaklık):** Günlük fiyat değişimlerinin standart sapmasını alıp yıllıklandırır. Fiyat çok sert dalgalanıyorsa risk skoruna büyük bir pay ekler.
    *   **Trend Analizi:** Kısa vadeli hareketli ortalama ile uzun vadeli hareketi kıyaslar. Varlık düşüş trendindeyse (ayı piyasası) bunu riske ekler.
    *   **Likidite (Hacim):** Günlük işlem hacimlerine bakar. Hacim düşükse "bu parayı satmak istersen alıcı bulamayabilirsin" diyerek riski yükseltir.
    *   **Finansal Metrikler:** Sharpe Oranını hesaplar (Alınan riske değecek bir getiri var mı?).
4.  **Sonuç ve Çizim:** Motor bu verileri harmanlayarak 0 ile 100 arasında bir `Risk Skoru` çıkarır. Bu veriler JSON formatında frontend'e yollanır.
5.  **Arayüz (UI):** React, bu skoru alır. Skor 80'in üzerindeyse kutuyu kırmızıya boyar (Yüksek Risk). Eğer 20 civarıysa yeşile boyar (Düşük Risk). Aynı zamanda Recharts kütüphanesi gelen bu son 30 günlük fiyat verisiyle ekrana çok şık bir fiyat trend grafiği çizer. Tüm bu işlem 1 saniyeden kısa sürede gerçekleşir.
