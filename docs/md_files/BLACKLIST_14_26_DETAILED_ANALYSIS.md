# BLACKLIST SİSTEMİ DETAYLI ANALİZ RAPORU — Madde 14–26

> **Proje:** Car Clicker Mobile 3D  
> **Tarih:** Güncel analiz  
> **Kapsam:** UI List Madde 14–26 (Blacklist Paneli İçi)

---

## SİSTEM MİMARİSİ ÖZET

Devam etmeden önce, Blacklist sisteminin **nasıl çalıştığını** özetleyelim:

```
                    ┌──────────────────────────┐
                    │    BlacklistTierSO        │  ← ScriptableObject (.asset dosyaları)
                    │  Assets/Prefabs/Blacklist/│
                    │                           │
                    │  • tierIndex (1-6)        │
                    │  • carImage: Sprite       │  ← Madde 13: Araba görseli
                    │  • missions[5]:           │
                    │    ├── icon: Sprite       │  ← Madde 16: Görev ikonları
                    │    ├── description: str   │
                    │    ├── missionType: enum  │
                    │    └── reward:            │
                    │        ├── rewardIcon     │  ← Madde 23: Ödül ikonları
                    │        ├── goldAmount     │
                    │        └── ...            │
                    └──────────┬───────────────┘
                               │
                    ┌──────────▼───────────────┐
                    │  BlacklistPanelController │  ← Main.unity Scene Object
                    │  (Panel_BlackList üzerinde)│
                    │                           │
                    │  BuildUI() metodu:        │
                    │  1. ActiveTier'ı al       │
                    │  2. ClearMissionRows()    │
                    │  3. Her mission için:     │
                    │     Instantiate(prefab,   │
                    │       missionsContainer)  │
                    │  4. rowUI.Setup(def, i)   │
                    └──────────┬───────────────┘
                               │
                    ┌──────────▼───────────────┐
                    │  MissionRow.prefab        │  ← Runtime'da klonlanır
                    │  Assets/Prefabs/Blacklist/│
                    │                           │
                    │  MissionRowUI.cs:         │
                    │  Setup() → icon.sprite =  │
                    │    definition.icon        │
                    │  SetVisualState() →       │
                    │    BarBG vs BarBGComplete  │
                    │  SetClaimed() →           │
                    │    CompletedBtn text+renk  │
                    └──────────────────────────┘
```

### Akış Sırası (Runtime)

