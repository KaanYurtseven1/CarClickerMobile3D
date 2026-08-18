# UI LIST DOĞRULAMA RAPORU

> Proje: CarClickerMobile3D  
> Tarih: Güncel  
> Kaynak: UI List (1).docx ↔ Assets/UI/UIListAssets/ ↔ UI_LIST_MAPPING_REPORT.md

---

## GERÇEK DOSYA YAPISI

Asset'ler `Assets/UI/UIListAssets/` altında **4 alt klasör** halinde organize edilmiş:

| Klasör             | Dosya Sayısı | İçerik                                    |
| ------------------ | ------------ | ----------------------------------------- |
| `bank ui/`         | 21 dosya     | Madde 1-6, 27-28, 34-39, 51 + varyantlar  |
| `black list ui 2/` | 33 dosya     | Madde 11, 14-26 + mission/reward ikonları |
| `garage ui/`       | 7 dosya      | Madde 40-42, 44-49 (paylaşımlı)           |
| `shop ui/`         | 3 dosya      | Madde 7-8 + gradient                      |

### Kritik Dosya Adlandırma Farklılıkları

Rapor **basit numaralı dosyalar (1.png, 2.png...)** varsaymıştı. Gerçekte:

1. **Paylaşımlı dosyalar** (virgüllü): `2,9,10.png` / `18,20.png` / `44,48.png` / `45,49.png` / `23.7-54.png`
2. **Çift noktalı dosyalar**: `35..png` / `36..png` / `37..png` / `39..png`
3. **Varyant dosyalar**: `37.1.png` / `37.2.png` / `38.1.png` / `51.1.png` / `51.2.png`
4. **İsimsiz gradient'ler**: `bottom bar bank gradient mavi bg.png` / `black list bottom arkası blurlu bg.png` / `shop bg gradient blurlu.png`
5. **Numaralama kayması**: Mission ikonları `16.1`-`16.13` (rapor: 16-16.12), Reward ikonları `23.1`-`23.9` (rapor: 23-23.8)

---

## KATEGORİ 1: DOĞRU EŞLEŞMELER ✅

Unity hedefleri (scene/path/component/field) doğru, sadece dosya yolu alt klasör düzeltmesi gereken maddeler.

