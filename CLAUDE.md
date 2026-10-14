# CLAUDE.md — EventTicketing

Etkinlik ve bilet rezervasyon API'si: kullanıcılar etkinlik arar, koltuğu 10 dakika tutar, mock ödeme yapar,
QR'lı bilet ve e-posta alır. Odak; eşzamanlı koltuk alımı, ödeme akışı, zaman aşımı, cache ve mesaj güvenilirliği.

- Ürün, kapsam, kabul kriterleri, mimari, veri modeli, plan → **ISKELET.md** (tek doğruluk kaynağı)
- Agent yetkileri, görev döngüsü, "bitti" tanımı → **AGENT.md**
- Bu dosya: yığın, komutlar, kod ve çalışma kuralları.

**Durum:** MVP ve v2 tamam; sıradaki görev ISKELET.md §9'daki ilk işaretsiz kutu. Aşama etiketi olan komutlar
o aşamadan itibaren çalışır.

## Teknoloji yığını
- .NET 10 (`net10.0`, C# 14), ASP.NET Core Minimal API, Vertical Slice (ISKELET §5.1, §5.3)
- EF Core + Npgsql, PostgreSQL 17, optimistic concurrency `xmin` (`RowVersion`)
- FluentValidation, ProblemDetails + `IExceptionHandler`, Serilog
- v2: Redis 7 (`HybridCache`, distributed lock), `BackgroundService` sweeper, tek komutla yerel çalıştırma
- v3: RabbitMQ 4 + MassTransit 8.x (EF outbox), Notification Worker, OpenTelemetry → Aspire Dashboard, GitHub Actions
- Bağımlı servisler `compose.yaml` ile Docker'da; API `dotnet run` ile çalışır, `Dockerfile` yok (ISKELET K-10)
- Test: xUnit v3, compose PostgreSQL'inde geçici test DB'si, Respawn, Shouldly, NSubstitute, `FakeTimeProvider`
- Paket listesi ve yasaklı paketler: ISKELET §8.1, K-03

## Önemli yollar
- `src/EventTicketing.Api/Features/<Alan>/<UseCase>.cs` — bir use case = bir dosya
- `src/EventTicketing.Api/Domain/` — entity'ler ve durum geçişleri
- `src/EventTicketing.Api/Infrastructure/Persistence/` — `AppDbContext`, konfigürasyonlar, migration'lar, seed
- `tests/EventTicketing.UnitTests/`, `tests/EventTicketing.IntegrationTests/`, `tests/load/`
- `docs/decisions.md` — dokümanlarda olmayan kararların günlüğü

## Kurulum (bir kez)
```bash
dotnet --version                     # 10.0.x olmalı
docker compose version               # Docker Desktop (WSL 2) çalışıyor olmalı
dotnet tool restore                  # dotnet-ef
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/EventTicketing.Api
```

## Geliştirme
```bash
docker compose up -d                           # PostgreSQL, Redis, Mailpit, RabbitMQ, Aspire Dashboard
dotnet run --project src/EventTicketing.Api    # http://localhost:8080 · Scalar: /scalar · OpenAPI: /openapi/v1.json
dotnet watch --project src/EventTicketing.Api  # hot reload
```
Development ortamında migration'lar ve seed başlangıçta uygulanır (`Database:MigrateOnStartup`).
Seed kullanıcıları: `appsettings.Development.json` → `Seed` bölümü.

## Migration
```bash
dotnet ef migrations add <PascalCaseAd> --project src/EventTicketing.Api --output-dir Infrastructure/Persistence/Migrations
dotnet ef database update --project src/EventTicketing.Api
dotnet ef migrations remove --project src/EventTicketing.Api   # yalnız henüz commit edilmemiş son migration için
```

## Test
```bash
dotnet test tests/EventTicketing.UnitTests                 # dış bağımlılık gerektirmez
dotnet test tests/EventTicketing.IntegrationTests          # yerel PostgreSQL gerektirir
dotnet test                                                # hepsi
dotnet test --filter "AC=AC-08.3"                          # tek kabul kriteri
dotnet test --collect:"XPlat Code Coverage"                # coverage (Cobertura, TestResults/ altına)
```

## Build ve kalite
```bash
dotnet build -c Release                  # 0 uyarı zorunlu (TreatWarningsAsErrors)
dotnet format --verify-no-changes        # biçim kontrolü; düzeltmek için: dotnet format
```

## Tüm sistem (v2+) ve yük testi (bonus)
```bash
dotnet run --project src/EventTicketing.Api            # API: :8080 · Mailpit: :8025 · RabbitMQ: :15672 (v3) · Dashboard: :18888 (v3)
./scripts/smoke.sh                                     # AC-16.1 doğrulaması
k6 run tests/load/browse.js && k6 run tests/load/hot-seat.js
```

## Kod kuralları
1. Dosya kapsamlı namespace, `Nullable` açık, `sealed` varsayılan, DTO'lar `record`.
2. Her use case tek dosyada statik sınıf: `Request`, `Response`, `Validator`, `Handler`, `Endpoint` (ISKELET §5.3).
3. Bir dilim başka dilimin `Handler`'ını çağırmaz; ortak mantık `Domain/` entity metodu veya `Infrastructure/` servisidir.
4. Repository yok; handler `AppDbContext` kullanır. Okumalar `AsNoTracking()` + `Select` projeksiyonu.
5. Durum değişiklikleri yalnız domain metotlarıyla (`reservation.Confirm(now)`); setter'lar `private`.
6. Beklenen hata → `Result<T>` + `Error(code)`; hata kodları ISKELET §5.12 tablosundan. Yeni kod önce tabloya eklenir.
7. Exception yalnız beklenmeyen durumlar için; exception yutulmaz (`catch { }` yasak).
8. Şimdiki zaman yalnız `TimeProvider` ile; `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.UtcNow` yasak.
9. Tüm I/O `async`, `CancellationToken` sonuna kadar iletilir; `.Result` / `.Wait()` yasak.
10. Ham SQL gerekiyorsa `FromSql`/`ExecuteSql` interpolasyonlu parametre ile; string birleştirme yasak.
11. Para `decimal`; yuvarlama yapılmaz (fiyatlar zaten 2 hane).
12. Log: message template (`logger.LogInformation("Reservation {ReservationId} created", id)`), interpolasyon yok.
    Parola, token, `paymentToken`, tam e-posta loglanmaz (AC-18.4).
13. Yapılandırma `IOptions<T>` + `ValidateDataAnnotations().ValidateOnStart()`; sihirli sayı yok (ISKELET §5.14).
14. DTO ↔ entity eşlemesi elle; AutoMapper/MediatR yok.
15. Yeni NuGet paketi yalnız ISKELET §8.1'den, sürüm `Directory.Packages.props`'ta.
16. Public API değişikliği (yol, alan adı, durum kodu) ISKELET §5.13/§5.12 ile birebir uyumlu olmalı.

## Test kuralları
1. Her AC için en az bir test; test `[Trait("AC", "AC-xx.y")]` taşır.
2. Test adı: `Metot_Senaryo_BeklenenSonuç` (ör. `CreateReservation_SeatAlreadyHeld_Returns409`).
3. Integration testleri gerçek PostgreSQL (yerel sunucu, fixture'ın oluşturduğu geçici DB) kullanır; EF InMemory/SQLite yasak.
4. Zaman `FakeTimeProvider` ile ilerletilir; `Task.Delay` ile süre beklenmez.
5. Concurrency testleri istekleri `Barrier` ile aynı anda başlatır.
6. Integration testleri tek `[Collection("Api")]` içinde sıralı çalışır (tek test DB'si); her test sınıfı Respawn
   ile temiz DB'den başlar; sıra bağımlılığı yok. Test ortamı: `RateLimiting:Enabled=false`, `MockPayment:LatencyMs=0`.
7. Unit testlerde dış bağımlılık NSubstitute ile; assertion'lar Shouldly ile.

## Çalışma kuralları
- Görev sırası ISKELET §9; görev bitince ilgili kutu `[x]` yapılır.
- Dal adı `feat/<görev-id>-<kısa-ad>` (ör. `feat/M6-seat-hold`), `main`'den açılır; görev bitince yerel `main`'e
  `--ff-only` merge (push onay ister, AGENT.md §4). Commit'ler Conventional Commits
  (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`), mesajda AC/görev kimliği.
- Commit öncesi: build (0 uyarı) + `dotnet format --verify-no-changes` + ilgili testler yeşil.
- Commit edilmiş migration düzenlenmez; değişiklik yeni migration ile yapılır.
- Dokümanlarda olmayan her karar `docs/decisions.md`'ye tarih + gerekçe ile yazılır.
- Gerçek gizli değerler yalnız user-secrets veya ortam değişkeni; repoda yalnız `dev-only-` önekli sahte
  değerler bulunabilir (`appsettings.Development.json`, test varsayılanları) — tanım ISKELET §5.14.

## Tuzaklar
- `Held` koltuk `held_until ≤ now` ise müsaittir — her sorguda bu kural uygulanır (ISKELET §5.5).
- `RowVersion` = Npgsql `xmin`; migration'da sütun oluşmaz, normaldir.
- JWT `nbf/exp` doğrulaması `TimeProvider` ile yapılır; aksi halde `FakeTimeProvider` ilerletilen testlerde token'lar
  reddedilir (ISKELET §5.11).
- Redis lock doğruluk kaynağı değildir; Redis yokken akış DB korumasıyla devam eder (ISKELET §5.4).
- MassTransit 9+ ticari lisanslıdır; 8.x'te kalınır.
- Integration testleri compose servisleri (PostgreSQL, Redis, Mailpit) çalışmıyorsa çalışmaz; PostgreSQL bağlantısı
  `EVENTTICKETING_TEST_POSTGRES` ortam değişkeninden okunur (yoksa
  `Host=localhost;Port=5432;Username=postgres;Password=dev-only-postgres`). Hata alırsan önce `docker compose ps`.