1. Oyuncu Blacklist panelini açar
2. `BlacklistPanelController.BuildUI()` çağrılır
3. `BlacklistManager.Instance.ActiveTier` → mevcut `BlacklistTierSO` asset'ini döner
4. `carImage.sprite = tier.carImage` → Araba görseli SO'dan gelir
5. `missionsContainer` altındaki eski çocuklar `Destroy()` ile silinir
6. `tier.missions[0..4]` için döngü: `Instantiate(missionRowPrefab, missionsContainer)` → 5 MissionRow prefab klonu oluşur
7. Her klon için `rowUI.Setup(definition, index)` çağrılır
8. `Setup()` içinde: `icon.sprite = definition.icon` (SO'dan), açıklama metni set edilir
9. Claim butonu tıklanınca: `RewardPopupController.Show()` → `rewardIcon.sprite = reward.rewardIcon` (SO'dan)

---

## SCENE HİYERARŞİSİ (TAM)

```
Panel_BlackList                              [Image, CanvasGroup, BlacklistPanelController]
│                                             m_IsActive: 0 (başlangıçta gizli)
│                                             m_Sprite: Unity "Background" (10907)
│                                             m_Color: (0, 0, 0, 0.588) — yarı saydam siyah overlay
│
├── Header                                   [RectTransform, Image, CanvasRenderer]
│   │                                         m_Sprite: ✅ ATANMIŞ (guid: 2fc45cbfc4efd85469115d3affd54045)
│   │                                         Size: stretch × 200
│   └── BlacklistTitle (1)                   [TMP_Text]  "BLACKLIST #6"
│
└── ScrollView_Blacklist                     [ScrollRect, Image, Mask, CanvasRenderer]
    │                                         m_Horizontal: false, m_Vertical: true
    ├── Viewport                             [Image (Mask), RectMask2D]
    │   └── Content                          [VerticalLayoutGroup, ContentSizeFitter]
    │       │                                 Size: stretch × 1607.1
    │       └── Others (1)                   [VerticalLayoutGroup]
    │           │                             m_Spacing: 30, m_Padding: 40/40/30/200
    │           │
    │           ├── CarImage                 [Image, CanvasRenderer]
    │           │   │                         m_Sprite: {fileID: 0} — BOŞ ❌
    │           │   │                         m_Color: (1, 1, 1, 1) — beyaz
    │           │   │                         Size: 960 × 400
    │           │   │                         ➜ Runtime'da: carImage.sprite = tier.carImage (SO'dan)
    │           │   ├── CarName              [TMP_Text]
    │           │   └── (child 1524049988)
    │           │
    │           ├── Missions ★ MADDE 14 ★    [RectTransform, Image, VerticalLayoutGroup, CanvasRenderer]
    │           │   │                         m_Sprite: {fileID: 0} — BOŞ ❌
    │           │   │                         m_Color: (1, 1, 1, 1) — tam beyaz
    │           │   │                         Size: 960 × 800
    │           │   │                         VLG m_Spacing: 0, m_ChildAlignment: 4 (MiddleCenter)
    │           │   │
    │           │   └── (Runtime'da MissionRow prefab klonları buraya eklenir)
    │           │
    │           └── TakeTheCarButton ★26★    [RectTransform, Button, Image, CanvasGroup, CanvasRenderer]
    │                                         m_Sprite: Unity "Knob" (10905) ❌
    │                                         m_Color: (0, 1, 0.027, 1) — parlak yeşil
    │                                         Size: 300 × 117.1
    │                                         CanvasGroup.alpha → kod tarafından kontrol edilir
    │
    ├── (Scrollbar child - 1627933736)
    │
    └── RewardPopup (1) ★22-24★              [RectTransform, CanvasGroup, RewardPopupController]
        │                                     ScrollView_Blacklist'in kardeşi (sibling), Panel_BlackList'in torunu
        └── RewardBG                         [Image, CanvasRenderer]
            │                                 m_Sprite: Unity "Background" (10907)
            │                                 m_Color: (0, 0, 0, 0.698) — koyu yarı saydam overlay
            │                                 Size: stretch (full screen)
            │
            └── RewardImage ★22★             [RectTransform, Image, CanvasRenderer]
                │                             m_Sprite: {fileID: 0} — BOŞ ❌
                │                             m_Color: (1, 1, 1, 1) — beyaz
                │                             Size: 800 × 700
                │                             Bu = popupPanel referansı (animasyon hedefi)
                │
                ├── TitleText                [TMP_Text]  → "REWARD!"
                ├── YouReceivedText          [TMP_Text]  → "YOU RECEIVED:"
                ├── RewardText               [TMP_Text]  → reward.rewardDisplayText
                ├── RewardIcon ★23★          [Image, CanvasRenderer]
                │                             m_Sprite: {fileID: 0} — BOŞ ❌
                │                             m_Color: (1, 0, 0, 1) — KIRMIZI ❌
                │                             Size: 632 × 376
                │                             ➜ Runtime'da: rewardIcon.sprite = reward.rewardIcon (SO'dan)
                │
                └── CollectBtn ★24★          [RectTransform, Button, Image, CanvasRenderer]
                    │                         m_Sprite: Unity "Knob" (10905) ❌
                    │                         m_Color: (1, 1, 1, 1) — beyaz
                    │                         Size: 380 × 90
                    └── (Text child)         [TMP_Text]
```

### MissionRow.prefab (Her görev için runtime'da klonlanır)

```
MissionRow ★15★                              [RectTransform, Image, CanvasRenderer, MissionRowUI]
│                                             m_Sprite: {fileID: 0} — BOŞ ❌
│                                             m_Color: (1, 0, 0, 1) — KIRMIZI ❌
│                                             Size: 900 × 134.1
│
├── Image ★16★                               [RectTransform, Image, CanvasRenderer]
│                                             m_Sprite: {fileID: 0} — BOŞ (runtime'da set edilir)
│                                             m_Color: (1, 1, 1, 1) — beyaz
│                                             Size: 100 × 100
│                                             ➜ Setup(): icon.sprite = definition.icon (SO'dan)
│
├── MissionDesc                              [RectTransform, TMP_Text, CanvasRenderer]
│                                             Size: 200 × 50
│                                             ➜ Setup(): text = definition.description
│
├── BarBG ★17★                               [RectTransform, Image, CanvasRenderer]
│   │                                         m_Sprite: ✅ ATANMIŞ (guid: f4f9dd77...)
│   │                                         m_Color: (0.149, 0.149, 0.149, 1) — koyu gri
│   │                                         Size: 740 × 50
│   └── BarFill ★18★                        [RectTransform, Image, CanvasRenderer]
│                                             m_Sprite: ✅ ATANMIŞ (guid: 4b6e56fc...)
│                                             m_Type: 3 (Filled), horizontal
│                                             m_Color: (1, 1, 1, 1)
│
├── ProgressText                             [TMP_Text]
│                                             "43K / 50K" formatında
│
├── BarBGComplete ★19★                       [RectTransform, Image, CanvasRenderer]
│   │                                         m_Sprite: {fileID: 0} — BOŞ ❌
│   │                                         m_Color: (0.149, 0.149, 0.149, 1) — koyu gri
│   │                                         Size: 540 × 50
│   │                                         ➜ SetVisualState(true) ile görünür olur
│   │
│   ├── BarFillFull ★20★                    [Image]
│   │                                         m_Sprite: ✅ ATANMIŞ (guid: 9d1b5688...)
│   │                                         m_Type: 3 (Filled)
│   └── BarBGCompleteText                    [TMP_Text]
│
└── CompletedBtn ★21/25★                     [RectTransform, Image, Button, CanvasRenderer]
    │                                         m_Sprite: Unity "Knob" (10905) ❌
    │                                         m_Color: (0, 1, 0.004, 1) — parlak yeşil
    │                                         Size: 160 × 100
    │                                         ➜ SetVisualState(true) ile görünür olur
    │                                         ➜ SetClaimed() ile: renk → (0.5, 0.5, 0.5, 0.7), text → "CLAIMED"
    │
    └── Text (TMP)                           [TMP_Text]  "Button" → placeholder ❌
```

---

## MADDE MADDE DETAYLI ANALİZ

---

### MADDE 14 — MissionsContainer Arka Planı

| Soru                          | Cevap                                                                                                                                                                                           |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Görev satırlarının (MissionRow) yerleştirildiği konteyner arka planı. Runtime'da `Instantiate()` ile oluşturulan 5 MissionRow prefab klonunun parent'ı                                          |
| **Nerede?**                   | `Main.unity` → `Panel_BlackList → ScrollView_Blacklist → Viewport → Content → Others (1) → Missions`                                                                                            |
| **Scene / Prefab / Runtime?** | **SCENE OBJECT** — sahnede önceden bulunur, runtime'da değişmez. Yalnızca çocukları (MissionRow klonları) dinamiktir                                                                            |
| **Hangi component?**          | `UnityEngine.UI.Image` (fileID: 1307409787) + `VerticalLayoutGroup` (fileID: 1307409786)                                                                                                        |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 0}` — **BOŞ** ❌. `m_Color: (1, 1, 1, 1)` — **tam beyaz**. Sonuç: 960×800 düz beyaz dikdörtgen                                                                              |
| **Sprite nereye atanıyor?**   | Doğrudan Unity Editor'da → `Missions` GameObject → `Image` component → `Sprite` alanı                                                                                                           |
| **Kod referansı**             | `BlacklistPanelController.cs` → `[SerializeField] Transform missionsContainer` — yalnızca Transform olarak kullanılır (`Instantiate(prefab, missionsContainer)`), Image bileşenine kod dokunmaz |
| **Yapılacak işlem**           | Unity Editor'da `Missions` objesini seç → Image → Sprite alanına `14.png`'yi sürükle. Color'ı white `(1,1,1,1)` bırak (sprite kendi rengini getirsin)                                           |
| **Ek kontrol**                | VerticalLayoutGroup → Spacing: şu an **0**. Görev satırları bitişik görünecek. 10-20 px spacing önerilir                                                                                        |
| **Kod değişikliği**           | **HAYIR** — sadece sahne düzenlemesi                                                                                                                                                            |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/14.png` — bronz/bakır süslü panel çerçevesi                                                                                                             |

**Editor Adımları:**

- [ ] `Main.unity` aç → Hierarchy'de `Panel_BlackList / ScrollView_Blacklist / Viewport / Content / Others (1) / Missions` bul
- [ ] Inspector'da Image component → Source Image → `14` (14.png) sprite'ını ata
- [ ] Image Type: `Sliced` olarak ayarla (9-slice panel çerçevesi olarak kullanmak için)
- [ ] Color: `(1, 1, 1, 1)` white bırak
- [ ] (Opsiyonel) VerticalLayoutGroup → Spacing: 0'dan 10-15'e çıkar

---

### MADDE 15 — MissionRow Arka Planı (Prefab Root)

| Soru                          | Cevap                                                                                                                                                               |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Tek bir görev satırının arka plan görseli. MissionRow prefab'ın root GameObject'inin Image component'i                                                              |
| **Nerede?**                   | `Assets/Prefabs/Blacklist/MissionRow.prefab` → root `MissionRow`                                                                                                    |
| **Scene / Prefab / Runtime?** | **PREFAB** — bu dosya düzenlenir, runtime'da her `Instantiate()` çağrısında bu sprite otomatik klonlanır. 5 görev × 6 tier = 30 instance için tek düzenleme yeterli |
| **Hangi component?**          | Root GameObject'in `UnityEngine.UI.Image` bileşeni                                                                                                                  |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 0}` — **BOŞ** ❌. `m_Color: (1, 0, 0, 1)` — **KIRMIZI** ❌. Sonuç: her görev satırı düz kırmızı dikdörtgen olarak görünür                       |
| **Sprite nereye atanıyor?**   | Prefab dosyasında → root `MissionRow` → Image → Sprite alanı (Editor'da)                                                                                            |
| **Kod referansı**             | `MissionRowUI.cs` root Image'ı doğrudan değiştirmez — yalnızca çocuk objeleri manipüle eder                                                                         |
| **Yapılacak işlem**           | Prefab'ı aç → root MissionRow → Image → Sprite: `15.png`, Color: `(1, 1, 1, 1)` white                                                                               |
| **Ek kontrol**                | Size: 900×134.1 — parent Missions container 960 genişliğinde. 60px fark var; gerekirse anchor stretch yapılabilir                                                   |
| **Kod değişikliği**           | **HAYIR** — sadece prefab düzenlemesi                                                                                                                               |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/15.png` — gri-mavi yarı saydam görev satırı arka planı                                                                      |

**Editor Adımları:**

- [ ] `Assets/Prefabs/Blacklist/MissionRow.prefab` dosyasını çift tıkla (prefab mode)
- [ ] Root `MissionRow` objesini seç
- [ ] Image → Source Image → `15` sprite'ını ata
- [ ] Image → Color → `(1, 1, 1, 1)` white yap (şu an KIRMIZI!)
- [ ] Image → Image Type → `Sliced` ayarla
- [ ] Kaydet (Ctrl+S)

---

### MADDE 16 — Görev İkonları (16.1 – 16.13)

| Soru                          | Cevap                                                                                                                                                                               |
| ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Her görev satırının sol tarafındaki ikon. Görev türünü temsil eder (altın kazan, nitro topla, radar boz, vb.)                                                                       |
| **Nerede?**                   | `Assets/Prefabs/Blacklist/MissionRow.prefab` → `MissionRow → Image` (ilk çocuk)                                                                                                     |
| **Scene / Prefab / Runtime?** | **RUNTIME – ScriptableObject'tan beslenir.** Prefab'da sprite boş bırakılır (`{fileID: 0}`); `MissionRowUI.Setup()` çağrıldığında `icon.sprite = definition.icon` ile SO'dan atanır |
| **Hangi component?**          | `MissionRow → Image` child object → `UnityEngine.UI.Image`                                                                                                                          |
| **Mevcut sprite durumu**      | Prefab'da: `m_Sprite: {fileID: 0}` — BOŞ (bu doğru, runtime'da dolacak). SO asset dosyalarında: ✅ icon GUID'leri zaten atanmış (BlacklistTier\_ 1.asset doğrulandı)                |
| **Sprite nereye atanıyor?**   | Her `BlacklistTierSO` asset dosyasında → `missions[i].icon` Sprite alanına                                                                                                          |
| **Kod referansı**             | `MissionRowUI.cs` satır ~61: `icon.sprite = definition.icon;`                                                                                                                       |
| **Yapılacak işlem**           | Her `BlacklistTier_*.asset` dosyasını aç → her mission'ın `icon` alanına doğru 16.x sprite'ını ata                                                                                  |
| **Ek kontrol**                | 13 farklı görev türü var ama her tier'da 5 görev → aynı ikon birden fazla tier'da tekrar kullanılabilir                                                                             |
| **Kod değişikliği**           | **HAYIR** — sprite atama zaten kod tarafından yapılıyor, sadece SO'lar doldurulmalı                                                                                                 |

#### Görev Türü → İkon Eşleştirme Tablosu

| #     | BlacklistMissionType Enum | Enum Değeri | Açıklama                    | Asset Dosyası |
| ----- | ------------------------- | :---------: | --------------------------- | ------------- |
| 16.1  | `EarnGold`                |      0      | Altın kazan                 | `16.1.png`    |
| 16.2  | `CollectWorldNitro`       |      1      | Dünyadan nitro topla        | `16.2.png`    |
| 16.3  | `DefuseRadars`            |      2      | Radar boz                   | `16.3.png`    |
| 16.4  | `OwnBuildings`            |      3      | Bina sahibi ol              | `16.4.png`    |
| 16.5  | `OpenChests`              |      4      | Sandık aç                   | `16.5.png`    |
| 16.6  | `UseBoost`                |      5      | Boost kullan                | `16.6.png`    |
| 16.7  | `EscapePolice`            |      6      | Polisten kaç                | `16.7.png`    |
| 16.8  | `UpgradeAnyCardToLevel`   |      7      | Kartı seviyeye yükselt      | `16.8.png`    |
| 16.9  | `BuyGarageParts`          |      8      | Garaj parçası al            | `16.9.png`    |
| 16.10 | `TriggerNitroRain`        |      9      | Nitro yağmuru tetikle       | `16.10.png`   |
| 16.11 | `NitroMagnetCollect`      |     10      | Nitro mıknatısı ile topla   | `16.11.png`   |
| 16.12 | `UseTurbo`                |     11      | Turbo kullan                | `16.12.png`   |
| 16.13 | `ReachTotalCardLevel`     |     12      | Toplam kart seviyesine ulaş | `16.13.png`   |

#### Tier Bazlı İkon Atama Rehberi

Her tier'ın 5 görevi var. Her görevin `missionType` enum değerine göre yukarıdaki tablodan doğru ikonu atayın.

**Örnek — BlacklistTier\_ 1.asset (Tier 5: JMP-NARDO):**

| Mission Index | missionType           | Enum | Atanacak İkon |
| :-----------: | --------------------- | :--: | ------------- |
|       0       | EarnGold              |  0   | `16.1.png`    |
|       1       | UseBoost              |  5   | `16.6.png`    |
|       2       | EscapePolice          |  6   | `16.7.png`    |
|       3       | UpgradeAnyCardToLevel |  7   | `16.8.png`    |
|       4       | BuyGarageParts        |  8   | `16.9.png`    |

> **NOT:** BlacklistTier\_ 1.asset'te icon GUID'leri zaten dolu — bu ikonlar daha önce atanmış. Diğer tier'ları da kontrol edin.

**Editor Adımları:**

- [ ] `Assets/Prefabs/Blacklist/BlacklistTier_ 1.asset` aç → Inspector'da `Missions` dizisini genişlet
- [ ] Her mission'ın `Icon` alanını kontrol et
- [ ] Boş olanlar için: ilgili `16.x.png` sprite'ını ata (missionType enum değerine göre)
- [ ] `BlacklistTier_ 2.asset` ile `BlacklistTier_ 5.asset` arasını ve `BlacklistTier_.asset` için aynısını tekrarla
- [ ] **Toplam: 6 asset × 5 mission = 30 icon alanı kontrol edilecek**

---

### MADDE 17 — BarBG (İlerleme Çubuğu Arka Planı)

| Soru                          | Cevap                                                                                                                                                               |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Görev tamamlanmamışken gösterilen progress bar'ın arka plan çubuğu                                                                                                  |
| **Nerede?**                   | `MissionRow.prefab` → `MissionRow → BarBG`                                                                                                                          |
| **Scene / Prefab / Runtime?** | **PREFAB** — zaten sprite atanmış durumda                                                                                                                           |
| **Hangi component?**          | `BarBG` → `UnityEngine.UI.Image`                                                                                                                                    |
| **Mevcut sprite durumu**      | `m_Sprite: ✅ ATANMIŞ` (guid: `f4f9dd77eb0fe934db1b6c33bd100596`). `m_Color: (0.149, 0.149, 0.149, 1)` — koyu gri. Size: 740×50                                     |
| **Sprite nereye atanıyor?**   | Zaten prefab'da atanmış. `MissionRowUI.cs` çalışma zamanında `barBG` objesini yalnızca `SetActive()` ile gösterir/gizler                                            |
| **Yapılacak işlem**           | **Mevcut sprite'ı yeni tasarımla değiştirmek istiyorsanız**: Prefab → BarBG → Image → Source Image → `17.png`. **Mevcut halinden memnunsanız**: değişiklik gerekmez |
| **Ek kontrol**                | `SetVisualState(false)` → BarBG görünür + ProgressText görünür. `SetVisualState(true)` → BarBG gizli, BarBGComplete görünür                                         |
| **Kod değişikliği**           | **HAYIR**                                                                                                                                                           |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/17.png`                                                                                                                     |

**Editor Adımları:**

- [ ] `MissionRow.prefab` aç → `BarBG` seç
- [ ] Image → Source Image kontrol et (zaten atanmış)
- [ ] Yeni tasarım uygulanacaksa: `17` sprite'ını ata
- [ ] Kaydet

---

### MADDE 18 — BarFill (İlerleme Çubuğu Dolgu)

| Soru                          | Cevap                                                                                                          |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Progress bar'ın dolu kısmı. Görev ilerlemesine göre yüzdesel doluluk gösterir                                  |
| **Nerede?**                   | `MissionRow.prefab` → `MissionRow → BarBG → BarFill`                                                           |
| **Scene / Prefab / Runtime?** | **PREFAB** — sprite atanmış. Runtime'da `barFill.fillAmount` kodu tarafından güncellenir                       |
| **Hangi component?**          | `BarFill` → `UnityEngine.UI.Image`, `m_Type: 3` (Filled), `m_FillMethod: 0` (Horizontal)                       |
| **Mevcut sprite durumu**      | `m_Sprite: ✅ ATANMIŞ` (guid: `4b6e56fc090b4964a8bd837d2cd00f57`). `m_Color: (1, 1, 1, 1)` — beyaz             |
| **Sprite nereye atanıyor?**   | Prefab'da atanmış. Kod yalnızca `fillAmount` değerini günceller                                                |
| **Yapılacak işlem**           | Yeni tasarım uygulanacaksa: BarFill → Image → Source Image → `18,20` sprite'ı. Image Type: **Filled** kalmalı! |
| **Ek kontrol**                | `18,20.png` dosyası BarFill (madde 18) ve BarFillFull (madde 20) için **PAYLAŞIMLI** kullanılıyor              |
| **Kod değişikliği**           | **HAYIR**                                                                                                      |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/18,20.png` (paylaşımlı dosya)                                          |

**Editor Adımları:**

- [ ] `MissionRow.prefab` aç → `BarBG → BarFill` seç
- [ ] Image → Source Image kontrol et (zaten atanmış)
- [ ] Yeni tasarım istiyorsanız: `18,20` sprite'ını ata
- [ ] **ÖNEMLİ:** Image Type `Filled` olmalı, `Simple` olmamalı!
- [ ] Fill Method: Horizontal, Fill Origin: Left bırakın
- [ ] Kaydet

---

### MADDE 19 — BarBGComplete (Tamamlanan Görev Çubuğu Arka Planı)

| Soru                          | Cevap                                                                                              |
| ----------------------------- | -------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Görev tamamlandığında gösterilen çubuğun arka planı. BarBG'nin "complete" versiyonu                |
| **Nerede?**                   | `MissionRow.prefab` → `MissionRow → BarBGComplete`                                                 |
| **Scene / Prefab / Runtime?** | **PREFAB** — başlangıçta gizli; görev tamamlanınca `SetVisualState(true)` ile aktifleşir           |
| **Hangi component?**          | `BarBGComplete` → `UnityEngine.UI.Image`                                                           |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 0}` — **BOŞ** ❌. `m_Color: (0.149, 0.149, 0.149, 1)` — koyu gri. Size: 540×50 |
| **Sprite nereye atanıyor?**   | Prefab'da → `BarBGComplete` → Image → Sprite alanı                                                 |
| **Kod referansı**             | `MissionRowUI.cs`: `barBGComplete.SetActive(isComplete)` — gizle/göster; sprite'ı değiştirmez      |
| **Yapılacak işlem**           | BarBGComplete → Image → Sprite: `19.png` ata                                                       |
| **Ek kontrol**                | BarBG ile aynı yükseklikte (50) ama daha dar (540 vs 740) — CompletedBtn'a yer bırakmak için       |
| **Kod değişikliği**           | **HAYIR**                                                                                          |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/19.png`                                                    |

**Editor Adımları:**

- [ ] `MissionRow.prefab` aç → `BarBGComplete` seç
- [ ] Image → Source Image → `19` sprite'ını ata
- [ ] Image Type: uygun şekilde ayarla (Simple veya Sliced)
- [ ] Color: `(1, 1, 1, 1)` white bırak (sprite kendi rengini getirsin) veya mevcut koyu gri bırak
- [ ] Kaydet

---

### MADDE 20 — BarFillFull (Tamamlanan Görev Dolgu Çubuğu)

| Soru                          | Cevap                                                                                         |
| ----------------------------- | --------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Görev tamamlandığında, dolu çubuğun görsel dolgusu. BarFill'in "complete" versiyonu           |
| **Nerede?**                   | `MissionRow.prefab` → `MissionRow → BarBGComplete → BarFillFull`                              |
| **Scene / Prefab / Runtime?** | **PREFAB** — parent BarBGComplete ile birlikte gösterilir/gizlenir                            |
| **Hangi component?**          | `BarFillFull` → `UnityEngine.UI.Image`, `m_Type: 3` (Filled)                                  |
| **Mevcut sprite durumu**      | `m_Sprite: ✅ ATANMIŞ` (guid: `9d1b568828eb1b04388ac049510163bc`)                             |
| **Sprite nereye atanıyor?**   | Prefab'da atanmış. Kod `barFillFull` Image'ını dokunmaz (parent SetActive ile kontrol edilir) |
| **Yapılacak işlem**           | Yeni tasarım uygulanacaksa: `18,20` sprite'ını ata. Image Type **Filled** kalmalı             |
| **Ek kontrol**                | Bu aynı `18,20.png` paylaşımlı dosya — BarFill (18) ile aynı görsel                           |
| **Kod değişikliği**           | **HAYIR**                                                                                     |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/18,20.png` (paylaşımlı)                               |

**Editor Adımları:**

- [ ] `MissionRow.prefab` aç → `BarBGComplete → BarFillFull` seç
- [ ] Image → Source Image kontrol et (zaten atanmış)
- [ ] Yeni tasarım istiyorsanız: `18,20` sprite'ını ata
- [ ] **ÖNEMLİ:** Image Type `Filled` olmalı!
- [ ] Kaydet

---

### MADDE 21 — CompletedBtn (Görev Tamamlandı Butonu / CLAIM)

| Soru                          | Cevap                                                                                                                                                                                                   |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Görev tamamlandığında görünür olan "CLAIM" butonu. Tıklandığında `RewardPopup` açılır                                                                                                                   |
| **Nerede?**                   | `MissionRow.prefab` → `MissionRow → CompletedBtn`                                                                                                                                                       |
| **Scene / Prefab / Runtime?** | **PREFAB** — başlangıçta gizli; `SetVisualState(true)` ile aktifleşir                                                                                                                                   |
| **Hangi component?**          | `CompletedBtn` → `UnityEngine.UI.Image` + `UnityEngine.UI.Button`                                                                                                                                       |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 10905}` — **Unity built-in "Knob"** ❌. `m_Color: (0, 1, 0.004, 1)` — parlak yeşil. Button SpriteState: tüm state'ler `{fileID: 0}` (null). Child text: **"Button"** (placeholder!) |
| **Sprite nereye atanıyor?**   | Prefab'da → `CompletedBtn` → Image → Sprite alanı                                                                                                                                                       |
| **Kod referansı**             | `MissionRowUI.cs`: `completedBtn.SetActive(isComplete)` → görünürlük. Tıklama: `OnClaimPressed()` → `BlacklistPanelController.Instance.ShowRewardPopup(missionIndex)`                                   |
| **Yapılacak işlem**           | CompletedBtn → Image → Sprite: `21.png`. Color: white. Text: "CLAIM" olarak değiştir                                                                                                                    |
| **Ek kontrol**                | Bu buton Madde 25 (Claimed) ile **AYNI OBJEDIR**. Claimed durumu kod ile renk/text değiştirir, ayrı sprite yok                                                                                          |
| **Kod değişikliği**           | **HAYIR** — ama text child'ın metnini "Button"dan "CLAIM"e değiştirin                                                                                                                                   |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/21.png`                                                                                                                                                         |

**Editor Adımları:**

- [ ] `MissionRow.prefab` aç → `CompletedBtn` seç
- [ ] Image → Source Image → `21` sprite'ını ata
- [ ] Image → Color → `(1, 1, 1, 1)` white yap (şu an yeşil)
- [ ] Image → Image Type → gerekirse `Sliced`
- [ ] Button → Sprite State → (opsiyonel) Highlighted/Pressed/Disabled sprite'ları ata
- [ ] `CompletedBtn → Text (TMP)` seç → text'i `"Button"` → `"CLAIM"` değiştir
- [ ] Font boyutunu ve rengini stile göre ayarla
- [ ] Kaydet

---

### MADDE 22 — RewardPopup Arka Planı (Popup Çerçevesi)

| Soru                          | Cevap                                                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------ |
| **Nedir?**                    | Görev ödülü claim edildiğinde açılan popup'ın ana çerçeve/kart görseli                                                               |
| **Nerede?**                   | `Main.unity` → `Panel_BlackList → ScrollView_Blacklist → RewardPopup (1) → RewardBG → RewardImage`                                   |
| **Scene / Prefab / Runtime?** | **SCENE OBJECT** — sahnede bulunur. `RewardPopupController` ile açılır/kapanır (DOTween animasyonlu)                                 |
| **Hangi component?**          | `RewardImage` → `UnityEngine.UI.Image` — bu aynı zamanda `popupPanel` (RectTransform) referansıdır                                   |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 0}` — **BOŞ** ❌. `m_Color: (1, 1, 1, 1)` — beyaz. Sonuç: 800×700 düz beyaz dikdörtgen                           |
| **Sprite nereye atanıyor?**   | Unity Editor'da → `RewardImage` → Image → Sprite alanı                                                                               |
| **Kod referansı**             | `RewardPopupController.cs`: `popupPanel` referansı bu RectTransform'a bağlı. Scale animasyonu uygulanır. Image sprite'a kod dokunmaz |
| **Yapılacak işlem**           | RewardImage → Image → Sprite: `22.png` ata                                                                                           |
| **Ek kontrol**                | RewardBG (parent) semi-transparent siyah overlay — **bunun sprite'ı değiştirilmesin**, amacı arka planı karartmak                    |
| **Kod değişikliği**           | **HAYIR**                                                                                                                            |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/22.png` — turuncu süslü popup çerçevesi (3 satırlı slot yapısı)                              |

