# UI_Tutorial Text Plan — CarClickerMobile3D

> Analysis-only document. **No code, prefab, scene, or asset has been modified.**
> Source of truth verified against:
>
> - [Assets/Scripts/Tutorial/TutorialManager.cs](Assets/Scripts/Tutorial/TutorialManager.cs)
> - [Assets/Scripts/Tutorial/TutorialGate.cs](Assets/Scripts/Tutorial/TutorialGate.cs)
> - [Assets/Scripts/Tutorial/TutorialSaveData.cs](Assets/Scripts/Tutorial/TutorialSaveData.cs)
> - [Assets/Scenes/Main.unity](Assets/Scenes/Main.unity) (UI_Tutorial GameObject @ line 42255)

---

## 1. Verified Tutorial Flow vs. Brief

The flow described in the brief is **mostly correct, but understated**. The actual implementation has **far more frames than seven**. The full chain wired in `TutorialManager` is:

| #   | Frame GO    | Type             | Trigger                                                | Dismiss / Advance                                   |
| --- | ----------- | ---------------- | ------------------------------------------------------ | --------------------------------------------------- |
| 1   | `One`       | Welcome dialog   | First scene load, `currentStepIndex == 0`              | Tap anywhere                                        |
| 2   | `Two`       | Finger pointer   | Step 1 dismissed                                       | Player has enough money to afford Street Deals      |
| 3   | `Three`     | Finger pointer   | Step 2 affordability reached                           | Player taps `BottomBar/Btn_Shop&Cards`              |
| 4   | `Four`      | Finger pointer   | Shop & Cards opened                                    | Player buys the first Street Deals building         |
| 5   | `Five`      | Explanatory      | First building purchased                               | Tap anywhere                                        |
| 6   | `Six`       | Explanatory      | First tutorial Nitro Coin reaches center (game frozen) | Tap the coin / tap anywhere                         |
| 7   | `Seven`     | Pointer + dialog | First tutorial Chest reaches center, then ChestSlot    | Tap chest, tap chest-slot                           |
| –   | `Three_New` | Finger pointer   | After 1st free chest opened (re-points to Shop&Cards)  | Player taps Shop&Cards                              |
| 9   | `Nine`      | Finger pointer   | Shop&Cards opened (post-first-chest)                   | Player taps `Btn_TabCards`                          |
| 10  | `Ten`       | Finger pointer   | Cards tab opened                                       | Player taps the earned card slot (CardDetail opens) |
| 11  | `Eleven`    | Explanatory      | Tutorial Radar tapped (with `Twelve`)                  | Tap anywhere (dismisses both)                       |
| 12  | `Twelve`    | Finger pointer   | Tutorial Radar tapped (with `Eleven`)                  | Tap anywhere                                        |
| 13  | `Thirteen`  | Explanatory      | Police chase forced-started                            | Tap anywhere (chase ends elsewhere)                 |
| 14  | `Fourteen`  | Finger pointer   | After chase ends, points at `TopBar/Btn_Garage`        | Player taps Btn_Garage (loads NewGarage scene)      |
| 15  | `Fifteen`\* | Explanatory      | Inside `NewGarage.unity` — NOT under UI_Tutorial here  | Tap anywhere                                        |
| 17  | `Seventeen` | Explanatory      | First time `Panel_BlackList` is opened post-tutorial   | Tap anywhere                                        |

\*`Fifteen` lives in the NewGarage scene, not in `Main.unity/UI_Tutorial`. There are **no `Eight` and no `Sixteen` frames** — those step indices exist only as internal state machines (Eight = Chest collection wait, Sixteen = Radar tap wait).

### UI_Tutorial children present in `Main.unity` (16 total, in order)

`Dim`, `One`, `Two`, `Three`, `Four`, `Five`, `Six`, `Seven`, `Nine`, `Ten`, `Eleven`, `Twelve`, `Thirteen`, `Fourteen`, `Three_New`, `Seventeen`.

### Differences from the brief