| Madde | Açıklama                 | Rapordaki Dosya | Gerçek Dosya Yolu        | Unity Hedefi                                 | Not                                            |
| ----- | ------------------------ | --------------- | ------------------------ | -------------------------------------------- | ---------------------------------------------- |
| 1     | Bank Header              | "1"             | `bank ui/1.png`          | Main → Panel_Bank/Header → Image.Sprite      | ✅                                             |
| 3     | DailyOffers Section BG   | "3"             | `bank ui/3.png`          | Main → Section_DailyOffers → Image.Sprite    | ✅                                             |
| 5     | DailyOffers Slot Frame   | "5"             | `bank ui/5.png`          | Main → DailyOffer slot → Image               | ⚠️ Görsel: lavender, "kırmızı" değil           |
| 6     | CardPacks Section BG     | "6"             | `bank ui/6.png`          | Main → CardPacks Section → Image.Sprite      | ✅                                             |
| 7     | Shop Header/Buton        | "7"             | `shop ui/7.png`          | Main → Panel_ShopCards/Header → Image        | ✅ Klasör farklı: shop ui/                     |
| 8     | Items/Cards Buton        | "8"             | `shop ui/8.png`          | Main → Btn_TabCards → Image                  | ✅ Klasör farklı: shop ui/                     |
| 11    | Blacklist Header         | "11"            | `black list ui 2/11.png` | Main → Panel_BlackList/Header → Image        | ✅                                             |
| 12    | Blacklist BG (=Madde 2)  | "2" reuse       | `bank ui/2,9,10.png`     | Main → Panel_BlackList → Image               | ✅ Doğru niyetle, dosya paylaşımlı             |
| 14    | Missions Container Frame | "14"            | `black list ui 2/14.png` | Main → MissionsContainer → Image             | ✅                                             |
| 15    | MissionRow BG            | "15"            | `black list ui 2/15.png` | Prefab → MissionRow root → Image             | ✅                                             |
| 17    | BarBG                    | "17"            | `black list ui 2/17.png` | Prefab → MissionRow/BarBG → Image            | ✅                                             |
| 19    | BarBGComplete            | "19"            | `black list ui 2/19.png` | Prefab → MissionRow/BarBGComplete → Image    | ✅                                             |
| 21    | CompletedBtn             | "21"            | `black list ui 2/21.png` | Prefab → MissionRow/CompletedBtn → Image     | ✅                                             |
| 22    | RewardPopup BG           | "22"            | `black list ui 2/22.png` | Main → RewardPopup/PopupPanel → Image        | ✅                                             |
| 24    | CollectBtn               | "24"            | `black list ui 2/24.png` | Main → RewardPopup/CollectBtn → Image        | ✅                                             |
| 25    | Claimed Görseli          | "25"            | `black list ui 2/25.png` | Prefab → MissionRow/CompletedBtn (claimed)   | ✅ Dosya MEVCUT (rapor "isteğe bağlı" demişti) |
| 26    | TakeTheCarButton         | "26"            | `black list ui 2/26.png` | Main → TakeTheCarButton → Image              | ✅                                             |
| 27    | PopularityBar BG         | "27"            | `bank ui/27.png`         | Main → PopularityBar/Popularity_BG → Image   | ✅                                             |
| 28    | PopularityBar Fill       | "28"            | `bank ui/28.png`         | Main → PopularityBar/Popularity_Fill → Image | ✅                                             |
| 34    | ChestPopup BG Common     | "34"            | `bank ui/34.png`         | Main → ChestPopupController.commonPopupBg    | ✅                                             |
| 38    | OpenNow Button           | "38"            | `bank ui/38.png`         | Main → ChestPopup/openNowObj → Image         | ✅                                             |
| 40    | GoldIcon                 | "40"            | `garage ui/40.png`       | NewGarage → GoldIcon → Image                 | ✅                                             |
| 41    | NitroIcon                | "41"            | `garage ui/41.png`       | NewGarage → NitroIcon → Image                | ✅                                             |
| 42    | BuyPopup BG              | "42"            | `garage ui/42.png`       | NewGarage → BuyPopupPanel/child → Image      | ✅                                             |
| 46    | LockedUI                 | "46"            | `garage ui/46.png`       | NewGarage → LockedUI → Image                 | ✅                                             |
| 47    | ExitPopup BG             | "47"            | `garage ui/47.png`       | NewGarage → ExitPopupPanel/child → Image     | ✅                                             |

**Toplam: 27 madde doğru** (sadece alt klasör yolu düzeltmesi gerekiyor)

---

## KATEGORİ 2: YANLIŞ EŞLEŞMELER ❌

Dosya adı yanlış, paylaşımlı dosya belirtilmemiş, numaralama hatası veya madde uydurulmuş.

### 2.1 — Paylaşımlı Dosya Hataları (rapor ayrı dosya varsaymış)

| Madde  | Rapordaki Dosya | Gerçek Dosya                  | Paylaşılan Maddeler | Açıklama                                                                      |
| ------ | --------------- | ----------------------------- | ------------------- | ----------------------------------------------------------------------------- |
| **2**  | "2" (ayrı)      | `bank ui/2,9,10.png`          | 2 + 9 + 10          | Ortak Panel BG — TEK dosya 3 maddeye hizmet ediyor                            |
| **9**  | "2" reuse       | `bank ui/2,9,10.png`          | 2 + 9 + 10          | Shop BG — rapor "Madde 2 reuse" demiş, kısmen doğru ama dosya adı yanlış      |
| **10** | "2" reuse       | `bank ui/2,9,10.png`          | 2 + 9 + 10          | Blacklist BG — rapor "Madde 2 reuse" demiş, kısmen doğru ama dosya adı yanlış |
| **18** | "18" (ayrı)     | `black list ui 2/18,20.png`   | 18 + 20             | BarFill — TEK dosya hem 18 hem 20 için                                        |
| **20** | "20" (ayrı)     | `black list ui 2/18,20.png`   | 18 + 20             | BarFillFull — TEK dosya hem 18 hem 20 için                                    |
| **44** | "44" (ayrı)     | `garage ui/44,48.png`         | 44 + 48             | BtnYes — TEK dosya hem BuyPopup hem ExitPopup Yes butonu                      |
| **48** | "48" (ayrı)     | `garage ui/44,48.png`         | 44 + 48             | ExitPopup YesBtn — Madde 44 ile aynı dosya                                    |
| **45** | "45" (ayrı)     | `garage ui/45,49.png`         | 45 + 49             | BtnIncele — TEK dosya hem BuyPopup hem ExitPopup No butonu                    |
| **49** | "49" (ayrı)     | `garage ui/45,49.png`         | 45 + 49             | ExitPopup NoBtn — Madde 45 ile aynı dosya                                     |
| **54** | "54" (ayrı)     | `black list ui 2/23.7-54.png` | 23.7 + 54           | Kaplama icon — TEK dosya hem Blacklist reward hem ChestReveal için            |

