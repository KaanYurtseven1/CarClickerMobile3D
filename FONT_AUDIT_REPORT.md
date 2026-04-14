# Font Audit Report — CarClickerMobile3D

## Summary

| Metric                                  | Count                              |
| --------------------------------------- | ---------------------------------- |
| **Total text elements scanned**         | 315                                |
| **Using BigStem fonts (correct)**       | 218                                |
| **Using non-BigStem fonts (needs fix)** | 97                                 |
| **Offending font**                      | `LiberationSans SDF` (TMP default) |

**Approved fonts:**

- `Big Stem Oblique SDF` (GUID: `07fe05e9d49688640a74cdeab2ac57c6`)
- `BigStem-Regular SDF` (GUID: `2a4e0a7b22cc78349b1449da0667c945`)

**Scanned assets:** 6 scenes + 26 prefabs (excluding 3rd-party Houidisoft/Modular_Track_Free)

---

## Non-BigStem Elements by File

### 1. Main.unity — 37 items

All use `LiberationSans SDF`. Component type: `TextMeshProUGUI`.

| #   | Hierarchy Path                                                            | Text Content             | Active | Recommended Font     |
| --- | ------------------------------------------------------------------------- | ------------------------ | ------ | -------------------- |
| 1   | `TopBar/Text_MoneyBig`                                                    | 'Money: $0'              | ✅     | Big Stem Oblique SDF |
| 2   | `TopBar/Text_MPSLine`                                                     | (empty)                  | ✅     | Big Stem Oblique SDF |
| 3   | `TopBar/Premium/TextPremium`                                              | '0'                      | ✅     | Big Stem Oblique SDF |
| 4   | `TopBar/PopularityBar/Text_Popularity`                                    | 'Popularity'             | ✅     | Big Stem Oblique SDF |
| 5   | `ChestPopup/PopupRoot/TitleText`                                          | 'Chest'                  | ✅     | Big Stem Oblique SDF |
| 6   | `ChestPopup/PopupRoot/TimerTextObj/TimerText`                             | 'asdsadsadsadsa'         | ✅     | BigStem-Regular SDF  |
| 7   | `ChestPopup/PopupRoot/StartUnlockButton/Text (TMP)`                       | 'Start unlock'           | ✅     | Big Stem Oblique SDF |
| 8   | `ChestPopup/PopupRoot/OpenButton/Text (TMP)`                              | 'Open'                   | ✅     | Big Stem Oblique SDF |
| 9   | `ChestPopup/PopupRoot/HalfTimeButton/Text (TMP)`                          | 'Half Time'              | ✅     | Big Stem Oblique SDF |
| 10  | `ChestPopup/PopupRoot/OpenNowButton/OpenNowCostText`                      | '100'                    | ✅     | Big Stem Oblique SDF |
| 11  | `ChestPopup/PopupRoot/getYourReward`                                      | 'Get your reward'        | ❌     | Big Stem Oblique SDF |
| 12  | `ChestPopup/PopupRoot/OpenGetRewardText`                                  | 'Open & get your reward' | ✅     | Big Stem Oblique SDF |
| 13  | `Panel_BlackList/Header/BlacklistTitle (1)`                               | 'BLACKLIST #6'           | ✅     | Big Stem Oblique SDF |
| 14  | `Panel_BlackList/.../Others (1)/CarImage/CarName`                         | 'Car Name'               | ✅     | Big Stem Oblique SDF |
| 15  | `Panel_BlackList/.../Others (1)/CarImage/Text (TMP)`                      | 'Unlock After Missions'  | ✅     | BigStem-Regular SDF  |
| 16  | `Panel_BlackList/.../Others (1)/TakeTheCarButton/Text (TMP)`              | 'Take The Car'           | ✅     | Big Stem Oblique SDF |
| 17  | `Panel_BlackList/.../RewardPopup (1)/.../RewardText`                      | '+10,000'                | ✅     | Big Stem Oblique SDF |
| 18  | `Panel_BlackList/.../RewardPopup (1)/.../Title`                           | 'REWARD!'                | ✅     | Big Stem Oblique SDF |
| 19  | `Panel_BlackList/.../RewardPopup (1)/.../YouReceivedText`                 | 'YOU RECEIVED:'          | ✅     | Big Stem Oblique SDF |
| 20  | `Panel_BlackList/.../RewardPopup (1)/.../CollectBtn/Text (TMP)`           | 'COLLECT'                | ✅     | Big Stem Oblique SDF |
| 21  | `Panel_Bank/.../Section_NitroCoins/.../NitroSlot_1/Title`                 | 'Nitro Coin Rush'        | ✅     | Big Stem Oblique SDF |
| 22  | `Panel_Bank/.../Section_NitroCoins/.../NitroSlot_1/Button/Text (TMP)`     | 'Button'                 | ✅     | Big Stem Oblique SDF |
| 23  | `Panel_Bank/.../Section_NitroCoins/.../NitroSlot_1 (1)/Title`             | 'Nitro Coin Surge'       | ✅     | Big Stem Oblique SDF |
| 24  | `Panel_Bank/.../Section_NitroCoins/.../NitroSlot_1 (1)/Button/Text (TMP)` | 'Button'                 | ✅     | Big Stem Oblique SDF |
| 25  | `Panel_Bank/.../Section_NitroCoins/.../NitroSlot_1 (2)/Title`             | 'Nitro Coin Overdrive'   | ✅     | Big Stem Oblique SDF |
| 26  | `Panel_Bank/.../Section_NitroCoins/.../NitroSlot_1 (2)/Button/Text (TMP)` | 'Button'                 | ✅     | Big Stem Oblique SDF |
| 27  | `Panel_Bank/.../Section_CardPacks/.../BuyBtn_1/Button/PriceText`          | '$1.99'                  | ✅     | Big Stem Oblique SDF |
| 28  | `Panel_Bank/.../Section_CardPacks/.../BuyBtn_1 (1)/Button/PriceText`      | '$4.99'                  | ✅     | Big Stem Oblique SDF |
| 29  | `Panel_Bank/.../Section_CardPacks/.../BuyBtn_1 (2)/Button/PriceText`      | '$5.99'                  | ✅     | Big Stem Oblique SDF |
| 30  | `Panel_Bank/.../Section_DailyOffers/.../OfferSlot_2/PurchasedText`        | 'Purchased!'             | ❌     | Big Stem Oblique SDF |
| 31  | `Panel_Bank/.../Section_DailyOffers/.../OfferSlot_3/PurchasedText`        | 'Purchased!'             | ❌     | Big Stem Oblique SDF |
| 32  | `Panel_Bank/.../Section_DailyOffers/.../OfferSlot_3/Bar_BG/Text_Progress` | '1/2'                    | ✅     | BigStem-Regular SDF  |
| 33  | `Panel_ShopCards/Header/Btn_TabShopItems/Text (TMP)`                      | 'Shop Items'             | ✅     | Big Stem Oblique SDF |
| 34  | `Panel_ShopCards/Header/Btn_TabCards/Text (TMP)`                          | 'Cards'                  | ✅     | Big Stem Oblique SDF |
| 35  | `PoliceCatchUI/Text_Wasted`                                               | 'Wasted: 0/3'            | ✅     | Big Stem Oblique SDF |
| 36  | `PoliceCatchUI/Text_Prompt`                                               | '3x'                     | ✅     | Big Stem Oblique SDF |
| 37  | `RadarPopup/Background/Text_RadarCaught`                                  | 'WANTED'                 | ✅     | Big Stem Oblique SDF |

