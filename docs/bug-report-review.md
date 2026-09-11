# Bug raporu değerlendirmesi — 10 Eylül 2026

Mevcut çalışma ağacı üzerinden incelendi; önceden yapılmış değişiklikler korundu.

| Madde | Karar ve gerekçe |
| --- | --- |
| B01 | Hata doğrulanmadı. `Open` private ve bütün çağrıları `_gate` tutulurken yapılıyor. `volatile` eklemek gerekmiyor. |
| B02 | Hata doğrulanmadı. Her hesaplamadan önce en az 7 fiyat doğrulanıyor; rapor varsayımsal bir doğrulama kaldırılmasına dayanıyor. Ayrıca bu ifade double bölmesi, doğrudan `DivideByZeroException` üretmez. |
| B03 | Hata doğrulanmadı. Boş liste reddediliyor; `Select` eleman sayısını azaltmıyor. Parse hatası `MaxBy` çağrısına ulaşmadan yakalanıyor. |
| B04 | Düzeltildi. CoinGecko fiyat ve hacimleri JSON'dan doğrudan decimal okunuyor. Yüksek hassasiyetli fiyat ve hacim regresyon testi eklendi. Küçük fiyat tek başına double hassasiyetsizliği anlamına gelmez; kayıp anlamlı basamak sayısından kaynaklanır. |
| B05 | Düzeltildi. Risk endpoint'i kontrol karakterlerini ve Unicode satır ayırıcılarını loglamadan önce reddediyor; uzunluk kontrolü de loglamadan önce çalışıyor. |
| B06 | Düzeltildi. Yanıt başlamışsa asıl exception yeniden fırlatılıyor; header/body üzerine ikinci hata yanıtı yazılmıyor. |
| B07 | Düzeltildi. İzinli origin'ler `Cors:AllowedOrigins` ayarından okunuyor. |
| B08 | Düzeltildi. HTTPS hedef portu `HttpsRedirection:HttpsPort` ile değiştirilebiliyor. Varsayılan 443 ve 308 yönlendirme korunuyor. |
| B09 | Küçük performans iyileştirmesi yapıldı: grafik imzası model değişene kadar memoize ediliyor. 90 nokta için kritik performans hatası iddiası doğrulanmadı. |
| B10 | Düzeltildi. Ortak media-query hook'u değişiklik olaylarına abone oluyor; canlı reduced-motion değişikliği animasyonu iptal ediyor ve hedefi gösteriyor. |
| B11 | Somut yan etki düzeltildi. Dönem yönü değişikliği aktif vurgu zamanlayıcısını iptal edip vurguyu açık bırakabiliyordu. Yön effect event üzerinden okunuyor; tek başına yön değişikliği effect'i yeniden başlatmıyor. |
| B12 | Hata doğrulanmadı. Yerel bütçe bitince hızlı 429/Retry-After bilinçli davranış. Özel kota exception'ı standart HTTP retry koşullarına girmiyor. |
| B13 | İşlevsel hata değil. Null/boş kontrolü savunmacı; alternatif servis implementasyonu boş liste döndürebilir. Korundu. |
| B14 | Koruyucu erişilebilirlik düzeltmesi. Başlık kimlikleri iki bileşende de `useId` ile üretiliyor; mevcut tek örnekli sayfada çakışma yoktu. |
| B15 | Bu arayüz için hata doğrulanmadı. JS number görselleştirme ve yuvarlanmış analitik değerler için yeterli; parasal muhasebe veya kesin decimal round-trip sözleşmesi yok. |
| B16 | Öneri uygulanmadı. Microsoft.Data.Sqlite async metotları da senkron çalışır; mevcut transactional batch ve WAL korunuyor. |
| B17 | Hata doğrulanmadı. DOM PointerEvent doğru tip; ref optional chaining ile korunuyor ve listener'lar cleanup'ta kaldırılıyor. |
| B18 | Düzeltildi. Aynı sibling grubundaki kart ve grafik key'leri artık farklı. |
| B19 | Hata doğrulanmadı. Sıfırın `$0.00`, geçersiz negatif fiyatın `Unavailable` olması tutarlı; değişim tutarı ayrı işaretle gösteriliyor. |
| B20 | Raporun iddiası doğrulanmadı. Görünürlük URL eşitliğiyle kontrol ediliyor; aynı img'nin src'si güncelleniyor. İstek için ayrıca uygulama seviyesinde abort olmaması tek başına hata değil. |
| B21 | Orijinal days hatası doğrulanmadı: controller ile servis aynı 1–90 gün aralığını doğruluyor. Takip iyileştirmesi olarak tarih aralığı hatası `InvalidHistoricalDateRangeException` ile ifade edildi; controller artık `ParamName` metnine bağlı değil. Bugün, gelecek tarih ve başlangıç tarihinin alt sınırı aşması için 400 yanıtı test edildi. |
| B22 | SSR çökmesi iddiası doğrulanmadı: SSR subscribe çağırmaz. Ancak matchMedia olmayan test ortamı gerçekten hata verebilirdi; ortak hook güvenli fallback sağlıyor. |
| B23 | Zaten uygulanmış. `.gitignore` içinde `[Ll]ogs/` var. |

