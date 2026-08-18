# Audio System Implementation Report

## 1. What Was Implemented

### New Files Created

| File | Purpose |
|------|---------|
| `Assets/Scripts/MusicManager.cs` | Persistent singleton — dual-AudioSource crossfade, scene-based music selection, DuckMusic/RestoreMusic API, PlayerPrefs volume |
| `Assets/Scripts/BoostAudioController.cs` | Event subscriber — listens to BoostModeController events, plays boost SFX + loops, ducks music during boost |

### SFXManager.cs — Fully Rewritten

Expanded from 96 → ~510 lines. Now contains **~65 AudioClip fields** organized by category, **~50 public Play methods**, and built-in:
- Auto-created `loopSFXSource` for rain/boost loops (with DOFade transitions)
- Rate-limiting for coins (0.05s cooldown, max 3 voices), momentum (every 5th stack + 0.12s cooldown), building buy (0.15s cooldown)
- Pitch variation for car tap (±5%), chest hop (0.95–1.10), coin collect (0.90–1.10), momentum tick (0.9–1.3 scaled by stack progress)
- PlayerPrefs-based volume persistence ("SFXVolume" key)

### Audio Hooks Added (by category)

#### Chest Open Scene (C1–C10) — ChestOpenSceneController.cs
| ID | Trigger Point | SFXManager Method |
|----|---------------|-------------------|
| C1 | `PlayIntro()` | `PlayChestDrop()` |
| C2 | `PlayTapFeedback()` | `PlayChestHop()` |
| C3 | `OpenLid()` | `PlayChestLidOpen()` |
| C4 | `OnLidOpened()` | `PlayRewardMoney()` |
| C5 | `OnChestTapped()` → Reveal_Nitro | `PlayRewardNitro()` |
| C6 | `OnChestTapped()` → Reveal_Card | `PlayRewardCard()` |
| C7 | `OnChestTapped()` → Reveal_Sticker | `PlayRewardSticker()` |
| C8 | Both summary paths | `PlayChestSummary()` |
| C9 | Summary → Exit tap | `PlayChestExit()` |
| C10 | `SpawnWorldCard()` | `PlayWorldCardSwoosh()` |

#### Nitro Systems (N1–N11) — NitroCoin.cs, NitroRainController.cs, NitroMagnetController.cs
| ID | Trigger Point | SFXManager Method |
|----|---------------|-------------------|
| N1 | NitroCoin `OnTapped()` | `PlayNitroCoinCollect()` |
| N2 | NitroCoin `OnMagnetCollected()` | `PlayNitroCoinMagnet()` |
| N4 | BoostAudioController `OnNitroDeposit()` | `PlayNitroDeposit()` |
| N5 | NitroRainController `StartDelay()` | `PlayNitroRainDelay()` |
| N6 | NitroRainController `StartRain()` | `PlayNitroRainStart()` |
| N7 | NitroRainController `StartRain()` | `StartRainLoop()` |
| N8 | NitroRainController `EndRain()` | `PlayNitroRainEnd()` + `StopRainLoop()` |
| N9 | NitroMagnetController `ArmMagnet()` | `PlayMagnetActivate()` |
| N10 | NitroCoin `TransitionToPull()` | `PlayMagnetPull()` |
| N11 | NitroMagnetController `DisarmMagnet()` | `PlayMagnetDeactivate()` |

#### Boost Mode (B1–B5, N4) — BoostAudioController.cs
| ID | Trigger Event | SFXManager Method |
|----|---------------|-------------------|
| B1 | `OnBoostReady` | `PlayBoostReady()` |
| B2 | `OnBoostStarted` | `PlayBoostActivate()` |
| B3 | `OnBoostStarted` | `StartBoostLoop()` |
| B4 | `OnBoostEnded` | `PlayBoostEnd()` + `StopBoostLoop()` |
| B5 | `OnStateChanged` → Charging | `PlayBoostCooldownComplete()` |
| — | Also: `DuckMusic()` on start, `RestoreMusic()` on end | MusicManager |