> All paths above are under `UI/UI/Canvas/`.

---

### 2. NewGarage.unity — 13 items

All use `LiberationSans SDF`. Component type: `TextMeshProUGUI`.

| #   | Hierarchy Path                                             | Text Content                             | Active | Recommended Font     |
| --- | ---------------------------------------------------------- | ---------------------------------------- | ------ | -------------------- |
| 1   | `Canvas/ExitPopupPanel/Image/Text (TMP)`                   | 'Garage'den çıkmaya emin misiniz?'       | ✅     | BigStem-Regular SDF  |
| 2   | `Canvas/ExitPopupPanel/Image/Btn_No/Text (TMP)`            | 'No'                                     | ✅     | Big Stem Oblique SDF |
| 3   | `Canvas/ExitPopupPanel/Image/Btn_Yes/Text (TMP)`           | 'Yes'                                    | ✅     | Big Stem Oblique SDF |
| 4   | `Canvas/BuyPopupPanel/Image/Title`                         | 'Satın almak istediğinize emin misiniz?' | ✅     | BigStem-Regular SDF  |
| 5   | `Canvas/BuyPopupPanel/Image/Btn_Yes/Text (TMP)`            | 'Yes'                                    | ✅     | Big Stem Oblique SDF |
| 6   | `Canvas/BuyPopupPanel/Image/Btn_Incele/Text (TMP)`         | 'İncele'                                 | ✅     | Big Stem Oblique SDF |
| 7   | `Canvas/BuyPopupPanel/Image/CloseButton/Text (TMP)`        | 'X'                                      | ✅     | Big Stem Oblique SDF |
| 8   | `Canvas/BuyPopupPanel/Image/FiyatPart/GoldPart/GoldText`   | (empty)                                  | ✅     | Big Stem Oblique SDF |
| 9   | `Canvas/BuyPopupPanel/Image/FiyatPart/NitroPart/NitroText` | (empty)                                  | ✅     | Big Stem Oblique SDF |
| 10  | `Canvas/GoldPart/GoldText`                                 | '1000000000'                             | ✅     | Big Stem Oblique SDF |
| 11  | `Canvas/NitroPart/NitroText`                               | '10'                                     | ✅     | Big Stem Oblique SDF |
| 12  | `Canvas/LockedUI/LockedUI/LockedText`                      | 'LOCKED'                                 | ✅     | Big Stem Oblique SDF |
| 13  | `Canvas/LockedUI/LockedUI/BlacklistText`                   | 'Blacklist#6'                            | ✅     | Big Stem Oblique SDF |