**Editor Adımları:**

- [ ] `Main.unity` aç → Hierarchy'de `Panel_BlackList` → `ScrollView_Blacklist` → `RewardPopup (1)` → `RewardBG` → `RewardImage` bul
- [ ] Image → Source Image → `22` sprite'ını ata
- [ ] Image → Color → `(1, 1, 1, 1)` white bırak
- [ ] Image → Image Type → `Sliced` (9-slice panel çerçevesi)
- [ ] **RewardBG'yi DEĞİŞTİRMEYİN** — koyu overlay olarak kalmalı

---

### MADDE 23 — Ödül İkonları (23.1 – 23.9)

| Soru                          | Cevap                                                                                                                                                                                    |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | RewardPopup içinde gösterilen ödül ikonu. Her görevin ödül türünü temsil eder                                                                                                            |
| **Nerede?**                   | `Main.unity` → `RewardPopup (1) → RewardBG → RewardImage → RewardIcon`                                                                                                                   |
| **Scene / Prefab / Runtime?** | **RUNTIME – ScriptableObject'tan beslenir.** Scene'de RewardIcon Image objesi boş bırakılır; `RewardPopupController.Show()` çağrıldığında `rewardIcon.sprite = reward.rewardIcon` atanır |
| **Hangi component?**          | `RewardIcon` → `UnityEngine.UI.Image` (fileID: 1998989949)                                                                                                                               |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 0}` — BOŞ (doğru — runtime'da dolacak). **AMA** `m_Color: (1, 0, 0, 1)` — **KIRMIZI** ❌ Bu renk runtime sprite'ı bozar!                                             |
| **Sprite nereye atanıyor?**   | Her `BlacklistTierSO` asset → `missions[i].reward.rewardIcon` Sprite alanına. Runtime'da `RewardPopupController.Show()` ile aktarılır                                                    |
| **Kod referansı**             | `RewardPopupController.cs` satır ~92: `rewardIcon.sprite = reward.rewardIcon; rewardIcon.enabled = true;`                                                                                |
| **Yapılacak işlem**           | 1) Scene'de RewardIcon color'ını `(1,1,1,1)` WHITE yap. 2) Her BlacklistTierSO asset'inde `reward.rewardIcon` alanlarını doldur                                                          |
| **Ek kontrol**                | RewardIcon boyu 632×376 — büyük alan. `reward.rewardIcon` null ise `rewardIcon.enabled = false` yapılır (null-safe)                                                                      |
| **Kod değişikliği**           | **HAYIR** — kod zaten hazır                                                                                                                                                              |

#### Ödül İkonu Eşleştirme Tablosu

| #    | Ödül Türü          | Alan Adı(lar)                           | Asset Dosyası |
| ---- | ------------------ | --------------------------------------- | ------------- |
| 23.1 | Altın (Gold)       | `goldAmount > 0`                        | `23.1.png`    |
| 23.2 | Nitro              | `nitroAmount > 0`                       | `23.2.png`    |
| 23.3 | Popülerlik Reset   | `popularityReset = true`                | `23.3.png`    |
| 23.4 | Heat Reset         | `heatReset = true`                      | `23.4.png`    |
| 23.5 | Ücretsiz Sandık    | `freeChestCount > 0`                    | `23.5.png`    |
| 23.6 | Boost İndirimi     | `boostDiscountUses > 0`                 | `23.6.png`    |
| 23.7 | Kart İlerlemesi    | `cardProgressAmount > 0`                | `23.7-54.png` |
| 23.8 | Ücretsiz Kaplama   | `freeKaplamaCount > 0`                  | `23.8.png`    |
| 23.9 | Tüm Kozmetikler Aç | `unlockAllCosmeticsForOtherCars = true` | `23.9.png`    |

> **NOT:** `23.7-54.png` dosya adındaki "-54" muhtemelen başka bir listedeki eleman numarasını ifade eder (paylaşımlı dosya).

**Editor Adımları:**

- [ ] **ÖNCELİKLE:** `Main.unity` → `RewardIcon` → Image → Color → `(1, 0, 0, 1)` KIRMIZI'dan → `(1, 1, 1, 1)` WHITE'a değiştir. **Bu kritik — kırmızı tint tüm ödül ikonlarını bozar!**
- [ ] `BlacklistTier_ 1.asset` aç → her mission'ın `Reward → Reward Icon` alanını kontrol et
- [ ] Boş olanlar için: yukarıdaki tabloya göre doğru `23.x.png` sprite'ını ata
- [ ] Diğer 5 tier asset dosyası için aynısını tekrarla
- [ ] **Toplam: 6 tier × 5 mission = 30 rewardIcon alanı kontrol edilecek**

---

### MADDE 24 — CollectBtn (Ödül Toplama Butonu)

| Soru                          | Cevap                                                                                                                                             |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | RewardPopup içindeki "COLLECT" butonu. Tıklanınca ödüller verilir, popup kapanır                                                                  |
| **Nerede?**                   | `Main.unity` → `RewardPopup (1) → RewardBG → RewardImage → CollectBtn`                                                                            |
| **Scene / Prefab / Runtime?** | **SCENE OBJECT** — sahnede bulunur                                                                                                                |
| **Hangi component?**          | `CollectBtn` → `UnityEngine.UI.Button` + `UnityEngine.UI.Image`                                                                                   |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 10905}` — **Unity built-in "Knob"** ❌. `m_Color: (1, 1, 1, 1)` — beyaz. Button SpriteState: tüm state'ler null. Size: 380×90 |
| **Sprite nereye atanıyor?**   | Unity Editor'da → `CollectBtn` → Image → Sprite alanı                                                                                             |
| **Kod referansı**             | `RewardPopupController.cs`: `collectButton.onClick.AddListener(OnCollectPressed)` → ödül verir, popup kapatır                                     |
| **Yapılacak işlem**           | CollectBtn → Image → Sprite: `24.png` ata                                                                                                         |
| **Ek kontrol**                | Child text objesinin metnini "COLLECT" olarak ayarlayın                                                                                           |
| **Kod değişikliği**           | **HAYIR**                                                                                                                                         |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/24.png`                                                                                                   |

**Editor Adımları:**

- [ ] `Main.unity` → `RewardPopup (1) → RewardBG → RewardImage → CollectBtn` bul
- [ ] Image → Source Image → `24` sprite'ını ata
- [ ] Image → Color → `(1, 1, 1, 1)` bırak
- [ ] Button → Sprite State → (opsiyonel) Highlighted/Pressed sprite'ları ata
- [ ] Child text → metin "COLLECT" olarak ayarla
- [ ] Image Type → gerekirse `Sliced`

---

### MADDE 25 — Claimed Durumu

| Soru                          | Cevap                                                                                                                                                                                                                       |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Görev ödülü alındıktan sonra CompletedBtn'ın "claimed" (alındı) görünümü                                                                                                                                                    |
| **Nerede?**                   | `MissionRow.prefab` → `MissionRow → CompletedBtn` — **Madde 21 ile AYNI OBJE**                                                                                                                                              |
| **Scene / Prefab / Runtime?** | **RUNTIME STATE DEĞİŞİKLİĞİ** — ayrı bir obje DEĞİL. Aynı CompletedBtn'ın kodu tarafından görünümü değiştirilir                                                                                                             |
| **Hangi component?**          | Aynı: `CompletedBtn` → Image + Button + child Text (TMP)                                                                                                                                                                    |
| **Mevcut implementasyon**     | `MissionRowUI.SetClaimed()` metodu çağrılır: 1) `completedBtnText.text = "CLAIMED"` 2) Button `interactable = false` 3) `Image.color = new Color(0.5f, 0.5f, 0.5f, 0.7f)` — gri/soluk                                       |
| **Ayrı sprite var mı?**       | **HAYIR** — kod sadece renk koyulaştırması (color tinting) yapar. Ayrı "claimed sprite" field'ı yok                                                                                                                         |
| **Yapılacak işlem**           | Eğer claimed durumu için ayrı bir görsel isteniyorsa: `25.png`'yi kullanmak için `MissionRowUI.cs`'ye `claimedSprite` field'ı eklenmeli. **Mevcut davranıştan memnunsanız**: kod değişikliği gerekmez, sadece renk değişimi |
| **Ek kontrol**                | Claimed rengi `(0.5, 0.5, 0.5, 0.7)` — oldukça soluk/gri. Madde 21'deki sprite ile renk çarpımı sonucu nihai görünüm oluşur                                                                                                 |
| **Kod değişikliği**           | **Opsiyonel** — ayrı claimed sprite istenirse küçük kod değişikliği gerekir                                                                                                                                                 |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/25.png`                                                                                                                                                                             |

