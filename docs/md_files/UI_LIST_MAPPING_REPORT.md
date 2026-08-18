# UI LIST → UNITY MAPPING RAPORU (DÜZELTİLMİŞ)

> Proje: CarClickerMobile3D  
> Tarih: Güncel (orijinal: 13 Nisan 2026)  
> Asset klasörü: `Assets/UI/UIListAssets/` — 4 alt klasör: `bank ui/`, `black list ui 2/`, `garage ui/`, `shop ui/`

---

## ÖNEMLİ NOT — DÜZELTME NOTU

Asset'ler import edildi. Orijinal rapor **basit numaralı dosyalar (1.png, 2.png...)** varsaymıştı. Gerçek dosya yapısı farklıdır:

- **Paylaşımlı dosyalar** (tek dosya birden fazla maddeye hizmet ediyor): `2,9,10.png` / `18,20.png` / `44,48.png` / `45,49.png` / `23.7-54.png`
- **Çift noktalı dosyalar** (muhtemelen yazım hatası): `35..png` / `36..png` / `37..png` / `39..png`
- **Varyant dosyalar** (raporda hiç bahsedilmemişti): `37.1.png` / `37.2.png` / `38.1.png` / `51.1.png` / `51.2.png`
- **İsimsiz gradient'ler** (raporda yoktu): `bottom bar bank gradient mavi bg.png` / `black list bottom arkası blurlu bg.png` / `shop bg gradient blurlu.png`
- **Numaralama kayması**: Mission ikonları `16.1`-`16.13`, Reward ikonları `23.1`-`23.9` (düz "16.png" veya "23.png" yok)
- **Eksik dosyalar (10 adet)**: 13, 29, 30, 31, 32, 33, 43, 50, 52, 53 — tasarımcıdan istenmelidir

> 📋 Detaylı doğrulama raporu: **UI_LIST_VERIFICATION_REPORT.md**

---

## MADDE MADDE HARİTA

---

### [1] — Bank Header Görseli (1080×200)

- **UI List açıklaması:** Bank panelinin üst başlık görseli. "Car Clicker Bank" yazılı bir header.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_Bank / Header`
- **Prefab mı Scene object mi:** Scene object
- **FileID:** Header RectTransform = 79539076, Parent = Panel_Bank (1549933709)
- **Hangi component değişecek:** `UnityEngine.UI.Image` (Header GameObject üzerindeki Image)
- **Asset hangi field'a atanacak:** `m_Sprite` alanı (Inspector'da Image → Source Image)
- **Bu öğe şu anda mevcut mu, eksik mi:** MEVCUT — ama sprite olarak Unity default beyaz sprite (`fileID: 10907`) kullanılıyor
- **SizeDelta:** 0×200 (genişlik stretch, yükseklik 200)
- **Gerekli inspector işlemleri:** Image → Source Image → `bank ui/1.png` asset'ini sürükle-bırak
- **Gerekli script bağlantıları:** Yok (statik görsel)
- **Varsa aynı asset'in tekrar kullanılacağı diğer yerler:** Madde 7+8'deki header mantığına benzer
- **Gerçek dosya:** `Assets/UI/UIListAssets/bank ui/1.png` ✅ MEVCUT
- **Uygulandı mı, sadece raporlandı mı:** RAPORLANDI

---

### [2] — Ortak Panel Background (1080×1920)

- **UI List açıklaması:** Bottom bar butonlarına basılıp paneller açıldığında tüm panellerin ortak arkaplanı. Düz renk veya gradient.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path'leri (çoklu kullanım):**
  1. `Canvas / ContentRoot / Panel_Bank` — Image component (renk: rgba 0,0,0,0.588)
  2. `Canvas / ContentRoot / Panel_ShopCards` — Image component (renk: rgba 0,0,0,0.588)
  3. `Canvas / ContentRoot / Panel_BlackList` — Image component (renk: rgba 0,0,0,0.588)
  4. `Canvas / ContentRoot / Panel_Ranking` — Image component (renk: rgba 1,1,1,0.392)
- **Prefab mı Scene object mi:** Scene object (4 ayrı panel)
- **Hangi component değişecek:** Her paneldeki `UnityEngine.UI.Image` component
- **Asset hangi field'a atanacak:** `m_Sprite` (Image → Source Image)
- **Bu öğe şu anda mevcut mu, eksik mi:** MEVCUT — hepsi Unity default beyaz sprite kullanıyor, Color ile karartılmış
- **SizeDelta:** 980×1820 (tüm paneller)
- **⚠️ DÜZELTİLMİŞ — PAYLAŞIMLI DOSYA:** Ayrı "2.png" dosyası YOK. Gerçek dosya: `bank ui/2,9,10.png` — Madde 2, 9, 10 ve 12 için TEK dosya!
- **Gerekli inspector işlemleri:**
  - Panel_Bank → Image → Source Image → `bank ui/2,9,10.png`
  - Panel_ShopCards → Image → Source Image → `bank ui/2,9,10.png`
  - Panel_BlackList → Image → Source Image → `bank ui/2,9,10.png`
  - Panel_Ranking → Image → Source Image → `bank ui/2,9,10.png`
  - Color'ı white (1,1,1,1) yaparak sprite'ın kendi rengini göstermesini sağla
- **Gerekli script bağlantıları:** Yok
- **Varsa aynı asset'in tekrar kullanılacağı diğer yerler:** Madde 9, 10, 12 AYNI dosyayı kullanır
- **Gerçek dosya:** `Assets/UI/UIListAssets/bank ui/2,9,10.png` ✅ MEVCUT (paylaşımlı)
- **Uygulandı mı:** RAPORLANDI

---

### [3] — DailyOffers Section Background (980×760)

- **UI List açıklaması:** DailyOffers kısmının çerçeve/background'u. Hafif gri, düz renk veya gradient.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_Bank / ScrollView_Bank / Viewport / Content / Section_DailyOffers`
- **Prefab mı Scene object mi:** Scene object
- **FileID:** 1786697043
- **Hangi component değişecek:** `UnityEngine.UI.Image` (Section_DailyOffers üzerindeki Image)
- **Asset hangi field'a atanacak:** `m_Sprite` (şu an `{fileID: 0}` — BOŞ)
- **Bu öğe şu anda mevcut mu, eksik mi:** MEVCUT — ama sprite ATANMAMIş, sadece rgba(1,1,1,0.114) color ile görünüyor
- **SizeDelta:** 983×759
- **Gerekli inspector işlemleri:** Section_DailyOffers → Image → Source Image → "3", Image Type → Sliced veya Simple
- **Gerekli script bağlantıları:** Yok
- **Varsa aynı asset'in tekrar kullanılacağı diğer yerler:** Madde 6 (CardPacks BG) aynı görseli kullanabilir
- **Uygulandı mı:** RAPORLANDI

---

### ~~[3.1] — DailyOffers Slot Frame (280×550)~~ ❌ KALDIRILDI

> **⚠️ BU MADDE UYDURMA — SİLİNDİ.** Docx'te "3.1" numaralı ayrı bir madde YOKTUR. Bu, Madde 3'ün açıklama paragrafının yanlışlıkla ayrı madde olarak yorumlanmasından kaynaklandı. `3.1.png` dosyası da MEVCUT DEĞİLDİR. DailyOffer slot frame'leri için Madde 5 (`bank ui/5.png`) kullanılabilir.

---

### [4] — Free Slot Icons: Gold ve Nitro (250×260, 2 adet)

- **UI List açıklaması:** DailyOffers free kısmı için icon. Free gold ve free nitro olmak üzere 2 variant.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `OfferSlot_Free / Icon` (DailyOfferSlotUI.icon field)
- **Prefab mı Scene object mi:** Scene object
- **Hangi component değişecek:** `DailyOfferSlotUI.icon` (Image component)
- **Asset hangi field'a atanacak:** Kod tarafından atanır — `icon.sprite = ...`
- **Bu öğe şu anda mevcut mu, eksik mi:** MEVCUT — Image var ama sprite runtime'da atanır
- **KOD DEĞİŞİKLİĞİ GEREKEBİLİR:** DailyOffersController'da free slot setup'ında gold/nitro sprite'larını ayırt etmek için 2 sprite field eklenebilir (veya Resources'dan yüklenebilir). Şu an DailyOffersController'da free icon sprite field'ı yok.
- **⚠️ GÖRSEL UYARI:** `bank ui/4.png` kırmızı/bordo dikdörtgen frame şeklinde — "icon" yerine **çerçeve** gibi görünüyor. Tasarımcıya danışılmalı.
- **Gerekli inspector işlemleri:**
  - DailyOffersController'a `[SerializeField] Sprite freeGoldIcon;` ve `[SerializeField] Sprite freeNitroIcon;` eklenip Inspector'dan atanmalı
  - VEYA mevcut DailyOfferSlotUI.icon'a doğrudan sprite atanabilir