### 2.2 — Çift Noktalı Dosya Adı Hataları

| Madde  | Rapordaki Dosya | Gerçek Dosya      | Not                                                                                                                       |
| ------ | --------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------------- |
| **35** | "35"            | `bank ui/35..png` | ChestPopup Rare BG — çift nokta (35..png). ⚠️ Görsel: pink/magenta (Legendary'ye benziyor, Rare değil!)                   |
| **36** | "36"            | `bank ui/36..png` | ChestPopup Legendary BG — çift nokta (36..png). ⚠️ Görsel: grey/blue (Rare'e benziyor, Legendary değil!)                  |
| **37** | "37"            | `bank ui/37..png` | StartUnlock Button — çift nokta (37..png). Ek: 37.1.png (mavi/Common) ve 37.2.png (mor/Legendary) varyantları da mevcut   |
| **39** | "39"            | `bank ui/39..png` | Rapor: "RadarPopup BG (620×750)". Görsel: küçük metalik kare frame — popup BG değil, chest slot/item frame gibi görünüyor |

### 2.3 — Numaralama Hataları

| Madde        | Rapordaki Numara         | Gerçek Dosyalar          | Hata                                                                                                                                                                   |
| ------------ | ------------------------ | ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **16 (x13)** | 16, 16.1, 16.2 ... 16.12 | `16.1.png` — `16.13.png` | Düz "16.png" YOK. Dosyalar 16.1'den başlayıp 16.13'e kadar gidiyor. Rapor 16'dan başlayıp 16.12'ye kadar yazmış. +1 kayma var.                                         |
| **23 (x9)**  | 23, 23.1, 23.2 ... 23.8  | `23.1.png` — `23.9.png`  | Düz "23.png" YOK. Dosyalar 23.1'den başlayıp 23.9'a kadar gidiyor. Rapor 23'ten başlayıp 23.8'e kadar yazmış. +1 kayma var. Ayrıca `23.7-54.png` paylaşımlı dosya var. |

### 2.4 — Uydurulmuş/Yanlış Maddeler

| Madde   | Açıklama               | Sorun                                                                                                                                                                                           |
| ------- | ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **3.1** | DailyOffers Slot Frame | Bu madde **UYDURMA**. Docx'te "3.1" numaralı ayrı bir madde YOK. Docx'te paragraf devamı olarak yazılmış bir açıklama metniydi, rapor bunu ayrı madde sandı. `3.1.png` dosyası da mevcut DEĞİL. |

### 2.5 — Varyant Dosyaları (raporda hiç bahsedilmemiş)

| Dosya      | Klasör   | Görsel                          | Açıklama                                                                                      |
| ---------- | -------- | ------------------------------- | --------------------------------------------------------------------------------------------- |
| `37.1.png` | bank ui/ | Mavi rounded buton              | StartUnlock → Common chest varyantı                                                           |
| `37.2.png` | bank ui/ | Mor/magenta rounded buton       | StartUnlock → Legendary chest varyantı                                                        |
| `38.1.png` | bank ui/ | **Mavi nitro şişeleri — IKON**  | ⚠️ Bu bir buton değil, ikondur! openNowObj içindeki NitroCoin child Image için kullanılabilir |
| `51.1.png` | bank ui/ | Gri/gümüş gradient tam ekran    | ChestOpenScene BG → Rare varyantı                                                             |
| `51.2.png` | bank ui/ | Pink/magenta gradient tam ekran | ChestOpenScene BG → Legendary varyantı                                                        |

### 2.6 — İsimsiz Gradient Dosyaları (raporda hiç bahsedilmemiş)

| Dosya                                    | Klasör           | Görsel                   | Olası Unity Hedefi                     |
| ---------------------------------------- | ---------------- | ------------------------ | -------------------------------------- |
| `bottom bar bank gradient mavi bg.png`   | bank ui/         | Açık mavi gradient şerit | Main → BottomBar arka planı            |
| `black list bottom arkası blurlu bg.png` | black list ui 2/ | Şeftali/somon gradient   | Main → Blacklist paneli alt alanı      |
| `shop bg gradient blurlu.png`            | shop ui/         | Pink/mauve gradient      | Main → Shop paneli gradient arka planı |

### 2.7 — Rapordaki Unity Hedef Uyarıları

| Madde  | Rapordaki Hedef                              | Olası Sorun                                                                                                                         |
| ------ | -------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| **4**  | DailyOfferSlotUI.icon (free gold/nitro icon) | Görsel: kırmızı/bordo dikdörtgen frame. "Icon" yerine **çerçeve** gibi görünüyor. DailyOffer slot frame olarak daha uygun olabilir. |
| **35** | ChestPopupController.rarePopupBg             | Görsel: pink/magenta — **Legendary** paneline benziyor, Rare değil                                                                  |
| **36** | ChestPopupController.legendaryPopupBg        | Görsel: grey/blue — **Rare** paneline benziyor, Legendary değil                                                                     |
| **39** | RadarPopup Background (620×750)              | Görsel: küçük metalik kare frame — **chest slot frame** gibi görünüyor, büyük popup BG değil                                        |

---

## KATEGORİ 3: EKSİK / BELİRSİZ MADDELER ⚠️

Bu maddeler için `UIListAssets/` klasörlerinde dosya **bulunamadı**.

| Madde  | Açıklama                               | Rapordaki Unity Hedefi                       | Durum                              |
| ------ | -------------------------------------- | -------------------------------------------- | ---------------------------------- |
| **13** | Blacklist Car Images (960×400, 6 adet) | BlacklistTierSO → carImage                   | ❌ DOSYA YOK — 13.png mevcut değil |
| **29** | BoostBar BG (990×20)                   | Slider_BoostBar/Background → Image           | ❌ DOSYA YOK — 29.png mevcut değil |
| **30** | BoostBar Fill (990×20)                 | Slider_BoostBar/Fill → Image                 | ❌ DOSYA YOK — 30.png mevcut değil |
| **31** | Chest UI Icon - Common (180×140)       | ChestSlotUI.commonIcon                       | ❌ DOSYA YOK — 31.png mevcut değil |
| **32** | Chest UI Icon - Rare (180×140)         | ChestSlotUI.rareIcon                         | ❌ DOSYA YOK — 32.png mevcut değil |
| **33** | Chest UI Icon - Legendary (180×140)    | ChestSlotUI.legendaryIcon                    | ❌ DOSYA YOK — 33.png mevcut değil |
| **43** | CloseButton (80×80)                    | GarageBuyPopupController.closeButton → Image | ❌ DOSYA YOK — 43.png mevcut değil |
| **50** | Garage Arkaplanı (1080×1920)           | NewGarage → Canvas/Background → Image        | ❌ DOSYA YOK — 50.png mevcut değil |
| **52** | Money Icon - Chest Reveal (270×240)    | ChestRewardRevealController.moneySprite      | ❌ DOSYA YOK — 52.png mevcut değil |
| **53** | Nitro Icon - Chest Reveal (270×240)    | ChestRewardRevealController.nitroSprite      | ❌ DOSYA YOK — 53.png mevcut değil |

**Toplam: 10 madde eksik** — Tasarımcıdan bu asset'ler istenmelidir.

---

## GENEL ÖZET

| Kategori                      | Sayı   | Açıklama                                                        |
| ----------------------------- | ------ | --------------------------------------------------------------- |
| ✅ Doğru eşleşme              | 27     | Unity hedefi doğru, sadece alt klasör yolu düzeltmesi gerekiyor |
| ❌ Dosya adı yanlış           | 10     | Paylaşımlı dosyalar (virgüllü)                                  |
| ❌ Çift nokta hatası          | 4      | 35..png, 36..png, 37..png, 39..png                              |
| ❌ Numaralama kayması         | 2 grup | Mission icons (16.1-16.13), Reward icons (23.1-23.9)            |
| ❌ Uydurulmuş madde           | 1      | Madde 3.1 — docx'te bu numara yok                               |
| ⚠️ Raporda eksik varyantlar   | 5      | 37.1, 37.2, 38.1, 51.1, 51.2                                    |
| ⚠️ Raporda eksik gradient'ler | 3      | İsimsiz gradient BG dosyaları                                   |
| ⚠️ Görsel uyumsuzluk          | 4      | Madde 4, 35, 36, 39 — görsel içerik açıklamayla örtüşmüyor      |
| ❌ Dosya mevcut değil         | 10     | 13, 29, 30, 31, 32, 33, 43, 50, 52, 53                          |

---

## DOSYA → MADDE TAM HARİTASI

### bank ui/ (21 dosya)

| Dosya                                  | Madde(ler)   | Görsel Açıklama                                        |
| -------------------------------------- | ------------ | ------------------------------------------------------ |
| `1.png`                                | 1            | Metalik krom/mavi header bar                           |
| `2,9,10.png`                           | 2, 9, 10, 12 | Gri tam ekran panel BG                                 |
| `3.png`                                | 3            | Mavi yuvarlak köşeli section BG                        |
| `4.png`                                | 4            | Kırmızı/bordo dikdörtgen frame mavi kenar              |
| `5.png`                                | 5            | Lavanta dikdörtgen frame mavi kenar                    |
| `6.png`                                | 6            | Mavi geniş dikdörtgen BG                               |
| `27.png`                               | 27           | Gold/bronz süslü bar frame                             |
| `28.png`                               | 28           | Pink/şeftali hap buton                                 |
| `34.png`                               | 34           | Mavi panel BG yuvarlak köşe (Common)                   |
| `35..png`                              | 35           | Pink/magenta panel metalik frame (⚠️ Legendary?)       |
| `36..png`                              | 36           | Gri/mavi-gri panel devre deseni (⚠️ Rare?)             |
| `37..png`                              | 37           | Küçük beyaz/açık mavi yuvarlak dikdörtgen buton        |
| `37.1.png`                             | 37 varyant   | Mavi yuvarlak buton (Common StartUnlock?)              |
| `37.2.png`                             | 37 varyant   | Mor yuvarlak buton (Legendary StartUnlock?)            |
| `38.png`                               | 38           | Açık mavi/lavanta yuvarlak dikdörtgen (OpenNow buton)  |
| `38.1.png`                             | 38 ikon      | Mavi nitro şişeleri — IKON (NitroCoin görseli)         |
| `39..png`                              | 39           | Gri/mavi metalik kare frame                            |
| `51.png`                               | 51           | Mavi gradient tam ekran portrait (Common ChestScene)   |
| `51.1.png`                             | 51 varyant   | Gri/gümüş gradient tam ekran (Rare ChestScene)         |
| `51.2.png`                             | 51 varyant   | Pink/magenta gradient tam ekran (Legendary ChestScene) |
| `bottom bar bank gradient mavi bg.png` | EXTRA        | Açık mavi gradient şerit — BottomBar                   |

### black list ui 2/ (33 dosya)

| Dosya                                    | Madde(ler) | Görsel Açıklama                                              |
| ---------------------------------------- | ---------- | ------------------------------------------------------------ |
| `11.png`                                 | 11         | Turuncu progress bar metalik frame ile — Header              |
| `14.png`                                 | 14         | Bronz/bakır süslü popup panel BG                             |
| `15.png`                                 | 15         | İnce mavi/gri şerit — section ayırıcı                        |
| `16.1.png`                               | 16.1       | Mission icon — kırmızı araba yarış                           |
| `16.2.png`                               | 16.2       | Mission icon                                                 |
| `16.3.png`                               | 16.3       | Mission icon                                                 |
| `16.4.png`                               | 16.4       | Mission icon                                                 |
| `16.5.png`                               | 16.5       | Mission icon                                                 |
| `16.6.png`                               | 16.6       | Mission icon                                                 |
| `16.7.png`                               | 16.7       | Mission icon                                                 |
| `16.8.png`                               | 16.8       | Mission icon                                                 |
| `16.9.png`                               | 16.9       | Mission icon                                                 |
| `16.10.png`                              | 16.10      | Mission icon                                                 |
| `16.11.png`                              | 16.11      | Mission icon                                                 |
| `16.12.png`                              | 16.12      | Mission icon                                                 |
| `16.13.png`                              | 16.13      | Mission icon (⚠️ raporda yok — 13. ikon)                     |
| `17.png`                                 | 17         | Beyaz yuvarlak dikdörtgen koyu kenar — bar BG                |
| `18,20.png`                              | 18, 20     | Turuncu gradient şerit — fill bar (paylaşımlı)               |
| `19.png`                                 | 19         | Beyaz yuvarlak dikdörtgen — bar varyantı                     |
| `21.png`                                 | 21         | Küçük gümüş metalik frame — reward slot                      |
| `22.png`                                 | 22         | Turuncu panel 3 satır — reward section BG                    |
| `23.1.png`                               | 23.1       | Reward icon: UPGRADE kartı karakter + madeni para            |
| `23.2.png`                               | 23.2       | Reward icon                                                  |
| `23.3.png`                               | 23.3       | Reward icon                                                  |
| `23.4.png`                               | 23.4       | Reward icon                                                  |
| `23.5.png`                               | 23.5       | Reward icon                                                  |
| `23.6.png`                               | 23.6       | Reward icon                                                  |
| `23.7-54.png`                            | 23.7, 54   | Sprey boya + yarış çıkartmaları — Kaplama ikonu (paylaşımlı) |
| `23.8.png`                               | 23.8       | Reward icon                                                  |
| `23.9.png`                               | 23.9       | Reward icon (⚠️ raporda yok — 9. ikon)                       |
| `24.png`                                 | 24         | Koyu metalik bar — dark buton BG                             |
| `25.png`                                 | 25         | Küçük gümüş frame (21'e benzer)                              |
| `26.png`                                 | 26         | Turuncu/bronz özel buton mor aksan                           |
| `black list bottom arkası blurlu bg.png` | EXTRA      | Şeftali/somon gradient — Blacklist alt alan                  |

### garage ui/ (7 dosya)

| Dosya       | Madde(ler) | Görsel Açıklama                         |
| ----------- | ---------- | --------------------------------------- |
| `40.png`    | 40         | Gold madeni para yığını — Money icon    |
| `41.png`    | 41         | Mavi taş/elmas torbaları — Nitro icon   |
| `42.png`    | 42         | Teal panel 2 bar slot — garage popup BG |
| `44,48.png` | 44, 48     | Teal metalik buton panel (paylaşımlı)   |
| `45,49.png` | 45, 49     | Gri/mavi metalik panel (paylaşımlı)     |
| `46.png`    | 46         | Teal panel BG                           |
| `47.png`    | 47         | Gri/mavi dikdörtgen panel               |

### shop ui/ (3 dosya)

| Dosya                         | Madde(ler) | Görsel Açıklama                             |
| ----------------------------- | ---------- | ------------------------------------------- |
| `7.png`                       | 7          | Pink/magenta progress bar glow — shop buton |
| `8.png`                       | 8          | Pink/magenta küçük bar — shop element       |
| `shop bg gradient blurlu.png` | EXTRA      | Pink/mauve gradient — Shop panel gradient   |

---

## TASARIMCIYA İLETİLECEK EKSİK ASSET LİSTESİ

Aşağıdaki maddeler için `UIListAssets/` klasörlerinde dosya bulunamadı. Tasarımcıdan istenmelidir:

1. **Madde 13** — Blacklist Car Images (960×400, 6 adet araba görseli)
2. **Madde 29** — BoostBar BG (990×20)
3. **Madde 30** — BoostBar Fill (990×20)
4. **Madde 31** — Chest UI Icon: Common (180×140)
5. **Madde 32** — Chest UI Icon: Rare (180×140)
6. **Madde 33** — Chest UI Icon: Legendary (180×140)
7. **Madde 43** — CloseButton (80×80)
8. **Madde 50** — Garage Arkaplanı (1080×1920)
9. **Madde 52** — Money Icon - Chest Reveal (270×240)
10. **Madde 53** — Nitro Icon - Chest Reveal (270×240)

### Tasarımcıya Sorulacak Sorular

1. **Madde 35 ve 36**: 35..png pink/magenta, 36..png grey/blue — Hangisi Rare, hangisi Legendary? Renkler ters gibi görünüyor.
2. **Madde 39**: 39..png küçük bir kare frame — bu gerçekten RadarPopup BG (620×750) mi yoksa chest slot frame mi?
3. **Madde 4**: 4.png kırmızı dikdörtgen frame — bu icon mu yoksa slot frame mi?
4. **İsimsiz gradient dosyalar**: 3 adet gradient BG dosyası isimsiz bırakılmış — bunlar hangi UI elementlerine ait?
5. **Çift noktalı dosya adları** (35..png, 36..png, 37..png, 39..png): Bunlar bilerek mi çift nokta ile adlandırıldı yoksa yazım hatası mı?