#### Opsiyonel: Claimed Sprite Ekleme (Kod Değişikliği)

Eğer `25.png`'yi claimed durumunda kullanmak isterseniz, `MissionRowUI.cs`'de şu değişiklik yapılmalı:

```csharp
// MissionRowUI.cs'e eklenecek field:
[SerializeField] private Sprite claimedSprite;

// SetClaimed() metoduna eklenecek:
public void SetClaimed()
{
    completedBtnText.text = "CLAIMED";
    var btn = completedBtn.GetComponent<Button>();
    if (btn != null) btn.interactable = false;

    var img = completedBtn.GetComponent<Image>();
    if (img != null)
    {
        if (claimedSprite != null)
            img.sprite = claimedSprite;           // ← YENİ
        img.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
    }
}
```

Sonra prefab'da: `MissionRowUI` → `Claimed Sprite` → `25.png` atanır.

**Editor Adımları (Mevcut Davranış İçin):**

- [ ] Madde 21'deki CompletedBtn sprite'ını doğru atadıysanız, claimed durumu otomatik çalışır (renk koyulaşması)
- [ ] Test: Oyunda bir görevi tamamla → CLAIM'e tıkla → butonun grileşip "CLAIMED" yazdığını doğrula

**Editor Adımları (Ayrı Sprite İsteniyorsa):**