- **Varsa aynı asset'in tekrar kullanılacağı diğer yerler:** Madde 40 (GoldIcon) ve 41 (NitroIcon) ile ilişkili olabilir
- **Gerçek dosya:** `Assets/UI/UIListAssets/bank ui/4.png` ✅ MEVCUT
- **Uygulandı mı:** RAPORLANDI — Kod değişikliği gerekebilir

---

### [5] — DailyOffers Kırmızı Çerçeve / Card Pack Slot Frame (250×550)

- **UI List açıklaması:** Madde 2'deki background ile uyumlu çerçeve. Kırmızı çerçeve kısmı.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** DailyOffers slot'larındaki çerçeve — her slot'un frame background'u
- **Hangi component değişecek:** Slot root veya içindeki frame Image
- **Bu öğe şu anda mevcut mu, eksik mi:** MEVCUT — DailyOfferSlotUI slot'larının root Image'i
- **Not:** Bu asset, 3.1 ile aynı yere de gidebilir. DailyOffers slot çerçevesi olarak kullanılacak.
- **Uygulandı mı:** RAPORLANDI

---

### [6] — Card Packs Section Background (980×560)

- **UI List açıklaması:** Card packs kısmının açık gri background'u. Madde 3 ile aynı görsel kullanılabilir.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** Panel_Bank içindeki CardPacks bölümü
- **Hangi component değişecek:** CardPacks section background Image
- **Bu öğe şu anda mevcut mu, eksik mi:** KONTROL GEREKİR — Panel_Bank'ın ScrollView_Bank/Content'inde Section_DailyOffers'ın altında bir Card Packs section olmalı. Eğer yoksa oluşturulmalı.
- **Reuse notu:** Madde 3 ile aynı sprite kullanılabilir
- **Uygulandı mı:** RAPORLANDI

---

### [7+8] — Shop/Cards Header + Items/Cards Butonları

- **UI List açıklaması:** Header kısmı Madde 1 ile aynı mantık. 2 buton: Items (250×90) ve Cards (250×90).
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path'leri:**
  - Header: `Canvas / ContentRoot / Panel_ShopCards / Header`
  - Items butonu: `Canvas / ContentRoot / Panel_ShopCards / .../Btn_TabShopItems` (SizeDelta: 250×90)
  - Cards butonu: `Canvas / ContentRoot / Panel_ShopCards / .../Btn_TabCards` (SizeDelta: 250×90)
- **Prefab mı Scene object mi:** Scene object
- **Hangi component değişecek:**
  - Header → Image → Source Image → "7" (veya "1" reuse)
  - Btn_TabShopItems → Image → Source Image → "7" items buton görseli
  - Btn_TabCards → Image → Source Image → "8" cards buton görseli
- **Bu öğe şu anda mevcut mu, eksik mi:** MEVCUT
- **Gerekli script bağlantıları:** ShopCardsTabs.btnShopItems ve ShopCardsTabs.btnCards
- **Uygulandı mı:** RAPORLANDI

---

### [9] — Shop/Cards Background (Madde 2 ile aynı — PAYLAŞIMLI DOSYA)

- **UI List açıklaması:** Madde 2'deki background. Aynı kullanılır.
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_ShopCards` (Image)
- **⚠️ DÜZELTME:** `bank ui/2,9,10.png` paylaşımlı dosyasını kullan. Ayrı "9.png" dosyası YOK.
- **Uygulandı mı:** RAPORLANDI — Madde 2 ile aynı dosya

---

### [10] — Blacklist Background (Madde 2 ile aynı — PAYLAŞIMLI DOSYA)

- **UI List açıklaması:** Madde 2'deki background. Aynı kullanılır.
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_BlackList` (Image)
- **⚠️ DÜZELTME:** `bank ui/2,9,10.png` paylaşımlı dosyasını kullan. Ayrı "10.png" dosyası YOK.
- **Uygulandı mı:** RAPORLANDI — Madde 2 ile aynı dosya

---

### [11] — Blacklist Header (1080×200)

- **UI List açıklaması:** Blacklist paneli header görseli. "Blacklist" yazısını text olarak oyundan ekleyeceğiz.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_BlackList / Header`
- **Prefab mı Scene object mi:** Scene object
- **FileID:** Header RectTransform = 2093501287
- **Hangi component değişecek:** `UnityEngine.UI.Image` (Header üzerindeki)
- **Asset hangi field'a atanacak:** m_Sprite → `black list ui 2/11.png`
- **SizeDelta:** 0×200 (stretch width)
- **Bu öğe şu anda mevcut mu:** MEVCUT — default sprite ile
- **Not:** Blacklist yazısı TMP_Text ile eklenir — `BlacklistPanelController.blacklistTitle`
- **Gerçek dosya:** `Assets/UI/UIListAssets/black list ui 2/11.png` ✅ MEVCUT
- **Uygulandı mı:** RAPORLANDI

---

### [12] — Blacklist Background (Madde 2 ile aynı — PAYLAŞIMLI DOSYA)

- **UI List açıklaması:** 1080×1920 — Madde 2 ile aynı.
- **⚠️ DÜZELTME:** `bank ui/2,9,10.png` paylaşımlı dosyasını kullan. Ayrı "12.png" dosyası YOK.
- **Uygulandı mı:** RAPORLANDI

---

### [13] — Blacklist Car Images (960×400, 6 adet)

- **UI List açıklaması:** Blacklist'teki her araba için birer görsel. 6 adet.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_BlackList / .../CarImage`
- **Hangi component değişecek:** `BlacklistPanelController.carImage` (Image component)
- **Asset hangi field'a atanacak:** Runtime'da atanır — her tier geçişinde `carImage.sprite = tierData.carSprite` gibi bir logic olmalı
- **Bu öğe şu anda mevcut mu:** MEVCUT — Image nesnesi var, sprite koddan atanır
- **ÖNEMLİ:** 6 ayrı araba görseli gerekiyor. Bu görseller `BlacklistTierSO` asset'lerine veya `CarDataSO` asset'lerine eklenmeli.
- **⚠️ DÜZELTME:** `13.png` dosyası UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **Gerekli inspector işlemleri:**
  - BlacklistTierSO'lara (BlacklistTier*1 ... BlacklistTier_5 + BlacklistTier*) her birine car image sprite eklenmeli
  - VEYA BlacklistPanelController'a carSprite listesi eklenmeli
- **KOD DEĞİŞİKLİĞİ GEREKEBİLİR:** BlacklistTierSO'ya `Sprite carImage` field'ı yoksa eklenmeli
- **Uygulandı mı:** RAPORLANDI

---

### [14] — Missions Container Frame (960×800)

