# Chest System v2 — Unity Inspector Setup Guide

All code is complete and compiles with zero errors. Follow these steps to wire everything up in the Unity Editor.

---

## 1. Chest Prefabs (3 variants)

You need **3 chest prefab variants** for Common / Rare / Legendary. You can duplicate the existing chest prefab and give each a different material or color:

| Prefab                 | Suggested Color    |
| ---------------------- | ------------------ |
| `CommonChestPrefab`    | Brown / Bronze     |
| `RareChestPrefab`      | Blue / Silver      |
| `LegendaryChestPrefab` | Gold / Purple glow |

Each prefab must have:

- A `Chest` component (the `chestType` field will be set at runtime by `ChestSpawner`)
- A `Collider` on the mesh or root
- Lid bone hierarchy matching the existing chest (same `Cube.004` or `lidSearchPath`)
- A `CardMouthAnchor` child transform

---

## 2. ChestSpawner (Main Scene)

On the existing **ChestSpawner** GameObject:

| Field                  | Value                         |
| ---------------------- | ----------------------------- |
| `commonChestPrefab`    | → your Common chest prefab    |
| `rareChestPrefab`      | → your Rare chest prefab      |
| `legendaryChestPrefab` | → your Legendary chest prefab |

The old `chestPrefab` field still exists as a fallback. If a type-specific prefab is null, it falls back to the generic `chestPrefab`.

---

## 3. ChestShownUI (Main Scene HUD)

The old single-icon + count badge system is replaced by a **slot-based UI**.

On the **ChestShownUI** GameObject:

| Field           | Value                                                                                      |
| --------------- | ------------------------------------------------------------------------------------------ |
| `slotContainer` | → A `Transform` parent where slot items are instantiated (e.g., a `HorizontalLayoutGroup`) |
| `slotPrefab`    | → A prefab with a `ChestSlotUI` component (see below)                                      |

### ChestSlotUI Prefab Setup

Create a UI prefab with:

1. Root: `Button` component (for tap handling)
2. Child: `Image` (icon showing chest type)
3. Child: `TextMeshProUGUI` named for status text
4. Attach `ChestSlotUI` script
5. Assign:
   - `statusText` → the TMP child
   - `button` → the Button component

---

## 4. ChestPopupController (Main Scene)

On the existing **ChestPopupController** GameObject:

| Field             | Value                                                                                   |
| ----------------- | --------------------------------------------------------------------------------------- |
| `halfTimeObj`     | → The UI button/object for "Watch Ad to halve time" (replaces old `skip20Obj`)          |
| `openNowCostText` | → A `TextMeshProUGUI` that shows the nitro cost (auto-filled per chest type: 15/50/100) |

The old `skip20Obj` is removed. Everything else (unlock button, open button, timer text, etc.) is the same.

---

## 5. ChestOpenSceneController (ChestOpenScene)

On the existing **ChestOpenSceneController** GameObject:

| Field                  | Value                    |
| ---------------------- | ------------------------ |
| `commonChestPrefab`    | → Common chest prefab    |
| `rareChestPrefab`      | → Rare chest prefab      |
| `legendaryChestPrefab` | → Legendary chest prefab |

The old `chestPrefab` field is still used as fallback. The old `chestGoldPercentMin/Max` and `chestNitroPercentMin/Max` fields are still present in the Inspector but **no longer used** — reward scaling is now driven by `ChestTypeConfig` per chest type.

---

## 6. ChestRewardRevealController (ChestOpenScene)

On the existing **ChestRewardRevealController** GameObject:

| Field             | Value                                                                      |
| ----------------- | -------------------------------------------------------------------------- |
| `stickerSprite`   | → A `Sprite` icon representing a sticker reward (used in reveal & summary) |
| `summaryCard4`    | → A 4th `SpriteRenderer` in your summary layout (for sticker)              |
| `summaryOverlay4` | → A 4th `TextMeshPro` overlay for the sticker summary slot                 |

**If you don't wire `summaryCard4`/`summaryOverlay4`**, 3-reward chests work perfectly and 4-reward chests simply skip the sticker summary slot (the sticker is still granted).

To add the 4th summary slot:

