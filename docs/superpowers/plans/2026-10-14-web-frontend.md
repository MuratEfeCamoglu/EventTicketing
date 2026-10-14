# Web ön yüzü — uygulama planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** EventTicketing backend'ini tarayıcıdan (masaüstü + telefon) müşteri, organizatör ve admin olarak uçtan uca denemeyi sağlayan, profesyonel görünümlü Türkçe bir web ön yüzü.

**Architecture:** Kökte `web/` altında Vite + React + TypeScript SPA. Geliştirme sunucusu `/api` ve `/health` isteklerini `http://localhost:8080`'e proxy'ler (backend'de CORS yok). Sunucu durumu TanStack Query ile, yönlendirme React Router ile, görsel sistem Tailwind CSS ile kurulur. Backend'e tek ek: `GET /api/v1/organizer/events/{id}`.

**Tech Stack:** React 19.3.0, react-dom 19.3.0, react-router 8.4.0, @tanstack/react-query 5.104.2, Vite 8.3.4, @vitejs/plugin-react 6.1.2, TypeScript 6.0.3, tailwindcss 4.3.3 + @tailwindcss/vite 4.3.3, lucide-react 1.57.0; test: vitest 5.0.4, jsdom 30.1.2, @testing-library/react 16.3.3, @testing-library/dom 10.x, @testing-library/user-event 14.6.8, @testing-library/jest-dom 7.0.1; lint: eslint 10.12.0, @eslint/js 10.0.1, typescript-eslint 8.71.2, eslint-plugin-react-hooks 7.1.1, globals 17.13.0. Backend: .NET 10 (mevcut).

**Spec:** `docs/superpowers/specs/2026-10-14-web-frontend-design.md`

## Global Constraints

- Arayüz metinleri Türkçe; para `tr-TR` biçiminde TRY (`450,00 ₺`), tarihler `tr-TR`, kullanıcının saat diliminde.
- Görsel yön B: beyaz zemin (`#f7f8fa` sayfa, `#ffffff` yüzey), tek mavi vurgu (`#2563eb`), ince kenarlık (`#e6e9ef`), metin `#1b2230`, ikincil metin `#6b7385`; durum renkleri: tutuldu `#fde68a`, satıldı `#d7dbe3`, başarı `#137a4b`/`#e2f6ee`, hata `#b42318`/`#fde8e8`.
- Responsive: 360 px genişlikte hiçbir sayfa yatay taşmaz; koltuk haritası kendi kabında kaydırılır.
- Backend değişikliği yalnız `GET /api/v1/organizer/events/{id}` (OrganizerOrAdmin + sahiplik, başkasının etkinliği 404, Admin hepsini görür, her durumdaki etkinlik için `EventResponse`).
- Koltuk tutma ve ödeme yalnız Customer rolü; rezervasyon başına en fazla 6 koltuk; tutma süresi backend'in `expiresAt` değeri.
- Ödeme token'ları: `tok_success`, `tok_declined`; her ödeme denemesi bir `Idempotency-Key` (UUID) başlığı taşır.
- Seed kullanıcıları: `customer@example.com`, `organizer@example.com`, `admin@example.com`, parola `Dev-only-Passw0rd`.
- Salon: 1–5.000 koltuk; bölüm adları ve bir bölümdeki sıra adları benzersiz.
- npm sürümleri `package.json`'da tam sürüm (`^`/`~` yok), `package-lock.json` commit edilir; `node_modules/`, `web/dist/`, `.superpowers/` git'e girmez.
- "Bitti" (ön yüz): `npm run lint`, `npm run typecheck`, `npm test`, `npm run build` hatasız. (.NET): Release build 0 uyarı, `dotnet format --verify-no-changes`, tüm testler yeşil.
- Dal adı `feat/W-<n>-<ad>`, `main`'e `--ff-only`; Conventional Commits, mesajda görev kimliği; push onaylı.
- Proje kuralları: CLAUDE.md (backend kod ve test kuralları), AGENT.md görev döngüsü; kararlar `docs/decisions.md`'ye.

## Review Focus

1. Access token (15 dk) ödeme sayfasındayken dolar → istek bir kez `/auth/refresh` ile yenilenip tekrarlanır; refresh de başarısızsa kullanıcı dönüş adresiyle girişe gider (Task 3 testi).
2. Ödeme düğmesine iki kez basılır veya ağ hatası sonrası tekrar denenir → aynı `Idempotency-Key` kullanılır, tek tahsilat (Task 6 testi).
3. Geri sayım sıfıra iner ya da istemci saati sunucudan farklıdır → süre yalnız sunucunun `expiresAt`'inden hesaplanır, 0'da ödeme devre dışı ve rezervasyon yeniden okunur (Task 6 testi).
4. 5.000 koltuklu salon / 60 koltuklu sıra, 360 px telefonda → sayfa taşmaz, harita kendi kabında kaydırılır, ilk çizim makul sürede (Task 5 testi).
5. Sıra etiketleri alfabetik olmayan ("AA", "10"), numaraları sırasız gelen veya tek sayıda koltuklu sıralar → numaraya göre sıralı, koridor deterministik bölünür (Task 5 testi).

---

### Task 1 (W-1): Backend — taslak etkinlik okuma uç noktası + ISKELET kapsamı

**Files:**
- Create: `src/EventTicketing.Api/Features/Events/GetOrganizerEvent.cs`
- Create: `tests/EventTicketing.IntegrationTests/Features/Events/OrganizerEventTests.cs`
- Modify: `tests/EventTicketing.IntegrationTests/Features/Auth/EndpointAuthorizationMatrix.cs` (yeni satır)
- Modify: `ISKELET.md` (§1 "Frontend yoktur", §3.3 tablo satırı, yeni F-21 + AC-21.x, §5.13 satırı, §7 ağacına `web/`, §8.1 npm tablosu, §9 yeni "Aşama 5 — Ön yüz" W-1…W-8)
- Modify: `.gitignore` (`node_modules/`, `web/dist/`, `.superpowers/`)
- Modify: `docs/decisions.md`

**Interfaces:**
- Produces: `GET /api/v1/organizer/events/{id:guid}` → `200 EventResponse` (`id, organizerId, hallId, title, description, category, startsAt, endsAt, status, sectionPrices[{section, price}]`), `404 resource.not_found`, `401`, `403`.

- [ ] **Step 1: Write the failing tests** (`OrganizerEventTests`, `[Trait("AC", "AC-21.1")]`)
  - `GetOrganizerEvent_OwnDraft_Returns200WithEditableFields`: organizatör `CreateEventAsync` ile taslak oluşturur → 200; `status == "Draft"`, `description`, `hallId`, `sectionPrices` oluşturma isteğindekiyle aynı.
  - `GetOrganizerEvent_OtherOrganizersEvent_Returns404`.
  - `GetOrganizerEvent_Admin_Returns200ForAnyEvent`.
  - `GetOrganizerEvent_Cancelled_Returns200` (iptal edilmiş etkinlik de okunur).
  - Yetki matrisine `GET /api/v1/organizer/events/{id}` → OrganizerOrAdmin satırı (AC-03.1 testleri Customer 403, anonim 401'i kapsar).
- [ ] **Step 2: Run** `dotnet test tests/EventTicketing.IntegrationTests --filter "AC=AC-21.1|FullyQualifiedName~AuthorizationMatrixTests"` → FAIL (404/eksik route).
- [ ] **Step 3: Implement `GetOrganizerEvent`** dilimi (statik sınıf: `Handler.HandleAsync(Guid id, ClaimsPrincipal user, CancellationToken)` → `Result<EventResponse>`, `Endpoint` → `app.MapGet("/organizer/events/{id:guid}", …).RequireAuthorization(Policies.OrganizerOrAdmin).WithTags("Events")`). Sahiplik `Event.EnsureCanBeChangedBy` ile aynı kural ama ihlalde `DomainErrors.NotFound`. Okuma `AsNoTracking`, `EventResponse.From`.
- [ ] **Step 4: Run** aynı filtre → PASS; ardından tüm .NET testleri, Release build, `dotnet format --verify-no-changes`.
- [ ] **Step 5: ISKELET/README/decisions/.gitignore** güncellemeleri (Files listesindeki gibi); §9'a W-1…W-8 kutuları, W-1 `[x]`.
- [ ] **Step 6: Commit** `feat(W-1): organizer event read endpoint for editing drafts [AC-21.1]`; `main`'e ff-merge; push.

### Task 2 (W-2): Proje iskeleti, görsel sistem, uygulama kabuğu, CI

**Files:**
- Create: `web/package.json`, `web/package-lock.json`, `web/vite.config.ts`, `web/tsconfig.json`, `web/eslint.config.js`, `web/index.html`, `web/src/main.tsx`, `web/src/index.css`
- Create: `web/src/app/router.tsx`, `web/src/app/AppShell.tsx` (üst menü + mobil menü + içerik), `web/src/app/NotFoundPage.tsx`
- Create: `web/src/ui/` → `Button.tsx`, `Field.tsx` (etiket + input/select/textarea + hata), `Badge.tsx`, `Card.tsx`, `Modal.tsx` (onay penceresi), `Toast.tsx` (`useToast()`), `Spinner.tsx`, `EmptyState.tsx`, `PageHeader.tsx`
- Create: `web/src/test/setup.ts`, `web/src/app/AppShell.test.tsx`
- Modify: `.github/workflows/ci.yml` (yeni `web` işi), `README.md` ("Ön yüz" bölümü)

**Interfaces:**
- Produces: npm betikleri `dev`, `build`, `preview`, `lint`, `typecheck` (`tsc -b --noEmit`), `test` (`vitest run`); Vite proxy `/api` ve `/health` → `http://localhost:8080`; geliştirme portu `5173`.
- Produces: UI bileşenleri — `Button({variant: 'primary'|'secondary'|'danger'|'ghost', size?: 'sm'|'md', loading?: boolean, ...buttonProps})`, `Field({label, error?, hint?, children})`, `Badge({tone: 'neutral'|'info'|'success'|'warning'|'danger', children})`, `Modal({open, title, children, confirmLabel, tone?, onConfirm, onClose})`, `useToast(): { success(msg), error(msg) }`.
- Produces: `AppShell` menü öğeleri role göre (Task 3'ün `useAuth()`'u gelene kadar sabit "Giriş" gösterir).

- [ ] **Step 1:** `web/` iskeleti, Tech Stack'teki tam sürümlerle `package.json`; Tailwind 4 (`@import "tailwindcss"` + `@theme` ile Global Constraints renkleri ve `--font-sans: Inter, system-ui, …`); Vitest jsdom ortamı + jest-dom setup.
- [ ] **Step 2: Write the failing test** `AppShell.test.tsx`: `renders brand and primary navigation` (logo "EventTicketing", "Etkinlikler" bağlantısı), `opens mobile menu when the menu button is pressed` (`aria-expanded` false → true).
- [ ] **Step 3: Run** `npm test` → FAIL; **Step 4:** `AppShell`, router (`/` ana sayfa yer tutucu, `*` → NotFound), UI bileşenleri; **Step 5:** `npm test` → PASS.
- [ ] **Step 6:** CI'a `web` işi: `ubuntu-latest`, `actions/setup-node@v6` (node 22, npm cache, `web/package-lock.json`), `working-directory: web`, `npm ci` → `npm run lint` → `npm run typecheck` → `npm test` → `npm run build`; mevcut .NET işi değişmez. README'ye `cd web && npm install && npm run dev` (backend 8080'de çalışırken) ve `http://localhost:5173`.
- [ ] **Step 7:** Dört npm betiği yerelde yeşil → commit `feat(W-2): web app skeleton, design system and CI job`, merge, push, CI badge yeşil.

### Task 3 (W-3): API istemcisi, hata eşleme, oturum, giriş/kayıt

**Files:**
- Create: `web/src/api/client.ts`, `web/src/api/errors.ts`, `web/src/api/types.ts` (backend DTO'larının TS tipleri: `EventListItem`, `EventDetail` (=EventResponse), `SeatDto`, `HallDetail`, `VenueDetail`, `Reservation`, `PaymentResult`, `TicketItem`, `Paged<T>`)
- Create: `web/src/auth/session.ts` (token saklama + JWT çözme), `web/src/auth/AuthProvider.tsx`, `web/src/auth/RequireRole.tsx`
- Create: `web/src/pages/LoginPage.tsx`, `web/src/pages/RegisterPage.tsx`
- Test: `web/src/api/client.test.ts`, `web/src/api/errors.test.ts`, `web/src/auth/session.test.ts`

**Interfaces:**
- Produces: `api<T>(path: string, init?: RequestInit & { json?: unknown; idempotencyKey?: string }): Promise<T>` — `/api/v1` önekini ekler, `Authorization: Bearer`, JSON gövde, `204` → `undefined`; hata → `ApiError`.
- Produces: `apiBlob(path: string): Promise<Blob>` (QR için, aynı yetki/yenileme).
- Produces: `class ApiError extends Error { status: number; code: string; fieldErrors: Record<string,string[]>; traceId?: string; retryAfterSeconds?: number; extensions: Record<string, unknown> }` ve `messageFor(error: ApiError): string` (Türkçe).
- Produces: `decodeSession(accessToken: string): { userId: string; email: string; role: 'Customer'|'Organizer'|'Admin'; expiresAt: Date }` (claim'ler `sub`, `email`, `role`, `exp`).
- Produces: `useAuth(): { session: Session | null; login(email, password): Promise<void>; register(input: {email, password, fullName}): Promise<void>; logout(): Promise<void> }`; `<RequireRole roles={[...]}>` (yoksa `/giris?donus=<yol>`).

- [ ] **Step 1: Write the failing tests**
  - `client.test.ts`: `retries once with a refreshed token after 401` (ilk çağrı 401, `/auth/refresh` 200, tekrar 200 → sonuç döner, refresh tam 1 kez); `clears the session and rejects when refresh fails` (refresh 401 → `ApiError` status 401, oturum boş); `concurrent 401s share one refresh`; `sends Idempotency-Key when given`.
  - `errors.test.ts`: `maps known codes to Turkish` (`seat.unavailable` → "Seçtiğiniz koltuklardan biri az önce başkası tarafından alındı."), `uses Retry-After for rate_limit.exceeded` ("Çok fazla deneme. 42 sn sonra tekrar deneyin."), `falls back with traceId for unknown codes`, `keeps validation field errors`.
  - `session.test.ts`: `decodes sub, email, role and exp`.
- [ ] **Step 2: Run** `npm test` → FAIL.
- [ ] **Step 3: Implement.** Token'lar `localStorage`'da (`et.session`); refresh tek uçuşta paylaşılan promise. Hata kodu → mesaj tablosu (ISKELET §5.12 kodlarının hepsi):

| Kod | Mesaj |
|---|---|
| `validation.failed` | Lütfen işaretli alanları düzeltin. |
| `auth.invalid_credentials` | E-posta veya parola hatalı. |
| `auth.email_taken` | Bu e-posta ile zaten bir hesap var. |
| `auth.refresh_token_invalid` / `_expired` / `_reused`, `auth.unauthorized` | Oturumunuz sona erdi, lütfen tekrar giriş yapın. |
| `auth.forbidden` | Bu işlem için yetkiniz yok. |
| `user.cannot_assign_admin` | Admin rolü bu ekrandan verilemez. |
| `resource.not_found` | Aradığınız kayıt bulunamadı. |
| `event.not_owner` | Bu etkinlik size ait değil. |
| `event.invalid_state` | Etkinlik bu işlem için uygun durumda değil. |
| `event.has_sold_tickets` | Satılmış bileti olan etkinlik silinemez. |
| `event.not_on_sale` | Bu etkinlik şu an satışta değil. |
| `event.missing_section_price` | Salondaki her bölüm için fiyat girin. |
| `seat.not_in_event` | Seçilen koltuk bu etkinliğe ait değil. |
| `seat.unavailable` | Seçtiğiniz koltuklardan biri az önce başkası tarafından alındı. |
| `reservation.already_pending` | Bu etkinlik için bekleyen bir rezervasyonunuz var. |
| `reservation.expired` | Rezervasyon süresi doldu; koltuklar serbest bırakıldı. |
| `reservation.invalid_state` | Rezervasyon bu işlem için uygun durumda değil. |
| `payment.declined` | Ödeme reddedildi; koltuklar serbest bırakıldı. |
| `payment.idempotency_key_reused` | Bu ödeme isteği başka bir rezervasyonda kullanılmış. |
| `concurrency.conflict` | Kayıt aynı anda değişti, lütfen sayfayı yenileyip tekrar deneyin. |
| `rate_limit.exceeded` | Çok fazla deneme. {n} sn sonra tekrar deneyin. |
| `server.error` / bilinmeyen | Beklenmeyen bir hata oluştu (iz: {traceId}). |

  Giriş sayfası: form + "Demo hesapları" bölümünde üç tek tık düğmesi (Global Constraints'teki hesaplar); başarıda `donus` parametresine yönlendirir. Kayıt sayfası: e-posta, ad soyad, parola; alan hataları `fieldErrors`'tan. AppShell menüsü role göre (Customer: Biletlerim, Rezervasyonlarım; Organizer/Admin: Yönetim) + profil menüsünde e-posta, rol, "Kimliğimi kopyala" (`userId`), Çıkış.
- [ ] **Step 4: Run** `npm test` → PASS; dört betik yeşil.
- [ ] **Step 5: Commit** `feat(W-3): API client with token refresh, Turkish errors, login and register`; merge; push.

### Task 4 (W-4): Etkinlik listesi ve etkinlik sayfası başlığı

**Files:**
- Create: `web/src/lib/format.ts`, `web/src/events/EventsPage.tsx`, `web/src/events/EventCard.tsx`, `web/src/events/EventFilters.tsx`, `web/src/events/EventPage.tsx` (başlık + bilgiler; harita Task 5'te), `web/src/events/queries.ts`
- Test: `web/src/lib/format.test.ts`, `web/src/events/queries.test.ts`

**Interfaces:**
- Produces: `formatPrice(value: number): string` (`450` → `"450,00 ₺"`), `formatDateTime(iso: string): string` (`"13 Kasım 2026 Cuma, 20:00"`), `formatDateShort(iso)` (`"13 Kas"`), `categoryLabel(c: 'Concert'|'Theatre'|'Sports'|'Conference'|'Other')` → Konser, Tiyatro, Spor, Konferans, Diğer (backend `EventCategory` enum'u birebir).
- Produces: `eventsQueryString(filters: { city?: string; category?: string; from?: string; to?: string; q?: string; sort?: string; page?: number; pageSize?: number }): string` (boş alanlar atlanır), `useEvents(filters)`, `useEvent(id)` (EventResponse + `GET /halls/{hallId}` + `GET /venues/{venueId}` birleşik: `{ event, hall, venue }`).

- [ ] **Step 1: Write the failing tests**: `formatPrice` (`450`, `1250.5` → `"1.250,50 ₺"`), `formatDateTime` sabit bir ISO + `Europe/Istanbul` ile, `eventsQueryString({city:'İstanbul', q:'', page:2})` → `"?city=%C4%B0stanbul&page=2"`.
- [ ] **Step 2:** FAIL → **Step 3:** implement; liste sayfası (kart ızgarası: masaüstü 3, tablet 2, telefon 1 sütun; filtreler masaüstünde üstte satır, telefonda "Filtreler" çekmecesi; sayfalama; yükleniyor iskeletleri; boş durum), etkinlik sayfası başlığı (başlık, kategori etiketi, tarih, mekân · salon · şehir, açıklama, bölüm fiyatları). Yayında olmayan etkinlik 404 → "Etkinlik bulunamadı" sayfası. → **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(W-4): event list with filters and event page`; merge; push.

### Task 5 (W-5): Sinema tarzı koltuk haritası ve koltuk tutma

**Files:**
- Create: `web/src/seating/layout.ts`, `web/src/seating/SeatMap.tsx`, `web/src/seating/Seat.tsx`, `web/src/seating/SelectionSummary.tsx` (masaüstü panel + mobil yapışık çubuk), `web/src/seating/useSeatSelection.ts`
- Modify: `web/src/events/EventPage.tsx` (haritayı ve tutma akışını ekler)
- Test: `web/src/seating/layout.test.ts`, `web/src/seating/useSeatSelection.test.ts`, `web/src/seating/SeatMap.test.tsx`

**Interfaces:**
- Consumes: `api`, `ApiError`, `messageFor`, `useAuth`, `formatPrice`, `useEvent` (hall section order).
- Produces: `buildSeatLayout(seats: SeatDto[], sectionOrder: string[]): LayoutSection[]` where `LayoutSection = { name: string; price: number; rows: LayoutRow[] }`, `LayoutRow = { label: string; left: SeatDto[]; right: SeatDto[] }`.
- Produces: `useSeatSelection(max = 6): { selected: SeatDto[]; toggle(seat): 'added'|'removed'|'limit'; clear(); }`.
- Produces: route `/etkinlik/:id` haritalı; başarılı tutma → `/rezervasyon/:reservationId`.

- [ ] **Step 1: Write the failing tests**
  - `layout.test.ts`: `orders sections by the given section order` (`sectionOrder` ['Salon','Balkon'] → bu sırada; `GET /halls/{id}` bölümleri ada göre sıralı döndürür); `orders rows numeric-aware and seats by number` (satırlar "10","2","1" → 1, 2, 10; "B","A","AA" → A, AA, B; `localeCompare(…, 'tr', { numeric: true })`); `splits a row at the middle with the extra seat on the left` (7 koltuk → left 4, right 3; 8 → 4/4); `uses the section price`.
  - `useSeatSelection.test.ts`: `toggles seats`, `returns 'limit' on the seventh seat and does not add it`.
  - `SeatMap.test.tsx`: `held and sold seats are not selectable` (disabled + aria-label "Balkon C-7, satıldı"); `renders a 5000-seat hall` (50 sıra × 100 koltuk; render < 1500 ms, kabın `overflow` değeri `auto`); `seat buttons are keyboard focusable with an accessible name` ("Balkon C-7, 450,00 ₺, boş").
- [ ] **Step 2:** FAIL → **Step 3: Implement.** Harita: kavisli "SAHNE", bölüm başlığı (ad · fiyat), sıra harfi iki yanda, sol/sağ yarı arası koridor boşluğu; durum renkleri Global Constraints'ten; ipucu (hover/focus) "Balkon C-7 · 450,00 ₺"; telefonda +/− yakınlaştırma (CSS `transform: scale`, 0.6–1.6) ve kabın içinde kaydırma. Harita 15 sn'de bir ve tutma reddedilince yeniden çekilir (TanStack Query `refetchInterval`). "Koltukları ayır": giriş yoksa `/giris?donus=…`; rol Customer değilse buton yerine "Bilet almak için müşteri hesabıyla girin" bilgisi; `POST /events/{id}/reservations {eventSeatIds}`; `409 seat.unavailable` → `extensions.unavailableSeatIds` koltuklar seçimden çıkarılır + toast; `409 reservation.already_pending` → `GET /me/reservations` içinden `eventId` ve `status === 'Pending'` olan bulunup oraya yönlendirilir. → **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(W-5): cinema-style seat map and seat holding`; merge; push.

### Task 6 (W-6): Rezervasyon/ödeme, biletler, rezervasyonlarım

**Files:**
- Create: `web/src/checkout/useCountdown.ts`, `web/src/checkout/ReservationPage.tsx`, `web/src/checkout/PaymentForm.tsx`, `web/src/checkout/usePayment.ts`, `web/src/account/TicketsPage.tsx`, `web/src/account/TicketQr.tsx`, `web/src/account/ReservationsPage.tsx`
- Test: `web/src/checkout/useCountdown.test.ts`, `web/src/checkout/usePayment.test.ts`

**Interfaces:**
- Consumes: `api`, `apiBlob`, `ApiError`, `messageFor`, `formatPrice`, `formatDateTime`.
- Produces: `useCountdown(expiresAt: string, now?: () => number): { secondsLeft: number; expired: boolean; label: string }` (`label` `"09:42"`); `usePayment(reservationId): { pay(token: 'tok_success'|'tok_declined'): Promise<PaymentResult>; pending: boolean }` — tek bir `Idempotency-Key`'i, ödeme kesin sonuç (200/402/409) dönene kadar tüm denemelerde yeniden kullanır.
- Produces: routes `/rezervasyon/:id`, `/biletlerim`, `/rezervasyonlarim` (Customer).

- [ ] **Step 1: Write the failing tests**
  - `useCountdown.test.ts` (Vitest fake timers): `counts down from expiresAt` (`expiresAt = now+600 s` → `"10:00"`, 1 sn sonra `"09:59"`); `expired at zero and never negative`.
  - `usePayment.test.ts`: `reuses the same Idempotency-Key when retried after a network error` (ilk fetch reddedilir, ikinci 200 → iki çağrıda başlık aynı); `uses a new key for a new reservation`; `ignores a second pay() while one is pending` (tek fetch).
- [ ] **Step 2:** FAIL → **Step 3: Implement.** Rezervasyon sayfası: `GET /reservations/{id}`; durum Pending ise üstte sarı geri sayım şeridi, koltuk listesi + toplam, ödeme formu (sahte kart alanları salt görünüm + iki hazır seçim "Başarılı kart" / "Reddedilen kart", "Öde {toplam}" düğmesi), "Rezervasyonu iptal et" (`DELETE /reservations/{id}`, onay penceresi). Geri sayım 0 → düğme devre dışı + rezervasyon yeniden okunur. Sonuçlar: 200 → bilet görünümü (QR'lar `TicketQr` ile `apiBlob('/tickets/{id}/qr')` → object URL) + "Biletleriniz e-posta adresinize de gönderildi"; 402 → `payment.declined` mesajı + "Etkinliğe dön"; 409 `reservation.expired` → mesaj + "Etkinliğe dön". Durum Confirmed/Expired/Cancelled/PaymentFailed ise uygun bilgi kartı. Biletlerim: kart listesi (etkinlik, tarih, salon, koltuk, kod, QR). Rezervasyonlarım: durum etiketli liste; Pending için "Ödemeye devam et" ve "İptal et". → **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(W-6): checkout with countdown, payment, tickets and reservations`; merge; push.

### Task 7 (W-7): Organizatör ekranları

**Files:**
- Create: `web/src/admin/AdminLayout.tsx` (yan menü; telefonda açılır), `web/src/organizer/MyEventsPage.tsx`, `web/src/organizer/EventFormPage.tsx`, `web/src/organizer/eventForm.ts`
- Test: `web/src/organizer/eventForm.test.ts`

**Interfaces:**
- Consumes: `api`, `ApiError.fieldErrors`, `Modal`, `useToast`, `GET /api/v1/organizer/events/{id}` (Task 1).
- Produces: `toEventRequest(values: EventFormValues): EventRequest` and `fromEvent(event: EventDetail, hall: HallDetail): EventFormValues` where `EventFormValues = { title; description; category; venueId; hallId; startsAt: string /* datetime-local */; endsAt: string; prices: Record<string, string> }`; `EventRequest` = backend `IEventFields` alanları (`hallId, title, description, category, startsAt (ISO, offset'li), endsAt, sectionPrices[{section, price}]`).
- Produces: routes `/yonetim/etkinlikler`, `/yonetim/etkinlikler/yeni`, `/yonetim/etkinlikler/:id/duzenle` (Organizer/Admin).

- [ ] **Step 1: Write the failing tests**: `toEventRequest converts local datetime to ISO with offset` (`"2026-11-20T20:00"` → yerel saat dilimiyle aynı an); `toEventRequest maps prices for every hall section` (`{Salon:'750', Balkon:'450'}` → iki `sectionPrices` girdisi, sayı); `fromEvent prefills every field from the organizer event response`; `missing section price is reported for that section`.
- [ ] **Step 2:** FAIL → **Step 3: Implement.** Etkinliklerim: tablo (telefonda kart): başlık, tarih, mekân/salon, durum etiketi (Taslak=neutral, Yayında=success, İptal=danger); işlemler duruma göre: Taslak → Düzenle, Yayımla, Sil; Yayında → Düzenle, İptal et. Sil/İptal onay penceresi (iptal metni: "Bekleyen rezervasyonlar da düşer. Bu işlem geri alınamaz."). Form: mekân seçimi (`GET /venues`) → salon seçimi (`GET /venues/{id}` halls) → salonun bölümleri (`GET /halls/{id}`) için fiyat alanları; düzenlemede `GET /organizer/events/{id}` ile dolu; alan hataları `fieldErrors`'tan; kaydet → `POST /events` veya `PUT /events/{id}`. → **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(W-7): organizer event management`; merge; push.

### Task 8 (W-8): Admin ekranları

**Files:**
- Create: `web/src/admin/VenuesPage.tsx`, `web/src/admin/VenueFormModal.tsx`, `web/src/admin/VenueDetailPage.tsx`, `web/src/admin/HallBuilderPage.tsx`, `web/src/admin/hallBuilder.ts`, `web/src/admin/RolesPage.tsx`
- Test: `web/src/admin/hallBuilder.test.ts`

**Interfaces:**
- Consumes: `AdminLayout`, `SeatMap` (önizleme, salt okunur mod `readOnly` prop'u — Task 5'e eklenir), `buildSeatLayout`.
- Produces: `parseRowRange(range: string): string[] | null` (`"A-L"`/`"A–L"` → A…L; `"1-12"` → "1"…"12"; `"A"` → ["A"]; geçersiz → null); `buildHallRequest(name: string, sections: { name: string; rows: string; seatsPerRow: number }[]): { request: CreateHallRequest; seatCount: number; errors: string[] }` (`errors`: boş ad, tekrar eden bölüm adı, geçersiz aralık, 0 koltuk, toplam 1–5.000 dışı → "Salon 1 ile 5.000 arasında koltuk içermeli.").
- Produces: routes `/yonetim/mekanlar`, `/yonetim/mekanlar/:id`, `/yonetim/mekanlar/:id/salon-ekle`, `/yonetim/roller` (Admin).

- [ ] **Step 1: Write the failing tests**: `parseRowRange` (yukarıdaki dört örnek + `"L-A"` → null); `buildHallRequest counts seats` (Salon A–L × 20 + Balkon A–F × 18 → 348); `rejects more than 5000 seats`; `rejects duplicate section names`.
- [ ] **Step 2:** FAIL → **Step 3: Implement.** Mekânlar: liste (şehir filtresi), oluştur/düzenle penceresi (`POST/PUT /venues`); mekân detayı: bilgiler + salonlar (ad, koltuk sayısı) + "Salon ekle". Salon oluşturucu: ad + bölüm satırları (ad, sıra aralığı, sıra başına koltuk; ekle/kaldır), canlı toplam ve hata listesi, sinema düzeninde önizleme (sahte `SeatDto`'larla salt okunur `SeatMap`), kaydet → `POST /venues/{id}/halls`. Roller: kullanıcı kimliği (GUID) + rol seçimi (Customer/Organizer) → `PUT /admin/users/{id}/role`; açıklama "Kullanıcı kimliği, kullanıcının profil menüsündeki 'Kimliğimi kopyala' ile alınır." → **Step 4:** PASS.
- [ ] **Step 5: Commit** `feat(W-8): admin venues, hall builder and roles`; merge; push.

### Task 9: Uçtan uca doğrulama ve kapanış

- [ ] **Step 1:** Backend (API + Notification) ve `npm run dev` çalışırken tarayıcıda, masaüstü (1440 px) ve telefon (390 px) genişliğinde: müşteri bilet alır (başarılı ve reddedilen kart), e-posta Mailpit'e düşer; aynı koltuğu ikinci müşteri tutmaya çalışır (409 mesajı); organizatör taslak oluşturur, düzenler (form dolu), yayımlar, iptal eder; admin mekân + 348 koltuklu salon oluşturur, organizatör onu kullanır; rol değiştirilir. Bulunan hatalar düzeltilir (her biri test + commit).
- [ ] **Step 2:** Ekran görüntüleri `docs/frontend-*.png` (ana sayfa, koltuk haritası masaüstü/telefon, ödeme, yönetim) ve README "Ön yüz" bölümüne eklenir; ISKELET §9 "Ön yüz" aşaması kutuları `[x]`.
- [ ] **Step 3:** Tüm .NET ve web kontrolleri yeşil, commit `docs(W): frontend walkthrough and screenshots`, merge, push, CI yeşil.