#### Police / Radar / Popularity (P6–P11) — PoliceChaseFeedbackController.cs, Radar.cs, RadarPopupController.cs, PopularityManager.cs
| ID | Trigger Point | SFXManager Method |
|----|---------------|-------------------|
| P6 | `HandleChaseEnded()` + `WasLastChaseSuccess == true` | `PlayChaseSuccess()` |
| P7 | `HandleChaseEnded()` + `WasLastChaseSuccess == false` | `PlayChaseFail()` |
| P8 | Radar `OnTapped()` | `PlayRadarDefuse()` |
| P9 | Radar `OnMissed()` | `PlayRadarMiss()` |
| P10 | RadarPopupController `ShowSnapshot()` | `PlayRadarPopup()` |
| P11 | PopularityManager `AddPopularityNormalized()` on stage boundary | `PlayPopularityStageUp()` |

#### Systems / Card Effects (F1–F5, T1–T4)
| ID | File | Trigger Point | SFXManager Method |
|----|------|---------------|-------------------|
| F1 | TurboFingerController.cs | `Activate()` | `PlayTurboFingerActivate()` |
| F2 | TurboFingerController.cs | `TransitionToCooldown()` | `PlayTurboFingerDeactivate()` |
| F3 | GarageManagerController.cs | `Activate()` | `PlayGarageManagerActivate()` |
| F5 | PitStopCrewController.cs | Offline earnings grant | `PlayPitStopEarnings()` |
| T1 | MomentumController.cs | `RegisterClick()` | `PlayMomentumTick(stacks, cap)` |
| T2 | MomentumController.cs | `Update()` timeout reset | `PlayMomentumReset()` |
| T4 | CarEvolution.cs | `ApplyStage()` | `PlayCarEvolution()` |

#### UI / Panels / Popups (U1–U7)
| ID | File | Trigger Point | SFXManager Method |
|----|------|---------------|-------------------|
| U1 | BottomBarController.cs | `OnTabButtonClicked()` | `PlayUIClick()` |
| U2 | PanelTransitionManager.cs | `SwitchTo()` non-Clicker tab | `PlayPanelOpen()` |
| U3 | PanelTransitionManager.cs | `SwitchTo()` Clicker tab | `PlayPanelClose()` |
| U4 | ChestPopupController.cs | `ShowPopupForChest()` | `PlayPopupAppear()` |
| U5 | DailyOffersController.cs | `OnFreeSlotClicked()` | `PlayDailyFreeClaim()` |
| U6 | DailyOffersController.cs | `TryPurchaseCardSlot()` success | `PlayDailyPackBuy()` |
| U7 | RewardPopupController.cs | `Show()` | `PlayRewardPopupAppear()` |

#### Garage Scene (G1–G4)
| ID | File | Trigger Point | SFXManager Method |
|----|------|---------------|-------------------|
| G1 | GarageController.cs | `GoLeft()` / `GoRight()` | `PlayGarageCarSwitch()` |
| G2 | GarageController.cs | `FinalizeColorPurchase()` | `PlayGaragePurchase()` |
| G3 | GarageController.cs | `FinalizeStickerPurchase()` | `PlayGaragePurchase()` |
| G4 | GarageController.cs | `FinalizePartPurchase()` | `PlayGaragePurchase()` |

#### Cinematic (K1–K3)
| ID | File | Trigger Point | SFXManager Method |
|----|------|---------------|-------------------|
| K1 | CarShowcaseDirector.cs | `Play()` | `PlayCinematicReveal()` |
| K2 | CarShowcaseDirector.cs | `FinishCinematic()` | `PlayCinematicFadeOut()` |
| K3 | ShowcaseCarNameReveal.cs | `Play()` | `PlayCinematicNameReveal()` |

#### World Collectibles
| ID | File | Trigger Point | SFXManager Method |
|----|------|---------------|-------------------|
| — | Chest.cs | `OnTapped()` | `PlayWorldChestCollect()` |