- [ ] `MissionRowUI.cs`'de `claimedSprite` field'ı ekle (yukarıdaki kod)
- [ ] `MissionRow.prefab` → MissionRowUI → `Claimed Sprite` → `25` sprite'ını ata
- [ ] Kaydet ve test et

---

### MADDE 26 — TakeTheCarButton (Arabayı Al Butonu)

| Soru                          | Cevap                                                                                                                                                                                                                                   |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Nedir?**                    | Bir tier'ın tüm görevleri tamamlandığında aktifleşen ana CTA butonu. Tıklanınca ödül arabası verilir                                                                                                                                    |
| **Nerede?**                   | `Main.unity` → `Panel_BlackList → ScrollView_Blacklist → Viewport → Content → Others (1) → TakeTheCarButton`                                                                                                                            |
| **Scene / Prefab / Runtime?** | **SCENE OBJECT** — sahnede bulunur. CanvasGroup ile görünürlük/etkileşim kontrol edilir                                                                                                                                                 |
| **Hangi component?**          | `TakeTheCarButton` → `UnityEngine.UI.Button` + `UnityEngine.UI.Image` + `CanvasGroup`                                                                                                                                                   |
| **Mevcut sprite durumu**      | `m_Sprite: {fileID: 10905}` — **Unity built-in "Knob"** ❌. `m_Color: (0, 1, 0.027, 1)` — parlak yeşil. Button SpriteState: tüm state'ler null. Size: 300×117.1                                                                         |
| **Sprite nereye atanıyor?**   | Unity Editor'da → `TakeTheCarButton` → Image → Sprite alanı                                                                                                                                                                             |
| **Kod referansı**             | `BlacklistPanelController.cs`: `RefreshTakeTheCarButton()` → tüm görevler tamamlandıysa CanvasGroup.alpha = 1, interactable = true; değilse alpha = 0.4, interactable = false. `takeTheCarImage` referansı bu objenin Image bileşenidir |
| **Yapılacak işlem**           | TakeTheCarButton → Image → Sprite: `26.png`. Color: white                                                                                                                                                                               |
| **Ek kontrol**                | CanvasGroup alpha değeri kod tarafından değişir (0.4 disabled / 1.0 enabled) — sprite doğru atandığında solma efekti otomatik çalışır                                                                                                   |
| **Kod değişikliği**           | **HAYIR**                                                                                                                                                                                                                               |
| **Asset dosyası**             | `Assets/UI/UIListAssets/black list ui 2/26.png`                                                                                                                                                                                         |