- **UI List açıklaması:** Görevlerin olduğu kısımdaki beyaz çerçeve.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_BlackList / ScrollView_Blacklist / Viewport / Content / MissionsContainer`
- **Prefab mı Scene object mi:** Scene object
- **FileID:** 1307409785 (missionsContainer)
- **Hangi component değişecek:** Eğer Image component varsa → `m_Sprite`. Yoksa Image component eklenmeli.
- **SizeDelta:** 960×800
- **Bu öğe şu anda mevcut mu:** MEVCUT — Transform olarak var ama Image component'i KONTROL EDİLMELİ
- **Gerekli inspector işlemleri:** MissionsContainer'a Image component ekle (yoksa), Source Image → "14"
- **Uygulandı mı:** RAPORLANDI

---

### [15] — MissionRow Background (900×130)

- **UI List açıklaması:** Kırmızı satırların çerçevesi. Tek renk veya gradient.
- **Unity'de bulunduğu scene:** Main.unity (runtime instantiate edilir)
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab mı Scene object mi:** PREFAB
- **GameObject path (prefab içinde):** `MissionRow` (root)
- **Hangi component değişecek:** `UnityEngine.UI.Image` (MissionRow root Image)
- **Asset hangi field'a atanacak:** `m_Sprite` — şu an `{fileID: 0}` (BOŞ!), renk kırmızı (1,0,0,1)
- **SizeDelta:** 900×134.1
- **Bu öğe şu anda mevcut mu:** MEVCUT — ama sprite BOŞ, sadece kırmızı Color ile gösteriliyor
- **Gerekli inspector işlemleri:**
  - MissionRow.prefab aç → Root MissionRow seç → Image → Source Image → "15"
  - Color'ı (1,1,1,1) yap ki sprite'ın kendi rengi görünsün
  - Apply to Prefab
- **Uygulandı mı:** RAPORLANDI

---

### [16, 16.1, 16.2...] — Mission Type Icons (100×100, 13 adet)

- **UI List açıklaması:** MissionRow içindeki beyaz icon. Görev tipine göre değişir.
- **⚠️ DÜZELTME — NUMARALAMA KAYMASI:** Düz `16.png` dosyası **YOK**! Dosyalar `16.1.png` ile başlayıp `16.13.png` ile bitiyor. Rapordaki orijinal eşleştirme (16 → EarnGold) yanlış.
- **Gerçek dosyalar (black list ui 2/):**
  - 16.1.png → Görev tipi 1
  - 16.2.png → Görev tipi 2
  - 16.3.png → Görev tipi 3
  - 16.4.png → Görev tipi 4
  - 16.5.png → Görev tipi 5
  - 16.6.png → Görev tipi 6
  - 16.7.png → Görev tipi 7
  - 16.8.png → Görev tipi 8
  - 16.9.png → Görev tipi 9
  - 16.10.png → Görev tipi 10
  - 16.11.png → Görev tipi 11
  - 16.12.png → Görev tipi 12
  - 16.13.png → Görev tipi 13 (⚠️ raporda yoktu!)
- **Görev tipleri (koddan):**
  EarnGold, CollectWorldNitro, DefuseRadars, OwnBuildings, OpenChests, UseBoost, EscapePolice, UpgradeAnyCardToLevel, BuyGarageParts, TriggerNitroRain, NitroMagnetCollect, UseTurbo, ReachTotalCardLevel
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab` → `MissionRow/Image` (icon field)
- **Hangi component/field atanır:** `BlacklistMissionDefinition.icon` (Sprite) — her tier için ayrı SO'da
- **MissionRowUI.icon:** Runtime'da `definition.icon` sprite'ını gösterir
- **Image SizeDelta:** 100×100 (prefab'daki Image child)
- **Current sprite:** `{fileID: 0}` (BOŞ) — runtime'da atanır
- **Gerekli inspector işlemleri:**
  - `Assets/Prefabs/Blacklist/BlacklistTier_1.asset` ... `BlacklistTier_5.asset` her birinin içindeki mission'ların `icon` field'ına ilgili sprite atanmalı
  - Her mission'ın tipine göre doğru 16.X sprite'ı kullanılmalı
- **Uygulandı mı:** RAPORLANDI — SO asset'lerine Inspector'dan atanmalı

---

### [17] — BarBG (740×50) — Mission Progress Bar Background

- **UI List açıklaması:** Dolma barı kısmı — boş hali.
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab path:** `MissionRow / BarBG`
- **Hangi component:** `UnityEngine.UI.Image`
- **SizeDelta:** 740×50
- **Current sprite:** `{fileID: 21300000, guid: f4f9dd77eb0fe934db1b6c33bd100596}` — DOLU! Mevcut sprite atanmış.
- **Bu öğe şu anda mevcut mu:** MEVCUT VE SPRİTE ATANMIŞ
- **Gerekli inspector işlemleri:** Mevcut sprite yeterliyse dokunma. Yenisiyle değiştirmek istiyorsan → Image → Source Image → "17"
- **Uygulandı mı:** MEVCUT SPRİTE VAR — isteğe bağlı değiştirilir

---

### [18] — BarFill (740×50) — Mission Progress Bar Fill

- **UI List açıklaması:** Barın dolacak olan kısmı. Düz renk.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "18.png" dosyası YOK! Gerçek dosya: `black list ui 2/18,20.png` — Madde 18 ve 20 için TEK dosya!
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab path:** `MissionRow / BarBG / BarFill`
- **Hangi component:** `UnityEngine.UI.Image` (m_Type: 3 = Filled, Horizontal)
- **Current sprite:** `{fileID: 21300000, guid: 4b6e56fc090b4964a8bd837d2cd00f57}` — DOLU!
- **Bu öğe şu anda mevcut mu:** MEVCUT VE SPRİTE ATANMIŞ
- **Uygulandı mı:** MEVCUT SPRİTE VAR

---

### [19] — BarBGComplete (540×50) — Completed Bar Background

- **UI List açıklaması:** Görev tamamlandığında bar'ın küçük hali.
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab path:** `MissionRow / BarBGComplete`
- **Hangi component:** `UnityEngine.UI.Image`
- **SizeDelta:** 540×50
- **Current sprite:** `{fileID: 0}` — BOŞ! Sadece koyu gri renk (0.149, 0.149, 0.149)
- **Gerekli inspector işlemleri:**
  - MissionRow.prefab → BarBGComplete → Image → Source Image → "19"
  - Apply to Prefab
- **Uygulandı mı:** RAPORLANDI

---

### [20] — BarFillFull (540×50) — Completed Bar Fill

- **UI List açıklaması:** Tamamlanmış barın doluluk kısmı. Düz renk.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "20.png" dosyası YOK! Gerçek dosya: `black list ui 2/18,20.png` — Madde 18 ile aynı dosya!
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab path:** `MissionRow / BarBGComplete / BarFillFull`
- **Hangi component:** `UnityEngine.UI.Image` (m_Type: 3 = Filled)
- **Current sprite:** `{fileID: 21300000, guid: 9d1b568828eb1b04388ac049510163bc}` — DOLU!
- **Bu öğe şu anda mevcut mu:** MEVCUT VE SPRİTE ATANMIŞ
- **Uygulandı mı:** MEVCUT SPRİTE VAR

---

### [21] — CompletedBtn (160×100) — Görev Tamamlanma Butonu

- **UI List açıklaması:** Görevi tamamladıktan sonra ödül almak için tıklanan buton.
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab path:** `MissionRow / CompletedBtn`
- **Hangi component:** `UnityEngine.UI.Image` + `Button`
- **SizeDelta:** 160×100
- **Current sprite:** `{fileID: 10905}` (Unity built-in Knob) — yeşil renk tint
- **Gerekli inspector işlemleri:**
  - MissionRow.prefab → CompletedBtn → Image → Source Image → "21"
  - Renk ayarını yapabilirsin (şu an yeşil: 0, 1, 0.004, 1)
  - Apply to Prefab
- **Uygulandı mı:** RAPORLANDI

---

### [22] — RewardPopup Background (800×700)

- **UI List açıklaması:** Reward popup'ın beyaz arkaplanı.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu GameObject path:** `Canvas / ContentRoot / Panel_BlackList / ScrollView_Blacklist / Viewport / Content / RewardPopup (1) / RewardBG / PopupPanel`
- **Prefab mı Scene object mi:** Scene object
- **Hangi component:** `UnityEngine.UI.Image` (PopupPanel)
- **Current sprite:** `{fileID: 21300000, guid: 007537661a7ef644ba03c2317b2f63e3}` — DOLU! Mevcut popup BG sprite atanmış (ve AspectRatioFitter 0.69 ile boyutlandırılıyor)
- **Gerekli inspector işlemleri:** Mevcut sprite yeterliyse dokunma. Değiştirmek istiyorsan → PopupPanel → Image → Source Image → "22"
- **Uygulandı mı:** MEVCUT SPRİTE VAR — isteğe bağlı

---

### [23, 23.1, 23.2...] — Reward Icons (630×380, ~9 adet)

- **UI List açıklaması:** Ödül popup'ında ortadaki icon. Her ödül tipi için ayrı.
- **⚠️ DÜZELTME — NUMARALAMA KAYMASI:** Düz `23.png` dosyası **YOK**! Dosyalar `23.1.png` ile başlayıp `23.9.png` ile bitiyor.
- **Gerçek dosyalar (black list ui 2/):**
  - 23.1.png → Upgrade (kartç karakter + madeni para)
  - 23.2.png → Ödül tipi 2
  - 23.3.png → Ödül tipi 3
  - 23.4.png → Ödül tipi 4
  - 23.5.png → Ödül tipi 5
  - 23.6.png → Ödül tipi 6
  - 23.7-54.png → Kaplama (sprey boya + çıkartma) — **PAYLAŞIMLI**: Madde 54 ile aynı dosya!
  - 23.8.png → Ödül tipi 8
  - 23.9.png → Ödül tipi 9 (⚠️ raporda yoktu!)
- **Ödül tipleri (orijinal rapordan — numaralar 1 kaydırılmalı):**
  Gold, Nitro, Popularity Reset, Heat Reset, Chest, Cooldown Discount, Card Progress, Kaplama, Cosmetics
- **Unity'de bulunduğu path:** `...RewardPopup (1) / RewardBG / PopupPanel / RewardIcon`
- **Hangi component:** `UnityEngine.UI.Image` (RewardIcon)
- **SizeDelta:** 632×376
- **Current sprite:** `{fileID: 0}` — BOŞ! (kırmızı placeholder renk)
- **Asset hangi field'a atanacak:** `BlacklistRewardDefinition.rewardIcon` — her tier SO'sunun her mission'ının reward'ındaki `rewardIcon` field'ı
- **Gerekli inspector işlemleri:**
  - `BlacklistTier_1.asset` ... `BlacklistTier_5.asset` → her mission'ın `reward.rewardIcon` alanına uygun 23.X sprite'ını ata
- **Uygulandı mı:** RAPORLANDI — SO'lara Inspector'dan atanmalı

---

### [24] — CollectBtn (380×90) — Ödül Toplama Butonu

- **UI List açıklaması:** RewardPopup'taki Collect butonu.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `...RewardPopup (1) / RewardBG / PopupPanel / CollectBtn`
- **Hangi component:** `UnityEngine.UI.Image` + `Button`
- **SizeDelta:** 380×90
- **Current sprite:** `{fileID: 10905}` (Unity built-in Knob)
- **Gerekli inspector işlemleri:** CollectBtn → Image → Source Image → "24"
- **Uygulandı mı:** RAPORLANDI

---

### [25] — Claimed Görseli (160×100)

- **UI List açıklaması:** Görev tamamlandıysa ve reward collect edildiyse CompletedBtn "CLAIMED" oluyor.
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/Blacklist/MissionRow.prefab`
- **Prefab path:** `MissionRow / CompletedBtn` (aynı buton, claimed state'te)
- **Hangi component:** `MissionRowUI.SetClaimed()` → Button text "CLAIMED" yapar, color dimler
- **Current durum:** Madde 21 ile aynı buton. Claimed state'te renk griye döner.
- **Önerilen çözüm:** Ayrı bir "claimed" sprite isteniyorsa:
  - MissionRowUI'ya `[SerializeField] Sprite claimedSprite;` eklenebilir
  - `SetClaimed()` içinde `img.sprite = claimedSprite;` yapılabilir
  - VEYA mevcut dimming yeterli bulunabilir
- **Uygulandı mı:** RAPORLANDI — Kod değişikliği isteğe bağlı

---

### [26] — TakeTheCarButton (300×120) — Arabayı Açma Butonu

- **UI List açıklaması:** Arabayı açmak için tıklanan buton.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `Canvas / ContentRoot / Panel_BlackList / .../TakeTheCarButton`
- **Hangi component:** `UnityEngine.UI.Image` + `Button` + `CanvasGroup`
- **SizeDelta:** 300×117.1
- **BlacklistPanelController:** `takeTheCarButton` + `takeTheCarImage` (Image) referansları
- **Gerekli inspector işlemleri:** TakeTheCarButton → Image → Source Image → "26"
- **Uygulandı mı:** RAPORLANDI

---

### [27+28] — PopularityBar Background (420×100) + Fill

- **UI List açıklaması:** TopBar'daki popularity bar. Boş hali 420×100. Fill'i Unity'den halledilecek.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:**
  - Bar BG: `Canvas / TopBar / PopularityBar / Popularity_BG`
  - Fill: `Canvas / TopBar / PopularityBar / Popularity_Fill` (PopularityUI.fillImage)
- **Hangi component:** Image (Popularity_BG ve Popularity_Fill)
- **Gerekli inspector işlemleri:**
  - Popularity_BG → Image → Source Image → "27"
  - Fill için sprite gerekmiyorsa (kullanıcı "gerek yok şimdilik" dedi) → fill'e dokunma
  - Ama "28" varsa → Popularity_Fill → Image → Source Image → "28"
- **Not:** Kullanıcı "BarFill'i ben unity üzerinden halledeceğim" dedi
- **Uygulandı mı:** RAPORLANDI

---

### [29+30] — BoostBar Background + Fill (990×20)

- **UI List açıklaması:** Boost mode devreye girmesi için dolacak bar. 990×20 boş hali + fill.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `Canvas / TopBar / Slider_BoostBar`
- **Prefab mı:** Scene object (Slider component)
- **SizeDelta:** 988.02×20
- **Yapı:** Unity Slider → Background child + Fill Area / Fill child
  - Background (78695935): Default white sprite → "29"
  - Fill (471192017): Fill sprite → "30"
- **⚠️ DÜZELTME:** `29.png` ve `30.png` dosyaları UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **BoostModeController:** `boostBarSlider` field'ı bu slider'ı referans eder
- **Gerekli inspector işlemleri:**
  - Slider_BoostBar / Background → Image → Source Image → "29"
  - Slider_BoostBar / Fill Area / Fill → Image → Source Image → "30"
- **Uygulandı mı:** RAPORLANDI

---

### [31+32+33] — Chest UI Icons (180×140, 3 adet)

- **UI List açıklaması:** Common, Rare, Legendary chest UI görselleri.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu prefab:** `Assets/Prefabs/ChestSlotPrefab.prefab` (SizeDelta: 180×140)
- **Hangi component:** `ChestSlotUI` script
- **Asset hangi field'lara atanacak:**
  - `ChestSlotUI.commonIcon` → "31" (Common)
  - `ChestSlotUI.rareIcon` → "32" (Rare)
  - `ChestSlotUI.legendaryIcon` → "33" (Legendary)
- **⚠️ DÜZELTME:** `31.png`, `32.png`, `33.png` dosyaları UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **ChestSlotUI.chestIcon:** Runtime'da gösterilecek Image
- **Gerekli inspector işlemleri:**
  - Main.unity scene'de ChestShownPlace altında instantiate edilen ChestSlotPrefab → ChestSlotUI component → commonIcon, rareIcon, legendaryIcon field'larına sprite ata
  - VEYA prefab'ı aç → ChestSlotUI → Inspector'dan ata → Apply
- **Mevcut prefab sprite'ları:** Prefab'da default sprite guid var: `cadd9b9c2b0d98e4c9d152015f0950c7`
- **Ayrıca:** `Assets/Prefabs/UI/` altında `chesta.png`, `chestb.png`, `chestc.png` dosyaları mevcut — bunlar mevcut chest icon'ları olabilir!
- **Uygulandı mı:** RAPORLANDI

---

### [34+35+36] — ChestPopup Backgrounds (1250×1100, 3 adet)

- **UI List açıklaması:** Common, Rare, Legendary chest popup arkaplanları.
- **⚠️ DÜZELTME — DOSYA ADI ve GÖRSEL UYARISI:**
  - `bank ui/34.png` → Mavi panel → Common için uygun ✅
  - `bank ui/35..png` → ÇİFT NOKTA! Pink/magenta panel → **Legendary'ye** benziyor (rapor Rare diyor!)
  - `bank ui/36..png` → ÇİFT NOKTA! Grey/blue panel → **Rare'e** benziyor (rapor Legendary diyor!)
  - ⚠️ Tasarımcıya 35 ve 36'nın hangi chest tipine ait olduğu sorulmalı!
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `Canvas / ChestPopup` — ChestPopupController component
- **Hangi component:** `ChestPopupController`
- **Asset hangi field'lara atanacak:**
  - `commonPopupBg` → `bank ui/34.png`
  - `rarePopupBg` → `bank ui/35..png` VEYA `bank ui/36..png` (⚠️ dogrulama gerekli!)
  - `legendaryPopupBg` → `bank ui/36..png` VEYA `bank ui/35..png` (⚠️ dogrulama gerekli!)
- **Runtime:** `popupBackgroundImage.sprite = commonPopupBg/rarePopupBg/legendaryPopupBg`
- **Gerekli inspector işlemleri:**
  - Main.unity → Canvas / ChestPopup → ChestPopupController (Inspector) → commonPopupBg, rarePopupBg, legendaryPopupBg field'larına ata
- **Uygulandı mı:** RAPORLANDI

---

### [37] — StartUnlock Button (500×190)

- **UI List açıklaması:** Chest popup'taki Start Unlock butonu (sağdaki).
- **⚠️ DÜZELTME — DOSYA ADI + VARYANTLAR:**
  - `bank ui/37..png` → ÇİFT NOKTA! Küçük beyaz/açık mavi buton (temel versiyon)
  - `bank ui/37.1.png` → Mavi buton varyantı (Common chest için?)
  - `bank ui/37.2.png` → Mor buton varyantı (Legendary chest için?)
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `Canvas / ChestPopup / .../ startUnlockObj`
- **Hangi component:** `ChestPopupController`
- **Asset hangi field'lara atanacak:**
  - `commonStartUnlockSprite` → `bank ui/37.1.png` (mavi)
  - `rareStartUnlockSprite` → `bank ui/37..png` (temel)
  - `legendaryStartUnlockSprite` → `bank ui/37.2.png` (mor)
- **Runtime:** `startUnlockButtonImage.sprite = ...StartUnlockSprite`
- **Gerekli inspector işlemleri:** ChestPopupController → commonStartUnlockSprite / rareStartUnlockSprite / legendaryStartUnlockSprite → "37"
- **Uygulandı mı:** RAPORLANDI

---

### [38] — OpenNow Button (500×190) + NitroCoin Icon (120×120)

- **UI List açıklaması:** Soldaki Open Now butonu. Üzerinde nitro coin görseli olmalı.
- **Gerçek dosyalar:**
  - `bank ui/38.png` → Açık mavi buton ✅
  - `bank ui/38.1.png` → **⚠️ Bu bir İKON (mavi nitro şişeleri), buton değil!** openNowObj içindeki NitroCoin child Image için kullanılabilir.
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `Canvas / ChestPopup / .../openNowObj`
- **Hangi component:** openNowObj'un Image + Button
- **NitroCoin icon:** openNowObj içinde bir child Image olarak eklenebilir (120×120)
- **Gerekli inspector işlemleri:**
  - openNowObj → Image → Source Image → "38" (buton görseli)
  - İçine NitroCoin child Image ekle (yoksa) → "38" nitro coin variant veya ayrı (120×120)
- **Uygulandı mı:** RAPORLANDI

---

### [39] — RadarPopup Background (620×750) ⚠️ GÖRSEL UYUMSUZLUĞU

- **UI List açıklaması:** Radar popup çerçevesi. "Wanted" yazısını da ekleyebilirsin.
- **⚠️ DÜZELTME — DOSYA ADI + GÖRSEL:** Gerçek dosya `bank ui/39..png` (ÇİFT NOKTA!). Görsel: küçük metalik kare frame — büyük popup BG değil, **chest slot/item frame** gibi görünüyor. Tasarımcıya sorulmalı!
- **Unity'de bulunduğu scene:** Main.unity
- **Unity'de bulunduğu path:** `Canvas / RadarPopup`
- **Hangi component:** RadarPopupController
- **Mevcut yapı:** `popupRoot` (GameObject) + `snapshotImage` (RawImage — kamera çıktısı)
- **DURUM:** Şu anda RadarPopup'ın arkaplanı doğrudan bir Image component DEĞİL — %70.6 siyah overlay rengi var. Bir background frame sprite eklemek için:
  - popupRoot altına bir Background Image child ekle (620×750)
  - Bu child'a "39" sprite'ını ata
  - snapshotImage'ı bu frame'in içine yerleştir
- **Uygulandı mı:** RAPORLANDI — Yeni child oluşturulması gerekebilir

---

### [40] — GoldIcon (100×100) — Garage Para Alanı

- **UI List açıklaması:** Garage kısmındaki gold icon.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / .../GoldIcon`
- **FileID:** 6918 (line reference)
- **Hangi component:** Image
- **Gerekli inspector işlemleri:** GoldIcon → Image → Source Image → "40"
- **Varsa reuse:** Madde 4 (free gold icon) ile ilişkili olabilir. Ayrıca BuyPopup'taki gold icon.
- **Uygulandı mı:** RAPORLANDI

---

### [41] — NitroIcon (100×100) — Garage Nitro Alanı

- **UI List açıklaması:** Garage kısmındaki nitro coin icon.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / .../NitroIcon`
- **FileID:** 1847 (line reference)
- **Hangi component:** Image
- **Gerekli inspector işlemleri:** NitroIcon → Image → Source Image → "41"
- **Varsa reuse:** Madde 38'deki nitro coin ve Madde 4'teki free nitro ile ilişkili
- **Uygulandı mı:** RAPORLANDI

---

### [42] — BuyPopupPanel Background (800×700)

- **UI List açıklaması:** Beyaz arkaplan — satın alma popup'ı.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / BuyPopupPanel / PopupPanel(child)`
- **Hangi component:** `UnityEngine.UI.Image` (popup panel)
- **GarageBuyPopupController:** `popupPanel` ve `popupRect` referansları
- **Gerekli inspector işlemleri:** PopupPanel Image → Source Image → "42"
- **Not:** GoldIcon ve NitroIcon (Madde 40+41) bu popup'ta da kullanılabilir
- **Uygulandı mı:** RAPORLANDI

---

### [43] — CloseButton (80×80)

- **UI List açıklaması:** BuyPopup kapatma butonu.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / BuyPopupPanel / .../CloseButton`
- **Hangi component:** Image + Button
- **GarageBuyPopupController:** `closeButton` referansı
- **Gerekli inspector işlemleri:** CloseButton → Image → Source Image → "43"
- **⚠️ DÜZELTME:** `43.png` dosyası UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **Uygulandı mı:** RAPORLANDI

---

### [44] — BtnYes (300×150)

- **UI List açıklaması:** BuyPopup'taki onay butonu.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "44.png" dosyası YOK! Gerçek dosya: `garage ui/44,48.png` — Madde 44 (BtnYes) ve 48 (YesBtn Exit) için TEK dosya!
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / BuyPopupPanel / .../Btn_Yes`
- **Hangi component:** Image + Button
- **GarageBuyPopupController:** `btnYes` referansı
- **Gerekli inspector işlemleri:** Btn_Yes → Image → Source Image → "44"
- **Uygulandı mı:** RAPORLANDI

---

### [45] — BtnIncele (300×150)

- **UI List açıklaması:** BuyPopup'taki incele/preview butonu.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "45.png" dosyası YOK! Gerçek dosya: `garage ui/45,49.png` — Madde 45 (BtnIncele) ve 49 (NoBtn Exit) için TEK dosya!
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / BuyPopupPanel / .../BtnIncele`
- **Hangi component:** Image + Button
- **GarageBuyPopupController:** `btnIncele` referansı
- **Gerekli inspector işlemleri:** BtnIncele → Image → Source Image → "45"
- **Uygulandı mı:** RAPORLANDI

---

### [46] — LockedUI (700×280)

- **UI List açıklaması:** Transparent bir locked görseli. "Locked" yazısı eklenebilir, "Blacklist" yazısı kullanıcı ekleyecek.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / LockedUI` (veya `LockedOverlay`)
- **Hangi component:** Image
- **GarageController:** `lockedOverlay` field + `lockedBlacklistText` (TMP)
- **Gerekli inspector işlemleri:** LockedUI → Image → Source Image → "46"
- **Uygulandı mı:** RAPORLANDI

---

### [47] — ExitPopupPanel Background (800×700)

- **UI List açıklaması:** Çıkış popup çerçevesi. Yazı eklenebilir.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / ExitPopupPanel / PopupPanel(child)`
- **Hangi component:** Image
- **GarageExitPopupController:** `popupPanel` referansı
- **Gerekli inspector işlemleri:** PopupPanel → Image → Source Image → "47"
- **Uygulandı mı:** RAPORLANDI

---

### [48] — YesBtn (250×160) — Exit Popup Onay

- **UI List açıklaması:** Çıkış popup'ındaki evet butonu. Boyut değişebilir.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "48.png" dosyası YOK! Gerçek dosya: `garage ui/44,48.png` — Madde 44 ile aynı dosya!
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / ExitPopupPanel / .../confirmButton`
- **Hangi component:** Image + Button
- **GarageExitPopupController:** `confirmButton` referansı
- **Gerekli inspector işlemleri:** confirmButton → Image → Source Image → "48"
- **Uygulandı mı:** RAPORLANDI

---

### [49] — NoBtn (250×160) — Exit Popup İptal

- **UI List açıklaması:** Çıkış popup'ındaki hayır butonu. Boyut değişebilir.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "49.png" dosyası YOK! Gerçek dosya: `garage ui/45,49.png` — Madde 45 ile aynı dosya!
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / ExitPopupPanel / .../cancelButton`
- **Hangi component:** Image + Button
- **GarageExitPopupController:** `cancelButton` referansı
- **Gerekli inspector işlemleri:** cancelButton → Image → Source Image → "49"
- **Uygulandı mı:** RAPORLANDI

---

### [50] — Garage Arkaplanı (1080×1920)

- **UI List açıklaması:** Garage sahnesinin arkaplanı.
- **Unity'de bulunduğu scene:** NewGarage.unity
- **Unity'de bulunduğu path:** `Canvas / Background` (veya sahnenin Camera Clear Color'u ile)
- **DURUM:** NewGarage'da "Background" isimli bir element var (line 3126). Ancak garage 3D sahne olduğu için arkaplan genellikle Camera clear color veya Skybox ile belirlenir.
- **Mevcut:** `Assets/Prefabs/UI/garaj bg.png` dosyası MEVCUT — bu mevcut garage background olabilir
- **⚠️ DÜZELTME:** `50.png` dosyası UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **Gerekli inspector işlemleri:**
  - Eğer Canvas'ta bir Background Image varsa → Source Image → "50"
  - Yoksa Canvas altına bir Image ekle (stretch, sort order en arkada) → "50"
- **Uygulandı mı:** RAPORLANDI

---

### [51] — ChestScene Background (1080×1920) + VARYANTLAR

- **UI List açıklaması:** Chest açılış sahnesinin arkaplanı. Düz renk veya gradient.
- **⚠️ DÜZELTME — VARYANT DOSYALAR:** 3 farklı dosya mevcut (her chest tipi için farklı arka plan!):
  - `bank ui/51.png` → Mavi gradient (Common chest)
  - `bank ui/51.1.png` → Gri/gümüş gradient (Rare chest)
  - `bank ui/51.2.png` → Pink/magenta gradient (Legendary chest)
- **Unity'de bulunduğu scene:** ChestOpenScene.unity
- **DURUM:** ChestOpenScene 3D sahne — chest modeli 3D olarak gösterilir. Canvas şu anda DEAKTIF. Arkaplan Camera Clear Color veya bir skybox ile sağlanıyor.
- **Çözüm önerisi:**
  - Camera clear color'unu gradient'e benzer bir renk yap
  - VEYA Canvas'ı aktif edip arkaplana bir full-screen Image ekle → "51"
  - VEYA bir world-space quad ile background oluştur
- **Gerekli inspector işlemleri:**
  - ChestOpenScene → Canvas'ı aktifleştir → Background Image ekle → Source Image → "51"
  - Canvas Sort Order'ı en arkaya al
- **Uygulandı mı:** RAPORLANDI

---

### [52] — Money Icon - Chest Reveal (270×240)

- **UI List açıklaması:** Chest açılışında çıkan para icon'u.
- **Unity'de bulunduğu scene:** ChestOpenScene.unity
- **Unity'de bulunduğu controller:** `ChestRewardRevealController`
- **Hangi field:** `moneySprite` (Sprite)
- **Kullanım:** SummarySlotPrefab'da reward icon olarak gösterilir
- **⚠️ DÜZELTME:** `52.png` dosyası UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **Gerekli inspector işlemleri:**
  - ChestOpenScene → ChestRewardRevealController → `moneySprite` → "52"
- **Uygulandı mı:** RAPORLANDI

---

### [53] — Nitro Icon - Chest Reveal (270×240)

- **UI List açıklaması:** Chest açılışında çıkan nitro icon'u.
- **Unity'de bulunduğu scene:** ChestOpenScene.unity
- **Unity'de bulunduğu controller:** `ChestRewardRevealController`
- **Hangi field:** `nitroSprite` (Sprite)
- **⚠️ DÜZELTME:** `53.png` dosyası UIListAssets klasörlerinde **MEVCUT DEĞİL**. Tasarımcıdan istenmelidir!
- **Gerekli inspector işlemleri:**
  - ChestOpenScene → ChestRewardRevealController → `nitroSprite` → "53"
- **Uygulandı mı:** RAPORLANDI

---

### [54] — Free Kaplama Icon - Chest Reveal (270×240)

- **UI List açıklaması:** Chest'ten çıkan kaplama için ortak icon.
- **⚠️ DÜZELTME — PAYLAŞIMLI DOSYA:** Ayrı "54.png" dosyası YOK! Gerçek dosya: `black list ui 2/23.7-54.png` — Madde 23.7 (Kaplama reward) ile aynı dosya!
- **Görsel:** Sprey boya kutusu + yarış çıkartmaları
- **Unity'de bulunduğu scene:** ChestOpenScene.unity
- **Unity'de bulunduğu controller:** `ChestRewardRevealController`
- **Hangi field:** `stickerSprite` (Sprite)
- **Gerekli inspector işlemleri:**
  - ChestOpenScene → ChestRewardRevealController → `stickerSprite` → "54"
- **Uygulandı mı:** RAPORLANDI

---

---

## ÖZET 1: Hedefi Tam Bulunan ve Doğrudan Yerleştirilebilecek Maddeler

| Madde    | Açıklama               | Scene     | Path / Field                                                               | Durum                       |
| -------- | ---------------------- | --------- | -------------------------------------------------------------------------- | --------------------------- |
| 1        | Bank Header            | Main      | Panel_Bank/Header → Image.Sprite                                           | Mevcut, sprite değişecek    |
| 2        | Ortak Panel BG         | Main      | Panel_Bank, Panel_ShopCards, Panel_BlackList, Panel_Ranking → Image.Sprite | Mevcut, 4x sprite değişecek |
| 3        | DailyOffers BG         | Main      | Section_DailyOffers → Image.Sprite                                         | Mevcut, BOŞ → atanacak      |
| 7+8      | Shop Header + Butonlar | Main      | Panel_ShopCards/Header, Btn_TabShopItems, Btn_TabCards → Image.Sprite      | Mevcut                      |
| 9        | Shop BG                | Main      | Panel_ShopCards → Image.Sprite                                             | Madde 2 reuse               |
| 10+12    | Blacklist BG           | Main      | Panel_BlackList → Image.Sprite                                             | Madde 2 reuse               |
| 11       | Blacklist Header       | Main      | Panel_BlackList/Header → Image.Sprite                                      | Mevcut                      |
| 15       | MissionRow BG          | Prefab    | MissionRow.prefab → Root Image.Sprite                                      | Mevcut, BOŞ → atanacak      |
| 17       | BarBG                  | Prefab    | MissionRow.prefab → BarBG Image.Sprite                                     | MEVCUT SPRİTE VAR           |
| 18       | BarFill                | Prefab    | MissionRow.prefab → BarFill Image.Sprite                                   | MEVCUT SPRİTE VAR           |
| 19       | BarBGComplete          | Prefab    | MissionRow.prefab → BarBGComplete Image.Sprite                             | Mevcut, BOŞ → atanacak      |
| 20       | BarFillFull            | Prefab    | MissionRow.prefab → BarFillFull Image.Sprite                               | MEVCUT SPRİTE VAR           |
| 21       | CompletedBtn           | Prefab    | MissionRow.prefab → CompletedBtn Image.Sprite                              | Built-in sprite → değişecek |
| 22       | RewardPopup BG         | Main      | RewardPopup/PopupPanel → Image.Sprite                                      | MEVCUT SPRİTE VAR           |
| 24       | CollectBtn             | Main      | RewardPopup/CollectBtn → Image.Sprite                                      | Built-in sprite → değişecek |
| 26       | TakeTheCarButton       | Main      | Panel_BlackList/.../TakeTheCarButton → Image.Sprite                        | Mevcut                      |
| 27       | PopularityBar BG       | Main      | PopularityBar/Popularity_BG → Image.Sprite                                 | Mevcut                      |
| 29+30    | BoostBar BG + Fill     | Main      | Slider_BoostBar/Background, Fill → Image.Sprite                            | Mevcut                      |
| 34+35+36 | ChestPopup BG'ler      | Main      | ChestPopupController → commonPopupBg/rarePopupBg/legendaryPopupBg          | Script field'ları           |
| 37       | StartUnlock Btn        | Main      | ChestPopupController → startUnlock sprites                                 | Script field'ları           |
| 40       | GoldIcon               | NewGarage | GoldIcon → Image.Sprite                                                    | Mevcut                      |
| 41       | NitroIcon              | NewGarage | NitroIcon → Image.Sprite                                                   | Mevcut                      |
| 42       | BuyPopup BG            | NewGarage | BuyPopupPanel child → Image.Sprite                                         | Mevcut                      |
| 43       | CloseButton            | NewGarage | CloseButton → Image.Sprite                                                 | Mevcut                      |
| 44       | BtnYes                 | NewGarage | Btn_Yes → Image.Sprite                                                     | Mevcut                      |
| 45       | BtnIncele              | NewGarage | BtnIncele → Image.Sprite                                                   | Mevcut                      |
| 46       | LockedUI               | NewGarage | LockedUI → Image.Sprite                                                    | Mevcut                      |
| 47       | ExitPopup BG           | NewGarage | ExitPopupPanel child → Image.Sprite                                        | Mevcut                      |
| 48       | YesBtn                 | NewGarage | confirmButton → Image.Sprite                                               | Mevcut                      |
| 49       | NoBtn                  | NewGarage | cancelButton → Image.Sprite                                                | Mevcut                      |
| 52       | Money Icon             | ChestOpen | ChestRewardRevealController.moneySprite                                    | Script field                |
| 53       | Nitro Icon             | ChestOpen | ChestRewardRevealController.nitroSprite                                    | Script field                |
| 54       | Kaplama Icon           | ChestOpen | ChestRewardRevealController.stickerSprite                                  | Script field                |

---

## ÖZET 2: Hedefi Bulunan Ama Asset'i Inspector'dan Atanması Gereken Maddeler (Script Field'ları)

| Madde    | Açıklama           | Nereye Atanacak                                        | Not                        |
| -------- | ------------------ | ------------------------------------------------------ | -------------------------- |
| 16 (x13) | Mission Icons      | BlacklistTierSO asset'leri → mission.icon              | Her tier/mission için ayrı |
| 23 (x9)  | Reward Icons       | BlacklistTierSO asset'leri → mission.reward.rewardIcon | Her reward tipi için ayrı  |
| 31+32+33 | Chest UI Icons     | ChestSlotUI → commonIcon/rareIcon/legendaryIcon        | Prefab veya scene instance |
| 34+35+36 | ChestPopup BG      | ChestPopupController → Sprite field'ları               | Scene'deki component       |
| 37       | StartUnlock Sprite | ChestPopupController → startUnlock sprites             | 3 variant olabilir         |

---

## ÖZET 3: Unity Tarafında Karşılığı Eksik / Yeni Oluşturulması Gereken Maddeler

| Madde | Açıklama              | Sorun                                                             | Çözüm                                                                                   |
| ----- | --------------------- | ----------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| 3.1   | DailyOffer Slot Frame | Slot rootlarının Image'ı var ama ayrı frame child yok             | Her slot içine ayrı frame Image child eklenebilir VEYA mevcut root Image kullanılabilir |
| 4     | Free Gold/Nitro Icon  | DailyOffersController'da free icon sprite field'ı yok             | Script'e 2 sprite field ekle (freeGoldIcon, freeNitroIcon)                              |
| 5     | Kırmızı çerçeve       | Mevcut slot'larda ayrı bir "çerçeve" katmanı yok                  | Slot layout'una frame Image child eklenebilir                                           |
| 6     | CardPacks Section BG  | Panel_Bank'ta ayrı bir Card Packs section'ın varlığı doğrulanmalı | ScrollView_Bank/Content altına Section_CardPacks eklenebilir                            |
| 13    | Car Images (6 adet)   | BlacklistTierSO'da carImage Sprite field'ı YOK                    | BlacklistTierSO'ya `public Sprite carImage;` field'ı ekle                               |
| 14    | Missions Container BG | MissionsContainer'da Image component var mı belirsiz              | Image component ekle (yoksa)                                                            |
| 25    | Claimed Görseli       | Separate sprite yok, sadece code renk dimming                     | MissionRowUI'ya claimedSprite field ekle (isteğe bağlı)                                 |
| 38    | OpenNow + NitroCoin   | NitroCoin child Image eksik olabilir                              | openNowObj içine 120×120 NitroCoin child Image ekle                                     |
| 39    | RadarPopup Frame      | Background frame Image yok, sadece overlay color                  | popupRoot altına Background Image child ekle                                            |
| 50    | Garage Arkaplanı      | 3D sahne — Canvas background var mı doğrulanmalı                  | Canvas altına background Image ekle                                                     |
| 51    | ChestScene BG         | Canvas DEAKTIF, arkaplan yok                                      | Canvas aktifleştir + background Image ekle                                              |

---

## Unity Editor'da Benim Manuel Yapmam Gerekenler

### ADIM 0: Asset'ler Import Edildi ✅

Asset'ler `Assets/UI/UIListAssets/` altında 4 alt klasöre import edilmiş durumda:

- `bank ui/` (21 dosya)
- `black list ui 2/` (33 dosya)
- `garage ui/` (7 dosya)
- `shop ui/` (3 dosya)

**ÖNEMLİ:** Her sprite'ın Texture Type'ını **Sprite (2D and UI)** yap. 9-slice gereken sprite'lar için **Sprite Editor** ile border'ları ayarla.

**DİKKAT — Paylaşımlı dosyalar:** Aşağıdaki dosyalar birden fazla maddeye hizmet ediyor. Unity'de aynı sprite'ı birden fazla yere atayabilirsin:

- `2,9,10.png` → Madde 2, 9, 10, 12 (panel BG'ler)
- `18,20.png` → Madde 18, 20 (bar fill'ler)
- `44,48.png` → Madde 44, 48 (yes butonlar)
- `45,49.png` → Madde 45, 49 (no/incele butonlar)
- `23.7-54.png` → Madde 23.7 ve 54 (kaplama ikonları)

**EKSİK DOSYALAR (tasarımcıdan istenecek):** 13, 29, 30, 31, 32, 33, 43, 50, 52, 53

### ADIM 1: Main.unity Scene'ini Aç

**Panel_Bank:**

- `Canvas/ContentRoot/Panel_Bank` seç → Image → Source Image → `bank ui/2,9,10.png` ata, Color → white yap
- `Panel_Bank/Header` seç → Image → Source Image → `bank ui/1.png` ata
- `Section_DailyOffers` seç → Image → Source Image → `bank ui/3.png` ata
- Her DailyOfferSlot (Free, 2, 3) → root Image → Source Image → `bank ui/5.png` (slot frame) ata

**Panel_ShopCards:**

- `Canvas/ContentRoot/Panel_ShopCards` seç → Image → Source Image → `bank ui/2,9,10.png` ata
- `Panel_ShopCards/Header` → Image → Source Image → `shop ui/7.png` ata
- `Btn_TabShopItems` → Image → Source Image → `shop ui/7.png` (items buton görseli)
- `Btn_TabCards` → Image → Source Image → `shop ui/8.png` (cards buton görseli)

**Panel_BlackList:**

- `Canvas/ContentRoot/Panel_BlackList` seç → Image → Source Image → `bank ui/2,9,10.png` ata
- `Panel_BlackList/Header` → Image → Source Image → `black list ui 2/11.png` ata
- `TakeTheCarButton` → Image → Source Image → `black list ui 2/26.png` ata

**Panel_Ranking:**

- `Canvas/ContentRoot/Panel_Ranking` seç → Image → Source Image → `bank ui/2,9,10.png` ata

**Blacklist RewardPopup:**

- `RewardPopup (1)/RewardBG/PopupPanel` → Image → Source Image → `black list ui 2/22.png` (isteğe bağlı, mevcut var)
- `PopupPanel/CollectBtn` → Image → Source Image → `black list ui 2/24.png` ata

**PopularityBar:**

- `TopBar/PopularityBar/Popularity_BG` → Image → Source Image → `bank ui/27.png` ata

**BoostBar:** (⚠️ 29.png ve 30.png MEVCUT DEĞİL — tasarımcıdan bekleniyor)

**ChestPopup:**

- `Canvas/ChestPopup` → ChestPopupController → Inspector:
  - `commonPopupBg` → `bank ui/34.png`
  - `rarePopupBg` → `bank ui/35..png` VEYA `bank ui/36..png` (⚠️ tasarımcıya sor!)
  - `legendaryPopupBg` → `bank ui/36..png` VEYA `bank ui/35..png` (⚠️ tasarımcıya sor!)
  - `commonStartUnlockSprite` → `bank ui/37.1.png` (mavi)
  - `rareStartUnlockSprite` → `bank ui/37..png` (temel)
  - `legendaryStartUnlockSprite` → `bank ui/37.2.png` (mor)

**RadarPopup (Madde 39):** (⚠️ 39..png küçük kare frame — popup BG'ye uygunluğu belirsiz!)

- `Canvas/RadarPopup` → popupRoot altına yeni Image child oluştur (620×750) → Source Image → `bank ui/39..png`
- snapshotImage'ı bu frame'in içine child olarak taşı

### ADIM 2: MissionRow Prefab'ı Düzenle

- `Assets/Prefabs/Blacklist/MissionRow.prefab` aç (Prefab Mode)
- `MissionRow` (root) → Image → Source Image → `black list ui 2/15.png` ata, Color → white yap
- `BarBGComplete` → Image → Source Image → `black list ui 2/19.png` ata (şu an BOŞ)
- `CompletedBtn` → Image → Source Image → `black list ui 2/21.png` ata
- Apply All

### ADIM 3: ChestSlotPrefab Düzenle (⚠️ 31, 32, 33 MEVCUT DEĞİL)

- `Assets/Prefabs/ChestSlotPrefab.prefab` aç
- `ChestSlotUI` component → Inspector:
  - `commonIcon` → "31" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)
  - `rareIcon` → "32" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)
  - `legendaryIcon` → "33" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)
- Apply

### ADIM 4: BlacklistTierSO Asset'lerini Düzenle

Her bir SO dosyası için (`Assets/Prefabs/Blacklist/BlacklistTier_1.asset` ... `BlacklistTier_5.asset`):

- Her mission'ın `icon` field'ına ilgili `black list ui 2/16.X.png` sprite'ını ata (⚠️ 16.1-16.13 arası, düz 16.png yok!)
- Her mission'ın `reward.rewardIcon` field'ına ilgili `black list ui 2/23.X.png` sprite'ını ata (⚠️ 23.1-23.9 arası, düz 23.png yok!)

### ADIM 5: NewGarage.unity Scene'ini Aç

- `GoldIcon` → Image → Source Image → `garage ui/40.png`
- `NitroIcon` → Image → Source Image → `garage ui/41.png`
- `BuyPopupPanel / child panel` → Image → Source Image → `garage ui/42.png`
- `CloseButton` → Image → Source Image → "43" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)
- `Btn_Yes` → Image → Source Image → `garage ui/44,48.png`
- `BtnIncele` → Image → Source Image → `garage ui/45,49.png`
- `LockedUI (LockedOverlay)` → Image → Source Image → `garage ui/46.png`
- `ExitPopupPanel / child panel` → Image → Source Image → `garage ui/47.png`
- `ExitPopup confirmButton` → Image → Source Image → `garage ui/44,48.png` (Madde 44 ile aynı!)
- `ExitPopup cancelButton` → Image → Source Image → `garage ui/45,49.png` (Madde 45 ile aynı!)
- Garage arkaplanı → "50" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)

### ADIM 6: ChestOpenScene.unity Scene'ini Aç

- `ChestRewardRevealController` component → Inspector:
  - `moneySprite` → "52" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)
  - `nitroSprite` → "53" (⚠️ DOSYA YOK — tasarımcıdan bekleniyor)
  - `stickerSprite` → `black list ui 2/23.7-54.png`
- Arkaplan için Canvas'ı aktif et → Background Image child ekle (1080×1920, stretch):
  - Common: `bank ui/51.png` (mavi)
  - Rare: `bank ui/51.1.png` (gri)
  - Legendary: `bank ui/51.2.png` (pink)
  - ⚠️ ChestRewardRevealController'a 3 BG sprite field eklenip runtime'da seçilmeli!

### ADIM 7: Kod Değişiklikleri (İsteğe Bağlı)

Bu maddeler doğrudan bir Image component'e statik atama değil, runtime/script üzerinden sprite değiştirme gerektiriyor:

1. **DailyOffersController** → freeGoldIcon ve freeNitroIcon field'ları ekle (Madde 4)
2. **BlacklistTierSO** → carImage Sprite field'ı ekle (Madde 13)
3. **MissionRowUI** → claimedSprite field'ı ekle (Madde 25, isteğe bağlı)

### ADIM 8: Son Kontroller

- Her scene'i kaydet (Ctrl+S)
- Her prefab değişikliğini Apply et
- Play mode'da tüm panelleri aç kapat, sprite'ların doğru gösterildiğini kontrol et
- Chest popup'ı her 3 tipte test et (Common, Rare, Legendary)
- Blacklist mission row'larını test et
- Reward popup'ı test et
- RadarPopup'ı test et (yeni background frame'in doğru çalıştığını kontrol et)

---

## Asset Boyutları Özet Tablosu

| Madde | Asset Adı                  | Boyut     | Kullanım Sayısı                                |
| ----- | -------------------------- | --------- | ---------------------------------------------- |
| 1     | Bank Header                | 1080×200  | 1                                              |
| 2     | Panel BG                   | 1080×1920 | 4+ (Panel_Bank, ShopCards, BlackList, Ranking) |
| 3     | DailyOffers BG             | 980×760   | 1 (reuse: Madde 6)                             |
| 3.1   | Slot Frame                 | 280×550   | 3 (DailyOffer slot'ları)                       |
| 4     | Free Icons                 | 250×260   | 2 (gold + nitro)                               |
| 5     | Kırmızı Çerçeve            | 250×550   | Slot frame                                     |
| 6     | CardPacks BG               | 980×560   | 1 (Madde 3 reuse olabilir)                     |
| 7     | Header                     | -         | 1                                              |
| 8     | Items/Cards Buttons        | 250×90    | 2                                              |
| 11    | Blacklist Header           | 1080×200  | 1                                              |
| 13    | Car Images                 | 960×400   | 6                                              |
| 14    | Missions Container         | 960×800   | 1                                              |
| 15    | MissionRow BG              | 900×130   | n (prefab, runtime instantiate)                |
| 16    | Mission Icons              | 100×100   | 13 tip                                         |
| 17    | BarBG                      | 740×50    | n (prefab)                                     |
| 18    | BarFill                    | 740×50    | n (prefab)                                     |
| 19    | BarBGComplete              | 540×50    | n (prefab)                                     |
| 20    | BarFillFull                | 540×50    | n (prefab)                                     |
| 21    | CompletedBtn               | 160×100   | n (prefab)                                     |
| 22    | RewardPopup BG             | 800×700   | 1                                              |
| 23    | Reward Icons               | 630×380   | 9 tip                                          |
| 24    | CollectBtn                 | 380×90    | 1                                              |
| 25    | Claimed                    | 160×100   | n (prefab)                                     |
| 26    | TakeTheCarBtn              | 300×120   | 1                                              |
| 27    | Popularity BG              | 420×100   | 1                                              |
| 28    | (Fill — kullanıcı yapacak) | -         | -                                              |
| 29    | BoostBar BG                | 990×20    | 1                                              |
| 30    | BoostBar Fill              | 990×20    | 1                                              |
| 31    | Common Chest Icon          | 180×140   | 1 (ChestSlotUI)                                |
| 32    | Rare Chest Icon            | 180×140   | 1                                              |
| 33    | Legendary Chest Icon       | 180×140   | 1                                              |
| 34    | Common Popup BG            | 1250×1100 | 1                                              |
| 35    | Rare Popup BG              | 1250×1100 | 1                                              |
| 36    | Legendary Popup BG         | 1250×1100 | 1                                              |
| 37    | StartUnlock Btn            | 500×190   | 1 (veya 3 variant)                             |
| 38    | OpenNow Btn                | 500×190   | 1 + NitroCoin 120×120                          |
| 39    | RadarPopup Frame           | 620×750   | 1                                              |
| 40    | GoldIcon                   | 100×100   | 2+ (garage, buyPopup)                          |
| 41    | NitroIcon                  | 100×100   | 2+ (garage, buyPopup)                          |
| 42    | BuyPopup BG                | 800×700   | 1                                              |
| 43    | CloseButton                | 80×80     | 1                                              |
| 44    | BtnYes                     | 300×150   | 1                                              |
| 45    | BtnIncele                  | 300×150   | 1                                              |
| 46    | LockedUI                   | 700×280   | 1                                              |
| 47    | ExitPopup BG               | 800×700   | 1                                              |
| 48    | YesBtn                     | 250×160   | 1                                              |
| 49    | NoBtn                      | 250×160   | 1                                              |
| 50    | Garage BG                  | 1080×1920 | 1                                              |
| 51    | ChestScene BG              | 1080×1920 | 1                                              |
| 52    | Money Icon                 | 270×240   | 1                                              |
| 53    | Nitro Icon                 | 270×240   | 1                                              |
| 54    | Kaplama Icon               | 270×240   | 1                                              |