#### Blacklist / Goals
| ID | File | Trigger Point | SFXManager Method |
|----|------|---------------|-------------------|
| — | BlacklistManager.cs | `AdvanceToNextTier()` | `PlayGoalComplete()` |

#### Background Music — MusicManager.cs
| Feature | Details |
|---------|---------|
| Scene-based auto-switch | Main, ChestOpenScene, NewGarage, TakeTheCarScene |
| Crossfade system | Dual AudioSource with DOFade transitions (1s default) |
| Duck/Restore API | `DuckMusic(targetVol, fadeDuration)` / `RestoreMusic(fadeDuration)` — used by BoostAudioController and chase system |
| Volume persistence | PlayerPrefs "MusicVolume" key |

---

## 2. Manual Inspector Assignments Needed

### SFXManager (on main scene persistent GameObject)

You must drag AudioClip assets into **every** slot below. Each field appears in the Inspector under the listed header. If the clip file doesn't exist yet, you'll need to create/import it first.

**Existing clips (already in Assets/SFX/):**
- `carTapClips[]` — 11 existing car tap variants
- `buildingBuyClip` — BuildingBuy.wav
- `upgradeClip` — Upgrade.wav
- `goalCompleteClip` — GoalComplete.wav

**New clips required (~61 slots):**

| Header | Field Name | Suggested Clip Name |
|--------|-----------|-------------------|
| Chest Open SFX | `chestDropClip` | SFX_ChestDrop |
| | `chestHopClip` | SFX_ChestHop |
| | `chestLidOpenClip` | SFX_ChestLidOpen |
| | `rewardMoneyClip` | SFX_RewardMoney |
| | `rewardNitroClip` | SFX_RewardNitro |
| | `rewardCardClip` | SFX_RewardCard |
| | `rewardStickerClip` | SFX_RewardSticker |
| | `chestSummaryClip` | SFX_ChestSummary |
| | `chestExitClip` | SFX_ChestExit |
| | `worldCardSwooshClip` | SFX_WorldCardSwoosh |
| Nitro SFX | `nitroCoinCollectClip` | SFX_NitroCoinCollect |
| | `nitroCoinMagnetClip` | SFX_NitroCoinMagnet |
| | `nitroDepositClip` | SFX_NitroDeposit |
| | `nitroRainDelayClip` | SFX_NitroRainDelay |
| | `nitroRainStartClip` | SFX_NitroRainStart |
| | `nitroRainLoopClip` | SFX_NitroRainLoop |
| | `nitroRainEndClip` | SFX_NitroRainEnd |
| | `magnetActivateClip` | SFX_MagnetActivate |
| | `magnetPullClip` | SFX_MagnetPull |
| | `magnetDeactivateClip` | SFX_MagnetDeactivate |
| Boost SFX | `boostReadyClip` | SFX_BoostReady |
| | `boostActivateClip` | SFX_BoostActivate |
| | `boostActiveLoopClip` | SFX_BoostActiveLoop |
| | `boostEndClip` | SFX_BoostEnd |
| | `boostCooldownCompleteClip` | SFX_BoostCooldownComplete |
| Police & Radar SFX | `chaseSuccessClip` | SFX_ChaseSuccess |
| | `chaseFailClip` | SFX_ChaseFail |
| | `radarDefuseClip` | SFX_RadarDefuse |
| | `radarMissClip` | SFX_RadarMiss |
| | `radarPopupClip` | SFX_RadarPopup |
| | `popularityStageUpClip` | SFX_PopularityStageUp |
| Systems SFX | `turboFingerActivateClip` | SFX_TurboFingerActivate |
| | `turboFingerDeactivateClip` | SFX_TurboFingerDeactivate |
| | `garageManagerActivateClip` | SFX_GarageManagerActivate |
| | `garageManagerDeactivateClip` | SFX_GarageManagerDeactivate |
| | `pitStopEarningsClip` | SFX_PitStopEarnings |
| | `momentumTickClip` | SFX_MomentumTick |
| | `momentumResetClip` | SFX_MomentumReset |
| | `carEvolutionClip` | SFX_CarEvolution |
| | `cashbackClip` | SFX_Cashback |
| UI SFX | `uiClickClip` | SFX_UIClick |
| | `panelOpenClip` | SFX_PanelOpen |
| | `panelCloseClip` | SFX_PanelClose |
| | `popupAppearClip` | SFX_PopupAppear |
| | `startUnlockClip` | SFX_StartUnlock |
| | `openNowClip` | SFX_OpenNow |
| | `chestTimerDoneClip` | SFX_ChestTimerDone |
| | `dailyFreeClaimClip` | SFX_DailyFreeClaim |
| | `dailyPackBuyClip` | SFX_DailyPackBuy |
| | `rewardCollectClip` | SFX_RewardCollect |
| | `missionCompleteClip` | SFX_MissionComplete |
| | `tierAdvanceClip` | SFX_TierAdvance |
| | `rewardPopupAppearClip` | SFX_RewardPopupAppear |
| | `cardPopupOpenClip` | SFX_CardPopupOpen |
| Garage SFX | `garageCarSwitchClip` | SFX_GarageCarSwitch |
| | `garageColorClip` | SFX_GarageColor |
| | `garageStickerClip` | SFX_GarageSticker |
| | `garagePartOnClip` | SFX_GaragePartOn |
| | `garagePartOffClip` | SFX_GaragePartOff |
| | `garagePurchaseClip` | SFX_GaragePurchase |
| | `garagePurchaseFailClip` | SFX_GaragePurchaseFail |
| | `garageFocusInClip` | SFX_GarageFocusIn |
| | `garageFocusOutClip` | SFX_GarageFocusOut |
| | `garageLockedClip` | SFX_GarageLocked |
| Cinematic SFX | `cinematicRevealClip` | SFX_CinematicReveal |
| | `cinematicNameRevealClip` | SFX_CinematicNameReveal |
| | `cinematicFadeOutClip` | SFX_CinematicFadeOut |
| World Collectibles | `worldChestCollectClip` | SFX_WorldChestCollect |