---

### 3. TakeTheCarScene.unity — 3 items

All use `LiberationSans SDF`. Component type: `TextMeshProUGUI`.

| #   | Hierarchy Path                                       | Text Content  | Active | Recommended Font     |
| --- | ---------------------------------------------------- | ------------- | ------ | -------------------- |
| 1   | `Canvas_Cinematic/CarNameRevealGroup/CarNameLabel`   | (empty)       | ✅     | Big Stem Oblique SDF |
| 2   | `Canvas_Cinematic/CarNameRevealGroup/ModelNameLabel` | (empty)       | ✅     | BigStem-Regular SDF  |
| 3   | `Canvas_Cinematic/SkipPanel/SkipButton/SkipHint`     | 'Tap to skip' | ✅     | BigStem-Regular SDF  |

---

### 4. Prefabs — 6 items

#### MissionRow.prefab (4 items)

Component type: `TextMeshProUGUI`. Font: `LiberationSans SDF`.

| #   | Hierarchy Path                                           | Text Content         | Active | Recommended Font     |
| --- | -------------------------------------------------------- | -------------------- | ------ | -------------------- |
| 1   | `MissionRow/CompletedBtn/Text (TMP)`                     | 'Button'             | ✅     | Big Stem Oblique SDF |
| 2   | `MissionRow/ProgressText`                                | '43K/50K'            | ✅     | BigStem-Regular SDF  |
| 3   | `MissionRow/MissionDesc`                                 | 'asddasdsadassadsad' | ✅     | BigStem-Regular SDF  |
| 4   | `MissionRow/BarBGComplete/BarFillFull/BarBGCompleteText` | '50,000/50,000'      | ✅     | BigStem-Regular SDF  |

#### FloatingText.prefab (1 item)

Component type: `TextMeshProUGUI`. Font: `LiberationSans SDF`.