B16 kaynağı: [Microsoft.Data.Sqlite async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async).

## Doğrulama

- Backend: Güncel fiyat akışı dahil 167 test başarılı.
- Frontend: 110 test başarılı; TypeScript ve production build başarılı.
- Frontend lint ve `git diff --check` temiz.
- Bilinmeyen varlıklardaki boş icon URL uyarısı takip düzeltmesiyle giderildi.

## Güncel fiyat akışı — 11 Eylül 2026

7/30/90 günlük analiz önbelleklerindeki mum fiyatı artık risk API'sinin `currentPrice`
alanına aktarılmıyor. Backend, tarihsel verinin sağlayıcısına ait gerçek fiyat
endpoint'ini kullanıyor; `currentQuote` alanı kaynak, para birimi, alınma zamanı ve
varsa sağlayıcı güncelleme zamanını taşıyor. Kısa fiyat önbelleği dönemden bağımsız;
Refresh bu önbelleği aşıyor ancak sağlayıcı kotasını aşmıyor. Tarayıcı eski döneme
dönüşte sunucudan yeniden doğruluyor. Risk formülleri ve SQLite şeması değişmedi.

Gerçek Binance yanıtlarıyla yerel API kontrol edildi: zorunlu yenileme alınma
zamanını güncelledi ve sonraki dönem isteği yeni fiyat kaydını kullandı. Sayısal
fiyatın her yenilemede değişmesi beklenmiyor; yeni istek ve zaman bilgisi doğrulanıyor.
Yerel API yeni kodla 5058 portunda yeniden başlatıldı. Ayrıntılar: [veri akışı](market-data.md#current-quotes-and-explicit-refresh).

## Gemini takip bulguları — 11 Eylül 2026

Orijinal B01–B23 numaralarından ayrı BUG-1–BUG-8 değerlendirmesi:

| Madde | Uygulanan değişiklik |
| --- | --- |
| BUG-1 | History endpoint'i null/boş assetId kontrolünü sembol eşlemesinden önce yapıyor. HTTP üzerinden null ile 500 iddiası doğrulanmamıştı; doğrudan çağrılar da artık 400 dönüyor. |
| BUG-2 | Her iki sağlayıcının önbelleği günlük fiyat ve hacimlerin tamamını saklıyor. Cache hit sırasında istenen assetId için arşive upsert yapılıyor. Aynı POLUSDT sembolünü paylaşan iki kimlik ve sıcak cache/boş arşiv senaryoları ek HTTP çağrısı olmadan test edildi. |
| BUG-3 | Geçmiş Retry-After tarihi veya sıfır süre için ortak iki saniyelik cooldown uygulanıyor; pozitif süre davranışı korunuyor. |
| BUG-4 | Boş veya yalnızca boşluk içeren ikon URL'sinde img oluşturulmuyor; metin fallback gösteriliyor. |
| BUG-5 | Binance fiyat/hacim string'leri NumberStyles.Float ile bilimsel gösterimi kabul ediyor. Bu koruyucu parse iyileştirmesi, örnek 1.5e-7 fiyat ve 1.5e3 hacimle test edildi. |
| BUG-6 | Standart TimeoutException da CoinGecko fallback'ini tetikliyor. Beklenmeyen hatalar ve kullanıcı iptali için mevcut davranış korunuyor. |
| BUG-7 | History endpoint'i kontrol karakterlerini ve Unicode satır ayırıcılarını servislere ulaşmadan reddediyor. |
| BUG-8 | AssetSummary'nin ikinci koşulundaki erişilemez fiyat yönü dalı kaldırıldı. |

Önbellek hit'lerinde arşiv tutarlılığı için küçük bir SQLite yazma işlemi yapılır. Tam arşiv pencerelerini okuyan history endpoint'i yalnızca okumaya devam eder. Veritabanı şeması değişmedi.

## Dağıtım ayarları

Production frontend adresini `Cors__AllowedOrigins__0=https://risk.example.com` olarak ayarlayın. Birden fazla adres için dizinin sonraki indekslerini kullanın. Özel HTTPS hedef portu için `HttpsRedirection__HttpsPort=8443` kullanın. Origin şema, alan adı ve varsa port içermeli; path içermemelidir. Reverse proxy arkasında mevcut `ReverseProxy` güven ayarları geçerlidir.