- The brief stops at "Step Seven (chest)". Reality has **8 additional frames** (`Three_New`, `Nine`, `Ten`, `Eleven`, `Twelve`, `Thirteen`, `Fourteen`, `Seventeen`) that the player will actually see — they all need text decisions too.
- `Six` is shown on first **tutorial** Nitro Coin (force-spawned), but the brief said "when first nitro reaches center" — confirmed.
- `Seven` is reused twice: once for "tap the chest" pointer, then immediately re-purposed for "tap the chest slot" — visually it's a single GameObject, but conceptually two beats.
- The **first three Common Chests are free** (`TutorialFreeChestQuota = 3`) — relevant for `Seven` copy.
- `Eleven` + `Twelve` are shown **simultaneously** (paired dialog + finger pointer).
- Step 5 is when the BottomBar fully unlocks for Bank/Blacklist/Ranking only after Garage tutorial completes (`fifteenDismissed`). Step 5 itself only unlocks Clicker.

---

## 2. Per-Frame Text Plan

> **Text style rules applied:** ≤ 6 words for titles, ≤ 14 words per body line, max 2 body lines, no jargon, action-first verbs, motivating tone.

---

### Step 1 — `One` (Welcome dialog)

- **What happens:** First-launch popup over a dimmed screen. All UI is locked.
- **Player action:** Tap anywhere to continue.
- **Visible text?** Yes — title + body + "Tap to continue" hint.
- **TR — Title:** `Hoş Geldin, Sürücü!`
- **TR — Body:** `Şehrin yeni patronu sensin. Hadi imparatorluğunu kuralım.`
- **TR — Hint:** `Devam etmek için dokun`
- **EN — Title:** `Welcome, Driver!`
- **EN — Body:** `Your street empire starts now. Let's build it together.`
- **EN — Hint:** `Tap to continue`
- **Mobile-short alt:** Title `Welcome!` / Body `Your empire starts here.` / Hint `Tap to continue`
- **Notes:** Authored as a centered explanatory frame. Already has a dim layer (`Dim`). Add a continue hint at the bottom; don't use a button — the whole screen dismisses on any tap (matches `WasAnyPointerPressedThisFrame`).

---

### Step 2 — `Two` (Pointer at the car)

- **What happens:** Big finger pointer on the active car. Player must tap the car repeatedly until they can afford the first building.
- **Player action:** Tap the car to earn cash.
- **Visible text?** Yes — short instruction. Pure pointer would feel empty for first-time players.
- **TR — Title:** `Arabaya Dokun!`
- **TR — Body:** `Para kazanmak için arabaya dokunmaya devam et.`
- **EN — Title:** `Tap the Car!`
- **EN — Body:** `Keep tapping to earn your first cash.`
- **Mobile-short alt:** Title `Tap the Car` / Body `Earn cash by tapping.`
- **Notes:** Frame loops with a pulse + bounce (`stepTwoPulseScalePercent`, `stepTwoBounceDistance`) — keep text **short** so it doesn't fight the bounce animation. Place text **below or to the side** of the finger sprite, never under it.

---

### Step 3 — `Three` (Pointer to Shop & Cards tab)

- **What happens:** Finger pointer on `BottomBar/Btn_Shop&Cards`.
- **Player action:** Tap Shop & Cards.
- **Visible text?** Yes — one short instruction.
- **TR — Title:** `Mağazayı Aç`
- **TR — Body:** `İlk binanı satın almak için Mağazaya gir.`
- **EN — Title:** `Open the Shop`
- **EN — Body:** `Tap Shop & Cards to buy your first building.`
- **Mobile-short alt:** Title `Open Shop` / Body `Tap Shop & Cards.`
- **Notes:** Pointer hovers over BottomBar; place text **above** the finger so it's not covered by the bar. Keep total text height ≤ 120 px at 1080×1920 ref.

---

### Step 4 — `Four` (Pointer to "Street Deals" buy button)

- **What happens:** Inside Shop & Cards panel, finger points at the first building's BUY button.
- **Player action:** Buy "Street Deals" (the first building).
- **Visible text?** Yes — one motivational instruction.
- **TR — Title:** `İlk Binanı Al`
- **TR — Body:** `Street Deals binası sana pasif gelir kazandırır.`
- **EN — Title:** `Buy Your First Building`
- **EN — Body:** `Street Deals earns money for you, even idle.`
- **Mobile-short alt:** Title `Buy Street Deals` / Body `Earn money while idle.`
- **Notes:** This pointer sits inside an active scrollable shop panel — make sure the text label is part of `Four`'s overlay (CanvasGroup-driven) and **not** parented to the building row.

---

### Step 5 — `Five` (Explanatory frame after first purchase)

