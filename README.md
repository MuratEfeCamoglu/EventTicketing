# EventTicketing

[![CI](https://github.com/MuratEfeCamoglu/EventTicketing/actions/workflows/ci.yml/badge.svg)](https://github.com/MuratEfeCamoglu/EventTicketing/actions/workflows/ci.yml)

Etkinlik ve bilet rezervasyon API'si. Kullanıcı etkinlik arar, koltuğu 10 dakika tutar, mock ödeme yapar, QR kodlu
bilet ve e-posta alır.

Projenin amacı, gerçek backend'lerin zor problemlerini çalışan kod ve testlerle göstermek: eşzamanlı koltuk alımı,
idempotent ödeme, zaman aşımına uğrayan rezervasyon. Mülakattaki "İki kullanıcı aynı koltuğu aynı anda alırsa ne olur?"
sorusunun cevabı [`CreateReservation.cs`](src/EventTicketing.Api/Features/Reservations/CreateReservation.cs) ve onu
50 paralel istekle 10 tur deneyen
[`SeatHoldConcurrencyTests.cs`](tests/EventTicketing.IntegrationTests/Features/Reservations/SeatHoldConcurrencyTests.cs)
dosyalarındadır.

> Durum: **MVP** tamam (ISKELET.md §9, M1–M9); **v2** tamam (Redis + HybridCache, Redis lock, arka plan
> temizleyicisi, tek komutla yerel çalıştırma); **v3** tamam (EF outbox + RabbitMQ, Notification servisi,
> OpenTelemetry → Aspire Dashboard, GitHub Actions CI). Sırada bonus görevler var.

## Teknoloji

.NET 10 · ASP.NET Core Minimal API · EF Core 10 + Npgsql · PostgreSQL 17 · FluentValidation · Serilog ·
Redis 7 + HybridCache · MailKit + Mailpit · QRCoder · xUnit v3 · Shouldly · Respawn. Bağımlı servisler Docker
Compose ile çalışır; API `dotnet run` ile.

## Çalıştırma

### Önkoşullar

| Araç | Not |
|---|---|
| .NET SDK 10.0.x | `dotnet --version` |
| Docker Desktop (WSL 2) | Bağımlı servisler için; `docker compose version` |

Bağımlı servisler kökteki [`compose.yaml`](compose.yaml) ile başlatılır (imaj sürümleri orada sabittir, portlar
yalnız loopback'e, `127.0.0.1` ve `::1`'e açılır):

```bash
docker compose up -d     # ilk seferde imajlar indirilir
docker compose ps        # hepsi "running" / "healthy" olmalı
```

| Servis | Adres | Not |
|---|---|---|
| PostgreSQL 17 | `localhost:5432` | kullanıcı `postgres`, parola `dev-only-postgres`; veri `postgres-data` volume'ünde |
| Redis 7 | `localhost:6379` | yalnız önbellek ve kilit; kalıcılık kapalı |
| Mailpit | SMTP `localhost:1025`, arayüz `http://localhost:8025` | gönderilen bilet e-postaları |
| RabbitMQ 4 (v3) | `localhost:5672`, yönetim `http://localhost:15672` | kullanıcı `eventticketing`, parola `dev-only-rabbitmq` |
| Aspire Dashboard (v3) | `http://localhost:18888`, OTLP `localhost:4317` | trace, metrik ve loglar |

`dev-only-` önekli parolalar yalnız bu makinedeki geliştirme içindir. Durdurmak için `docker compose stop`; verileri de
silmek için `docker compose down -v`. Redis kapalıysa API çalışmaya devam eder, okumalar veritabanından gelir.

### API

```bash
dotnet tool restore
dotnet run --project src/EventTicketing.Api
```

Servisler `docker compose up -d` ile ayaktayken tek komut yeterlidir: migration'lar ve örnek veri başlangıçta
uygulanır. Ayrı bir terminalde doğrulama (Git Bash): `./scripts/smoke.sh`. Betik `GET /api/v1/events` için 5 sn arayla
24 kez dener ve 120 sn içinde en az bir etkinlikle `200` bekler (AC-16.1).

- API: `http://localhost:8080/api/v1` · Scalar UI: `http://localhost:8080/scalar` · OpenAPI: `/openapi/v1.json`
- Development ortamında migration'lar ve örnek veri başlangıçta uygulanır: 100 koltuklu bir salon, 30 gün sonraya
  yayımlanmış bir konser ve şu kullanıcılar (parola hepsinde `Dev-only-Passw0rd`):
  `admin@example.com` (Admin), `organizer@example.com` (Organizer), `customer@example.com` (Customer).
- `appsettings.Development.json` içindeki `dev-only-` önekli değerler yalnız yerel kullanım içindir ve Production
  ortamında başlangıçta reddedilir. Kendi JWT anahtarınızı kullanmak için:
  `dotnet user-secrets set "Jwt:SigningKey" "<en az 32 bayt>" --project src/EventTicketing.Api`

Örnek akış (Scalar UI'dan da yapılabilir):

```bash
TOKEN=$(curl -s localhost:8080/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"customer@example.com","password":"Dev-only-Passw0rd"}' | jq -r .accessToken)
EVENT=$(curl -s localhost:8080/api/v1/events | jq -r '.items[0].id')
SEAT=$(curl -s localhost:8080/api/v1/events/$EVENT/seats | jq -r '.[0].eventSeatId')
RES=$(curl -s localhost:8080/api/v1/events/$EVENT/reservations -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d "{\"eventSeatIds\":[\"$SEAT\"]}" | jq -r .reservationId)
curl -s localhost:8080/api/v1/reservations/$RES/payments -H "Authorization: Bearer $TOKEN" \
  -H "Idempotency-Key: $(uuidgen)" -H 'Content-Type: application/json' -d '{"paymentToken":"tok_success"}'
# Bilet e-postası: http://localhost:8025 (Notification servisi çalışıyorken)
```

### Notification servisi (v3)

```bash
dotnet run --project src/EventTicketing.Notifications
```

Bilet e-postasını bu servis gönderir: RabbitMQ'daki `ticket-purchased` kuyruğundan `TicketPurchased` mesajını alır,
QR PNG'lerini üretir ve Mailpit'e (SMTP `localhost:1025`) yollar. API'yle aynı PostgreSQL'i kullanır, ancak yalnız
kendi `notifications` şemasındaki MassTransit inbox tablolarına yazar; Development'ta migration'larını başlangıçta
uygular. Kuyruk ilk başlatmada oluşur ve exchange'e bağlanır; bundan önce yayımlanan mesajları alacak kuyruk yoktur,
bu yüzden servisi ilk ödemeden önce bir kez başlatın.

## Mimari: Vertical Slice

**Karar:** Tek bir API projesi içinde özellik (use case) bazlı dilimler. Her use case
`Features/<Alan>/<UseCase>.cs` içinde tek bir statik sınıftır ve `Request`, `Validator`, `Handler`, `Endpoint`
tiplerini barındırır.

**Gerekçe:**
1. Bu projede değişiklikler use case bazında gelir ("koltuk tutma", "ödeme"). Vertical Slice'ta bir use case'in isteği,
   doğrulaması, iş mantığı ve endpoint'i tek dosyadadır; değişiklik tek yere dokunur.
2. Concurrency mantığı (lock → transaction → optimistic concurrency → hata eşleme) tek dosyada, yukarıdan aşağı
   okunabilir. Mülakatta "kodunu göster" denildiğinde tek dosya açılır.
3. Clean Architecture'ın 4 proje + repository + arayüz katmanı, tek geliştiricili ve tek veritabanlı bir projede
   soyutlama maliyeti getirir; EF Core zaten Unit of Work ve repository işlevi görür.
4. Domain kuralları (durum geçişleri, invariant'lar) yine zengin domain entity'lerinde tutulur; Vertical Slice "her şey
   handler'da" demek değildir (`reservation.Confirm(now)`, `seat.Hold(...)`).
5. Geri dönüş maliyeti düşüktür: dilimler ileride ayrı projelere taşınabilir.

**Reddedilen alternatif:** Clean Architecture. Katman sınırları net ama bu boyuttaki bir projede ek proje ve arayüz
sayısı okunabilirliği düşürür.

```
src/EventTicketing.Api/
├─ Common/          Result/Error, ProblemDetails, IEndpoint keşfi, ValidationFilter, sayfalama, policy'ler
├─ Domain/          Entity'ler ve durum geçişleri (Event, EventSeat, Reservation, Payment, Ticket, …)
├─ Features/        Auth, Users, Venues, Events, Reservations, Payments, Tickets — her use case bir dosya
└─ Infrastructure/  Persistence (AppDbContext, migration, seed), Auth (JWT), Payments (mock), Qr, Email
```

Beklenen hatalar `Result<T>` + makine okunur `code` ile RFC 9457 ProblemDetails'e çevrilir
(ör. `409 seat.unavailable`). Her hata yanıtı, aynı isteğin log satırındaki `TraceId` ile eşleşen bir `traceId` taşır.

## Çift satış nasıl önleniyor?

Doğruluğun kaynağı **PostgreSQL**'dir. Üç katman vardır ve alttaki katman üsttekiler olmadan da doğruluğu garanti
eder; Redis lock yalnız bir performans katmanıdır.

| Katman | Mekanizma | Görevi |
|---|---|---|
| 1. Redis lock (v2) | `lock:event-seat:{id}`, değer rastgele token, TTL 5 sn (`Locking:TtlSeconds`), `LockTakeAsync` / `LockReleaseAsync` | Sıcak koltukta kaybedenleri beklemeden ve DB'ye gitmeden reddeder. Redis yoksa atlanır (fail-open) |
| 2. Optimistic concurrency | `event_seats.xmin` → EF Core `RowVersion`; `UPDATE … WHERE xmin = @okunan` | Aynı koltuğu okuyan iki işlemden ikincisi `DbUpdateConcurrencyException` alır → `409 seat.unavailable` |
| 3. Unique constraint | `tickets.event_seat_id` UNIQUE; kullanıcı + etkinlik başına tek `Pending` rezervasyon (partial unique index) | Son savunma hattı: kod hatası olsa bile aynı koltuğa ikinci bilet yazılamaz |

Koltuk tutma akışı (`CreateReservation`):
1. (`Locking:Enabled`) Koltuk kilitleri artan `eventSeatId` sırasıyla alınır; bu sıra iki isteğin birbirini kilitlemesini
   önler. Biri alınamazsa alınanlar bırakılır ve beklemeden `409 seat.unavailable` döner. Kilitler istek sonunda yalnız
   sahibi olan token ile bırakılır.
2. Transaction açılır, etkinlik satırı `FOR SHARE` ile okunur (aynı anda iptal edilen etkinlikte koltuk tutulamaz).
3. Etkinlik satışta değilse `409 event.not_on_sale`. Kullanıcının süresi dolmamış `Pending` rezervasyonu varsa
   `409 reservation.already_pending`; süresi dolmuşsa `Expired` yapılır ve koltukları bırakılır.
4. Koltuklar takipli yüklenir. `Sold` veya süresi dolmamış `Held` koltuk varsa `409 seat.unavailable` döner ve yanıt
   `unavailableSeatIds` alanını taşır. Ya hepsi tutulur ya hiçbiri.
5. Rezervasyon eklenir, koltuklar `Held` yapılır, `SaveChanges` çalışır. Arada başka bir işlem koltuğu değiştirdiyse
   `xmin` uyuşmaz ve istek `409` alır.

**10 dakikalık tutma** bir iş kuralıdır ve DB'de (`held_until`, `expires_at`) saklanır. Süresi geçmiş bir tutma hiçbir
okumada ve yazmada geçerli sayılmaz, bu yüzden arka plan işi olmadan da doğruluk korunur. v2'deki
`ReservationExpiryService` her 30 sn'de (`Reservations:ExpirySweepIntervalSeconds`) süresi dolan `Pending`
rezervasyonları `Expired` yapıp koltuklarını bırakarak kayıtlı durumu düzenli tutar. Birden fazla API örneğinde
aynı anda çalışabilir, çünkü her güncelleme `status = 'Pending'` koşuluyla yapılır. Kanıt: 50 farklı
kullanıcı aynı koltuğa `Barrier` ile aynı anda istek atar; 10 turun her birinde tam 1 adet `201`, 49 adet `409` döner ve
DB'de o koltuğa bağlı tek `Pending` rezervasyon kalır (AC-08.3). Test üç düzende koşar:
- **Kilit açık:** kaybedenler kilitte veya koltuğu `Held` bularak `409` alır;
  `seat_hold_concurrency_conflicts_total` (DB'deki `xmin` çakışması sayacı) 0 kalır (AC-14.1).
- **Kilit kapalı:** sonuç aynıdır; bu kez kaybedenlerin bir kısmını `xmin` durdurur.
- **Redis erişilemez:** kilit her istekte `Warning` loguyla atlanır, doğruluk yine DB'de korunur (AC-14.3).

Redis lock 10 dakikalık tutmayı temsil etmez: 5 saniyelik teknik bir kritik bölge kilididir.

### Önbellek (v2)

Okuma endpoint'leri `HybridCache` ile önbelleklenir: L1 süreç belleği, L2 Redis. Yazma kararlarının hiçbiri önbellekten
okunan veriyle verilmez; koltuk tutma ve ödeme her zaman veritabanından okur.

| Veri | Anahtar | L2 / L1 TTL | Geçersizleştiren işlemler |
|---|---|---|---|
| Etkinlik listesi | `events:list:{sorgu}` | 60 sn / 10 sn | Etkinlik oluşturma, güncelleme, yayımlama, iptal, silme |
| Etkinlik detayı | `events:{id}` | 5 dk / 10 sn | Aynı |
| Koltuk haritası | `events:{id}:seats` | 5 sn / 2 sn | Tutma, rezervasyon iptali, ödeme (başarılı, reddedilen, iade), etkinlik iptali |

- Geçersizleştirme tag'lerle yapılır (`events`, `event:{id}`, `event:{id}:seats`) ve commit'ten sonra çalışır.
- Koltuk haritası koltuğun kayıtlı durumunu ve `heldUntil` değerini saklar. Süresi geçmiş tutma okuma anında
  `Available` sayılır, bu yüzden önbellekteki bir tutma da zamanında düşer.
- Redis erişilemezse okumalar L1 ve veritabanıyla `200` dönmeye devam eder. Yazmalardaki geçersizleştirme hatası
  `Warning` olarak loglanır ve isteği bozmaz; kalan girdiler TTL ile düşer. Bağlantı dizesindeki
  `abortConnect=false,connectTimeout=1000,syncTimeout=1000` sayesinde bir istek Redis için en fazla ~1 sn bekler.

### Ödeme ve idempotency

`POST /reservations/{id}/payments` bir `Idempotency-Key` (UUID) başlığı ister:
- Aynı anahtarla tekrar gelen istek kayıtlı sonucu döndürür ve ağ geçidini tekrar çağırmaz (aynı `paymentId`, aynı
  gövde). Aynı anahtar başka bir rezervasyonda kullanılırsa `422` döner.
- Ödeme başarılı olur ama onay yarışı kaybedilirse (rezervasyonun süresi doldu, iptal edildi, aynı anahtarla eşzamanlı
  istek geldi) ödeme iade edilir ve aynı anahtarla `Refunded` kaydı yazılır.
- Reddedilen ödemede rezervasyon `PaymentFailed` olur ve koltuklar serbest kalır.
- E-postayı API göndermez (SMTP'ye bağlanmaz). Ödeme aşağıdaki outbox mesajını bırakır, e-postayı Notification
  servisi gönderir; SMTP'ye ulaşılamasa da ödeme yanıtı `200`'dür.

### Outbox (v3)

Başarılı ödemede `TicketPurchased` mesajı (`EventTicketing.Contracts`) MassTransit'in EF Core outbox'ı ile ödeme,
onay ve biletlerle **aynı transaction'da** `outbox_message` tablosuna yazılır: transaction geri alınırsa (ör. onay
yarışı kaybedildi) mesaj da yoktur. Commit'ten sonra outbox teslim servisi mesajı RabbitMQ'ya iletir; RabbitMQ
kapalıyken mesaj tabloda bekler ve bağlantı gelince gönderilir. Bağlantı `ConnectionStrings:RabbitMq` (AMQP URI;
yerelde compose'daki `eventticketing` / `dev-only-rabbitmq` kullanıcısı). Gönderilen mesajlar RabbitMQ yönetim
arayüzünde (`http://localhost:15672`) `EventTicketing.Contracts:TicketPurchased` exchange'inde görünür.

Notification servisi tarafında:
- **Inbox:** consumer MassTransit'in EF inbox'ıyla çalışır; aynı `MessageId` ikinci kez gelirse consumer çağrılmaz,
  tek e-posta gider.
- **Retry:** gönderim hata verirse mesaj 1 sn, 5 sn ve 15 sn arayla 3 kez yeniden denenir, sonra
  `ticket-purchased_error` kuyruğuna düşer (her deneme `Error` seviyesinde, `ReservationId` ile loglanır).
- **Teslim garantisi en az bir kez'dir:** servis e-postayı gönderip inbox kaydını commit etmeden çökerse aynı e-posta
  ikinci kez gidebilir. Bu bilinçli olarak kabul edildi; e-posta tekrarı zararsız, kaybı değil.

### Gözlemlenebilirlik (v3)

API ve Notification servisi OpenTelemetry ile trace, metrik ve logları OTLP üzerinden compose'daki **Aspire
Dashboard**'a gönderir: `http://localhost:18888`. Development ayarlarında `OTEL_EXPORTER_OTLP_ENDPOINT` =
`http://localhost:4317`; bu anahtar boşsa dışa aktarım tamamen kapalıdır (testlerde olduğu gibi).

- **Trace:** ASP.NET Core, HttpClient, Npgsql ve MassTransit. MassTransit trace bağlamını mesaj başlıklarında taşıdığı
  için tek bir ödeme, Dashboard'da API'nin HTTP isteğini, SQL komutlarını, outbox'tan RabbitMQ'ya gönderimi ve
  Notification servisindeki tüketimi içeren **tek bir trace** olarak görünür:

  ![Bir ödemenin uçtan uca trace'i](docs/observability.png)

- **Metrikler:** ASP.NET Core, HttpClient, .NET runtime, MassTransit ve uygulamanın sayaçları:
  `tickets_sold_total`, `seat_hold_conflicts_total`, `seat_hold_concurrency_conflicts_total`,
  `reservations_expired_total`.
- **Loglar:** Serilog konsola ek olarak OTLP'ye de yazar; Dashboard'da trace ile ilişkilendirilmiş görünür.
- Redis komutları trace'e eklenmez: `OpenTelemetry.Instrumentation.StackExchangeRedis`'in kararlı sürümü yok.

### Rate limiting (bonus)

ASP.NET Core rate limiting, bir dakikalık sabit pencerelerle:
- `POST /auth/login` ve `POST /auth/register`: IP başına toplam 10 istek
- `POST /events/{id}/reservations`: kullanıcı başına 30 istek

Sınırı aşan istek `429` + `rate_limit.exceeded` ProblemDetails ve `Retry-After` başlığı (saniye) alır.
`RateLimiting:Enabled=false` ile kapatılır; integration testleri ve k6 koşuları kapalı çalışır
(k6 için `RateLimiting__Enabled=false` ortam değişkeni).

### API sürümleme (bonus)

Sürüm URL segmentindedir (`Asp.Versioning.Http`): bugün yalnız `1.0` vardır ve her yanıt
`api-supported-versions: 1.0` başlığını taşır. Desteklenmeyen bir sürüm (ör. `/api/v2/events`) `400`
`validation.failed` ProblemDetails alır. Asp.Versioning URL segmentinde bu durumda her zaman `404` döndürdüğü için
400'ü küçük bir middleware üretir. OpenAPI belgesinde yollar `/api/v1/...` olarak görünür.

### Health check (bonus)

- `GET /health/live`: bağımlılıklara bakmadan `200` (süreç ayakta mı).
- `GET /health/ready`: PostgreSQL, Redis ve RabbitMQ'dan biri erişilemezse `503`, hepsi erişilebilirse `200`.
  RabbitMQ durumu MassTransit'in bus health check'inden gelir (bus broker'a bağlı mı).

## Endpoint özeti

Tüm yollar `/api/v1` önekiyle sunulur. Tam sözleşme `/openapi/v1.json` ve Scalar UI'dadır.

| Metot | Yol | Yetki |
|---|---|---|
| POST | `/auth/register`, `/auth/login`, `/auth/refresh`, `/auth/logout` | Anonim |
| PUT | `/admin/users/{id}/role` | Admin |
| POST, PUT | `/venues`, `/venues/{id}` | Admin |
| POST | `/venues/{id}/halls` | Admin |
| GET | `/venues`, `/venues/{id}`, `/halls/{id}` | Anonim |
| POST, PUT, DELETE | `/events`, `/events/{id}` | Organizer veya Admin (+ sahiplik) |
| POST | `/events/{id}/publish`, `/events/{id}/cancel` | Organizer veya Admin (+ sahiplik) |
| GET | `/organizer/events` | Organizer veya Admin |
| GET | `/events`, `/events/{id}`, `/events/{id}/seats` | Anonim |
| POST | `/events/{id}/reservations` | Customer |
| GET | `/reservations/{id}` | Customer (sahibi) veya Admin |
| DELETE | `/reservations/{id}` | Customer (sahibi) |
| POST | `/reservations/{id}/payments` | Customer (sahibi) |
| GET | `/me/reservations`, `/me/tickets` | Customer |
| GET | `/tickets/{id}/qr` | Customer (sahibi) veya Admin |

Başkasına ait rezervasyon veya bilete erişim `404` döner; böylece kaydın var olup olmadığı sızmaz.

## Testler

```bash
dotnet test tests/EventTicketing.UnitTests          # dış bağımlılık gerektirmez
dotnet test tests/EventTicketing.IntegrationTests   # docker compose up -d (PostgreSQL, Redis, Mailpit, RabbitMQ) gerektirir
dotnet test --filter "AC=AC-08.3"                   # tek bir kabul kriterinin testleri
dotnet test --collect:"XPlat Code Coverage"         # Cobertura raporu TestResults/ altına
```

- Her kabul kriteri en az bir testle doğrulanır ve test `[Trait("AC", "AC-xx.y")]` taşır.
- Integration testleri gerçek PostgreSQL kullanır (EF InMemory/SQLite yok). Çalıştırma başında compose'daki sunucuda
  geçici bir `eventticketing_test_<guid>` veritabanı oluşturulur, sonunda silinir. Her test Respawn ile temiz DB'den başlar.
- Zaman `FakeTimeProvider` ile ilerletilir (`Task.Delay` ile beklenmez). JWT doğrulaması da aynı `TimeProvider`'ı
  kullanır.
- Bağlantılar ortam değişkenleriyle değiştirilebilir: `EVENTTICKETING_TEST_POSTGRES` (varsayılan
  `Host=localhost;Port=5432;Username=postgres;Password=dev-only-postgres`), `EVENTTICKETING_TEST_REDIS`
  (varsayılan `localhost:6379`), `EVENTTICKETING_TEST_MAILPIT` (varsayılan `http://localhost:8025`),
  `EVENTTICKETING_TEST_RABBITMQ` (varsayılan `amqp://eventticketing:dev-only-rabbitmq@localhost:5672/`). Her test
  koşusu Redis'te kendi anahtar önekini kullanır. Paylaşılan test host'unda outbox teslim servisi kapalıdır;
  mesajlar `outbox_message` tablosunda incelenir. E-posta ve mesajlaşma testleri teslimi açık ayrı bir API host'u ve
  süreç içinde başlatılan Notification servisiyle uçtan uca çalışır (API → RabbitMQ → Notification → Mailpit).
  Her test koşusu RabbitMQ'da kendi sanal host'unu (`eventticketing-test-<guid>`) açar ve sonunda siler; bu yüzden
  testler, aynı anda çalışan geliştirme Notification servisinden etkilenmez.

Son ölçüm (2026-10-14): 71 unit + 278 integration testi; `EventTicketing.Api` birleşik satır kapsamı **%91,7** (üretilmiş migration dosyaları hariç; hedef ≥ %70, NFR-05).

### CI

[`.github/workflows/ci.yml`](.github/workflows/ci.yml) `main`'e her push'ta ve her PR'da `ubuntu-latest` üzerinde
çalışır; adımlardan biri başarısızsa iş kırmızıdır:

1. `dotnet format --verify-no-changes`
2. Release build (uyarılar hata sayılır)
3. Unit ve integration testleri, coverage toplanarak. PostgreSQL, Redis, RabbitMQ ve Mailpit, `compose.yaml` ile aynı
   imaj sürümlerinde service container olarak çalışır.
4. Coverage eşiği: ReportGenerator raporları birleştirir, [`scripts/check-coverage.sh`](scripts/check-coverage.sh)
   `EventTicketing.Api` satır kapsamı %70'in altındaysa işi kırar. Özet, işin "Summary" sayfasında görünür.
5. API ve Notification servisinin `dotnet publish -c Release` çıktısı

Aynı eşik kontrolü yerelde de çalışır:

```bash
dotnet test -c Release --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet reportgenerator "-reports:TestResults/**/coverage.cobertura.xml" -targetdir:TestResults/coverage \
  "-reporttypes:TextSummary;Cobertura" -assemblyfilters:+EventTicketing.Api "-filefilters:-*/Migrations/*"
./scripts/check-coverage.sh TestResults/coverage/Cobertura.xml 70
```

## Lisans

[MIT](LICENSE)
