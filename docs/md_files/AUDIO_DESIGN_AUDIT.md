# AUDIO DESIGN AUDIT — CarClickerMobile3D

**Date:** 2026-03-29  
**Scope:** Complete project audit — all scripts, prefabs, scenes, assets  
**Scenes:** Main, ChestOpenScene, NewGarage, TakeTheCarScene, TestScene  

---

## TABLE OF CONTENTS

1. [Existing Audio Implementation](#1-existing-audio-implementation)
2. [Missing Audio Opportunities](#2-missing-audio-opportunities)
3. [Audio Recommendation List](#3-audio-recommendation-list)
4. [Mobile Audio Fit](#4-mobile-audio-fit)
5. [Duplicate / Overlap / Spam Risk](#5-duplicate--overlap--spam-risk)
6. [Implementation Notes](#6-implementation-notes)
7. [Final Master Checklist](#7-final-master-checklist)

---

## 1. EXISTING AUDIO IMPLEMENTATION

### 1.1 Audio Assets on Disk

| Asset Path | Type | Count | Used By |
|------------|------|-------|---------|
| `Assets/SFX/SFX_CarTap/1.mp3` – `11.mp3` | MP3 clips | 11 | `SFXManager.carTapClips[]` |
| `Assets/SFX/SFX_BuildingBuy.mp3` | MP3 clip | 1 | `SFXManager.buildingBuyClip` |
| `Assets/SFX/SFX_Upgrade.mp3` | MP3 clip | 1 | `SFXManager.upgradeClip` |
| `Assets/SFX/SFX_GoalComplete.mp3` | MP3 clip | 1 | `SFXManager.goalCompleteClip` |

**Total clip assets: 14 files (11 tap variants + 3 one-shots)**  
**No music assets found anywhere in the project.**

---

### 1.2 SFXManager — Central Audio Singleton

**Script:** `Assets/Scripts/SFXManager.cs`  
**Pattern:** `DontDestroyOnLoad` singleton with static `Instance`  

| Field / Method | Type | Status |
|----------------|------|--------|
| `sfxSource` | `AudioSource` (serialized) | ✅ Wired — single shared AudioSource for all SFX |
| `carTapClips[]` | `AudioClip[]` | ✅ 11 clips — random non-repeat selection |
| `buildingBuyClip` | `AudioClip` | ✅ Assigned |
| `goalCompleteClip` | `AudioClip` | ✅ Assigned |
| `upgradeClip` | `AudioClip` | ✅ Assigned |
| `sfxEnabled` | `bool` | ✅ Exists — but no settings UI exposes it |
| `PlayCarTap()` | Public method | ✅ Called from TapInputRaycaster |
| `PlayBuildingBuy()` | Public method | ✅ Called from BuildingButton |
| `PlayUpgrade()` | Public method | ✅ Called from UpgradeButton + CardDetailPopupController |
| `PlayGoalComplete()` | Public method | ⚠️ **Exists but NO CALLER found** in any script |
| Volume control | — | ❌ No volume slider / mute toggle API |
| Pitch variation | — | ❌ Only random clip selection, no pitch randomization |
| Music system | — | ❌ Does not exist |

**Key gap:** `PlayGoalComplete()` is wired in SFXManager but nothing calls it. Likely intended for blacklist mission completion or chest rewards.

---

### 1.3 Active SFX Call Sites in Gameplay Code

| Script | Line(s) | Method Called | Trigger |
|--------|---------|--------------|---------|
| `TapInputRaycaster.cs` | ~242–243 | `PlayCarTap()` | Normal car tap (Main scene) |
| `TapInputRaycaster.cs` | ~347–349 | `PlayCarTap()` | Chase tap during police chase |
| `BuildingButton.cs` | ~261–262 | `PlayBuildingBuy()` | Successful building purchase |
| `UpgradeButton.cs` | ~81–83 | `PlayUpgrade()` | Upgrade purchased (tap/MPS/global) |
| `CardDetailPopupController.cs` | ~587–589 | `PlayUpgrade()` | Card level-up in card detail popup |

**Total active SFX trigger points: 5** (across 4 scripts)

---

### 1.4 PoliceChaseFeedbackController — Dedicated Chase Audio System

**Script:** `Assets/Scripts/PoliceChaseFeedbackController.cs`  
**Status:** Fully coded audio architecture — but **likely has NO audio clips assigned** in Inspector (no chase-related .mp3/.wav files exist in Assets/SFX/).

| AudioSource Field | Clip Field | Purpose | Status |
|-------------------|------------|---------|--------|
| `gameplayMusicSource` | — | Ducks gameplay music during chase | ⚠️ No music system exists to duck |
| `chaseStingerSource` | `chaseStingerClip` | One-shot chase start sting | ⚠️ Coded, needs clip |
| `chaseLoopSource` | `chaseLoopClip` | Looping high-BPM chase track | ⚠️ Coded, needs clip |
| `heartbeatSource` | `heartbeatClip` | Looping heartbeat, pitch/vol scales with danger | ⚠️ Coded, needs clip |
| `sirenSource` | `sirenClip` | Looping police siren, scales with danger | ⚠️ Coded, needs clip |
| `engineSource` | `engineRoarClip` | Looping engine stress during chase | ⚠️ Coded, needs clip |

**Audio behaviors implemented in code:**
- Music ducking with DOTween fade (musicDuckVolume, musicDuckFadeTime)
- Heartbeat volume/pitch ramping with `DangerFraction` (0→1)
- Siren volume/pitch ramping with `DangerFraction`
- Engine pitch/volume ramp on chase start/end
- Haptic vibration on each chase tap (`Handheld.Vibrate()`)
- All audio starts on `OnChaseStarted` event, stops on `OnChaseEnded`

**Assessment:** The most complete audio design in the project. Architecture is production-ready — only clips need to be authored and assigned.

---

### 1.5 Cinematic Audio (TakeTheCarScene)

| Script | Audio Feature | Status |
|--------|--------------|--------|
| `CinematicShotSO.cs` | `public AudioClip sfxClip` per shot | ✅ Field exists — needs clips per SO asset |
| `CarShowcaseDirector.cs` | `PlaySFX(clip)` on each shot start | ✅ Routes through SFXManager.sfxSource or AudioSource.PlayClipAtPoint fallback |

**Assessment:** Architecture ready. Each `CinematicShotSO` asset can hold a per-shot SFX (whoosh, rev, sting). Needs actual clips assigned to the BlacklistTier SO assets.

---

### 1.6 Places Where Code Structure Suggests Sound (No Audio Implemented)

These scripts have visual feedback, VFX triggers, animation events, state changes, or callback hooks that strongly imply audio should accompany them, but currently have zero audio code:

| Script | Evidence | Missing Audio |
|--------|----------|---------------|
| `ChestOpenSceneController.cs` | 8-phase state machine with DOTween animations (intro, hop, lid, reveals, summary) | Entire chest open flow is silent |
| `BoostModeController.cs` | Events: `OnBoostStarted`, `OnBoostEnded`, `OnBoostReady`, `OnNitroChargeAccepted`, `OnStateChanged` | All boost transitions silent |
| `NitroCoin.cs` | `OnWorldNitroCollected` event, `OnTapped()`, magnet pull phases | Coin collect is silent |
| `NitroRainController.cs` | Events: `OnDelayStarted`, `OnRainStarted`, `OnRainEnded`, continuous spawning | Rain system entirely silent |
| `NitroMagnetController.cs` | Shield VFX, pull phases, coin attraction | Magnet system entirely silent |
| `Radar.cs` | `OnTapped()` → vanish animation, `OnMissed()` → popup | Both tap and miss are silent |
| `RadarPopupController.cs` | Popup appear/disappear with camera snapshot | Silent popup |
| `Chest.cs` | `OnTapped()` → TapVanishAnimator.Play() | World chest collect is silent |
| `TurboFingerController.cs` | State: Ready→Active→Cooldown | Turbo activation/deactivation silent |
| `GarageManagerController.cs` | State: Ready→Active→Cooldown, MPS bonus | Activation/deactivation silent |
| `MomentumController.cs` | `OnMomentumChanged` event, stack gain/reset | Stack build feedback silent |
| `CarEvolution.cs` | Material stage change (0→1→2) | Evolution moment silent |
| `PanelTransitionManager.cs` | DOTween slide open/close for all panels | All panel transitions silent |
| `GarageController.cs` | Car switch (GoLeft/GoRight) with glitch transition | Car switch silent |
| `RewardPopupController.cs` | Popup open + collect animation | Reward collect silent |
| `BlacklistManager.cs` | `OnProgressChanged`, `OnTierChanged` events | All blacklist progress silent |
| `DailyOffersController.cs` | Free reward claim, card pack purchase | All daily offer actions silent |
| `SmallInvestmentController.cs` | Refund applied on building spend | Cashback moment silent |

---

## 2. MISSING AUDIO OPPORTUNITIES

### 2.1 CRITICAL — Core Game Loop (Every Session)

| # | System | Moment | Current State | Impact |
|---|--------|--------|---------------|--------|
| 1 | **Chest Open Scene** | Chest drop intro | Silent DOTween bounce | Players see chest appear with no audio feedback |
| 2 | **Chest Open Scene** | Tap to open hops (taps 1-3) | Silent hop animation | No anticipation build |
| 3 | **Chest Open Scene** | Lid opens | Silent rotation tween | The most dramatic chest moment has no payoff sound |
| 4 | **Chest Open Scene** | Money reveal (card parked) | Silent | Major reward moment |
| 5 | **Chest Open Scene** | Nitro reveal | Silent | Reward moment |
| 6 | **Chest Open Scene** | Card reveal | Silent | The rarest reward type |
| 7 | **Chest Open Scene** | Sticker reveal (Rare/Legendary) | Silent | Bonus surprise reward |
| 8 | **Chest Open Scene** | Summary screen | Silent | Session closing |
| 9 | **Nitro Coin Collect** | Tap on NitroCoin in world | Silent coin vanish | Most frequent collectible interaction outside car tapping |
| 10 | **Radar Defuse** | Tap on radar → vanish | Silent | High-tension moment, player needs audio reward for dodging |
| 11 | **Radar Miss** | Radar reaches despawnZ → popup | Silent photo | Should feel like a warning/consequence |
| 12 | **World Chest Collect** | Tap chest on road → inventory | Silent vanish animation | Loot pickup should feel good |
| 13 | **Background Music** | Entire game | No music at all | Game feels empty and unfinished |

### 2.2 IMPORTANT — Major Systems

| # | System | Moment | Current State |
|---|--------|--------|---------------|
| 14 | **Boost Mode** | Activation (Charging→Active) | Silent — events exist but no audio subscriber |
| 15 | **Boost Mode** | Ready notification (charge full) | Silent |
| 16 | **Boost Mode** | Deactivation / end | Silent |
| 17 | **Boost Mode** | Nitro deposited into charge bar | Silent |
| 18 | **Boost Mode** | Cooldown complete → Ready | Silent |
| 19 | **Nitro Rain** | Rain starts (coins begin spawning) | Silent |
| 20 | **Nitro Rain** | During rain (ambient) | No loop |
| 21 | **Nitro Rain** | Rain ends | Silent |
| 22 | **Nitro Magnet** | Shield activation | Silent |
| 23 | **Nitro Magnet** | Individual coin pulled to car | Silent |
| 24 | **Nitro Magnet** | Shield expires / deactivates | Silent |
| 25 | **Turbo Finger** | Activation (50 taps → multiplier active) | Silent |
| 26 | **Turbo Finger** | Deactivation (window ends) | Silent |
| 27 | **Police Chase** | Stinger, loop, heartbeat, siren, engine | Architecture ready, **clips not assigned** |
| 28 | **Police Chase** | Success result | No victory sting |
| 29 | **Police Chase** | Failure result | No failure sting |
| 30 | **Car Evolution** | Stage 0→1, 1→2 material change | Silent — significant visual event |
| 31 | **Momentum** | Stack building (each click) | Silent |
| 32 | **Momentum** | Stack reset (stopped tapping) | Silent |
| 33 | **Garage Manager** | Activation (MPS boost starts) | Silent |
| 34 | **Garage Manager** | Deactivation (boost expires) | Silent |

### 2.3 NICE-TO-HAVE — UI & Polish

| # | System | Moment | Current State |
|---|--------|--------|---------------|
| 35 | **Panel Transitions** | Bank/Shop/TimeWarp/Ranking open | Silent slide animation |
| 36 | **Panel Transitions** | Panel close → Clicker | Silent slide animation |
| 37 | **Bottom Bar** | Tab switch tap | No click SFX |
| 38 | **Chest Popup** | Popup appears (slot tapped) | Silent |
| 39 | **Chest Popup** | "Start Unlock" pressed | Silent |
| 40 | **Chest Popup** | "Open Now" (skip timer) pressed | Silent |
| 41 | **Chest Slot** | Timer complete → "Ready" state | Silent |
| 42 | **Daily Offers** | Free reward claimed | Silent |
| 43 | **Daily Offers** | Card pack purchased (NC spent) | Silent |
| 44 | **Daily Offers** | Free chest opened | Silent |
| 45 | **Blacklist Panel** | Panel opens | Silent |
| 46 | **Blacklist** | Mission completed | Silent |
| 47 | **Blacklist** | Tier advanced | Silent |
| 48 | **Blacklist** | Reward popup Collect pressed | Silent |
| 49 | **Garage** | Car switch (GoLeft/GoRight) | Silent glitch transition |
| 50 | **Garage** | Color applied | Silent |
| 51 | **Garage** | Sticker applied | Silent |
| 52 | **Garage** | Part toggled on/off | Silent |
| 53 | **Garage** | Buy popup confirm/cancel | Silent |
| 54 | **Garage** | Focus mode enter/exit (double-tap) | Silent |
| 55 | **Garage** | Exit popup confirm | Silent |
| 56 | **Cinematic** | Per-shot transition SFX | Architecture ready, clips not assigned |
| 57 | **Cinematic** | Car name reveal text | Silent slide-in animation |
| 58 | **Cinematic** | Skip button tap | Silent |
| 59 | **Small Investment** | Cashback on building purchase | Silent |
| 60 | **PitStop Crew** | Offline earnings shown | Silent |
| 61 | **Popularity** | Stage transition (1→2→3…) | Silent |
| 62 | **Card Collection** | Card slot tapped in grid | Silent |
| 63 | **Card Detail** | Popup opens | Silent |
| 64 | **Shop/Cards tabs** | Tab switch | Silent |
| 65 | **Floating Text** | "+money" text spawned on tap | No accompanying pop SFX (tap SFX covers this) |

---

## 3. AUDIO RECOMMENDATION LIST

### 3.1 CHEST OPEN SCENE (ChestOpenSceneController.cs)

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| C1 | Chest drops in (Phase.Intro) | `PlayIntro()` | Anticipation | Layered SFX | Magical whoosh + soft thud on land, filter sweep up | **Critical** |
| C2 | Tap hop (taps 1-2, Closed_TapToOpen) | `PlayTapFeedback()` | Feedback | SFX one-shot | Wooden/metallic tap + subtle rattle (chain links), pitch rising per tap | **Critical** |
| C3 | Lid opens (Phase.LidOpening) | `OpenLid()` | Reward reveal | Layered SFX | Creaky hinge + golden burst + shimmer tail, 0.5s | **Critical** |
| C4 | Money reveal (Reveal_Money) | `OnLidOpened()` → auto reveal | Reward | Stinger + SFX | Coin cascade + small fanfare sting (2-3 notes ascending) | **Critical** |
| C5 | Nitro reveal (Reveal_Nitro) | `OnChestTapped()` → Reveal_Nitro | Reward | Stinger | Electric/tech chime, different tonality from money | **Critical** |
| C6 | Card reveal (Reveal_Card) | `OnChestTapped()` → Reveal_Card | Reward | Stinger | Card flip whoosh + reveal shimmer, rarity-scaled (common=soft, legendary=dramatic) | **Critical** |
| C7 | Sticker reveal (Reveal_Sticker) | `OnChestTapped()` → Reveal_Sticker | Bonus reward | Stinger | Sticker slap + sparkle, lighter than card reveal | Important |
| C8 | Summary shown (Phase.Summary) | `revealController.ShowSummary()` | Closure | Musical accent | Soft completion jingle, 1-2 seconds | Important |
| C9 | Exit tap (Phase.Exit) | `FinishAndReturnToMain()` | Transition | SFX one-shot | Quick swoosh-out | Nice-to-have |
| C10 | World card rise + park animation | `SpawnWorldCard()` → WorldRewardCardController | Anticipation | SFX one-shot | Card swoosh/slide, 0.3s | Important |

### 3.2 TAPPING & ECONOMY

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| T1 | Momentum stack gained | `MomentumController.RegisterClick()` | Feedback | SFX one-shot | Subtle ascending tick/ping, pitch increases with stack count | Important |
| T2 | Momentum stack reset | `MomentumController.Update()` timeout | Warning | SFX one-shot | Soft descending tone, quick (0.2s) | Nice-to-have |
| T3 | Momentum high stack (>80% cap) | `RegisterClick()` when near cap | Reward | SFX one-shot | Brighter/fuller version of T1 | Nice-to-have |
| T4 | Car evolution stage change | `CarEvolution.ApplyStage()` | Reward | Stinger | Level-up fanfare, 1-2s, ascending tones + sparkle | Important |
| T5 | Small Investment cashback | `SmallInvestmentController` refund moment | Feedback | SFX one-shot | Soft "cha-ching" register sound, subtle | Nice-to-have |

### 3.3 NITRO SYSTEMS

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| N1 | Nitro coin collected (tap) | `NitroCoin.OnTapped()` | Feedback | SFX one-shot | Clean bell/chime pling, short (0.15s), bright high-mid frequency | **Critical** |
| N2 | Nitro coin collected (magnet) | `NitroCoin` magnet pull complete | Feedback | SFX one-shot | Same family as N1 but softer, slightly detuned — avoids spam when many coins pulled | **Critical** |
| N3 | Nitro coin spawned on road | `NitroCoinSpawner.SpawnNitroCoin()` | Awareness | SFX one-shot | Very subtle sparkle/twinkle, barely audible, high frequency | Nice-to-have |
| N4 | Nitro deposited into boost bar | `BoostModeController.OnNitroChargeAccepted` | Feedback | SFX one-shot | Soft electronic "ding" deposit sound | Important |
| N5 | Nitro Rain delay started | `NitroRainController.OnDelayStarted` | Anticipation | SFX one-shot | Distant thunder/buildup rumble, 0.5s | Important |
| N6 | Nitro Rain starts | `NitroRainController.OnRainStarted` | Excitement | Stinger + Loop | Rain start: "release" whoosh + sustained gentle shimmer/rain loop underneath | **Critical** |
| N7 | Nitro Rain ambient (during rain) | `NitroRainController` while `IsRaining` | Ambience | Loop | Soft coin rain patter loop, dreamy, not fatiguing, ~10s loop | **Critical** |
| N8 | Nitro Rain ends | `NitroRainController.OnRainEnded` | Transition | SFX one-shot | Rain fade-out whoosh + gentle descending chime | Important |
| N9 | Nitro Magnet shield activated | `NitroMagnetController` (shield VFX on) | Feedback | SFX one-shot | Energy field "hum-on" — quick electronic activation, 0.3s | Important |
| N10 | Nitro Magnet individual coin pull | `NitroCoin.TransitionToPull()` | Feedback | SFX one-shot | Light magnetic whoosh/zip per coin, very short (0.1s) | Important |
| N11 | Nitro Magnet deactivates | `NitroMagnetController` shield expires | Feedback | SFX one-shot | Energy field power-down, 0.3s | Nice-to-have |
| N12 | ArcLineVFX active (electric arc) | `ArcLineVFX` between magnet and coin | Ambience | Loop | Subtle electric crackle, very quiet, position-based | Nice-to-have |

### 3.4 BOOST MODE

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| B1 | Boost Ready (charge full) | `BoostModeController.OnBoostReady` | Notification | SFX one-shot | Bright "fully charged" ding + slight shimmer tail, 0.5s | **Critical** |
| B2 | Boost Activation | `BoostModeController.OnBoostStarted` | Impact | Layered SFX | Turbo ignition: low rumble → whoosh → high-pitched sustain, 0.6s | **Critical** |
| B3 | Boost Active loop (during boost) | While `IsBoostActive` | Ambience | Loop | Sustained turbo hum/wind, subtle, ducks on tap | Important |
| B4 | Boost Ends | `BoostModeController.OnBoostEnded` | Transition | SFX one-shot | Power-down: descending pitch whoosh, engine wind-down, 0.5s | **Critical** |
| B5 | Boost Cooldown complete | `BoostModeController.OnStateChanged` → Charging | Feedback | SFX one-shot | Soft "ready to charge" notification pip | Nice-to-have |

### 3.5 POLICE / RADAR / POPULARITY

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| P1 | Chase stinger (start) | `PoliceChaseFeedbackController.chaseStingerClip` | Impact | Stinger | Sharp alarm burst + bass hit, 1-2s | **Critical** |
| P2 | Chase loop (during chase) | `PoliceChaseFeedbackController.chaseLoopClip` | Tension | Loop | High-BPM percussive loop, 120-140 BPM, dark/urgent, 8-16 bars | **Critical** |
| P3 | Heartbeat loop | `PoliceChaseFeedbackController.heartbeatClip` | Tension | Loop | Clean heartbeat thump, pitch/vol controlled by code | **Critical** |
| P4 | Police siren loop | `PoliceChaseFeedbackController.sirenClip` | Atmosphere | Loop | Classic European police siren wail, 2-4s loop | **Critical** |
| P5 | Engine roar loop | `PoliceChaseFeedbackController.engineRoarClip` | Atmosphere | Loop | Stressed engine rev loop, mid-high RPM | Important |
| P6 | Chase success (escaped) | `PoliceCatchController.OnChaseEnded` (success) | Reward | Stinger | Victory sting: 2-3 bright ascending notes, relief feel | **Critical** |
| P7 | Chase failure (caught) | `PoliceCatchController.OnChaseEnded` (fail) | Consequence | Stinger | Failure sting: 2-3 descending minor notes, deflation | **Critical** |
| P8 | Radar tapped (defused) | `Radar.OnTapped()` | Reward | SFX one-shot | Quick electronic "zap/disable" beep, satisfying, 0.2s | **Critical** |
| P9 | Radar missed (photo taken) | `Radar.OnMissed()` | Warning | SFX one-shot | Camera shutter click + short alarm beep, ominous | **Critical** |
| P10 | Radar popup appears | `RadarPopupController.ShowSnapshot()` | Warning | SFX one-shot | Polaroid-style photo develop sound, brief | Important |
| P11 | Popularity stage up | `PopularityManager` stage boundary | Warning | SFX one-shot | Escalation tone, 2 ascending notes, more urgent per stage | Important |
| P12 | Heat threshold warning | `AmbientHeatManager` near chase trigger | Anticipation | SFX one-shot | Subtle threat build (soft alarm ping), very quiet | Nice-to-have |

### 3.6 TURBO FINGER / GARAGE MANAGER / PITSTOP

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| F1 | Turbo Finger activation | `TurboFingerController` → Active state | Excitement | Stinger | Finger snap + power-up whoosh, punchy, 0.3s | Important |
| F2 | Turbo Finger deactivation | `TurboFingerController` → Cooldown state | Transition | SFX one-shot | Power-down swoosh, brief | Nice-to-have |
| F3 | Garage Manager activation | `GarageManagerController` → Active state | Feedback | SFX one-shot | Wrench/tool clink + engine purr start, 0.3s | Important |
| F4 | Garage Manager deactivation | `GarageManagerController` → Cooldown | Transition | SFX one-shot | Soft wind-down | Nice-to-have |
| F5 | PitStop Crew offline earnings | `PitStopCrewController` earnings display | Reward | SFX one-shot | Cash register / coins counting, 0.5s, satisfying | Important |
| F6 | PitStop Crew count-up animation | While counting up displayed earnings | Feedback | Loop | Rapid soft coin tick loop, ends when count finishes | Nice-to-have |

### 3.7 UI / PANELS / POPUPS

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| U1 | Bottom bar tab switched | `BottomBarController` + `BottomBarTabUI` | UI feedback | UI click | Soft, neutral "tok" click, 0.1s | Important |
| U2 | Panel slides open | `PanelTransitionManager.SwitchTo()` → open tween | UI transition | SFX one-shot | Gentle "fwip" / paper slide, 0.2s | Important |
| U3 | Panel slides closed | `PanelTransitionManager` → close tween | UI transition | SFX one-shot | Softer reverse "fwip", 0.15s | Nice-to-have |
| U4 | Chest popup appears | `ChestPopupController.Show()` | UI transition | SFX one-shot | Soft popup "pop", 0.15s | Important |
| U5 | Chest popup "Start Unlock" | Button press | UI action | UI click | Mechanical tick/lock sound, 0.15s | Important |
| U6 | Chest popup "Open Now" (NC spend) | Button press | UI action + spend | SFX one-shot | Coin spend + quick whoosh, 0.2s | Important |
| U7 | Chest slot timer complete | `ChestSlotUI` → Ready state | Notification | SFX one-shot | Soft "ding" + brief sparkle, 0.3s | Important |
| U8 | Daily offer free reward claimed | `DailyOffersController` free slot pressed | Reward | SFX one-shot | Gift unwrap / positive pling, 0.3s | Important |
| U9 | Daily offer card pack purchased | `DailyOffersController` slot2/slot3 buy | Purchase | SFX one-shot | Soft purchase confirmation, 0.2s | Important |
| U10 | Card popup opened | `CardDetailPopupController` popup shown | UI transition | SFX one-shot | Card flip / paper reveal, 0.15s | Nice-to-have |
| U11 | Shop/Cards tab switched | `ShopCardsTabs` tab press | UI feedback | UI click | Same as U1 | Nice-to-have |
| U12 | Blacklist reward popup Collect | `RewardPopupController.OnCollectPressed()` | Reward | SFX one-shot | Satisfying collect sting, 0.4s | Important |
| U13 | Blacklist mission complete | `BlacklistManager.OnProgressChanged` → done state | Achievement | Stinger | Short achievement jingle, 0.5s | Important |
| U14 | Blacklist tier advanced | `BlacklistManager.OnTierChanged` | Major achievement | Stinger | Ascending fanfare, longer (1s), celebratory | **Critical** |
| U15 | Reward popup appears | `RewardPopupController.Show()` | UI transition | SFX one-shot | Popup + brief fanfare start | Important |

### 3.8 GARAGE SCENE (NewGarage)

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| G1 | Car switch (GoLeft/GoRight) | `GarageController.GoLeft()` / `GoRight()` | Transition | SFX one-shot | Glitch/digital swoosh matching the visual glitch transition, 0.25s | Important |
| G2 | Color applied | `GarageController.SetColor()` via `ColorUIController` | Feedback | SFX one-shot | Soft spray/paint whoosh, brief 0.2s | Important |
| G3 | Sticker applied | `GarageController.SetSticker()` via `StickerUIController` | Feedback | SFX one-shot | Sticker slap / vinyl peel, 0.15s | Important |
| G4 | Part toggled on | `GarageController.TogglePart()` via `PartsUIController` | Feedback | SFX one-shot | Mechanical snap/click, 0.1s | Important |
| G5 | Part toggled off | Same | Feedback | SFX one-shot | Reverse click / detach, 0.1s | Nice-to-have |
| G6 | Purchase confirmed | `GarageBuyPopupController` confirm | Purchase | SFX one-shot | Cash register + brief sparkle, 0.3s | Important |
| G7 | Purchase failed (not enough) | Attempt to buy without funds | Warning | SFX one-shot | Soft error buzz, 0.2s | Nice-to-have |
| G8 | Focus mode enter (double-tap) | `GarageFocusController` zoom in | Transition | SFX one-shot | Camera zoom-in whoosh, 0.2s | Nice-to-have |
| G9 | Focus mode exit | `GarageFocusController` zoom out | Transition | SFX one-shot | Camera zoom-out whoosh, 0.2s | Nice-to-have |
| G10 | Locked car shake | Attempt to customize locked car | Warning | SFX one-shot | Lock rattle / denied buzz, 0.3s | Nice-to-have |
| G11 | Garage ambient | NewGarage scene background | Ambience | Loop | Soft garage ambience (distant echoes, subtle hum), quiet | Nice-to-have |

### 3.9 CINEMATIC / TAKE THE CAR

| # | Trigger Moment | Script / Method | Purpose | Sound Type | Style | Priority |
|---|---------------|-----------------|---------|-----------|-------|----------|
| K1 | Showcase starts | `CarShowcaseDirector.Play()` intro particles | Impact | Musical accent | Dramatic reveal sting, 1-2s | Important |
| K2 | Per-shot transition | `CinematicShotSO.sfxClip` | Emphasis | SFX one-shot | Camera whoosh / engine rev / tyre screech per shot type | Important |
| K3 | Car name reveal text | `ShowcaseCarNameReveal.Play()` | Emphasis | SFX one-shot | Stylized text whoosh, brand-name impact, 0.3s | Important |
| K4 | Skip button tap | `ShowcaseSkipButton` input | UI feedback | UI click | Subtle tap confirmation | Nice-to-have |
| K5 | Fade out to black | `ShowcaseFadeController.CreateFadeOut()` | Transition | SFX one-shot | Soft cinematic fade-out drone, 0.5s | Nice-to-have |
| K6 | Cinematic background music | Entire TakeTheCarScene duration | Atmosphere | Music loop | Dramatic, pump-up track matching the car being revealed, 15-30s loop | **Critical** |

### 3.10 BACKGROUND MUSIC

| # | Context | Where | Purpose | Sound Type | Style | Priority |
|---|---------|-------|---------|-----------|-------|----------|
| M1 | Main scene idle | Main scene, always playing | Ambience | Music loop | Chill lo-fi / electronic beat, moderate tempo (100 BPM), non-intrusive, 60-120s loop | **Critical** |
| M2 | Chase override | PoliceChaseFeedbackController takes over | Tension | Music loop | Already handled by chase loop — M1 ducks | **Critical** |
| M3 | Boost mode layer | During boost active | Energy | Music layer | Optional high-energy layer on top of M1, or M1 gets pitch/filter boost | Nice-to-have |
| M4 | Garage scene | NewGarage scene | Atmosphere | Music loop | Cooler, showroom vibe track, 60-90s loop | Important |
| M5 | ChestOpenScene | Chest opening | Anticipation | Music loop | Mystical/magical ambient pad, 30-60s, builds tension | Important |
| M6 | TakeTheCarScene | Cinematic showcase | Drama | Music loop | See K6 | **Critical** |

---

## 4. MOBILE AUDIO FIT

### 4.1 Per-Recommendation Mobile Behavior Guide

| ID | Duration | Volume | Frequency Balance | Prominence | Stacking | Variation | Ducking |
|----|----------|--------|-------------------|------------|----------|-----------|---------|
| **C1** (Chest drop) | 0.6-0.8s | Medium | Mid-heavy + shimmer highs | Prominent — scene centrepiece | N/A (once) | No | Ducks music |
| **C2** (Chest hop) | 0.2s | Low-medium | Mid, hollow wood/metal | Moderate | Rate-limit: max 4/s | Pitch variation (+/- 5% random per tap) | No |
| **C3** (Lid open) | 0.5s | Medium-high | Wide (creak lows + shimmer highs) | Prominent | N/A | No | Ducks ambient |
| **C4-C7** (Reveals) | 0.4-0.8s | Medium | Mid-high, bright | Prominent | Sequential, no overlap | Different clip per reward type | Each ducks previous |
| **N1** (Coin tap) | 0.15s | Low-medium | High-mid (bell/chime) | Subtle but clear | Max 3 simultaneous | 3-5 pitch variants (random) | No |
| **N2** (Coin magnet) | 0.1s | Low | High (soft pling) | Quiet — many fire during magnet | Voice-limit: max 3 | Random pitch (+/- 10%) | No duck |
| **N6-N7** (Rain) | N6: 0.4s / N7: loop | N6: Medium / N7: Low | N7: High-frequency shimmer, soft | N6: Noticeable / N7: Background | N/A | N/A | N7 ducks slightly under taps |
| **B2** (Boost on) | 0.5-0.7s | High | Full spectrum, punchy | Very prominent — dramatic moment | N/A | No | Ducks everything briefly |
| **B3** (Boost loop) | Loop (8-16s) | Low | Low-mid hum | Background | N/A | N/A | Ducks under taps and SFX |
| **P1** (Chase stinger) | 1-2s | High | Bass hit + sharp highs | Very prominent | N/A | No | Ducks music |
| **P4** (Siren) | Loop | Variable (code-controlled) | Mid-high wail | Scales with danger | N/A | Pitch varies by code | Code handles ducking |
| **P8** (Radar defuse) | 0.2s | Medium | Mid-high electronic zap | Clear, satisfying | Max 1 (one radar at a time) | No | No |
| **P9** (Radar miss) | 0.3s | Medium | Mid (camera shutter) + low alarm | Warning prominence | Max 1 | No | No |
| **U1-U11** (UI clicks) | 0.1-0.15s | Low | Mid, neutral | Subtle | Rate-limit: max 8/s | 2-3 variants for repeated presses | No |
| **G1** (Car switch) | 0.25s | Medium | Digital glitch, wide spectrum | Moderate | N/A (transition guard prevents spam) | No | No |
| **M1** (BGM) | 60-120s loop | Low | Full but gentle, bass not heavy | Background — never fatiguing | N/A | N/A | Ducks for all priority SFX |

### 4.2 General Mobile Audio Rules

1. **Short over long.** Most one-shot SFX should be 0.1-0.5s. Only loops and stingers exceed 1s.
2. **Soft baseline, punchy peaks.** Background loop at -12dB reference; reward stingers at -6dB; boost/chase at -3dB.
3. **High-frequency rolloff.** Phone speakers emphasize highs; roll off above 10kHz to prevent piercing quality.
4. **Rate-limiting is essential.** Coin collects, momentum ticks, and any tap-driven SFX MUST be voice-limited (max simultaneous instances).
5. **Variation prevents fatigue.** Any sound that plays more than once per 2 seconds needs either pitch variation (±5-10% random) or multiple clip variants (3+ clips).
6. **Loops must be seamless.** Rain loop, boost hum, and chase loop need proper sample-accurate loop points — no clicks.
7. **Ducking hierarchy:** Music → Ambient loops → Gameplay SFX → UI SFX → Reward stingers (highest wins).
8. **Mute on app background.** Ensure AudioListener.pause = true on OnApplicationPause(true).
9. **Respect system silent mode.** Check if device is on silent/vibrate before playing.

---

## 5. DUPLICATE / OVERLAP / SPAM RISK

### 5.1 High Risk: Simultaneous Sound Pileup

| Situation | What Could Happen | Mitigation Required |
|-----------|-------------------|---------------------|
| **Rapid car tapping** (3+ TPS) | carTapClips fire 3-4x per second, overlapping on single AudioSource | ✅ Already handled — `PlayOneShot` on shared source naturally layers. BUT pitch variation should be added to avoid "machine gun" feel. |
| **Magnet pulling 5-10 coins simultaneously** | 5-10 coin collect SFX fire within 0.5s | Voice-limit N2 to 2-3 simultaneous. Use a cooldown of 0.05s between coin pull SFX. |
| **Nitro Rain spawning coins every 0.2-0.35s** | New coin spawns every ~quarter second for 5-20 seconds | Do NOT play spawn SFX (N3) during rain. Only play collect SFX (N2) with voice limiting. |
| **Turbo Finger + Momentum + Car tap** | Three SFX systems firing on every tap simultaneously | Momentum tick (T1) should only play on specific stack milestones (every 5th stack), not every tap. Turbo active should be a loop, not per-tap SFX. |
| **Chase tap + tap SFX + heartbeat + siren + engine + loop** | All running simultaneously during police chase | Already managed by PoliceChaseFeedbackController's ducking system. Chase tap SFX should reduce car tap volume or skip car tap SFX entirely during chase. |
| **Chest open: lid + money burst + card whoosh** | Multiple SFX fire sequentially but could overlap on rapid tap | Use phase gate: each SFX waits for previous to reach a certain point before starting. Already structured by phase state machine. |
| **Building buy spam** (rapid purchases) | BuildingBuy SFX fires on every purchase in quick succession | Rate-limit: max 1 BuildingBuy SFX per 0.15s. Or use cooldown. |
| **Multiple panels opening/closing** | Panel open/close SFX during ongoing transition | Check `isTransitioning` before playing panel SFX — only play on settled state. |

### 5.2 Medium Risk: Repetition Fatigue

| Sound | Frequency | Mitigation |
|-------|-----------|------------|
| Car tap SFX | 2-4x per second, thousands per session | ✅ Already has 11 variants. Add ±5% pitch variation. Monitor for long-session fatigue. |
| Coin collect (N1/N2) | 1-10x per second during rain/magnet | Voice limit + pitch variation + softer volume during burst modes |
| Boost Ready ding (B1) | Every 20-60s depending on play style | Fine at this frequency — no mitigation needed |
| Radar defuse (P8) | Every 10-20s | Fine at this frequency |
| UI click (U1) | Variable, but players tab-hop frequently | 2-3 click variants, quiet volume |
| Bottom bar tab switch | Could be rapid during exploration | Same click sound with 0.1s minimum cooldown |

### 5.3 Low Risk (but worth noting)

| Situation | Note |
|-----------|------|
| Chase heartbeat at high danger | Pitch goes up to 1.4×, volume to 1.0 — ensure the clip sounds good at max pitch without distortion |
| Multiple chest slots completing timers near-simultaneously | If 2 chests finish at same time, two "ready" dings overlap — acceptable, no fix needed |
| Evolution stage change during boost | If car changes material stage during an active boost, both stingers could overlap — rare, acceptable |

---

## 6. IMPLEMENTATION NOTES

### 6.1 SFXManager Expansion Strategy

The current `SFXManager` is too narrow (4 fixed clip fields). Two viable approaches:

**Option A: Expand SFXManager with categorized clip fields (Recommended — simplest)**
```csharp
// Add to SFXManager.cs:
[Header("Nitro SFX")]
public AudioClip nitroCoinCollectClip;
public AudioClip nitroRainStartClip;
public AudioClip nitroRainLoopClip; // separate looping AudioSource needed

[Header("Boost SFX")]
public AudioClip boostReadyClip;
public AudioClip boostActivateClip;
public AudioClip boostEndClip;

[Header("Chest SFX")]
public AudioClip chestDropClip;
public AudioClip chestHopClip;
public AudioClip chestLidOpenClip;
public AudioClip rewardMoneyClip;
public AudioClip rewardNitroClip;
public AudioClip rewardCardClip;
public AudioClip rewardStickerClip;
public AudioClip chestSummaryClip;

[Header("UI SFX")]
public AudioClip uiClickClip;
public AudioClip panelOpenClip;
public AudioClip panelCloseClip;
public AudioClip popupAppearClip;
// ... etc

// Add public Play___() methods for each
```

**Option B: Dictionary/Enum-based SFX registry (More scalable)**
```csharp
public enum SFXType { CarTap, BuildingBuy, Upgrade, GoalComplete, NitroCoinCollect, ... }
[System.Serializable] public struct SFXEntry { public SFXType type; public AudioClip[] clips; }
public SFXEntry[] sfxLibrary;
public void Play(SFXType type) { /* lookup + PlayOneShot */ }
```

**Recommendation:** Start with Option A (fast, no refactor risk). Migrate to Option B later if the clip count exceeds ~30-40 entries.

### 6.2 Additional AudioSources Needed

The single `sfxSource` in SFXManager is insufficient for:

| Need | Reason |
|------|--------|
| **Looping SFX source** | For rain loop (N7), boost hum (B3), and any ambient loop. PlayOneShot cannot loop. |
| **Music source** | For M1/M4/M5 background music. Separate volume control from SFX. |
| **Chase sources** | Already designed in PoliceChaseFeedbackController (5 AudioSources). Just needs clips. |

**Minimum addition to SFXManager:** 2 new AudioSources:
1. `musicSource` (loop=true, separate volume slider)
2. `loopSFXSource` (loop=true, for rain/boost ambient)

### 6.3 Where to Trigger Audio — Implementation Points

| Audio | Trigger Method | Implementation Approach |
|-------|---------------|------------------------|
| Chest open scene SFX (C1-C10) | `ChestOpenSceneController` at each phase transition | Add `SFXManager.Instance.PlayChestXxx()` calls directly in existing methods (`PlayIntro`, `PlayTapFeedback`, `OpenLid`, `OnLidOpened`, `OnChestTapped` phase handlers) |
| Nitro coin collect (N1) | `NitroCoin.OnTapped()` | Add call right after `isCollected = true` guard |
| Nitro coin magnet collect (N2) | `NitroCoin` end of pull phase / `onMagnetCollectCallback` | Add call in pull completion |
| Boost events (B1-B5) | Subscribe to `BoostModeController` events | Create a new `BoostAudioController` MonoBehaviour that subscribes to OnBoostStarted/OnBoostEnded/OnBoostReady, similar pattern to BoostBarFeedbackController |
| Nitro Rain events (N5-N8) | Subscribe to `NitroRainController` events | Add calls in a new subscriber or directly in NitroRainController |
| Radar SFX (P8-P9) | `Radar.OnTapped()` and `Radar.OnMissed()` | Add SFXManager calls directly in these methods |
| UI SFX (U1-U15) | Various UI scripts' button callbacks | Add SFXManager calls in button handlers / transition managers |
| Garage SFX (G1-G10) | `GarageController` action methods | Add calls in SetColor, SetSticker, TogglePart, GoLeft/GoRight |
| Turbo/GarageManager/Momentum | State change handlers in each controller | Add SFXManager calls at state transitions |
| Chase success/fail (P6-P7) | `PoliceCatchController.OnChaseEnded` | Subscribe a new handler or extend PoliceChaseFeedbackController |
| Background music (M1-M6) | Scene lifecycle (Start, OnSceneLoaded) | Add a MusicManager singleton alongside SFXManager |

### 6.4 Safest Implementation Order

1. **Phase 1: Background Music (M1)** — Biggest perceived polish gain. Add MusicManager, one ambient track looping in Main scene.
2. **Phase 2: Chest Open Scene (C1-C8)** — Greatest player-facing reward loop. Add SFX calls directly in ChestOpenSceneController.
3. **Phase 3: Police Chase clips (P1-P7)** — Architecture is done. Just create/assign clips to PoliceChaseFeedbackController.
4. **Phase 4: Nitro Coin + Radar (N1, P8-P9)** — Frequent interactions, easy to wire.
5. **Phase 5: Boost Mode (B1-B4)** — Create BoostAudioController or add calls via event subscriptions.
6. **Phase 6: UI polish (U1-U15)** — Low effort, broad impact on feel.
7. **Phase 7: Nitro Rain + Magnet (N5-N12)** — More complex (loops, voice limiting).
8. **Phase 8: Garage (G1-G11)** — Separate scene, lower priority.
9. **Phase 9: Remaining nice-to-haves** — Momentum ticks, car evolution, cinematic music.

### 6.5 Architecture Recommendations

| Topic | Recommendation |
|-------|---------------|
| **Settings persistence** | Add `SFXVolume` (0–1) and `MusicVolume` (0–1) PlayerPrefs keys. Expose in a settings popup. |
| **AudioMixer** | Strongly recommended. Create a Unity AudioMixer with groups: Master → Music, SFX, UI, AmbientLoop. This enables ducking via snapshots. |
| **Event bus vs direct calls** | For small project, direct `SFXManager.Instance.PlayXxx()` calls are fine. No need for a complex event bus. |
| **Animation Events** | The chest open scene and cinematic systems use DOTween, not Animator — audio should be triggered from code (DOTween OnComplete/AppendCallback), NOT animation events. |
| **Scene transitions** | SFXManager survives scene changes (DontDestroyOnLoad). MusicManager should too. Music should crossfade on scene load. |
| **Clip memory** | 14 small MP3s is negligible. Even adding 50-80 clips (at 50-200KB each) adds <10MB. No streaming needed for mobile. |

---

## 7. FINAL MASTER CHECKLIST

| # | Feature | Trigger | Sound Type | Style Suggestion | Existing / Missing | Priority | Implementation Point |
|---|---------|---------|-----------|-----------------|-------------------|----------|---------------------|
| 1 | Background Music (Main) | Scene loaded | Music loop | Chill lo-fi/electronic, 100 BPM, 60-120s | **Missing** | Critical | New MusicManager singleton |
| 2 | Background Music (Garage) | Scene loaded | Music loop | Showroom ambient, 60-90s | **Missing** | Important | MusicManager OnSceneLoaded |
| 3 | Background Music (ChestOpen) | Scene loaded | Music loop | Mystical pad, 30-60s | **Missing** | Important | MusicManager OnSceneLoaded |
| 4 | Background Music (Cinematic) | Cinematic starts | Music loop | Dramatic pump-up, 15-30s | **Missing** | Critical | MusicManager + CarShowcaseDirector |
| 5 | Car Tap | Tap on car | SFX one-shot | 11 variants, add pitch variation | **Exists** ✅ | — | TapInputRaycaster |
| 6 | Building Buy | Building purchased | SFX one-shot | Cash register / build | **Exists** ✅ | — | BuildingButton |
| 7 | Upgrade | Upgrade purchased | SFX one-shot | Level-up ding | **Exists** ✅ | — | UpgradeButton |
| 8 | Card Upgrade | Card leveled up | SFX one-shot | Reuses upgrade clip | **Exists** ✅ | — | CardDetailPopupController |
| 9 | Goal Complete | Goal/mission done | SFX one-shot | Completion jingle | **Exists** (no caller) ⚠️ | Important | Wire to BlacklistManager |
| 10 | Chest Drop (Intro) | ChestOpenScene start | Layered SFX | Magical whoosh + thud | **Missing** | Critical | ChestOpenSceneController.PlayIntro() |
| 11 | Chest Hop (pre-open) | Taps 1-3 | SFX one-shot | Wood/metal tap + rattle | **Missing** | Critical | ChestOpenSceneController.PlayTapFeedback() |
| 12 | Chest Lid Open | Tap 3 → lid rotates | Layered SFX | Hinge creak + golden burst | **Missing** | Critical | ChestOpenSceneController.OpenLid() |
| 13 | Reveal: Money | Auto after lid | Stinger | Coin cascade + ascending notes | **Missing** | Critical | ChestOpenSceneController.OnLidOpened() |
| 14 | Reveal: Nitro | Tap 4 | Stinger | Electric/tech chime | **Missing** | Critical | OnChestTapped() → Reveal_Nitro |
| 15 | Reveal: Card | Tap 5 | Stinger | Card flip + shimmer | **Missing** | Critical | OnChestTapped() → Reveal_Card |
| 16 | Reveal: Sticker | Tap 6 (if exists) | Stinger | Sticker slap + sparkle | **Missing** | Important | OnChestTapped() → Reveal_Sticker |
| 17 | Summary | After all reveals | Musical accent | Soft completion jingle | **Missing** | Important | revealController.ShowSummary() |
| 18 | World Card Animation | Card rises from chest | SFX one-shot | Card swoosh, 0.3s | **Missing** | Important | SpawnWorldCard() |
| 19 | Nitro Coin Collect (tap) | Tap on world coin | SFX one-shot | Bell/chime pling, 0.15s | **Missing** | Critical | NitroCoin.OnTapped() |
| 20 | Nitro Coin Collect (magnet) | Magnet pull completes | SFX one-shot | Softer pling | **Missing** | Critical | NitroCoin magnet callback |
| 21 | Boost Ready | Charge bar full | SFX one-shot | Charged ding + shimmer | **Missing** | Critical | BoostModeController.OnBoostReady |
| 22 | Boost Activate | Boost starts | Layered SFX | Turbo ignition whoosh | **Missing** | Critical | BoostModeController.OnBoostStarted |
| 23 | Boost Active Loop | During boost | Loop | Sustained turbo hum | **Missing** | Important | New looping AudioSource |
| 24 | Boost End | Boost expires | SFX one-shot | Power-down whoosh | **Missing** | Critical | BoostModeController.OnBoostEnded |
| 25 | Nitro Rain Start | Rain begins | Stinger + Loop start | Whoosh + shimmer loop | **Missing** | Critical | NitroRainController.OnRainStarted |
| 26 | Nitro Rain Ambient | During rain | Loop | Coin rain patter | **Missing** | Critical | While NitroRainController.IsRaining |
| 27 | Nitro Rain End | Rain stops | SFX one-shot | Fade-out whoosh | **Missing** | Important | NitroRainController.OnRainEnded |
| 28 | Nitro Magnet On | Shield activates | SFX one-shot | Energy field hum-on | **Missing** | Important | NitroMagnetController |
| 29 | Nitro Magnet Pull | Per-coin attraction | SFX one-shot | Magnetic zip, 0.1s | **Missing** | Important | NitroCoin.TransitionToPull() |
| 30 | Nitro Magnet Off | Shield expires | SFX one-shot | Power-down, 0.3s | **Missing** | Nice-to-have | NitroMagnetController |
| 31 | Chase Stinger | Chase starts | Stinger | Alarm burst + bass hit | **Missing** (clip needed) | Critical | PoliceChaseFeedbackController |
| 32 | Chase Loop | During chase | Loop | High-BPM percussive | **Missing** (clip needed) | Critical | PoliceChaseFeedbackController |
| 33 | Heartbeat | During chase | Loop | Clean heartbeat thump | **Missing** (clip needed) | Critical | PoliceChaseFeedbackController |
| 34 | Police Siren | During chase | Loop | European police wail | **Missing** (clip needed) | Critical | PoliceChaseFeedbackController |
| 35 | Engine Roar | During chase | Loop | Stressed engine rev | **Missing** (clip needed) | Important | PoliceChaseFeedbackController |
| 36 | Chase Success | Player escaped | Stinger | Victory sting, bright | **Missing** | Critical | PoliceCatchController → new handler |
| 37 | Chase Failure | Player caught | Stinger | Failure sting, minor | **Missing** | Critical | PoliceCatchController → new handler |
| 38 | Radar Defused | Tap radar | SFX one-shot | Electronic zap/disable | **Missing** | Critical | Radar.OnTapped() |
| 39 | Radar Missed | Radar passes | SFX one-shot | Camera shutter + alarm | **Missing** | Critical | Radar.OnMissed() |
| 40 | Radar Popup | Snapshot shows | SFX one-shot | Photo develop sound | **Missing** | Important | RadarPopupController.ShowSnapshot() |
| 41 | Popularity Stage Up | Stage boundary | SFX one-shot | Escalation warning tone | **Missing** | Important | PopularityManager stage check |
| 42 | Turbo Finger Activate | 50-tap threshold | Stinger | Snap + power-up, 0.3s | **Missing** | Important | TurboFingerController |
| 43 | Turbo Finger Deactivate | Window ends | SFX one-shot | Subtle power-down | **Missing** | Nice-to-have | TurboFingerController |
| 44 | Garage Manager Activate | MPS boost starts | SFX one-shot | Wrench clink | **Missing** | Important | GarageManagerController |
| 45 | Garage Manager Deactivate | Boost expires | SFX one-shot | Soft wind-down | **Missing** | Nice-to-have | GarageManagerController |
| 46 | Momentum Stack Tick | Per-click stack build | SFX one-shot | Ascending tick/ping | **Missing** | Important | MomentumController.RegisterClick() |
| 47 | Momentum Reset | Stack timeout | SFX one-shot | Descending tone | **Missing** | Nice-to-have | MomentumController.Update() |
| 48 | Car Evolution | Stage 0→1→2 | Stinger | Level-up fanfare | **Missing** | Important | CarEvolution.ApplyStage() |
| 49 | PitStop Crew Earnings | Offline earnings display | SFX one-shot | Cash register, 0.5s | **Missing** | Important | PitStopCrewController |
| 50 | Bottom Tab Switch | Tab pressed | UI click | Soft "tok", 0.1s | **Missing** | Important | BottomBarController |
| 51 | Panel Open | Panel slides in | SFX one-shot | Fwip/slide, 0.2s | **Missing** | Important | PanelTransitionManager |
| 52 | Panel Close | Panel slides out | SFX one-shot | Reverse fwip, 0.15s | **Missing** | Nice-to-have | PanelTransitionManager |
| 53 | Chest Popup Show | Popup appears | SFX one-shot | Soft pop, 0.15s | **Missing** | Important | ChestPopupController.Show() |
| 54 | Start Unlock | Button pressed | UI click | Lock tick, 0.15s | **Missing** | Important | ChestPopupController button |
| 55 | Open Now (NC) | Skip button pressed | SFX one-shot | Coin spend + whoosh | **Missing** | Important | ChestPopupController button |
| 56 | Chest Timer Done | Slot → Ready | SFX one-shot | Ding + sparkle, 0.3s | **Missing** | Important | ChestSlotUI |
| 57 | World Chest Collect | Chest tapped on road | SFX one-shot | Loot pickup whoosh | **Missing** | Important | Chest.OnTapped() |
| 58 | Daily Free Claimed | Free slot pressed | SFX one-shot | Gift pling, 0.3s | **Missing** | Important | DailyOffersController |
| 59 | Daily Pack Purchased | Slot2/3 buy | SFX one-shot | Purchase confirm, 0.2s | **Missing** | Important | DailyOffersController |
| 60 | BL Reward Collect | Collect pressed | SFX one-shot | Collect sting, 0.4s | **Missing** | Important | RewardPopupController |
| 61 | BL Mission Complete | Mission done | Stinger | Achievement jingle, 0.5s | **Missing** | Important | BlacklistManager / MissionRowUI |
| 62 | BL Tier Advanced | Tier up | Stinger | Ascending fanfare, 1s | **Missing** | Critical | BlacklistManager.OnTierChanged |
| 63 | Reward Popup Appears | Popup open | SFX one-shot | Brief fanfare start | **Missing** | Important | RewardPopupController.Show() |
| 64 | Garage Car Switch | GoLeft / GoRight | SFX one-shot | Digital glitch swoosh | **Missing** | Important | GarageController |
| 65 | Garage Color Applied | Color button | SFX one-shot | Paint spray, 0.2s | **Missing** | Important | GarageController.SetColor() |
| 66 | Garage Sticker Applied | Sticker button | SFX one-shot | Vinyl slap, 0.15s | **Missing** | Important | GarageController.SetSticker() |
| 67 | Garage Part On | Part toggle | SFX one-shot | Mechanical snap, 0.1s | **Missing** | Important | GarageController.TogglePart() |
| 68 | Garage Purchase | Buy confirm | SFX one-shot | Cash register, 0.3s | **Missing** | Important | GarageBuyPopupController |
| 69 | Garage Locked Shake | Customize locked car | SFX one-shot | Lock rattle / buzz | **Missing** | Nice-to-have | GarageController |
| 70 | Small Investment Cashback | Refund on spend | SFX one-shot | Soft cha-ching | **Missing** | Nice-to-have | SmallInvestmentController |
| 71 | Cinematic Per-Shot SFX | Each CinematicShotSO | SFX one-shot | Whoosh/rev/screech | **Exists** (needs clips) ⚠️ | Important | CinematicShotSO.sfxClip |
| 72 | Cinematic Name Reveal | Text slide-in | SFX one-shot | Text impact whoosh | **Missing** | Important | ShowcaseCarNameReveal.Play() |
| 73 | Boost Nitro Deposit | Charge accepted | SFX one-shot | Deposit ding | **Missing** | Important | BoostModeController.OnNitroChargeAccepted |
| 74 | Nitro Rain Delay Start | Threshold → delay | SFX one-shot | Thunder rumble, 0.5s | **Missing** | Important | NitroRainController.OnDelayStarted |
| 75 | FX: Electric Arc | Arc VFX during magnet | Loop | Electric crackle | **Missing** | Nice-to-have | ArcLineVFX |
| 76 | BuildingPanel Open | Panel opened | SFX one-shot | Soft slide | **Missing** | Nice-to-have | BuildingPanelController |
| 77 | Card Collection Open | Card grid shown | SFX one-shot | Card shuffle | **Missing** | Nice-to-have | CardCollectionUI |
| 78 | Coin Spawn (world) | Nitro coin appears | SFX one-shot | Subtle twinkle | **Missing** | Nice-to-have | NitroCoinSpawner |
| 79 | Chest Spawn (world) | Chest appears on road | SFX one-shot | Soft magical jingle | **Missing** | Nice-to-have | ChestSpawner |
| 80 | Heat Threshold Warning | Near chase trigger | SFX one-shot | Subtle alarm ping | **Missing** | Nice-to-have | AmbientHeatManager |

---

### SUMMARY STATISTICS

| Category | Count |
|----------|-------|
| **Existing & working** | 5 trigger points (car tap, building buy, upgrade ×2, + unused GoalComplete) |
| **Existing architecture, needs clips** | 7 AudioSource/Clip fields in PoliceChaseFeedbackController + CinematicShotSO |
| **Missing: Critical priority** | 26 items |
| **Missing: Important priority** | 38 items |
| **Missing: Nice-to-have** | 16 items |
| **Total unique sound entries needed** | ~80 |
| **Estimated unique clip assets needed** | ~50-60 (some share clips, tap variants count as one) |

### AUDIO ASSET BUDGET ESTIMATE

| Category | Clip Count | Approx Size |
|----------|-----------|-------------|
| Music tracks (4 scenes) | 4 loops | 4-8 MB |
| Chase system (5 layers) | 5 clips | 1-2 MB |
| Chest open (8 oneshots + 1 ambient) | 9 clips | 0.5-1 MB |
| Nitro (coin, rain, magnet) | 6-8 clips | 0.3-0.5 MB |
| Boost (ready, activate, loop, end) | 4 clips | 0.3-0.5 MB |
| Radar + police outcome | 4 clips | 0.2 MB |
| UI clicks + popups | 5-8 clips | 0.1-0.2 MB |
| Garage (switch, color, sticker, part, buy) | 6-8 clips | 0.2-0.3 MB |
| Systems (turbo, momentum, evolution, etc.) | 6-8 clips | 0.2-0.3 MB |
| **Total** | **~50-60 clips** | **~7-13 MB** |

This is well within mobile budget.

---

*End of Audio Design Audit*