- **What happens:** Centered explanatory popup. BottomBar's Clicker button gets unlocked here.
- **Player action:** Read, then tap anywhere.
- **Visible text?** Yes — title + body + tap hint. This is a teaching beat.
- **TR — Title:** `Harika İş!`
- **TR — Body:** `Binalar sana sürekli para kazandırır.\nKartlar ise gelirini ve hızını yükseltir.`
- **TR — Hint:** `Devam etmek için dokun`
- **EN — Title:** `Nice Work!`
- **EN — Body:** `Buildings earn money over time.\nCards boost your income and speed.`
- **EN — Hint:** `Tap to continue`
- **Mobile-short alt:** Title `Nice Work!` / Body `Buildings earn idle. Cards make you stronger.` / Hint `Tap to continue`
- **Notes:** This is the **only "lecture" frame** — keep it under 2 lines. Don't explain Nitro/Chest/Radar yet; those have their own frames.

---

### Step 6 — `Six` (First Nitro Coin — gameplay frozen)

- **What happens:** Gameplay is frozen, the first tutorial Nitro Coin floats to screen-center, finger points at it. `Premium` topbar slot reveals.
- **Player action:** Tap the coin.
- **Visible text?** Short callout — keep it tiny so it doesn't compete with the coin VFX.
- **TR — Title:** `Nitro Topla!`
- **TR — Body:** `Şu altın parayı yakala — büyük ödül kazandırır.`
- **EN — Title:** `Grab the Nitro!`
- **EN — Body:** `Tap the gold coin to claim a big reward.`
- **Mobile-short alt:** Title `Grab Nitro!` / Body `Tap the coin.`
- **Notes:** Place text **above** the coin since the player's finger will be on the coin. Avoid heavy backgrounds — this beat is visually busy.

---

### Step 7 — `Seven` (Chest intro — used twice)

- **What happens — Beat A:** After 3 Nitros collected, `chestUnlocked` → a tutorial Common Chest is force-spawned. Gameplay freezes when it reaches center; finger points at the chest.
- **What happens — Beat B:** Player taps chest → it goes into Chest inventory; `Seven` re-points to the new ChestSlot icon. First three chests are FREE and instantly openable. ChestPopup shows "Open (Free)".
- **Player action:** Tap chest → tap chest slot → opens ChestPopup → `Open (Free)`.
- **Visible text?** Yes — but the two beats need different sub-texts. Two text states are required (toggle via TutorialManager).

#### Beat A (chest in world)

- **TR — Title:** `Sandığı Yakala!`
- **TR — Body:** `Sandığa dokun, envanterine eklensin.`
- **EN — Title:** `Catch the Chest!`
- **EN — Body:** `Tap the chest to add it to your inventory.`

#### Beat B (chest slot pointer)

- **TR — Title:** `Sandığı Aç`
- **TR — Body:** `Slota dokun. İlk 3 sandık ücretsiz!`
- **EN — Title:** `Open the Chest`
- **EN — Body:** `Tap the slot. The first 3 chests are FREE!`

- **Mobile-short alt (B):** Title `Open Chest` / Body `First 3 are FREE!`
- **Notes:** Recommend **two TMP_Text states inside `Seven`** (or two child sibling text objects: `TXT_Body_A`, `TXT_Body_B` — one active per beat). Mention "FREE" — it explains the missing cost timer.

---

### Step 9 — `Three_New` (Re-pointer to Shop & Cards after first chest)

- **What happens:** Player returns from ChestOpenScene with a card reward; pointer re-shows on Shop & Cards.
- **Player action:** Tap Shop & Cards.
- **Visible text?** Yes — but slightly different from Step 3.
- **TR — Title:** `Yeni Kartını Gör`
- **TR — Body:** `Mağazaya gir ve yeni kazandığın kartı incele.`
- **EN — Title:** `Check Your New Card`
- **EN — Body:** `Open the shop to see the card you just won.`
- **Mobile-short alt:** Title `Your New Card!` / Body `Open the Shop.`
- **Notes:** Visually identical to `Three` but **must read differently** — it's a second reveal, not a "first open". If both must share an image asset, keep image generic (just finger) and put the new copy in TMP_Text.

---

### Step 9 (real number) — `Nine` (Pointer to Btn_TabCards)