### MusicManager (new persistent GameObject)

| Field | Description |
|-------|-------------|
| `musicSourceA` | AudioSource component (PlayOnAwake=false, Loop=true) |
| `musicSourceB` | AudioSource component (PlayOnAwake=false, Loop=true) |
| `mainSceneMusic` | Loop for Main scene gameplay |
| `chestSceneMusic` | Loop for ChestOpenScene |
| `garageSceneMusic` | Loop for NewGarage scene |
| `takeTheCarSceneMusic` | Loop for TakeTheCarScene |

### BoostAudioController (attach to BoostModeController's GameObject)
No clip fields — uses SFXManager methods. Just needs the component added.

### PoliceChaseFeedbackController
Already has its own 5 AudioSource + 5 clip fields for chase-specific audio layers (heartbeat, siren, engine, stinger, loop). Those still need assignment as before. The new P6/P7 stingers go through SFXManager — no additional setup here.

---

## 3. Unity Editor Setup Checklist

### Step 1: Import Audio Files
1. Create folder `Assets/SFX/NewClips/` (or organize into subfolders)
2. Import all ~61 new .wav/.ogg clip files
3. Set import settings: **Force To Mono** = true, **Load Type** = Decompress On Load (for short SFX) or Compressed In Memory (for loops), **Compression** = Vorbis, Quality 70%

### Step 2: Scene — Main.unity
1. **SFXManager**: Select the existing SFXManager GameObject → assign all new clips to their fields
2. **MusicManager**: Create → Empty GameObject "MusicManager"
   - Add `MusicManager` component
   - Add 2x `AudioSource` components (PlayOnAwake=false, Loop=true)
   - Drag them into `musicSourceA` and `musicSourceB` fields
   - Assign the 4 music clips (mainSceneMusic, chestSceneMusic, garageSceneMusic, takeTheCarSceneMusic)
3. **BoostAudioController**: Find the BoostModeController GameObject → Add Component → `BoostAudioController`

