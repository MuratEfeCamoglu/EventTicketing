# EventTicketing

Etkinlik ve bilet rezervasyon API'si. Kullanıcı etkinlik arar, koltuğu 10 dakika tutar, mock ödeme yapar, QR kodlu
bilet ve e-posta alır.

Projenin amacı, gerçek backend'lerin zor problemlerini çalışan kod ve testlerle göstermek: eşzamanlı koltuk alımı,
idempotent ödeme, zaman aşımına uğrayan rezervasyon. Mülakattaki "İki kullanıcı aynı koltuğu aynı anda alırsa ne olur?"
sorusunun cevabı [`CreateReservation.cs`](src/EventTicketing.Api/Features/Reservations/CreateReservation.cs) ve onu
50 paralel istekle 10 tur deneyen
[`SeatHoldConcurrencyTests.cs`](tests/EventTicketing.IntegrationTests/Features/Reservations/SeatHoldConcurrencyTests.cs)
dosyalarındadır.

> Durum: **MVP** tamam (ISKELET.md §9, M1–M9); **v2** tamam (Redis + HybridCache, Redis lock, arka plan
> temizleyicisi, tek komutla yerel çalıştırma). Outbox + bildirim servisi ve CI sonraki görevlerdedir.

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
yalnız `127.0.0.1`'e açılır):

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
# Bilet e-postası: http://localhost:8025
```

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
- E-posta commit'ten sonra best-effort gönderilir. SMTP'ye ulaşılamazsa ödeme yanıtı yine `200` olur ve hata loglanır.
  v3'te outbox ile güvenilir hale getirilecek.

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
dotnet test tests/EventTicketing.IntegrationTests   # docker compose up -d (PostgreSQL + Redis + Mailpit) gerektirir
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
  (varsayılan `localhost:6379`), `EVENTTICKETING_TEST_MAILPIT` (varsayılan `http://localhost:8025`). Her test
  koşusu Redis'te kendi anahtar önekini kullanır.

Son ölçüm (2026-10-12): 70 unit + 253 integration testi; `EventTicketing.Api` birleşik satır kapsamı **%92,2** (üretilmiş migration dosyaları hariç; hedef ≥ %70, NFR-05).

## Lisans

[MIT](LICENSE)
