# Yük testi sonuçları (B-4, AC-20.4)

Ölçüm: 2026-10-14, tek koşu, k6 v2.2.0. Betikler: [`tests/load/browse.js`](../tests/load/browse.js),
[`tests/load/hot-seat.js`](../tests/load/hot-seat.js).

## Ortam

| | |
|---|---|
| Makine | Intel Core i9-13900H (14 çekirdek / 20 iş parçacığı), 15,7 GB RAM, Windows 11 Pro |
| API | `dotnet run -c Release`, .NET SDK 10.0.401, Development ortamı (örnek veri), tek örnek |
| Bağımlılıklar | `compose.yaml` ile Docker Desktop (WSL 2): PostgreSQL 17.11, Redis 7.4.11, RabbitMQ 4.3.6 |
| Yük üreticisi | k6, API ile aynı makinede |
| API ayarları | `RateLimiting__Enabled=false` (setup 200 kullanıcı kaydeder), `Serilog__MinimumLevel__Default=Warning` (istek başına konsol satırı ölçümü bozmasın diye), `OTEL_EXPORTER_OTLP_ENDPOINT=` (telemetri kapalı) |

Referans makine (ISKELET V-16: 8 çekirdek, 16 GB) bundan daha zayıftır; sayılar NFR-02 için iyimser bir üst sınırdır.

## browse: 50 VU, 60 sn (`GET /events` + `GET /events/{id}`)

| Metrik | Değer | NFR-02 hedefi |
|---|---|---|
| İstek sayısı | 292.959 | |
| Verim | 4.802 istek/sn | |
| p50 | 7,8 ms | |
| p95 | **19,6 ms** | < 200 ms ✓ |
| p99 | 43,5 ms | |
| Hata oranı | **%0,00** | < %1 ✓ |

Okumalar HybridCache'ten (L1 bellek + Redis) gelir (ISKELET §5.9).

## hot-seat: 200 VU, aynı koltuk, kullanıcı başına tek istek

| | `Locking:Enabled=true` (varsayılan) | `Locking:Enabled=false` |
|---|---|---|
| `201 Created` | **1** | **1** |
| `409 seat.unavailable` | 199 | 199 |
| `POST /events/{id}/reservations` p50 | 200 ms | 771 ms |
| p95 | **261 ms** (< 400 ms ✓) | **880 ms** (< 400 ms ✗) |
| p99 | 269 ms | 889 ms |

- **Doğruluk iki durumda da aynı:** tam bir koltuk tutma başarılı olur. Çift satışı veritabanı katmanı (xmin
  optimistic concurrency) engeller; Redis kilidi doğruluk için gerekli değildir (ISKELET §5.4).
- **Kilidin faydası gecikmede:** kilit açıkken 199 kaybeden istek Redis'te reddedilir ve veritabanına hiç gitmez.
  Kilit kapalıyken hepsi transaction açıp koltuğu okur, yazmaya çalışır ve `xmin` çakışmasıyla elenir. Rezervasyon
  p95'i bu yüzden 261 ms'den 880 ms'ye çıkar.
- **NFR-02 sapması (kilit kapalı):** rezervasyon p95 hedefi yalnız kilit kapalıyken tutmuyor. Neden: 200 eşzamanlı
  isteğin hepsinin aynı satır için veritabanı transaction'ı açması. Varsayılan yapılandırma (kilit açık) hedefi
  sağlar.
- k6'nın `http_req_failed` metriği bu senaryoda %33 gösterir: beklenen 199 adet `409` yanıtı k6'ya göre "başarısız"
  sayılır. Senaryonun kendi kontrolü (`201 or 409`) %100 geçer.

## Tekrar çalıştırma

README'deki "Yük testi" bölümündeki komutlar. hot-seat her koşuda örnek etkinliğin müsait bir koltuğunu tüketir
(koltuk 10 dk tutulur); koltuklar biterse `docker compose down -v` ile veritabanı sıfırlanır.