- **What happens:** Inside Shop&Cards, finger points at the Cards tab (which was locked until now).
- **Player action:** Tap Cards tab.
- **Visible text?** Yes — one short line.
- **TR — Title:** `Kartlar Sekmesi`
- **TR — Body:** `Kartlarını görmek için Kartlar sekmesine dokun.`
- **EN — Title:** `Cards Tab`
- **EN — Body:** `Tap the Cards tab to view your collection.`
- **Mobile-short alt:** Title `Cards Tab` / Body `Tap to open.`
- **Notes:** `cardsTabUnlockStepIndex = 4` in `TutorialManager`, so the Cards tab is interactable here. Pointer-only would be ambiguous; keep the label.

---

### Step 10 — `Ten` (Pointer to earned card slot)

- **What happens:** Inside Cards tab; pointer is dynamically positioned over the slot of the card the player won. All other slots are **suppressed** (non-interactable).
- **Player action:** Tap that specific card slot → opens CardDetailPopup.
- **Visible text?** Yes — short, instructive.
- **TR — Title:** `Yeni Kartını İncele`
- **TR — Body:** `Detayları görmek için karta dokun.`
- **EN — Title:** `Inspect Your Card`
- **EN — Body:** `Tap the card to see its stats.`
- **Mobile-short alt:** Title `Inspect Card` / Body `Tap the card.`
- **Notes:** Pointer is positioned by `CardCollectionUI` resolution at runtime → text container should be a sibling, NOT a child of the finger. After the popup closes, Step 12 internally tells player to press Clicker, but **no dedicated frame exists** for that step. Either accept that (Clicker BottomBar is already pulsing in current design) **or** add a brief toast — recommend leaving it textless and keep Clicker tab pulsing animation as the cue.

---

### Step 11 — `Eleven` (Radar dialog — paired with `Twelve`)

- **What happens:** Player tapped the tutorial Radar; explanatory dialog appears.
- **Player action:** Read, tap anywhere (dismisses both `Eleven` and `Twelve`).
- **Visible text?** Yes — this is a teaching frame.
- **TR — Title:** `Dikkat — Radar!`
- **TR — Body:** `Radarlar polisi tetikler ve seni kovalamaca başlatır.`
- **TR — Hint:** `Devam etmek için dokun`
- **EN — Title:** `Watch Out — Radar!`
- **EN — Body:** `Radars trigger the police and start a chase.`
- **EN — Hint:** `Tap to continue`
- **Mobile-short alt:** Title `Radar!` / Body `Triggers the police.` / Hint `Tap to continue`
- **Notes:** Keep title punchy because `Twelve` (the pointer) is on screen at the same time and competes for attention.

---

### Step 12 — `Twelve` (Pointer to the Radar — paired with `Eleven`)

- **What happens:** Finger pointer over the radar visual.
- **Player action:** Same dismiss as `Eleven`.
- **Visible text?** **Pointer-only is acceptable** — the dialog text lives in `Eleven`. **Recommend: leave `Twelve` textless.**
- **Recommended text:** _(none)_
- **Notes:** Avoid duplicating text on both. If a label is desired, use a single word like `Radar` / `Radar` in the same colour as the dialog accent.

---

### Step 13 — `Thirteen` (Police chase warning)

- **What happens:** Police chase has just been force-started; explanatory dialog overlays.
- **Player action:** Read, tap anywhere. Chase continues normally afterwards.
- **Visible text?** Yes — important survival instruction.
- **TR — Title:** `Polis Peşinde!`
- **TR — Body:** `Yakalanmamak için sür ve kaç. Popülerlik düşer ama hayatta kalırsın.`
- **TR — Hint:** `Devam etmek için dokun`
- **EN — Title:** `Cops on Your Tail!`
- **EN — Body:** `Drive to escape. You'll lose popularity, but stay free.`
- **EN — Hint:** `Tap to continue`
- **Mobile-short alt:** Title `Cops!` / Body `Escape to stay free.` / Hint `Tap to continue`
- **Notes:** Strong action verbs, must be readable in < 2 seconds because the chase camera is moving.

---

### Step 14 — `Fourteen` (Pointer to Btn_Garage)

- **What happens:** After chase ends, `Btn_Garage` reveals from compact TopBar; pointer finger lands on it. Input is blocked everywhere except Btn_Garage by `fourteenInputBlocker`.
- **Player action:** Tap Btn_Garage → loads `NewGarage` scene.
- **Visible text?** Yes — concise pointer label.
- **TR — Title:** `Garaja Git`
- **TR — Body:** `Arabanı geliştirmek için Garaja dokun.`
- **EN — Title:** `Visit the Garage`
- **EN — Body:** `Tap Garage to upgrade your car.`
- **Mobile-short alt:** Title `Garage` / Body `Tap to upgrade.`
- **Notes:** `Fourteen` lives in TopBar zone — text should be **below** the finger so it doesn't clip out of safe area on tall devices.

