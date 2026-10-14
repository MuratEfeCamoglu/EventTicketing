# Web ön yüzü — tasarım

Tarih: 2026-10-14 · Durum: onaylandı (2026-10-14, proje sahibi)

## 1. Amaç ve neden

EventTicketing backend'i (MVP, v2, v3, bonus tamam) bugün yalnız Scalar UI ile denenebiliyor. Proje sahibi, backend'in
her parçasını tarayıcıdan **kendisi deneyebilmek** için bir ön yüz istiyor. Ön yüz bir demo/deneme aracıdır, ama
**gerçek bir bilet sitesi kadar profesyonel görünmeli** ve **telefonda da düzgün çalışmalıdır**.

Başarı: proje sahibi tarayıcıdan (masaüstü ve telefon) müşteri, organizatör ve admin olarak backend'in bütün
akışlarını Scalar'a hiç dönmeden deneyebiliyor; ekranlar özenli, tutarlı ve hatasız görünüyor.

## 2. Hedefler

- Müşteri akışı uçtan uca: etkinlik bulma → sinema tarzı koltuk haritası → koltuk tutma ve 10 dk geri sayım →
  mock ödeme → QR'lı biletler → biletlerim / rezervasyonlarım.
- Organizatör ekranları: etkinliklerim, etkinlik oluşturma/düzenleme, yayımlama, iptal, silme.
- Admin ekranları: mekân oluşturma/düzenleme, salon oluşturma (önizlemeli), kullanıcı rolü değiştirme.
- Kimlik: giriş, kayıt, çıkış; access token süresi dolunca refresh ile sessizce yenileme.
- Responsive: telefonda (≥ 360 px genişlik) her ekran kullanılabilir.
- Arayüz metinleri Türkçe.

## 3. Hedef olmayanlar

- Backend değişikliği, **tek istisna dışında**: ön yüz mevcut `/api/v1` uç noktalarını (ISKELET §5.13) kullanır;
  eksikler ön yüzde sade bir çözümle karşılanır (bkz. §7 kullanıcı rolleri). İstisna, proje sahibinin onayıyla
  eklenen `GET /organizer/events/{id}` okuma uç noktasıdır (bkz. §7).
- Üretim dağıtımı, SSR/SEO, çoklu dil, karanlık tema, PWA/çevrimdışı.
- Gerçek ödeme formu; ödeme yalnız backend'in mock token'larıyla yapılır.

## 4. Anti-hedefler (teknik olarak çalışsa bile başarısızlık sayılır)

- Ham/"geliştirici paneli" görünümü: şablon bileşen kütüphanesi görüntüsü, hizasız formlar, İngilizce hata metinleri.
- Telefonda yatay taşan, tıklanamayan ya da okunamayan ekranlar.
- Backend'in döndürdüğü durumu gizleyen ya da yanlış gösteren akışlar (ör. süre dolmuş rezervasyonu ödenebilir
  göstermek, satılmış koltuğu boş göstermek).

## 5. Proje sahibinin verdiği kararlar