| #   | Hierarchy Path        | Text Content | Active | Recommended Font     |
| --- | --------------------- | ------------ | ------ | -------------------- |
| 1   | `FloatingText` (root) | 'New Text'   | ✅     | Big Stem Oblique SDF |

#### WorldCardPrefab_TMP.prefab (1 item)

Component type: `TextMeshPro` (3D). Font: `LiberationSans SDF`.

| #   | Hierarchy Path                   | Text Content | Active | Recommended Font     |
| --- | -------------------------------- | ------------ | ------ | -------------------- |
| 1   | `WorldCardPrefab_TMP/OverlayTMP` | '3x'         | ✅     | Big Stem Oblique SDF |

---

### 5. \_Recovery/0.unity — 38 items

This is a recovery backup of Main.unity with nearly identical non-BigStem elements (same 37 items as Main.unity + 1 extra: `ChestPopup/PopupRoot/CloseButton/Text (TMP)` = 'X').

> **Recommendation:** Fix Main.unity first. If this recovery scene is still needed, apply the same changes. Otherwise it can be ignored.

---

### 6. Scenes & Prefabs with No Issues

| Asset                                           | Status                                                         |
| ----------------------------------------------- | -------------------------------------------------------------- |
| ChestOpenScene.unity                            | 0 text elements (Canvas disabled; texts live in reveal prefab) |
| TestScene.unity                                 | 0 text elements                                                |
| All chest prefabs (Chest_Common/Rare/Legendary) | No text components                                             |
| CardSlot.prefab                                 | No inline font reference (likely runtime-assigned)             |
| ChestSlotPrefab.prefab                          | No inline font reference (likely runtime-assigned)             |
| SummarySlotPrefab.prefab                        | No inline font reference (likely runtime-assigned)             |
| WorldRewardCardPrefab_TMP.prefab                | No inline font reference (likely runtime-assigned)             |
| VFX prefabs                                     | No text components                                             |

---

## Font Recommendation Guide

| Use Case                                                  | Recommended Font         |
| --------------------------------------------------------- | ------------------------ |
| **Headlines, UI labels, buttons, big numbers**            | **Big Stem Oblique SDF** |
| **Body text, descriptions, progress counters, long text** | **BigStem-Regular SDF**  |

The "Recommended Font" column in the tables above follows this rule:

- Titles, button labels, short labels → **Big Stem Oblique SDF**
- Descriptive text, progress values, multi-line text → **BigStem-Regular SDF**

---

## How to Fix

### Option A — Manual (Unity Inspector)

1. Open each scene/prefab in Unity
2. Select each listed object
3. In the Inspector, change **Font Asset** from `LiberationSans SDF` to the recommended SDF asset
4. Save the scene/prefab

### Option B — Bulk YAML replacement (faster)

Replace the font GUID in the `.unity` / `.prefab` files directly:

```
Find:    m_fontAsset: {fileID: 11400000, guid: 8f586378b4e144a9851e7b34d9b748ee
Replace: m_fontAsset: {fileID: 11400000, guid: 07fe05e9d49688640a74cdeab2ac57c6
```

This replaces **all** LiberationSans SDF → Big Stem Oblique SDF in one pass. For elements where BigStem-Regular SDF is recommended, do a targeted replace with GUID `2a4e0a7b22cc78349b1449da0667c945` instead.

> ⚠️ **Important:** Close Unity before editing `.unity`/`.prefab` files in a text editor, then reopen to avoid corruption.

---

## Prefabs with Runtime-Assigned Fonts

The following prefabs contain `TextMeshProUGUI` components but have **no `m_fontAsset` serialized in the file**. Their font is either set at runtime via script, or they inherit from a TMP default. Verify these manually in Unity:

- `CardSlot.prefab`
- `ChestSlotPrefab.prefab`
- `SummarySlotPrefab.prefab`
- `WorldRewardCardPrefab_TMP.prefab`

Check `TMP Settings` asset → **Default Font Asset** to ensure it's set to a BigStem font, since these components may use that default.