---

### Step 15 — (Lives in NewGarage scene, not here)

- Out of scope of `UI_Tutorial` in `Main.unity`. The brief did not list it. Recommended copy if/when reviewed:
  - **TR:** `Bu senin garajın. Buradan araba seçer ve yükseltirsin.` / `Tap to continue`
  - **EN:** `This is your garage. Pick and upgrade your cars here.` / `Tap to continue`

---

### Step 17 — `Seventeen` (First Blacklist visit)

- **What happens:** First time `Panel_BlackList` is opened **after** Garage tutorial completes.
- **Player action:** Read, tap anywhere.
- **Visible text?** Yes — explanatory.
- **TR — Title:** `Kara Liste`
- **TR — Body:** `Hedef arabaları yakala, ödülleri kap. Görevlere başla!`
- **TR — Hint:** `Devam etmek için dokun`
- **EN — Title:** `The Blacklist`
- **EN — Body:** `Hunt target cars and grab their rewards.`
- **EN — Hint:** `Tap to continue`
- **Mobile-short alt:** Title `Blacklist` / Body `Hunt cars. Earn rewards.` / Hint `Tap to continue`
- **Notes:** Tone shift toward a "hunt" feel matches Blacklist mission flavor.

---

## 3. Frames That Should Stay Textless

| Frame    | Why                                                                                   |
| -------- | ------------------------------------------------------------------------------------- |
| `Twelve` | Paired with `Eleven` (dialog already explains). Two competing texts hurt readability. |
| `Dim`    | Pure background dim. Never holds text.                                                |

All **other** frames should carry text. Pure-pointer frames in a first-time onboarding always cause measurable drop-off on mobile if no labels are given.

---

## 4. Missing TMP_Text Components — Where to Add

Based on the scene scan, none of the tutorial frames currently have authored tutorial copy — many of the matches we saw inside their line ranges are **unrelated UI elements that happen to live in adjacent YAML blocks** (e.g. building cost labels, MPS readouts). Treat all frames as needing fresh TMP_Text children.

Recommended additions (one set per frame, **NOT to be implemented yet**):

| Frame       | TMP_Texts to add                                         |
| ----------- | -------------------------------------------------------- |
| `One`       | `TXT_Title`, `TXT_Body`, `TXT_Hint`                      |
| `Two`       | `TXT_Title`, `TXT_Body`                                  |
| `Three`     | `TXT_Title`, `TXT_Body`                                  |
| `Four`      | `TXT_Title`, `TXT_Body`                                  |
| `Five`      | `TXT_Title`, `TXT_Body`, `TXT_Hint`                      |
| `Six`       | `TXT_Title`, `TXT_Body`                                  |
| `Seven`     | `TXT_Title`, `TXT_Body_A`, `TXT_Body_B` (toggle by beat) |
| `Three_New` | `TXT_Title`, `TXT_Body`                                  |
| `Nine`      | `TXT_Title`, `TXT_Body`                                  |
| `Ten`       | `TXT_Title`, `TXT_Body`                                  |
| `Eleven`    | `TXT_Title`, `TXT_Body`, `TXT_Hint`                      |
| `Twelve`    | _(none — keep pointer-only)_                             |
| `Thirteen`  | `TXT_Title`, `TXT_Body`, `TXT_Hint`                      |
| `Fourteen`  | `TXT_Title`, `TXT_Body`                                  |
| `Seventeen` | `TXT_Title`, `TXT_Body`, `TXT_Hint`                      |

---

## 5. TMP_Text vs. Baked-Image Text — Recommendation

**Use TMP_Text everywhere. Do not bake text into image assets.** Reasons:

1. The project already standardises on TMP (font asset GUID `8f586378b4e144a9851e7b34d9b748ee` is shared across UI).
2. Localisation: TR + EN are required already, more languages are likely. Baked images would multiply asset count by language count.
3. Step 7 has **two text states** in one frame — only TMP can swap at runtime cheaply.
4. Text balance / line-breaks differ between TR and EN (TR is ~15-20% longer). TMP auto-sizing solves it.
5. Designers can A/B copy without re-exporting art.