1. Duplicate one of the existing `summaryCard1/2/3` objects in the hierarchy
2. Reposition it to the right of card 3
3. Assign the SpriteRenderer to `summaryCard4` and the TMP to `summaryOverlay4`

---

## 7. GarageDatabaseSO (Resources)

The sticker system requires a `GarageDatabaseSO` asset loadable from `Resources`:

```
Assets/Resources/GarageDatabase.asset   (type: GarageDatabaseSO)
```

`StickerRewardHelper` calls `Resources.Load<GarageDatabaseSO>("GarageDatabase")`. Make sure this asset exists and contains your car entries with sticker data.

---

## 8. No Ad SDK Yet

`AdProvider.cs` is a placeholder that **immediately calls onRewarded**. When you integrate a real ad SDK (AdMob, Unity Ads, IronSource):

1. Replace the body of `AdProvider.ShowRewardedAd()` with your SDK's rewarded ad flow
2. Call `onRewarded` when the ad completes successfully
3. Call `onFailed` if the ad fails or is dismissed early

---

## 9. PlayerPrefs Migration

The save format has changed:

- `ChestData` no longer has: `minReward`, `maxReward`, `cardReward`, `turboMin`, `turboMax`, `remainingTime`, `skipUsed`, `activeIndex`
- `ChestData` now has: `chestType`, `unlockEndUtcTicks`, `halfTimeUsed`
- `ChestOpeningSession` version bumped to 2 (adds sticker fields)

**For existing players**: Old saves will deserialize with default values (`chestType = Common`, `unlockEndUtcTicks = 0`). The system handles this gracefully — any unlocking chest will appear as "ready" since its UTC time is in the past.

---

## 10. Tuning Reference

All tuning constants are in `ChestTypeDefs.cs` → `ChestTypeConfig`:

| Parameter             | Common     | Rare        | Legendary  |
| --------------------- | ---------- | ----------- | ---------- |
| Unlock Duration       | 20 min     | 40 min      | 60 min     |
| Open Now Cost (Nitro) | 15         | 50          | 100        |
| Money % Range         | 5-15%      | 10-25%      | 20-40%     |
| Nitro % Range         | 5-20%      | 10-30%      | 20-40%     |
| Card Rarity Weights   | 50/30/15/0 | 30/35/25/10 | 0/20/50/30 |
| Sticker Eligible      | No         | Yes         | Yes        |
| Max Inventory Slots   | 5          | 5           | 5          |

Spawn rate progression (controlled by player money):

- Base weights: Common 75 / Rare 20 / Legendary 5
- At $1M milestone: roughly Common 55 / Rare 33 / Legendary 12
- Common never drops below 25

---

## Files Changed Summary

### New Files (4)

- `Assets/Scripts/ChestTypeDefs.cs` — ChestType enum, ChestState enum, ChestTypeConfig
- `Assets/Scripts/AdProvider.cs` — Placeholder rewarded ad provider
- `Assets/Scripts/StickerRewardHelper.cs` — Pick & grant random unowned stickers
- `Assets/Scripts/ChestSlotUI.cs` — Per-slot UI component for chest inventory

### Rewritten Files (6)

- `Assets/Scripts/Chest.cs` — Simplified, uses ChestType
- `Assets/Scripts/ChestOpeningSession.cs` — Added sticker fields, version 2
- `Assets/Scripts/ChestInventoryManager.cs` — 5-slot system, UTC timers, half-time, per-type costs
- `Assets/Scripts/ChestSpawner.cs` — 3-prefab weighted spawn, inventory full guard
- `Assets/Scripts/ChestShownUI.cs` — Slot-based UI with per-slot timers
- `Assets/Scripts/ChestPopupController.cs` — Per-chest-index popup, half-time ad, per-type costs

### Edited Files (4)

- `Assets/Scripts/ChestSessionManager.cs` — Sticker commit + grant in reward flow
- `Assets/Scripts/ChestOpenSceneController.cs` — 3 prefabs, idle motion, per-type rewards, Reveal_Sticker phase, sticker computation
- `Assets/Scripts/ChestRewardRevealController.cs` — Sticker fields in ChestRewardPackage, ShowStickerInfo(), 4th summary slot
- `Assets/Scripts/Blacklist/FreeChestRewardHandler.cs` — Updated ChestData construction