### Step 3: Scene — ChestOpenScene.unity
- No additional setup needed — all hooks use `SFXManager.Instance` which persists via DontDestroyOnLoad

### Step 4: Scene — NewGarage.unity
- No additional setup needed — GarageController hooks use `SFXManager.Instance`
- Music auto-switches via MusicManager's `OnSceneLoaded`

### Step 5: Scene — TakeTheCarScene.unity
- No additional setup needed — CarShowcaseDirector hooks use `SFXManager.Instance`

### Step 6: Verify DontDestroyOnLoad
- SFXManager must have DontDestroyOnLoad ✓ (already implemented)
- MusicManager must have DontDestroyOnLoad ✓ (already implemented)
- BoostModeController must survive through Main scene ✓ (already a persistent singleton)

### Step 7: Audio Mixer (Optional but Recommended)
1. Create `Assets/Audio/MainMixer.asset` with groups: Master → Music, SFX, UI
2. Route MusicManager's AudioSources → Music group
3. Route SFXManager's sfxSource + loopSFXSource → SFX group
4. Enable duck effect on Music group (sidechained from SFX) for auto-ducking

---

## 4. Validation & Test Plan

### Smoke Tests (play each scene once)

| Test | Expected Result | Pass? |
|------|----------------|-------|
| **Main scene loads** | Background music starts playing | ☐ |
| **Tap car** | Random car tap SFX plays with slight pitch variation | ☐ |
| **Buy building** | BuildingBuy SFX plays (rate-limited if rapid) | ☐ |
| **Upgrade building** | Upgrade SFX plays | ☐ |
| **Bottom bar tab switch** | UI click + panel open/close SFX | ☐ |
| **Nitro rain triggers** | Rain delay SFX → rain start SFX → rain loop starts → rain end SFX → loop stops | ☐ |
| **Collect nitro coin (tap)** | Coin collect SFX with pitch variation | ☐ |
| **Collect nitro coin (magnet)** | Softer coin magnet SFX | ☐ |
| **Magnet activates** | Magnet activate SFX plays | ☐ |
| **Magnet coin pull** | Magnetic pull SFX per coin | ☐ |
| **Magnet deactivates** | Magnet deactivate SFX | ☐ |
| **Boost charges fully** | Boost ready ding | ☐ |
| **Activate boost** | Boost activate SFX + loop starts + music ducks | ☐ |
| **Boost ends** | Boost end SFX + loop stops + music restores | ☐ |
| **Cooldown ends** | Cooldown complete pip | ☐ |
| **Police chase begins** | Chase feedback system activates (heartbeat, siren, etc.) | ☐ |
| **Escape chase** | Chase success stinger plays | ☐ |
| **Fail chase** | Chase fail stinger plays | ☐ |
| **Tap radar** | Radar defuse SFX | ☐ |
| **Miss radar** | Radar miss SFX + popup SFX | ☐ |
| **Popularity crosses stage** | Stage-up SFX | ☐ |
| **Momentum build** | Tick SFX every 5th stack, ascending pitch | ☐ |
| **Momentum reset** | Reset SFX on timeout | ☐ |
| **Turbo Finger activates** | Activate SFX | ☐ |
| **Turbo Finger ends** | Deactivate SFX | ☐ |
| **Garage Manager triggers** | Activate SFX | ☐ |
| **Car evolution threshold** | Evolution SFX | ☐ |
| **Open chest popup** | Popup appear SFX | ☐ |
| **Enter ChestOpenScene** | Music crossfades, all C1-C10 SFX fire at correct phases | ☐ |
| **Claim daily free reward** | Free claim SFX | ☐ |
| **Buy daily card pack** | Pack buy SFX | ☐ |
| **Blacklist tier advance** | Goal complete SFX | ☐ |
| **Reward popup shows** | Reward popup SFX | ☐ |
| **Collect world chest** | World chest collect SFX | ☐ |

### Garage Scene Tests