The **only** place baked text is acceptable is decorative stylised words on the pointer's tail (e.g. small "TAP!" stickers) — but those are art, not tutorial copy, and should still not carry meaning that gameplay depends on.

---

## 6. Naming Convention (Final Recommendation)

```
<Frame>/
  Background                (Image, optional)
  Finger                    (Image, pointer art)
  TXT_Title                 (TMP_Text — bold, larger size)
  TXT_Body                  (TMP_Text — body, regular weight)
  TXT_Hint                  (TMP_Text — small italic, "Tap to continue")
  TXT_Button                (TMP_Text — only if a real Button child is added later)
```

Rules:

- Prefix all tutorial text objects with `TXT_`.
- Use PascalCase suffixes: `TXT_Title`, `TXT_Body`, `TXT_BodyA`, `TXT_BodyB`, `TXT_Hint`, `TXT_Button`.
- Keep text objects **siblings** of the finger sprite, not children — finger position is animated independently (loop pulse / bounce).
- Each `TXT_*` should have its own `RectTransform` anchored independently so the bounce on the parent doesn't pulse the text.

---

## 7. Localisation Strategy

**Yes, localise.** Recommended approach:

- Add a single `TutorialStrings` static class (or ScriptableObject) keyed by enum:
  `TutorialStringId.OneTitle`, `OneBody`, `OneHint`, `TwoTitle`, ... `SeventeenHint`.
- Each entry holds `tr` and `en` strings.
- Put `TutorialStringBinder` MonoBehaviour on each `TXT_*` referencing the enum + a slot type (`Title`/`Body`/`Hint`).
- On `Awake`, the binder reads the current language from a `LanguageService` (currently absent — add when needed; for now wire to `Application.systemLanguage` with TR/EN fallback to EN).
- This avoids the typical anti-pattern of typing strings directly into the scene.

If full localisation infra is out of scope short-term, **at minimum** ship strings with both TR and EN as serialized fields on a single `TutorialCopy` ScriptableObject, then bind on enable. Migrating later to a proper system stays cheap.

---

## 8. Placement & Readability Checklist (per frame)

- Min font size mobile body text: **36 px** at 1080×1920 ref resolution.
- Min title size: **52 px**.
- Text container width should never exceed **820 px** (≈ 75% of screen) to avoid edge clipping in safe area.
- Always 24–32 px padding from the finger sprite — text must not be obscured by the bouncing finger.
- Use a soft **dark backplate** (rounded rect, alpha ~0.55) behind text on busy frames (`Six`, `Seven`, `Fourteen`) so text remains legible over gameplay.
- Hint line ("Tap to continue") sits **bottom-anchored** with 60 px from the bottom of the dim screen.
- For paired frames (`Eleven`+`Twelve`), text only on `Eleven`.

---

## 9. Open Questions for Design Sign-off

1. Should `Five` mention "Cards" by name or just say "Upgrades"? Plan above uses "Cards" to teach correct terminology early.
2. Step 12 (after Cards tab tutorial) currently has **no dedicated frame** — should we add one ("Press Clicker to keep playing") or rely on the pulsing Clicker tab? Plan recommends staying textless to avoid frame inflation.
3. Should `Seven` Beat-B explicitly mention "FREE" pricing? Plan says **yes** — it explains the missing timer/cost UI and reduces support tickets.
4. `Seventeen` text tone — keep the "Hunt" / "kovalamaca" metaphor or use neutral mission language? Plan uses the hunt tone.
5. Brand vs. literal: keep "Street Deals" as a proper name in TR text or translate? Plan keeps it untranslated as a brand.

---

## 10. Summary

- **15 active tutorial frames** in `Main.unity` (`UI_Tutorial`), plus `Fifteen` in `NewGarage.unity`.
- **14 frames need TMP_Text**; `Twelve` should remain pointer-only.
- **No tutorial copy exists in the scene yet** — the placeholder strings observed in nearby YAML blocks belong to other UI systems (cost readouts, building names) and are not authored tutorial content.
- **Use TMP_Text + ScriptableObject-backed localisation**, not baked images.
- **Naming standard:** `TXT_Title`, `TXT_Body`, `TXT_Hint`, `TXT_Button`.
- Recommended copy is provided in TR + EN with mobile-short alts; ready for implementation in a follow-up pass.