**Editor Adımları:**

- [ ] `Main.unity` → Hierarchy'de `Panel_BlackList → ScrollView_Blacklist → Viewport → Content → Others (1) → TakeTheCarButton` bul
- [ ] Image → Source Image → `26` sprite'ını ata
- [ ] Image → Color → `(1, 1, 1, 1)` white yap (şu an yeşil!)
- [ ] Image → Image Type → `Sliced` (buton 9-slice)
- [ ] Button → Sprite State → (opsiyonel) Highlighted/Pressed/Disabled sprite'ları ata
- [ ] Child text objesinin metnini "TAKE THE CAR" olarak ayarlayın (mevcut text'i kontrol edin)

---

## TOPLU CHECKBOX LİSTESİ (Sıralı Uygulama Planı)

### Faz 1 — Prefab Düzenlemeleri (MissionRow.prefab)

> Prefab'ı bir kez düzenlersiniz, tüm runtime klonlar otomatik güncellenir.

- [ ] **`MissionRow.prefab` aç**
  - [ ] Root `MissionRow` → Image → Sprite: `15.png`, Color: white (1,1,1,1), Type: Sliced
  - [ ] `BarBGComplete` → Image → Sprite: `19.png`
  - [ ] `CompletedBtn` → Image → Sprite: `21.png`, Color: white
  - [ ] `CompletedBtn → Text (TMP)` → text: "CLAIM"
  - [ ] (Opsiyonel) `BarBG` → Image → Sprite: `17.png` (mevcut sprite varsa karşılaştır)
  - [ ] (Opsiyonel) `BarFill` → Image → Sprite: `18,20.png`, Type: Filled
  - [ ] (Opsiyonel) `BarFillFull` → Image → Sprite: `18,20.png`, Type: Filled
  - [ ] Prefab kaydet (Ctrl+S)

### Faz 2 — Scene Düzenlemeleri (Main.unity)

- [ ] **Missions Container**
  - [ ] `Others (1) → Missions` → Image → Sprite: `14.png`, Type: Sliced
  - [ ] Missions → VerticalLayoutGroup → Spacing: 10-15
- [ ] **TakeTheCarButton**
  - [ ] `Others (1) → TakeTheCarButton` → Image → Sprite: `26.png`, Color: white, Type: Sliced
- [ ] **RewardPopup**
  - [ ] `RewardPopup (1) → RewardBG → RewardImage` → Image → Sprite: `22.png`, Type: Sliced
  - [ ] `RewardImage → RewardIcon` → Image → Color: `(1,1,1,1)` WHITE (**KIRMIZIDAN BEYAZA!**)
  - [ ] `RewardImage → CollectBtn` → Image → Sprite: `24.png`, Type: Sliced
- [ ] Scene kaydet (Ctrl+S)

### Faz 3 — ScriptableObject Doldurma (6 Tier × 5 Mission)

- [ ] **`BlacklistTier_ 1.asset`** (Tier 5)
  - [ ] `missions[0].icon` → missionType'a göre doğru `16.x.png`
  - [ ] `missions[0].reward.rewardIcon` → ödül tipine göre doğru `23.x.png`
  - [ ] (missions[1]..missions[4] için tekrarla)
- [ ] **`BlacklistTier_ 2.asset`** — aynı kontrol (5 mission × 2 alan = 10 kontrol)
- [ ] **`BlacklistTier_ 3.asset`** — aynı
- [ ] **`BlacklistTier_ 4.asset`** — aynı
- [ ] **`BlacklistTier_ 5.asset`** — aynı
- [ ] **`BlacklistTier_.asset`** — aynı

> **Toplam SO kontrol: 6 × 5 × 2 = 60 sprite alanı**

### Faz 4 — Opsiyonel İyileştirmeler

- [ ] Claimed sprite istiyorsanız: `MissionRowUI.cs`'ye `claimedSprite` field'ı ekle, prefab'da `25.png` ata
- [ ] `11.png` dosyası: Bu **Blacklist tab butonu** (BottomBar)'dur — gerekirse ayrıca atanır (Madde 11)
- [ ] `black list bottom arkası blurlu bg.png`: Ana Panel_BlackList overlay'i veya ek arka plan olarak kullanılabilir

---

## DOSYA ENVANTERİ — `black list ui 2/` Klasörü

| Dosya Adı                                | Madde # | Hedef                                          | Durum                                        |
| ---------------------------------------- | :-----: | ---------------------------------------------- | -------------------------------------------- |
| `11.png`                                 |   11    | BottomBar → Blacklist tab ikonu                | Madde 14-26 kapsamı dışı                     |
| `14.png`                                 |   14    | Missions container Image (scene)               | ❌ Atanmamış                                 |
| `15.png`                                 |   15    | MissionRow root Image (prefab)                 | ❌ Atanmamış                                 |
| `16.1.png` – `16.13.png`                 |   16    | BlacklistTierSO → missions[].icon              | ⚠️ SO'larda kontrol gerekli                  |
| `17.png`                                 |   17    | MissionRow → BarBG Image (prefab)              | ✅ Mevcut sprite var (değiştirmek opsiyonel) |
| `18,20.png`                              | 18, 20  | BarFill + BarFillFull (prefab)                 | ✅ Mevcut sprite'lar var (paylaşımlı dosya)  |
| `19.png`                                 |   19    | MissionRow → BarBGComplete (prefab)            | ❌ Atanmamış                                 |
| `21.png`                                 |   21    | MissionRow → CompletedBtn (prefab)             | ❌ Unity Knob placeholder                    |
| `22.png`                                 |   22    | RewardImage popup çerçevesi (scene)            | ❌ Atanmamış                                 |
| `23.1.png` – `23.9.png`                  |   23    | BlacklistTierSO → missions[].reward.rewardIcon | ⚠️ SO'larda kontrol gerekli                  |
| `24.png`                                 |   24    | CollectBtn (scene)                             | ❌ Unity Knob placeholder                    |
| `25.png`                                 |   25    | Claimed durumu (opsiyonel sprite)              | ⏸️ Kod değişikliği gerekir                   |
| `26.png`                                 |   26    | TakeTheCarButton (scene)                       | ❌ Unity Knob placeholder                    |
| `black list bottom arkası blurlu bg.png` |    —    | Panel_BlackList overlay/BG                     | ⏸️ Kullanım kararı bekliyor                  |

---

## ÖZEt

| Kategori                             |                                Sayı                                |
| ------------------------------------ | :----------------------------------------------------------------: |
| Toplam madde (14-26)                 |                                 13                                 |
| Prefab düzenlemesi gerektiren        |                   7 (15, 17, 18, 19, 20, 21, 25)                   |
| Scene düzenlemesi gerektiren         |                         4 (14, 22, 24, 26)                         |
| ScriptableObject doldurma gerektiren |                      2 (16, 23) — ama 60 alan                      |
| Kod değişikliği gerektiren           |                          0 (25 opsiyonel)                          |
| Kritik renk düzeltmesi gereken       | 3 (MissionRow kırmızı, TakeTheCarButton yeşil, RewardIcon kırmızı) |
| Zaten sprite atanmış                 |              3 (17-BarBG, 18-BarFill, 20-BarFillFull)              |