| Test | Expected Result | Pass? |
|------|----------------|-------|
| **Switch car left/right** | Garage car switch SFX | ☐ |
| **Purchase color** | Garage purchase SFX | ☐ |
| **Purchase sticker** | Garage purchase SFX | ☐ |
| **Purchase part** | Garage purchase SFX | ☐ |
| **Music on garage load** | Crossfades to garage music | ☐ |

### Cinematic Tests

| Test | Expected Result | Pass? |
|------|----------------|-------|
| **Cinematic starts** | Cinematic reveal SFX | ☐ |
| **Car name reveals** | Name reveal SFX | ☐ |
| **Cinematic ends** | Fade-out SFX | ☐ |
| **Music on TakeTheCar load** | Crossfades to TakeTheCar music | ☐ |

### Offline Earnings Test

| Test | Expected Result | Pass? |
|------|----------------|-------|
| **Force-quit, wait 1+ min, reopen** | PitStop earnings SFX plays on startup | ☐ |

### Edge Cases

| Test | Expected Result | Pass? |
|------|----------------|-------|
| **Rapid coin collection** | Max 3 voices, no audio stack| ☐ |
| **Rapid car taps** | Pitch varies, no stacking | ☐ |
| **Mute SFX then act** | Complete silence, no errors | ☐ |
| **Scene transition during loop** | Loops stop cleanly | ☐ |
| **Missing clip field (null)** | Silent, no NullReferenceException | ☐ |

---

## Files Modified (Total: 22 files + 2 new)

### New Files
- `Assets/Scripts/MusicManager.cs`
- `Assets/Scripts/BoostAudioController.cs`

### Modified Files
| File | Changes |
|------|---------|
| `Assets/Scripts/SFXManager.cs` | Full rewrite — 65 clip fields, 50+ methods, loop source, rate-limiting |
| `Assets/Scripts/ChestOpenSceneController.cs` | 10 audio hooks (C1-C10) |
| `Assets/Scripts/NitroCoin.cs` | 3 audio hooks (N1, N2, N10) |
| `Assets/Scripts/NitroRainController.cs` | 3 audio hooks + loop start/stop (N5-N8) |
| `Assets/Scripts/NitroMagnetController.cs` | 2 audio hooks (N9, N11) |
| `Assets/Scripts/PoliceChaseFeedbackController.cs` | Chase outcome stingers (P6, P7) |
| `Assets/Scripts/Radar.cs` | 2 audio hooks (P8, P9) |
| `Assets/Scripts/RadarPopupController.cs` | 1 audio hook (P10) |
| `Assets/Scripts/PopularityManager.cs` | Stage-up detection + SFX (P11) |
| `Assets/Scripts/BottomBarController.cs` | 1 audio hook (U1) |
| `Assets/Scripts/PanelTransitionManager.cs` | 2 audio hooks (U2, U3) |
| `Assets/Scripts/ChestPopupController.cs` | 1 audio hook (U4) |
| `Assets/Scripts/DailyOffersController.cs` | 2 audio hooks (U5, U6) |
| `Assets/Scripts/Blacklist/RewardPopupController.cs` | 1 audio hook (U7) |
| `Assets/Scripts/Chest.cs` | 1 audio hook (world chest collect) |
| `Assets/Scripts/MomentumController.cs` | 2 audio hooks (T1, T2) |
| `Assets/Scripts/CarEvolution.cs` | 1 audio hook (T4) |
| `Assets/Scripts/TurboFingerController.cs` | 2 audio hooks (F1, F2) |
| `Assets/Scripts/GarageManagerController.cs` | 1 audio hook (F3) |
| `Assets/Scripts/PitStopCrewController.cs` | 1 audio hook (F5) |
| `Assets/Scripts/Blacklist/BlacklistManager.cs` | 1 audio hook (goal complete) |
| `Assets/Scripts/Garage/GarageController.cs` | 5 audio hooks (G1 ×2, G2, G3, G4) |
| `Assets/Scripts/Cinematic/CarShowcaseDirector.cs` | 2 audio hooks (K1, K2) |
| `Assets/Scripts/Cinematic/ShowcaseCarNameReveal.cs` | 1 audio hook (K3) |