- Kullanım: kendi denemesi için; profesyonel görünmeli.
- Kapsam: müşteri akışı merkezde + organizatör ve admin ekranları (bu belgenin §6–§7'si, aynen onaylandı).
- Görsel yön: **B — aydınlık, sade** (beyaz zemin, tek mavi vurgu, ince kenarlıklar, okunaklı tipografi).
- Koltuk haritası: **sinema salonu tarzı**, kalabalık salonları kaldırabilen (bkz. §6.3).
- Teknoloji seçimi builder'a bırakıldı; telefonda düzgün görünmesi şart.

Referans mockup'lar: `.superpowers/brainstorm/*/content/visual-style.html` (B seçildi),
`seat-selection-cinema.html`, `organizer-admin.html`.

## 6. Müşteri tarafı

### 6.1 Ana sayfa / etkinlik listesi
- `GET /events` kartları: başlık, tarih-saat, mekân · şehir, kategori, "X ₺'den" (`minPrice`).
- Filtreler: şehir, kategori, tarih aralığı, metin araması (`q`), sıralama; sayfalama.
- Boş liste ve yükleniyor durumları özenli gösterilir.

### 6.2 Etkinlik sayfası
- Başlık, açıklama, tarih-saat, mekân/salon, bölüm fiyatları. (`GET /events/{id}` salon ve mekân adını içermez;
  `GET /halls/{hallId}` ve `GET /venues/{venueId}` ile tamamlanır.)
- Koltuk haritası (§6.3) ve seçim özeti.

### 6.3 Sinema tarzı koltuk haritası
- Veri: `GET /events/{id}/seats` → düz liste (`section`, `row`, `number`, `price`, `status`). Backend koltukların
  fiziksel yerini (koridor vb.) tutmaz; düzen ön yüzde türetilir.
- Düzen: en üstte kavisli "SAHNE"; bölümler yukarıdan aşağıya, aralarında boşluk ve bölüm adı + fiyat; her bölümde
  sıralar, sıra içinde koltuklar numara sırasıyla, ortalanmış; **her sıranın ortasında koridor boşluğu**; sıra harfi
  her iki yanda. Koltuklar küçük koltuk şekli.
- Bölüm sırası: salonun bölüm sırası (`GET /halls/{id}` sırası) kullanılır.
- Durumlar: boş, seçili, başkası tuttu (`Held`), satıldı (`Sold`) — renk + lejant. Tutulmuş/satılmış koltuk
  seçilemez.
- Etkileşim: tıklayarak seç/bırak; en fazla 6 koltuk (backend sınırı, aşılınca uyarı); üzerine gelince/odaklanınca
  "Balkon C-7 · 450 ₺" ipucu; klavye ile erişilebilir.
- Ölçek: 5.000 koltuğa kadar salonlar akıcı kalmalı. Telefonda harita yakınlaştırılır (+/−) ve kaydırılır.
- Seçim özeti: masaüstünde sağ panel, telefonda alta yapışık çubuk (toplam + düğme).
- Güncellik: harita belli aralıklarla ve koltuk tutma reddedilince yenilenir.

### 6.4 Koltuk tutma, geri sayım, ödeme
- "Koltukları ayır" → `POST /events/{id}/reservations`. Giriş yoksa önce giriş ekranına, sonra geri döner.
- Başarılı: rezervasyon/ödeme sayfası; üstte `expiresAt`'e göre geri sayım (10 dk).
- `409 seat.unavailable`: yanıttaki `unavailableSeatIds` ile hangi koltukların alındığı söylenir, harita yenilenir.
  `409 reservation.already_pending`: yanıtta rezervasyon kimliği yoktur; bekleyen rezervasyon `GET /me/reservations`
  içinden aynı `eventId` ve `Pending` durumuyla bulunup ona yönlendirilir.
- Koltuk tutmayı ve ödemeyi yalnız **Customer** rolü yapabilir (backend kuralı); organizatör/admin ile girişte etkinlik
  sayfası haritayı gösterir ama "Koltukları ayır" yerine "Bilet almak için müşteri hesabıyla girin" bilgisi çıkar.
- Ödeme: sahte kart formu + iki hazır seçenek: "Başarılı kart" (`tok_success`), "Reddedilen kart" (`tok_declined`).
  Her ödeme denemesi bir `Idempotency-Key` (UUID) ile gönderilir; ağ hatasında aynı anahtarla tekrar denenir.
- Sonuçlar: başarı → bilet sayfası (QR'lar) + "e-postanıza gönderildi" bilgisi; ret (`402`) → anlaşılır mesaj,
  koltuklar serbest kaldı; süre doldu (`409 reservation.expired`) → anlaşılır mesaj.
- Geri sayım bittiğinde ödeme düğmesi devre dışı kalır ve durum backend'den tekrar okunur.

### 6.5 Hesap sayfaları
- Biletlerim (`GET /me/tickets`): etkinlik, tarih, salon, koltuk, bilet kodu, QR görseli.
  QR uç noktası yetki istediği için görsel `fetch` + `Authorization` ile alınıp gösterilir.
- Rezervasyonlarım (`GET /me/reservations`): durum etiketleri; bekleyen için "Ödemeye devam et" ve "İptal et".

### 6.6 Giriş / kayıt
- Giriş ve kayıt formları; giriş ekranında üç seed kullanıcısı için tek tıkla giriş düğmeleri
  (`customer@`, `organizer@`, `admin@example.com`, parola `Dev-only-Passw0rd`).
- Rol ve e-posta access token'dan okunur: claim adları birebir `sub`, `email`, `role`; menüler role göre değişir.

## 7. Organizatör ve admin tarafı ("Yönetim")

- Organizatör/Admin girişinde üst menüde "Yönetim"; solda yan menü (telefonda açılır kapanır).
- **Etkinliklerim** (`GET /organizer/events`): tablo (telefonda kart), durum etiketleri Taslak/Yayında/İptal; yalnız o
  duruma uygun işlemler: Taslak → düzenle, yayımla, sil; Yayında → düzenle, iptal et.
- **Etkinlik formu** (oluştur/düzenle): başlık, açıklama, kategori, salon (mekân → salon seçimi), başlangıç, bitiş;
  salon seçilince bölümler `GET /halls/{id}`'den gelir ve her bölüm için fiyat istenir.
- **Düzenleme verisi:** `GET /events/{id}` taslak için `404` döndüğünden ve `PUT /events/{id}` tüm alanları
  istediğinden backend'e **yeni bir uç nokta** eklenir (proje sahibinin kararı, 2026-10-14):
  `GET /api/v1/organizer/events/{id}` — yetki OrganizerOrAdmin + sahiplik (başkasının etkinliği `404`, Admin hepsini
  görür), her durumdaki (Taslak/Yayında/İptal) etkinlik için `EventResponse` döndürür (açıklama, `hallId`, bölüm
  fiyatları dahil). ISKELET §5.13'e, yetki matrisine (AC-03.1) ve bir kabul kriterine eklenir; integration testleriyle
  gelir. Form bu yanıtla dolu açılır.
- Geri alınamaz işlemler (iptal, silme) onay penceresi ister; iptal metni bekleyen rezervasyonların düşeceğini söyler.
- **Admin · Mekânlar**: liste, oluştur, düzenle; mekân detayında salonlar (`GET /venues/{id}`).
- **Admin · Salon oluştur**: ad + bölümler; her bölüm için sıra harf aralığı (ör. A–L) ve sıra başına koltuk sayısı;
  istek gövdesine (`sections[].rows[].{label, seatCount}`) çevrilir. Kaydetmeden önce toplam koltuk sayısı ve sinema
  düzeninde önizleme gösterilir. 1–5.000 koltuk sınırı (backend kuralı) önceden doğrulanır.
- **Admin · Kullanıcı rolleri**: backend'de kullanıcı arama/listeleme yok. Ekran kullanıcı kimliği (GUID) + rol alır
  (`PUT /admin/users/{id}/role`). Kimliği bulmak kolay olsun diye her kullanıcının profil menüsünde kendi kimliği
  kopyalanabilir gösterilir.

## 8. Ortak davranışlar

- Hata gösterimi: backend ProblemDetails'teki `code` alanı (ISKELET §5.12) Türkçe mesaja eşlenir; doğrulama hataları
  (`errors`) ilgili form alanının altında gösterilir; bilinmeyen kodda genel mesaj + `traceId`.
- `429 rate_limit.exceeded`: "Çok fazla deneme, X sn sonra tekrar deneyin" (`Retry-After`).
- Oturum: access token süresi dolunca/`401`'de `POST /auth/refresh` bir kez denenir, başarısızsa girişe yönlendirilir.
  Çıkışta `POST /auth/logout`.
- Para TRY, `tr-TR` biçiminde; tarihler kullanıcının saat diliminde, Türkçe.
- Erişilebilirlik: klavye ile gezinme, odak halkaları, yeterli renk kontrastı, form etiketleri.

## 9. Kısıtlar

- Repo: aynı depo, kökte `web/` klasörü. Backend `dotnet run` ile `http://localhost:8080`'de çalışır; ön yüz geliştirme
  sunucusu `/api` isteklerini oraya proxy'ler (backend'de CORS yok ve eklenmeyecek).
- Makinede Node.js 22 / npm 10 var.
- ISKELET.md §1 ve §3.3 "Frontend yoktur / kapsam dışı" der. Bu iş, proje sahibinin talebiyle kapsama alınır;
  ISKELET'e yeni bir aşama/görev ve kapsam değişikliği işlenir, karar `docs/decisions.md`'ye yazılır.
- Mevcut CI ve .NET testleri bozulmamalı; `node_modules/` ve `.superpowers/` git'e girmemeli.

## 10. Bu belgenin onayıyla birlikte onaylananlar (AGENT.md §4)

Proje sahibi belgeyi 2026-10-14'te onayladı ("1. yap geliştirmeye başla").

Proje sahibi bu belgeyi onayladığında şunlar da onaylanmış sayılır; ayrıca sorulmaz:
- ISKELET.md'ye yeni bir aşama ("Ön yüz", görev kimlikleri `W-1`, `W-2`, …) ve §1/§3.3'teki "Frontend yok/kapsam
  dışı" ifadelerinin güncellenmesi; ön yüz kabul kriterleri ISKELET'e `AC-21.x` olarak eklenir.
- Kökte `web/` klasörünün açılması.
- `.github/workflows/ci.yml`'e ön yüz için ayrı bir iş (lint, tip kontrolü, testler, build) eklenmesi.
- Ön yüzün npm bağımlılıkları: plan, kullanılacak paketlerin listesini (her biri yaygın, MIT/Apache benzeri lisanslı,
  sürümü sabit) verir; liste ISKELET'e işlenir. Ticari lisanslı paket kullanılmaz (K-03 ruhu).
- "Bitti" tanımı ön yüz için: `npm run lint`, tip kontrolü, birim testleri ve `npm run build` hatasız; mevcut .NET
  kontrolleri (0 uyarı, testler, coverage eşiği) değişmeden geçer. Dal adı `feat/W-<n>-<ad>`.

## 11. Builder'a bırakılanlar (öneriler, bağlayıcı değil)

- Teknoloji: React + TypeScript + Vite önerilir (en yaygın, Node 22 ile uyumlu). Yönlendirme, sunucu durumu,
  biçimlendirme ve form kütüphaneleri builder'ın seçimi; seçimler ve gerekçeleri `docs/decisions.md`'ye yazılır.
- Görsel sistem: §5'teki B yönüne uyan renk, boşluk ve tipografi ölçeği; ortak bileşenler (düğme, form alanı, etiket,
  kart, tablo, modal, bildirim).
- Test: koltuk düzeni türetme, geri sayım, hata kodu eşleme ve token yenileme mantığı için birim testleri; ön yüzün
  derlenmesi/testleri CI'a ayrı bir iş olarak eklenir.
- README'ye "Ön yüz" bölümü: kurulum ve çalıştırma komutları.
