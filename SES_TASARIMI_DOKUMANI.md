# 🎧 CAR CLICKER MOBILE 3D — TAM SES TASARIMI DOKÜM ANI

> **Versiyon:** 1.0  
> **Tarih:** Haziran 2025  
> **Hedef Kitle:** Ses Tasarımcısı / Sound Designer  
> **Proje:** CarClickerMobile3D — Mobil Idle/Clicker Araba Oyunu

---

## İÇİNDEKİLER

1. [Genel Bakış](#1-genel-bakış)
2. [Sandık Sistemi (Chest System)](#2-sandık-sistemi-chest-system)
3. [Nitro Sistemi (Nitro System)](#3-nitro-sistemi-nitro-system)
4. [Boost Sistemi (Boost Mode)](#4-boost-sistemi-boost-mode)
5. [Polis / Kovalamaca Sistemi (Police & Chase)](#5-polis--kovalamaca-sistemi-police--chase)
6. [Sistemler — Kart Efektleri (Card Effects)](#6-sistemler--kart-efektleri)
7. [UI / UX Sesleri](#7-ui--ux-sesleri)
8. [Garaj Sahnesi (Garage Scene)](#8-garaj-sahnesi-garage-scene)
9. [Sinematik Sahne (Cinematic Showcase)](#9-sinematik-sahne-cinematic-showcase)
10. [Dünya Toplanabilir Öğeleri (World Collectibles)](#10-dünya-toplanabilir-öğeleri)
11. [Arka Plan Müzikleri (Background Music)](#11-arka-plan-müzikleri-background-music)
12. [Bağlanmamış Sesler — Entegrasyon Bekleyenler](#12-bağlanmamış-sesler--entegrasyon-bekleyenler)
13. [Eksik Ses Fırsatları — Yeni Öneriler](#13-eksik-ses-fırsatları--yeni-öneriler)
14. [Teknik Davranış Rehberi](#14-teknik-davranış-rehberi)
15. [Toplam Sayılar](#15-toplam-sayılar)

---

## 1. GENEL BAKIŞ

### Proje Karakteri

Mobil idle/clicker araba oyunu. Oyuncu ana sahne'de arabaya tıklayarak para kazanır, binalar satın alır, kart koleksiyonu oluşturur, nitro coin toplar, boost modu aktive eder, polis kovalamacalarından kaçar ve garajda arabalarını özelleştirir.

### Ses Felsefesi

- **Ana Sahne:** Enerjik, tatmin edici tıklama geri bildirimleri. Hızlı oynanış hissi.
- **Sandık Sahnesi:** Büyülü, meraklı, ödül keşfi heyecanı.
- **Garaj Sahnesi:** Showroom ambiyansı, lüks ve teknolojik.
- **Sinematik Sahne:** Dramatik, prestijli, epik araba sunumu.
- **Polis Kovalamacası:** Adrenalin, gerilim, kalp atışı hissi.
- **Genel UI:** Hafif, temiz, rahatsız etmeyen — mobil dostu.

### Teknik Kısıtlamalar

- **Format:** Mono veya Stereo, 44.1kHz, 16-bit WAV (Unity import sırasında sıkıştırılacak)
- **Süre:** One-shot efektler 0.1s–3.0s arası; loop'lar seamless olmalı
- **Loudness:** Tüm sesler -6dB peak ile normalize edilmeli (Unity tarafında volume kontrol var)
- **Loop'lar:** Seamless loop noktaları ile teslim edilmeli (crossfade point belirtilmeli)

---

## 2. SANDIK SİSTEMİ (Chest System)

Sandık açma sahnesi 7–8 dokunuşluk bir akıştan oluşur. Her aşamada farklı bir ses tetiklenir.

| KOD     | İSİM                                     | AÇIKLAMA                                                                                                                                                                          | SÜRE / DAVRANIŞ     | TEKNİK NOT                                                                                                      | DURUM    |
| ------- | ---------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- | --------------------------------------------------------------------------------------------------------------- | -------- |
| **C1**  | `chestDropClip` — Sandık Düşüş           | Büyülü bir whoosh + ağır bir thud. Sandık yukarıdan sahneye düşer. Sihirli parıltı hissi ile birleşen derin bir darbe sesi. Fantezi + ağırlık hissi.                              | ~0.8–1.2s one-shot  | `PlayChestDrop()` — Intro animasyonunun ilk frame'inde çalar                                                    | ✅ BAĞLI |
| **C2**  | `chestHopClip` — Sandık Zıplama Dokunuşu | Ahşap/metalik hafif bir tık + hafif çıngırak. Her dokunuşta sandık zıplar. Eğlenceli, oyuncu geri bildirimi. Pitch ±5-10% variation ile çalar (her dokunuş hafif farklı duyulur). | ~0.15–0.3s one-shot | `PlayChestHop()` — pitch 0.95–1.10 arası rastgele. 3 dokunuş + lid açılıştan sonraki ödül geçişlerinde de çalar | ✅ BAĞLI |
| **C3**  | `chestLidOpenClip` — Kapak Açılışı       | Menteşe gıcırtısı + altın patlama. Sandığın kapağı açılırken dramatik bir "creak" ile başlayıp parlak bir "burst" ile biten ses. Keşif anı, sürpriz hissi.                        | ~0.6–1.0s one-shot  | `PlayChestLidOpen()` — 3. dokunuşta kapak rotasyonu başladığında tetiklenir                                     | ✅ BAĞLI |
| **C4**  | `rewardMoneyClip` — Para Ödülü           | Madeni para yağmuru + fanfar. Bol miktarda coin cascade sesi ile küçük bir zafer fanfarı. Zenginlik, bolluk hissi.                                                                | ~1.0–1.5s one-shot  | `PlayRewardMoney()` — Kapak açıldıktan hemen sonra otomatik çalar                                               | ✅ BAĞLI |
| **C5**  | `rewardNitroClip` — Nitro Ödülü          | Elektrikli/teknolojik chime. Enerji yükleme sesi + parlak dijital pling. Güç, teknoloji hissi.                                                                                    | ~0.5–0.8s one-shot  | `PlayRewardNitro()` — 4. dokunuşta                                                                              | ✅ BAĞLI |
| **C6**  | `rewardCardClip` — Kart Ödülü            | Kart çevirme + ışıltı. Kartın yüzü dönerken "flip" sesi + büyülü shimmer. Koleksiyon, nadir buluş hissi.                                                                          | ~0.5–0.8s one-shot  | `PlayRewardCard()` — 5. dokunuşta                                                                               | ✅ BAĞLI |
| **C7**  | `rewardStickerClip` — Sticker Ödülü      | Sticker yapıştırma + kıvılcım. "Slap" sesi + minik sparkle efekti. Eğlenceli kişiselleştirme hissi.                                                                               | ~0.4–0.6s one-shot  | `PlayRewardSticker()` — 6. dokunuşta (sticker varsa)                                                            | ✅ BAĞLI |
| **C8**  | `chestSummaryClip` — Özet Ekranı         | Yumuşak tamamlanma jingle'ı. Kısa, tatmin edici bir "tüm ödülleri aldın" melodisi. Huzurlu kapanış.                                                                               | ~0.8–1.2s one-shot  | `PlayChestSummary()` — 6. veya 7. dokunuşta (sticker sonrası veya kart sonrası)                                 | ✅ BAĞLI |
| **C9**  | `chestExitClip` — Çıkış Swoosh           | Hızlı swoosh-out. Sahne kapanırken kısa, temiz bir çıkış sesi. Geçiş finali.                                                                                                      | ~0.2–0.4s one-shot  | `PlayChestExit()` — Son dokunuşta, sahne değişimi öncesi                                                        | ✅ BAĞLI |
| **C10** | `worldCardSwooshClip` — Kart Uçuş Swoosh | Kart kaydırma/uçuş sesi. 3D kart sandıktan çıkıp ekrana doğru uçarken swoosh. Hareket, dinamizm hissi.                                                                            | ~0.3–0.5s one-shot  | `PlayWorldCardSwoosh()` — Her ödül kartı spawn olduğunda                                                        | ✅ BAĞLI |

---

## 3. NİTRO SİSTEMİ (Nitro System)

Nitro coin'ler yolda spawn olur, oyuncu dokunarak veya mıknatıs ile toplar. Yeterince toplandığında "Nitro Yağmuru" başlar.

| KOD     | İSİM                                                  | AÇIKLAMA                                                                                                                                                | SÜRE / DAVRANIŞ      | TEKNİK NOT                                                                                                           | DURUM    |
| ------- | ----------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- | -------------------------------------------------------------------------------------------------------------------- | -------- |
| **N1**  | `nitroCoinCollectClip` — Nitro Coin Toplama (Dokunuş) | Çan/chime pling sesi. Parlak, tatmin edici bir "ding" — madeni para toplama hissi ama daha enerjik ve dijital.                                          | ~0.1–0.2s one-shot   | `PlayNitroCoinCollect()` — Pitch 0.90–1.10 arası rastgele variation. Rate-limit: 0.05s cooldown, max 3 eşzamanlı ses | ✅ BAĞLI |
| **N2**  | `nitroCoinMagnetClip` — Nitro Coin Toplama (Mıknatıs) | Daha yumuşak / tınlayan pling. N1'in daha soft versiyonu — mıknatıs tarafından otomatik toplanma. Dolaylı kazanım hissi.                                | ~0.1–0.2s one-shot   | `PlayNitroCoinMagnet()` — %60 volume ile çalar. Aynı rate-limit sistemi (0.05s cooldown)                             | ✅ BAĞLI |
| **N4**  | `nitroDepositClip` — Boost'a Nitro Yatırma            | Elektronik deposit ding. Nitro coin boost çubuğuna aktarılırken kısa dijital onay sesi. Yatırım, ilerleme hissi.                                        | ~0.2–0.4s one-shot   | `PlayNitroDeposit()` — BoostModeController.OnNitroChargeAccepted event'i ile tetiklenir                              | ✅ BAĞLI |
| **N5**  | `nitroRainDelayClip` — Yağmur Gecikme Uyarısı         | Uzak gök gürültüsü. Yağmur eşiğine ulaşıldığında 30 saniyelik geri sayım başlıyor — bunu haber veren uzak bir rumble. Beklenti, yaklaşan fırtına hissi. | ~1.0–2.0s one-shot   | `PlayNitroRainDelay()` — Toplama eşiğine ulaşıldığında bir kez çalar                                                 | ✅ BAĞLI |
| **N6**  | `nitroRainStartClip` — Yağmur Başlangıcı              | Serbest bırakma whoosh'u. Rüzgârlı bir "release" sesi — gökten coinler dökülmeye başlıyor. Enerji patlaması, bolluk başlangıcı.                         | ~0.5–0.8s one-shot   | `PlayNitroRainStart()` — Yağmur başladığı anda bir kez çalar                                                         | ✅ BAĞLI |
| **N7**  | `nitroRainLoopClip` — Yağmur Ambiyans Loop            | Yumuşak yağmur patter loop'u. Hafif yağmur damlası sesleri sürekli arka planda çalar. Huzurlu bolluk, ambient enerji hissi.                             | Seamless loop, ~4–8s | `StartRainLoop()` — Fade-in 0.5s, hedef volume %30. `StopRainLoop()` — Fade-out 0.5s                                 | ✅ BAĞLI |
| **N8**  | `nitroRainEndClip` — Yağmur Bitişi                    | Fade-out whoosh. Yağmur sona ererken hafif bir "dissipate" sesi. Yumuşak kapanış.                                                                       | ~0.5–0.8s one-shot   | `PlayNitroRainEnd()` — Yağmur süresi dolduğunda çalar                                                                | ✅ BAĞLI |
| **N9**  | `magnetActivateClip` — Mıknatıs Aktivasyonu           | Enerji alanı açılma hum. Elektrifikasyon sesi — mıknatıs kalkanı aktive olurken güç alanı oluşuyor. Teknolojik güç hissi.                               | ~0.3–0.6s one-shot   | `PlayMagnetActivate()` — Mıknatıs silahlandığında (yeterli dokunuş sonrası)                                          | ✅ BAĞLI |
| **N10** | `magnetPullClip` — Mıknatıs Çekme                     | Manyetik whoosh/zip. Her coin mıknatısa doğru çekilirken kısa bir "magnetik çekiş" sesi. Güç, hız hissi.                                                | ~0.15–0.3s one-shot  | `PlayMagnetPull()` — %50 volume. Her coin drift→pull geçişinde çalar                                                 | ✅ BAĞLI |
| **N11** | `magnetDeactivateClip` — Mıknatıs Deaktivasyonu       | Enerji alanı kapanma. Güç alanı sönerken "power-down" sesi. Kapanış, sonlanma hissi.                                                                    | ~0.3–0.5s one-shot   | `PlayMagnetDeactivate()` — Mıknatıs devre dışı kaldığında (tüm coinler toplandığında veya timeout)                   | ✅ BAĞLI |

---

## 4. BOOST SİSTEMİ (Boost Mode)

Nitro coinler boost çubuğunu doldurur → dolduğunda turbo modu aktive olur → süre boyunca güçlendirilmiş hız + ses efektleri.

| KOD    | İSİM                                            | AÇIKLAMA                                                                                                                     | SÜRE / DAVRANIŞ      | TEKNİK NOT                                                                                           | DURUM    |
| ------ | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | -------------------- | ---------------------------------------------------------------------------------------------------- | -------- |
| **B1** | `boostReadyClip` — Boost Hazır                  | Parlak "tam dolu" ding. Boost çubuğu dolduğunda çalan tatmin edici bir "ready!" sesi. Başarı, hazırlık hissi.                | ~0.3–0.5s one-shot   | `PlayBoostReady()` — OnBoostReady event'i ile tetiklenir                                             | ✅ BAĞLI |
| **B2** | `boostActivateClip` — Boost Ateşleme            | Turbo ignition whoosh. Güçlü bir "ateşleme" sesi — jet motoru çalıştırma hissi. Adrenalini yükselten patlama başlangıcı.     | ~0.5–0.8s one-shot   | `PlayBoostActivate()` — OnBoostStarted event'i. Müzik aynı anda %50'ye duck edilir (0.3s fade)       | ✅ BAĞLI |
| **B3** | `boostActiveLoopClip` — Boost Turbo Uğultu Loop | Sürekli turbo hum loop'u. Motor turbini / jet uğultusu. Yüksek hız, güç hissi arka planda sürekli çalar.                     | Seamless loop, ~2–4s | `StartBoostLoop()` — Fade-in 0.3s, hedef volume %25. Boost bitişinde `StopBoostLoop()` fade-out 0.3s | ✅ BAĞLI |
| **B4** | `boostEndClip` — Boost Kapanışı                 | Power-down whoosh. Turbo'nun söndüğü "deceleration" sesi. Enerji azalma, normal moda dönüş hissi.                            | ~0.5–0.8s one-shot   | `PlayBoostEnd()` — OnBoostEnded event'i. Müzik 0.8s'de geri yükseltilir                              | ✅ BAĞLI |
| **B5** | `boostCooldownCompleteClip` — Soğuma Bitti Pip  | Hazır olma pip'i. Cooldown sona erip tekrar şarj edilebilir olduğunda küçük bir "pip" sesi. Bildirim, yeniden başlama hissi. | ~0.15–0.3s one-shot  | `PlayBoostCooldownComplete()` — State → Charging geçişinde tetiklenir                                | ✅ BAĞLI |

---

## 5. POLİS / KOVALAMACA SİSTEMİ (Police & Chase)

İki alt sistem: **Radar** (yolda beliren radarlar) ve **Polis Kovalamacası** (tap minigame).

### 5A. RADAR SESLERİ (SFXManager)

| KOD     | İSİM                                                 | AÇIKLAMA                                                                                                                                                    | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                         | DURUM          |
| ------- | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ | ------------------------------------------------------------------ | -------------- |
| **P8**  | `radarDefuseClip` — Radar Etkisizleştirme            | Elektronik zap/sönme sesi. Oyuncu radarı zamanında dokunarak etkisizleştirdiğinde çalan tatmin edici bir "deactivated" sesi. Başarı, refleks ödülü hissi.   | ~0.3–0.5s one-shot | `PlayRadarDefuse()` — Radar.OnTapped() ile tetiklenir              | ✅ BAĞLI       |
| **P9**  | `radarMissClip` — Radar Kaçırma                      | Kamera deklanşörü + alarm. Radar kaçırıldığında "fotoğraf çekildi!" hissi veren deklanşör + kısa alarm. Tehlike, ceza hissi.                                | ~0.4–0.7s one-shot | `PlayRadarMiss()` — Metot mevcut ama henüz oyun koduna bağlanmamış | ⚠️ BAĞLI DEĞİL |
| **P10** | `radarPopupClip` — Radar Popup                       | Fotoğraf banyo sesi. Radar fotoğrafı ekrana gelirken retro kamera/instant film sesi. Uyarı, sonuç gösterimi hissi.                                          | ~0.3–0.5s one-shot | `PlayRadarPopup()` — Metot mevcut ama henüz bağlanmamış            | ⚠️ BAĞLI DEĞİL |
| **P11** | `popularityStageUpClip` — Popülerlik Kademe Yükselme | Eskalasyon tonu. Popülerlik yeni bir tehlike kademesine geçtiğinde yükselen "uyarı" tonu. Artan risk, tehlike seviyesi hissi. Stage 1→6 arası 6 kademe var. | ~0.5–0.8s one-shot | `PlayPopularityStageUp()` — Metot mevcut ama henüz bağlanmamış     | ⚠️ BAĞLI DEĞİL |

### 5B. KOVALAMACA ANI SESLERİ (SFXManager)

| KOD    | İSİM                                      | AÇIKLAMA                                                                                                                                               | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                            | DURUM          |
| ------ | ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------ | --------------------------------------------------------------------- | -------------- |
| **P6** | `chaseSuccessClip` — Kovalamaca Başarı    | Zafer sting'i. Polisten başarıyla kaçıldığında çalan kısa, parlak zafer melodisi. Rahatlama + başarı hissi.                                            | ~1.0–1.5s one-shot | `PlayChaseSuccess()` — Metot mevcut ama henüz oyun koduna bağlanmamış | ⚠️ BAĞLI DEĞİL |
| **P7** | `chaseFailClip` — Kovalamaca Başarısızlık | Başarısızlık sting'i. Polis tarafından yakalandığında çalan dramatik, kısa bir "yakalandın" sesi. Hayal kırıklığı ama tekrar deneme motivasyonu hissi. | ~1.0–1.5s one-shot | `PlayChaseFail()` — Metot mevcut ama henüz oyun koduna bağlanmamış    | ⚠️ BAĞLI DEĞİL |

### 5C. KOVALAMACA KATMANLI SES SİSTEMİ (PoliceChaseFeedbackController)

Bu sesler ayrı AudioSource'larda çalar ve danger seviyesine göre dinamik pitch/volume değiştirir.

| KOD      | İSİM                                              | AÇIKLAMA                                                                                                                                                        | SÜRE / DAVRANIŞ      | TEKNİK NOT                                                                                 | DURUM    |
| -------- | ------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- | ------------------------------------------------------------------------------------------ | -------- |
| **PC-1** | `chaseStingerClip` — Kovalamaca Başlangıç Stinger | Kısa, sert, vurucu açılış sting. "POLİS GELDİ!" hissini veren dramatik bir-tek nota veya kısa motif. Şok, ani tehlike hissi.                                    | ~0.8–1.5s one-shot   | Kovalamaca başladığı anda bir kez çalar. Ayrı AudioSource (chaseStingerSource)             | ✅ BAĞLI |
| **PC-2** | `chaseLoopClip` — Kovalamaca BPM Loop             | Yüksek tempolu sürekli loop. Tüm kovalamaca süresince çalan hızlı ritimli müzik/beat. Adrenalin, koşu, kaçış hissi. BPM 140-160 arası önerilir.                 | Seamless loop, ~4–8s | Kovalamaca boyunca sürekli çalar. Ayrı AudioSource (chaseLoopSource). Loop=true            | ✅ BAĞLI |
| **PC-3** | `heartbeatClip` — Kalp Atışı                      | Kalp atışı loop. "Thump-thump" kalp sesi — düşük tehlikede yavaş, yüksek tehlikede çok hızlı. Panik, stres hissi.                                               | Seamless loop, ~1–2s | Ayrı AudioSource. Volume: 0.25→1.0, Pitch: 0.80→1.40 arası danger fraction ile ölçeklenir  | ✅ BAĞLI |
| **PC-4** | `sirenClip` — Polis Sireni                        | Polis siren loop. Klasik wee-woo siren — uzakta kısık, yakında çok yüksek ve keskin. Kovalanma, tehdit hissi.                                                   | Seamless loop, ~2–4s | Ayrı AudioSource. Volume: 0.20→0.90, Pitch: 0.90→1.30 arası danger fraction ile ölçeklenir | ✅ BAĞLI |
| **PC-5** | `engineRoarClip` — Motor Kükreme                  | Stresli motor loop. Motora aşırı yüklenmiş, yüksek devirde zorlanan araba motoru. Normal sürüşten daha agresif, daha yüksek pitch. Tehlike altında sürüş hissi. | Seamless loop, ~2–4s | Ayrı AudioSource. Normal: pitch=1.0 vol=0.45, Chase: pitch=1.35 vol=0.80. 0.3s ramp        | ✅ BAĞLI |

---

## 6. SİSTEMLER — KART EFEKTLERİ

Oyundaki kart efektleri çeşitli güç-up'lar sağlar. Her aktivasyon/deaktivasyon'un sesi olmalı.

| KOD    | İSİM                                                           | AÇIKLAMA                                                                                                                                                      | SÜRE / DAVRANIŞ      | TEKNİK NOT                                                                                                                     | DURUM          |
| ------ | -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------- | ------------------------------------------------------------------------------------------------------------------------------ | -------------- |
| **F1** | `turboFingerActivateClip` — Turbo Parmak Aktivasyonu           | Parmak şıklatma + güçlenme. Hızlı "snap" sesi ile birlikte yükselen enerji tonu. 50 hızlı dokunuştan sonra aktive olan güç modu. Süper güç kazanma hissi.     | ~0.4–0.6s one-shot   | `PlayTurboFingerActivate()` — Metot mevcut ama TurboFingerController'da çağrılmıyor                                            | ⚠️ BAĞLI DEĞİL |
| **F2** | `turboFingerDeactivateClip` — Turbo Parmak Deaktivasyonu       | Power-down sönme sesi. Turbo parmak efektinin 30 saniye sonra bitmesiyle sona eren yumuşak güç kaybı sesi. Normal moda dönüş hissi.                           | ~0.3–0.5s one-shot   | `PlayTurboFingerDeactivate()` — Metot mevcut ama bağlanmamış                                                                   | ⚠️ BAĞLI DEĞİL |
| **F3** | `garageManagerActivateClip` — Garaj Yöneticisi Aktivasyonu     | Anahtar tıklaması/cızırtı sesi. Garaj Yöneticisi kartı aktive olduğunda kısa mekanik "wrench clink" sesi. Otomasyon, pasif kazanım hissi.                     | ~0.3–0.5s one-shot   | `PlayGarageManagerActivate()` — GarageManagerController'da tetiklenir                                                          | ✅ BAĞLI       |
| **F4** | `garageManagerDeactivateClip` — Garaj Yöneticisi Deaktivasyonu | Yumuşak kapanış esintisi. 60 saniyelik bonus süre dolduğunda hafif bir "wind-down" sesi. Süre dolma, sakinleşme hissi.                                        | ~0.3–0.5s one-shot   | `PlayGarageManagerDeactivate()` — Metot mevcut ama bağlanmamış                                                                 | ⚠️ BAĞLI DEĞİL |
| **F5** | `pitStopEarningsClip` — PitStop Offline Kazanç                 | Yazar kasa sesi. Oyuncu geri döndüğünde offline kazançları alırken klasik "ka-ching" sesi. Kazanç, sürpriz gelir hissi.                                       | ~0.5–0.8s one-shot   | `PlayPitStopEarnings()` — PitStopCrewController offline kazanç verildiğinde tetiklenir                                         | ✅ BAĞLI       |
| **T1** | `momentumTickClip` — Momentum Stack Artışı                     | Yükselen tık sesi. Her 5 ardışık dokunuşta çalan, stack arttıkça pitch'i yükselen kısa "tick" sesi. İvme kazanma, combo oluşturma hissi.                      | ~0.08–0.15s one-shot | `PlayMomentumTick()` — Her 5. stack'te çalar. Pitch: 0.90→1.30 arası stack/cap oranıyla ölçeklenir. Rate-limit: 0.12s cooldown | ✅ BAĞLI       |
| **T2** | `momentumResetClip` — Momentum Sıfırlanma                      | İnen ton / düşüş sesi. Oyuncu dokunmayı bıraktığında (reset window aşımı) combo'nun sıfırlanmasını haber veren kısa "düşüş" tonu. Kayıp, yeniden başla hissi. | ~0.3–0.5s one-shot   | `PlayMomentumReset()` — MomentumController stack sıfırladığında tetiklenir                                                     | ✅ BAĞLI       |
| **T4** | `carEvolutionClip` — Araba Evrim                               | Level-up fanfarı. Araba yeni bir görsel evrime ulaştığında çalan parlak zafer fanfarı. İlerleme, prestij hissi.                                               | ~1.0–1.5s one-shot   | `PlayCarEvolution()` — CarEvolution.ApplyStage() ile tetiklenir                                                                | ✅ BAĞLI       |
| **T5** | `cashbackClip` — Küçük Yatırım Cashback                        | Yumuşak cha-ching. SmallInvestment kartı ile harcamanın bir kısmı geri geldiğinde minik bir "para geri" sesi. İncelikli kazanım, akıllı yatırım hissi.        | ~0.2–0.4s one-shot   | `PlayCashback()` — Metot mevcut ama SmallInvestmentController'da çağrılmıyor                                                   | ⚠️ BAĞLI DEĞİL |

---

## 7. UI / UX SESLERİ

Tüm menü, panel, popup ve buton etkileşim sesleri.

### 7A. TEMEL NAVİGASYON

| KOD    | İSİM                              | AÇIKLAMA                                                                                                                   | SÜRE / DAVRANIŞ      | TEKNİK NOT                                                                             | DURUM    |
| ------ | --------------------------------- | -------------------------------------------------------------------------------------------------------------------------- | -------------------- | -------------------------------------------------------------------------------------- | -------- |
| **U1** | `uiClickClip` — Tab/Buton Tıklama | Yumuşak "tok" sesi. Alt bar tab geçişlerinde çalan hafif, temiz bir dokunuş sesi. Rahatsız etmeyen, çok sık duyulabilecek. | ~0.05–0.1s one-shot  | `PlayUIClick()` — %70 volume ile çalar. BottomBarController.OnTabButtonClicked()       | ✅ BAĞLI |
| **U2** | `panelOpenClip` — Panel Açılış    | Nazik "fwip" sesi. Bank, ShopCards, TimeWarp, Ranking paneli açılırken hafif bir süpürme sesi. Geçiş, yeni içerik hissi.   | ~0.15–0.25s one-shot | `PlayPanelOpen()` — PanelTransitionManager.SwitchTo() — Clicker dışı tab'lara geçerken | ✅ BAĞLI |
| **U3** | `panelCloseClip` — Panel Kapanış  | Ters "fwip" sesi. Panelden Clicker'a geri dönerken U2'nin daha yumuşak, ters versiyonu. Geri dönüş hissi.                  | ~0.12–0.20s one-shot | `PlayPanelClose()` — Clicker tab'ına geçerken                                          | ✅ BAĞLI |
| **U4** | `popupAppearClip` — Popup Belirme | Yumuşak "pop" sesi. Sandık detay popup'ı gibi pencereler açıldığında hafif bir balon patlaması sesi. Bilgi sunumu hissi.   | ~0.1–0.2s one-shot   | `PlayPopupAppear()` — ChestPopupController.ShowPopupForChest()                         | ✅ BAĞLI |

### 7B. SANDIK YÖNETİMİ (Ana Ekran)

| KOD    | İSİM                                              | AÇIKLAMA                                                                                                            | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                                                                 | DURUM          |
| ------ | ------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ------------------ | ---------------------------------------------------------------------------------------------------------- | -------------- |
| **U5** | `startUnlockClip` — Kilit Açma Başlat             | Mekanik tık sesi. Sandık zamanlayıcısını başlatan butonun onay sesi. Mekanizma devreye girme, işlem başlatma hissi. | ~0.2–0.3s one-shot | `PlayStartUnlock()` — Metot mevcut, ChestPopupController'daki "Start Unlock" butonuna bağlanması gerekiyor | ⚠️ BAĞLI DEĞİL |
| **U6** | `openNowClip` — Hemen Aç                          | Coin harcama + whoosh. NitroCoin harcayarak sandığı anında açma butonunun sesi. Acele, hızlı ödül hissi.            | ~0.3–0.5s one-shot | `PlayOpenNow()` — Metot mevcut, "Open Now" butonuna bağlanması gerekiyor                                   | ⚠️ BAĞLI DEĞİL |
| **U7** | `chestTimerDoneClip` — Sandık Zamanlayıcısı Bitti | Yumuşak ding. Sandık geri sayımı sıfırlandığında "hazır!" bildirimi. Beklemenin ödülü, hazır olma hissi.            | ~0.3–0.5s one-shot | `PlayChestTimerDone()` — Metot mevcut, timer tamamlandığında çalması gerekiyor                             | ⚠️ BAĞLI DEĞİL |

### 7C. GÜNLÜK TEKLIFLER / ÖDÜLLER

| KOD    | İSİM                                         | AÇIKLAMA                                                                                                             | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                                  | DURUM    |
| ------ | -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- | ------------------ | --------------------------------------------------------------------------- | -------- |
| **U8** | `dailyFreeClaimClip` — Günlük Ücretsiz Ödül  | Hediye pling'i. Ücretsiz günlük ödül alındığında tatmin edici bir "bedava!" sesi. Mutlu sürpriz, günlük bonus hissi. | ~0.3–0.5s one-shot | `PlayDailyFreeClaim()` — DailyOffersController ücretsiz slot toplandığında  | ✅ BAĞLI |
| **U9** | `dailyPackBuyClip` — Günlük Paket Satın Alma | Satın alma onayı. NitroCoin ile günlük kart paketi alındığında kasa sesi. Yatırım, alışveriş hissi.                  | ~0.3–0.5s one-shot | `PlayDailyPackBuy()` — DailyOffersController ücretli slot satın alındığında | ✅ BAĞLI |

### 7D. KART & ÖDÜL SİSTEMİ

| KOD     | İSİM                                            | AÇIKLAMA                                                                                                                                    | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                                             | DURUM          |
| ------- | ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ | -------------------------------------------------------------------------------------- | -------------- |
| **U10** | `cardPopupOpenClip` — Kart Detay Popup Açılış   | Kart çevirme/reveal. Kart koleksiyonunda bir kartın detay popup'ı açıldığında "flip" + shimmer sesi. Koleksiyon inceleme hissi.             | ~0.3–0.5s one-shot | `PlayCardPopupOpen()` — Metot mevcut, CardDetailPopupController'a bağlanması gerekiyor | ⚠️ BAĞLI DEĞİL |
| **U12** | `rewardCollectClip` — Ödül Toplama              | Tatmin edici collect sting. Herhangi bir ödül popup'ında "Topla" butonuna basıldığında kısa zafer sesi. Sahiplenme, kazanım hissi.          | ~0.3–0.5s one-shot | `PlayRewardCollect()` — Metot mevcut, ödül toplama butonlarına bağlanması gerekiyor    | ⚠️ BAĞLI DEĞİL |
| **U13** | `missionCompleteClip` — Görev Tamamlama         | Kısa başarı jingle'ı. Blacklist görevlerinden biri tamamlandığında çalan küçük zafer motifi. İlerleme, hedef ödülü hissi.                   | ~0.5–1.0s one-shot | `PlayMissionComplete()` — Metot mevcut, görev tamamlama UI'ına bağlanması gerekiyor    | ⚠️ BAĞLI DEĞİL |
| **U14** | `tierAdvanceClip` — Blacklist Kademe İlerlemesi | Yükselen fanfar. Blacklist'te yeni bir kademeye geçildiğinde (yeni araba açıldığında) büyük bir zafer fanfarı. Prestij, büyük başarı hissi. | ~1.0–2.0s one-shot | `PlayTierAdvance()` — Metot mevcut, BlacklistManager'a bağlanması gerekiyor            | ⚠️ BAĞLI DEĞİL |
| **U15** | `rewardPopupAppearClip` — Ödül Popup Belirme    | Kısa fanfar. Ödül popup'ı göründüğünde çalan kısa bildirim + kutlama sesi. Sürpriz, ödül var hissi.                                         | ~0.3–0.6s one-shot | `PlayRewardPopupAppear()` — RewardPopupController.Show() ile tetiklenir                | ✅ BAĞLI       |

### 7E. GENEL OYUN İÇİ

| KOD      | İSİM                                                         | AÇIKLAMA                                                                                                                                                                                   | SÜRE / DAVRANIŞ                 | TEKNİK NOT                                                                            | DURUM    |
| -------- | ------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------- | ------------------------------------------------------------------------------------- | -------- |
| **TAP**  | `carTapClips[0..10]` — Araba Dokunuş Sesleri (×11 varyasyon) | Tatmin edici mekanik tık dizisi. 11 farklı dokunuş varyasyonu — her biri hafifçe farklı ton/timber. "Metal gövdeye dokunma" hissi ama mobil dostu, doyurucu. Oyunun EN çok duyulacak sesi. | ~0.05–0.12s one-shot (her biri) | `PlayCarTap()` — Ardışık tekrar engellemeli (no-repeat), pitch ±5% rastgele variation | ✅ BAĞLI |
| **BLD**  | `buildingBuyClip` — Bina Satın Alma                          | Kısa inşaat/kasa sesi. Bina satın alındığında çalan mimari onay sesi. Yatırım, büyüme hissi.                                                                                               | ~0.2–0.4s one-shot              | `PlayBuildingBuy()` — Rate-limit: 0.15s cooldown                                      | ✅ BAĞLI |
| **UPG**  | `upgradeClip` — Kart Yükseltme                               | Seviye atlama sesi. Kart yükseltildiğinde (8 kopya toplanıp level up) çalan tatmin edici artış sesi. İlerleme hissi.                                                                       | ~0.4–0.6s one-shot              | `PlayUpgrade()` — CardDetailPopupController'da level-up başarılı olduğunda            | ✅ BAĞLI |
| **GOAL** | `goalCompleteClip` — Hedef Tamamlama                         | Zafer jingle'ı. Blacklist kademe hedefi tamamlandığında çalan kısa zafer melodisi. Büyük başarı, ilerleme hissi.                                                                           | ~0.8–1.2s one-shot              | `PlayGoalComplete()` — BlacklistManager kademe tamamlandığında                        | ✅ BAĞLI |

---

## 8. GARAJ SAHNESİ (Garage Scene)

Garajda arabaları görüntüleme, renk/sticker/parça değiştirme, satın alma.

| KOD     | İSİM                                            | AÇIKLAMA                                                                                                                                                  | SÜRE / DAVRANIŞ     | TEKNİK NOT                                                                                       | DURUM          |
| ------- | ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------------ | -------------- |
| **G1**  | `garageCarSwitchClip` — Araba Değiştirme        | Dijital glitch swoosh. Sol/sağ ok ile araba değiştirildiğinde kısa bir "dijital geçiş" efekti. Futuristik showroom hissi.                                 | ~0.2–0.4s one-shot  | `PlayGarageCarSwitch()` — GarageController.GoLeft()/GoRight() — Glitch animasyonu ile senkronize | ✅ BAĞLI       |
| **G2**  | `garageColorClip` — Renk Uygulama               | Boya spreyi whoosh. Arabaya yeni renk uygulandığında sprey sesi. Kişiselleştirme, yaratıcılık hissi.                                                      | ~0.3–0.5s one-shot  | `PlayGarageColor()` — Metot mevcut ama GarageController.SetColor()'a bağlanmamış                 | ⚠️ BAĞLI DEĞİL |
| **G3**  | `garageStickerClip` — Sticker Uygulama          | Vinil yapıştırma "slap". Sticker uygulandığında "şak" sesi. Eğlenceli kişiselleştirme hissi.                                                              | ~0.2–0.4s one-shot  | `PlayGarageSticker()` — Metot mevcut ama GarageController.SetSticker()'a bağlanmamış             | ⚠️ BAĞLI DEĞİL |
| **G4**  | `garagePartOnClip` — Parça Takma                | Mekanik snap/click. Araba parçası (spoiler, jant vb.) takıldığında "tek" sesi. Montaj, donanım hissi.                                                     | ~0.15–0.3s one-shot | `PlayGaragePartOn()` — Metot mevcut ama GarageController.TogglePart(on)'a bağlanmamış            | ⚠️ BAĞLI DEĞİL |
| **G5**  | `garagePartOffClip` — Parça Çıkarma             | Ters click/sökme. Parça çıkarıldığında G4'ün hafif ters versiyonu. Sökme hissi.                                                                           | ~0.1–0.2s one-shot  | `PlayGaragePartOff()` — Metot mevcut ama TogglePart(off)'a bağlanmamış                           | ⚠️ BAĞLI DEĞİL |
| **G6**  | `garagePurchaseClip` — Garaj Satın Alma         | Kasa sesi. Renk, sticker veya parça satın alındığında onay sesi. Alışveriş, sahiplenme hissi.                                                             | ~0.3–0.5s one-shot  | `PlayGaragePurchase()` — GarageController.FinalizeColorPurchase/StickerPurchase/PartPurchase     | ✅ BAĞLI       |
| **G7**  | `garagePurchaseFailClip` — Satın Alma Başarısız | Yumuşak hata buzzer'ı. Yeterli para yokken satın alma denemesinde nazik bir "hayır" sesi. Engel, yeterli kaynak yok hissi.                                | ~0.2–0.4s one-shot  | `PlayGaragePurchaseFail()` — Metot mevcut ama başarısız alım denemelerine bağlanmamış            | ⚠️ BAĞLI DEĞİL |
| **G8**  | `garageFocusInClip` — Odak Modu Giriş           | Kamera zoom-in sesi. Arabanın detay görünümüne girerken yakınlaşma hissi. Detay inceleme, yakın bakış hissi.                                              | ~0.2–0.4s one-shot  | `PlayGarageFocusIn()` — Metot mevcut ama bağlanmamış                                             | ⚠️ BAĞLI DEĞİL |
| **G9**  | `garageFocusOutClip` — Odak Modu Çıkış          | Kamera zoom-out sesi. Detay görünümünden çıkarken uzaklaşma hissi. Geri çekilme, genel bakış hissi.                                                       | ~0.2–0.4s one-shot  | `PlayGarageFocusOut()` — Metot mevcut ama bağlanmamış                                            | ⚠️ BAĞLI DEĞİL |
| **G10** | `garageLockedClip` — Kilitli Araba Sallama      | Kilit zinciri çıngırdama. Kilitli bir arabaya dokunulduğunda "kilitli!" hissini veren zincir/kilit sesi + hafif sarsıntı. Engel, "henüz açılmamış" hissi. | ~0.3–0.5s one-shot  | `PlayGarageLocked()` — Metot mevcut ama locked overlay etkileşimine bağlanmamış                  | ⚠️ BAĞLI DEĞİL |

---

## 9. SİNEMATİK SAHNE (Cinematic Showcase)

"TakeTheCarScene" — Yeni araba açıldığında dramatik sinematik sunum.

| KOD    | İSİM                                           | AÇIKLAMA                                                                                                                                      | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                                                       | DURUM                |
| ------ | ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | ------------------ | ------------------------------------------------------------------------------------------------ | -------------------- |
| **K1** | `cinematicRevealClip` — Sinematik Açılış Sting | Dramatik reveal sting. Sinematik başladığında çalan epik bir "tadaa!" sesi. Birden yükselen orkestral/elektronik hit. Prestij, heyecan hissi. | ~1.0–2.0s one-shot | `PlayCinematicReveal()` — CarShowcaseDirector.Play() başlangıcında                               | ✅ BAĞLI             |
| **K2** | _(YOK — Kamera Geçiş Whoosh)_                  | Shot-bazlı kamera geçiş sesi. Her sinematik shot değişiminde kullanılabilecek kısa whoosh. CinematicShotSO üzerinde per-shot clip atanabilir. | ~0.2–0.4s one-shot | `PlaySFX(clip)` — CarShowcaseDirector'da mevcut, CinematicShotSO'da sfxClip alanı ile kullanılır | ⚙️ MEVCUT (per-shot) |
| **K3** | `cinematicNameRevealClip` — Araba İsim Reveal  | Metin impact whoosh. Araba markası + model ismi ekrana gelirken vurucu bir "impact" sesi. Marka tanıtma, isim damgası hissi.                  | ~0.4–0.7s one-shot | `PlayCinematicNameReveal()` — ShowcaseCarNameReveal.Play() reveal delay sonrası                  | ✅ BAĞLI             |
| **K5** | `cinematicFadeOutClip` — Sinematik Kapanış     | Dramuatik fade-out drone. Sinematik sona ererken derin, yavaşça sönen bir drone/pad sesi. Final, kapanış hissi.                               | ~1.0–2.0s one-shot | `PlayCinematicFadeOut()` — CarShowcaseDirector.FinishCinematic()                                 | ✅ BAĞLI             |

---

## 10. DÜNYA TOPLANABILIR ÖĞELERİ

| KOD    | İSİM                                           | AÇIKLAMA                                                                                                                     | SÜRE / DAVRANIŞ    | TEKNİK NOT                                                  | DURUM    |
| ------ | ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- | ------------------ | ----------------------------------------------------------- | -------- |
| **WC** | `worldChestCollectClip` — Yolda Sandık Toplama | Yolda beliren sandığa dokunulduğunda çalan toplama sesi. "Buldun!" hissi — keşif + kazanım. Kısa, parlak bir collect efekti. | ~0.3–0.5s one-shot | `PlayWorldChestCollect()` — Chest.OnTapped() ile tetiklenir | ✅ BAĞLI |

---

## 11. ARKA PLAN MÜZİKLERİ (Background Music)

4 sahneye özel müzik parçası + crossfade geçiş sistemi.

| KOD    | İSİM                                           | SAHNE           | AÇIKLAMA                                                                                                                                                 | SÜRE / DAVRANIŞ                    | TEKNİK NOT                                                             |
| ------ | ---------------------------------------------- | --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------- | ---------------------------------------------------------------------- |
| **M1** | `mainSceneMusic` — Ana Sahne Müziği            | Main            | Rahat lo-fi / hafif elektronik loop. Oyuncunun uzun süre tıkladığı sahne — yorucu olmamalı, arka planda rahatça çalabilmeli. Enerjik ama baskın olmayan. | Seamless loop, ~60–120s            | Base volume: %35. Crossfade 1.0s ile geçiş. Boost modunda %50'ye duck. |
| **M2** | `chestSceneMusic` — Sandık Sahnesi Müziği      | ChestOpenScene  | Mistik ambient pad. Büyülü, meraklı atmosfer — sandık açma deneyimini destekleyen gizemli ama sıcak bir ambiyans. Keşif, sürpriz bekleyişi hissi.        | Seamless loop, ~30–60s             | Base volume: %35. Sahne yüklendiğinde otomatik crossfade.              |
| **M3** | `garageSceneMusic` — Garaj Sahnesi Müziği      | NewGarage       | Showroom ambient loop. Lüks, modern showroom atmosferi — hafif synth pad'ler veya minimal beat. Premium, vitrin hissi.                                   | Seamless loop, ~60–90s             | Base volume: %35. Sahne yüklendiğinde otomatik crossfade.              |
| **M4** | `cinematicSceneMusic` — Sinematik Sahne Müziği | TakeTheCarScene | Dramatik pump-up track. Epik araba tanıtım müziği — heyecan, güç, prestij. Yükselen tempo, büyük ses. Kısa sahne için etkili bir build-up.               | Seamless loop veya linear, ~30–60s | Base volume: %35. Sahne yüklendiğinde otomatik crossfade.              |

### Müzik Sistemi Özellikleri

- **Çift AudioSource Crossfade:** A/B kaynakları arasında yumuşak 1.0s crossfade ile kesintisiz geçiş
- **Duck/Restore:** Boost modunda müzik %50'ye indirilir (0.3s fade), bitişte geri çıkar (0.8s fade)
- **Sahne Otomatik Geçiş:** `SceneManager.sceneLoaded` event'i ile sahne değiştiğinde otomatik parça değişimi
- **Kullanıcı Volume:** PlayerPrefs "MusicVolume" key'i ile kalıcı volume ayarı

---

## 12. BAĞLANMAMIŞ SESLER — ENTEGRASYON BEKLEYENLer

Aşağıdaki sesler SFXManager'da tanımlanmış ve Play metotları yazılmış ancak henüz oyun koduna entegre edilmemiş. **Ses dosyaları oluşturulmalı ama programcı tarafından da bağlanmaları gerekecek.**

| #   | KOD | METOT                           | BAĞLANMASI GEREKEN YER                                    | ÖNCELİK   |
| --- | --- | ------------------------------- | --------------------------------------------------------- | --------- |
| 1   | P6  | `PlayChaseSuccess()`            | PoliceCatchController — kovalamaca başarı sonucu          | 🔴 YÜKSEK |
| 2   | P7  | `PlayChaseFail()`               | PoliceCatchController — kovalamaca başarısızlık sonucu    | 🔴 YÜKSEK |
| 3   | P9  | `PlayRadarMiss()`               | Radar.OnMissed() — radar kaçırma anı                      | 🔴 YÜKSEK |
| 4   | P10 | `PlayRadarPopup()`              | RadarPopupController — radar fotoğrafı gösterildiğinde    | 🟡 ORTA   |
| 5   | P11 | `PlayPopularityStageUp()`       | PopularityManager — kademe değişiminde                    | 🟡 ORTA   |
| 6   | F1  | `PlayTurboFingerActivate()`     | TurboFingerController — state → Active geçişinde          | 🔴 YÜKSEK |
| 7   | F2  | `PlayTurboFingerDeactivate()`   | TurboFingerController — state → Cooldown geçişinde        | 🟡 ORTA   |
| 8   | F4  | `PlayGarageManagerDeactivate()` | GarageManagerController — state → Cooldown geçişinde      | 🟡 ORTA   |
| 9   | T5  | `PlayCashback()`                | SmallInvestmentController — OnRefundApplied event'inde    | 🟢 DÜŞÜK  |
| 10  | U5  | `PlayStartUnlock()`             | ChestPopupController — "Start Unlock" butonunda           | 🟡 ORTA   |
| 11  | U6  | `PlayOpenNow()`                 | ChestPopupController — "Open Now" butonunda               | 🟡 ORTA   |
| 12  | U7  | `PlayChestTimerDone()`          | ChestInventoryManager — timer tamamlandığında             | 🟡 ORTA   |
| 13  | U10 | `PlayCardPopupOpen()`           | CardDetailPopupController — popup açılışında              | 🟢 DÜŞÜK  |
| 14  | U12 | `PlayRewardCollect()`           | RewardPopupController — "Collect" butonunda               | 🟡 ORTA   |
| 15  | U13 | `PlayMissionComplete()`         | BlacklistGoalUI — görev tamamlandığında                   | 🟡 ORTA   |
| 16  | U14 | `PlayTierAdvance()`             | BlacklistManager — yeni kademeye geçişte                  | 🔴 YÜKSEK |
| 17  | G2  | `PlayGarageColor()`             | GarageController.SetColor() — renk uygulandığında         | 🟢 DÜŞÜK  |
| 18  | G3  | `PlayGarageSticker()`           | GarageController.SetSticker() — sticker uygulandığında    | 🟢 DÜŞÜK  |
| 19  | G4  | `PlayGaragePartOn()`            | GarageController.TogglePart(true) — parça takıldığında    | 🟢 DÜŞÜK  |
| 20  | G5  | `PlayGaragePartOff()`           | GarageController.TogglePart(false) — parça çıkarıldığında | 🟢 DÜŞÜK  |
| 21  | G7  | `PlayGaragePurchaseFail()`      | GarageController — satın alma başarısızlığında            | 🟡 ORTA   |
| 22  | G8  | `PlayGarageFocusIn()`           | Garaj focus mode girişinde                                | 🟢 DÜŞÜK  |
| 23  | G9  | `PlayGarageFocusOut()`          | Garaj focus mode çıkışında                                | 🟢 DÜŞÜK  |
| 24  | G10 | `PlayGarageLocked()`            | GarageController — kilitli araba etkileşiminde            | 🟡 ORTA   |

---

## 13. EKSİK SES FIRSATLARI — YENİ ÖNERİLER

Mevcut kod tabanında ses hook'u **hiç bulunmayan** ama kullanıcı deneyimini zenginleştirecek potansiyel sesler:

| #   | ÖNERİ                                           | AÇIKLAMA                                                                                                | ÖNERİLEN YAPI               | ÖNCELİK  |
| --- | ----------------------------------------------- | ------------------------------------------------------------------------------------------------------- | --------------------------- | -------- |
| 1   | **Yol Ambiyans Loop**                           | Ana sahnede sürekli çalan hafif rüzgâr + araba yol sesi ambiyansı. Oyun dünyasına derinlik katar.       | Seamless loop, düşük volume | 🟡 ORTA  |
| 2   | **Hız Değişim Efekti**                          | Araba hızlandığında/yavaşladığında (boost, chase) motor pitch'inin yumuşak geçişi                       | Dynamic pitch scaling loop  | 🟢 DÜŞÜK |
| 3   | **Para Sayacı Tık**                             | CurrencyManager'da para artarken (özellikle büyük miktarlarda) hafif bir "counter tick" sesi            | Rate-limited tick, ~0.05s   | 🟢 DÜŞÜK |
| 4   | **İlk Oynama / Tutorial Sesleri**               | Oyuna ilk girişte rehber sesler (ok işareti, ilk dokunuş eğitimi)                                       | One-shot, dostça tonlar     | 🟢 DÜŞÜK |
| 5   | **Ayarlar Toggle Sesi**                         | SFX on/off, müzik on/off butonları için küçük bir toggle sesi                                           | ~0.1s one-shot              | 🟢 DÜŞÜK |
| 6   | **Kovalamaca Sırasında Dokunma Geri Bildirimi** | Chase modunda her dokunuşta mevcut PlayCarTap'a ek olarak "acele/panik" tıklama hissi                   | Pitch-shifted tap variant   | 🟡 ORTA  |
| 7   | **Bina Animasyonu Tamamlanma**                  | Bina seviye atladığında kısa bir inşaat tamamlanma sesi (PlayBuildingBuy'dan ayrı)                      | ~0.5s one-shot              | 🟢 DÜŞÜK |
| 8   | **Nitro Coin Yolda Spawn Efekti**               | Nitro coin yolda belirdiğinde hafif bir "ortaya çıkma" sesi                                             | ~0.1s, çok kısık volume     | 🟢 DÜŞÜK |
| 9   | **Polis Araçası Giriş Animasyonu Sesi**         | PoliceCatchController'da polis aracı drift-in animasyonu sırasında lastik gıcırtısı + motor             | ~0.8s one-shot              | 🟡 ORTA  |
| 10  | **Kovalamaca "Last Chance Zone" Alarm**         | DangerFraction > %75 olduğunda tetiklenen acil alarm loop'u (ekran kenarı kırmızı flash'a eşlik edecek) | Short loop, ~0.5s, urgent   | 🟡 ORTA  |

---

## 14. TEKNİK DAVRANIŞ REHBERİ

### Volume Kontrolleri

| Kaynak            | Base Volume        | Kullanıcı Volume        | Notlar                              |
| ----------------- | ------------------ | ----------------------- | ----------------------------------- |
| SFX (sfxSource)   | 1.0× (clip volume) | PlayerPrefs "SFXVolume" | Tüm one-shot'lar bu kaynaktan çalar |
| UI Click          | 0.70×              | × UserVolume            | Daha kısık, rahatsız etmeyen        |
| Magnet Pull       | 0.50×              | × UserVolume            | Spam engellemesi için kısık         |
| Nitro Coin Magnet | 0.60×              | × UserVolume            | N1'den daha kısık                   |
| Rain Loop         | 0.30×              | × UserVolume            | 0.5s fade-in / fade-out             |
| Boost Loop        | 0.25×              | × UserVolume            | 0.3s fade-in / fade-out             |
| Müzik             | 0.35×              | × UserVolume            | PlayerPrefs "MusicVolume"           |
| Müzik Duck        | 0.35 × 0.50        | × UserVolume            | Boost sırasında                     |

### Rate-Limiting Kuralları

| Ses                     | Cooldown | Ek Kısıtlama                       |
| ----------------------- | -------- | ---------------------------------- |
| Nitro Coin Collect (N1) | 0.05s    | Max 3 eşzamanlı PlayOneShot        |
| Nitro Coin Magnet (N2)  | 0.05s    | Ortak cooldown (N1 ile aynı timer) |
| Momentum Tick (T1)      | 0.12s    | Sadece her 5. stack'te çalar       |
| Building Buy (BLD)      | 0.15s    | —                                  |

### Pitch Variation Kuralları

| Ses                     | Min Pitch | Max Pitch | Notlar                          |
| ----------------------- | --------- | --------- | ------------------------------- |
| Car Tap (TAP)           | 0.95      | 1.05      | Her dokunuşta ±5%               |
| Chest Hop (C2)          | 0.95      | 1.10      | Hafif yükselen his              |
| Nitro Coin Collect (N1) | 0.90      | 1.10      | ±10% belirgin variation         |
| Momentum Tick (T1)      | 0.90      | 1.30      | Stack/cap oranıyla linear artar |

### Crossfade Süreleri

| Geçiş                         | Süre | Ease   |
| ----------------------------- | ---- | ------ |
| Müzik sahne geçişi            | 1.0s | Linear |
| Music Duck (boost başlangıcı) | 0.3s | —      |
| Music Restore (boost bitişi)  | 0.8s | —      |
| Rain Loop fade-in             | 0.5s | —      |
| Rain Loop fade-out            | 0.5s | —      |
| Boost Loop fade-in            | 0.3s | —      |
| Boost Loop fade-out           | 0.3s | —      |

---

## 15. TOPLAM SAYILAR

### Ses Dosyası İhtiyaç Özeti

| Kategori                                 | Bağlı (Aktif) | Bağlı Değil (Kod Mevcut) | Yeni Öneri |  TOPLAM  |
| ---------------------------------------- | :-----------: | :----------------------: | :--------: | :------: |
| Sandık Sistemi (C1–C10)                  |      10       |            0             |     0      |  **10**  |
| Nitro Sistemi (N1–N11)                   |      11       |            0             |     0      |  **11**  |
| Boost Sistemi (B1–B5)                    |       5       |            0             |     0      |  **5**   |
| Polis/Kovalamaca — SFXManager (P6–P11)   |       1       |            5             |     0      |  **6**   |
| Polis/Kovalamaca — Chase Audio (PC1–PC5) |       5       |            0             |     0      |  **5**   |
| Kart Efekt Sistemleri (F1–F5, T1–T5)     |       5       |            4             |     0      |  **9**   |
| UI / UX (U1–U15 + TAP/BLD/UPG/GOAL)      |      11       |            7             |     0      |  **18**  |
| Garaj Sahnesi (G1–G10)                   |       2       |            8             |     0      |  **10**  |
| Sinematik (K1, K3, K5 + per-shot)        |  3+per-shot   |            0             |     0      |  **3+**  |
| Dünya Toplanabilir                       |       1       |            0             |     0      |  **1**   |
| Arka Plan Müzikleri (M1–M4)              |       4       |            0             |     0      |  **4**   |
| Araba Dokunuş Varyasyonları              |      11       |            0             |     0      |  **11**  |
| **Yeni Öneriler**                        |       —       |            —             |     10     |  **10**  |
|                                          |               |                          |            |          |
| **GENEL TOPLAM**                         |    **69**     |          **24**          |   **10**   | **≈103** |

### Dosya Tipi Dağılımı

- **One-shot efektler:** ~85 adet
- **Loop efektler:** 7 adet (rain, boost hum, chase loop, heartbeat, siren, engine, road ambiance)
- **Müzik parçaları:** 4 adet
- **Çoklu varyasyon:** 11 adet araba dokunuş sesi

### Öncelik Sıralaması

1. 🔴 **YÜKSEK** — Oynanışı doğrudan etkileyen sesler (chase success/fail, turbo finger, tier advance, radar miss) — **5 ses**
2. 🟡 **ORTA** — Deneyimi zenginleştiren sesler (UI hooks, popularity, garage locked, yeni öneriler) — **14 ses**
3. 🟢 **DÜŞÜK** — İnce dokunuş sesleri (garage detail, cashback, spawn efektleri) — **15 ses**

---

> **NOT:** Bu doküman tamamen mevcut kod tabanı analiz edilerek oluşturulmuştur. "✅ BAĞLI" olarak işaretlenen sesler oyun kodunda aktif olarak tetiklenmektedir. "⚠️ BAĞLI DEĞİL" olarak işaretlenenler için Play metotları mevcuttur ancak oyun akışına entegrasyonları programcı tarafından yapılacaktır. "Yeni Öneriler" bölümü tamamen yeni kod gerektiren önerilerdir.
