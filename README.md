<h1 align="center">🏎️ Car Clicker — Mobile 3D</h1>

<p align="center">
  A 3D mobile idle / clicker racing game built in <b>Unity 6</b> — tap to earn, build a garage of
  supercars, chase and escape the police, open reward chests, collect power-up cards and climb an
  online leaderboard.
</p>

<p align="center">
  <img alt="Unity" src="https://img.shields.io/badge/Unity-6000.2%20(URP)-000000?logo=unity&logoColor=white">
  <img alt="Language" src="https://img.shields.io/badge/Language-C%23-239120?logo=c-sharp&logoColor=white">
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Android%20%2F%20Mobile-3DDC84?logo=android&logoColor=white">
  <img alt="Backend" src="https://img.shields.io/badge/Backend-Supabase-3ECF8E?logo=supabase&logoColor=white">
  <img alt="Status" src="https://img.shields.io/badge/Status-In%20active%20development-orange">
</p>

<p align="center">
  <img src="docs/images/car-cinematic-front-view.jpg" width="80%" alt="Car cinematic showcase">
</p>

---

## 📖 About

**Car Clicker Mobile 3D** is a feature-rich idle/clicker game where the core tap-to-earn loop is wrapped
around a full 3D car fantasy: a cinematic garage, deep customization, a card-collection meta-game,
police-chase action sequences, and competitive online rankings.

The project is built on **Unity 6 (6000.2)** with the **Universal Render Pipeline (URP)** and a
**ScriptableObject-driven architecture** across ~**150 C# scripts**. Online features (leaderboard /
score submission) are powered by a **Supabase** backend with a TypeScript **Edge Function**.

> This repository is a **source-code showcase**. The heavy game content (3D models, textures, prefabs,
> scenes, audio, VFX) is intentionally kept out of version control to keep the repo lightweight and
> readable — see [Repository layout](#-repository-layout).

<p align="center">
  <img src="docs/images/car-cinematic-side-view.jpg" width="45%" alt="Cinematic side view">
  <img src="docs/images/main-gameplay-chest-spawn.jpg" width="45%" alt="Main gameplay">
</p>

---

## ✨ Features

### 💰 Idle & clicker economy
Tap to generate income, then automate it. Passive **AutoIncome**, upgradeable income **buildings**, a
central **CurrencyManager**, floating click feedback and a fully custom **save system** per subsystem.

### 🚗 Garage & car customization
Browse, purchase and evolve a collection of supercars. Swap **body colors** and **parts**, preview
changes live, and watch cars evolve visually as you progress.

<p align="center">
  <img src="docs/images/car-selection-rx7.jpg" width="30%" alt="Car selection">
  <img src="docs/images/car-customization-preview.jpg" width="30%" alt="Customization">
  <img src="docs/images/car-customization-green-preview.jpg" width="30%" alt="Color preview">
</p>

### 🎁 Chests & card collection (gacha)
Chests spawn during gameplay, are collected into an inventory, and open in a dedicated cinematic
scene. Rewards feed a **card-collection meta-game** — power-up cards (Nitro Rain, Pit-Stop Crew,
Wanted events and more) with rarities, drop-tuning and detail popups.

<p align="center">
  <img src="docs/images/chest-opening-screen.jpg" width="30%" alt="Chest opening">
  <img src="docs/images/rare-chest-reward-popup.jpg" width="30%" alt="Rare reward">
  <img src="docs/images/card-collection-screen.jpg" width="30%" alt="Card collection">
</p>

<p align="center">
  <img src="docs/images/nitro-rain-card-details.jpg" width="30%" alt="Nitro Rain card">
  <img src="docs/images/pit-stop-crew-card-details.jpg" width="30%" alt="Pit-Stop Crew card">
  <img src="docs/images/card-reward-garage-manager.jpg" width="30%" alt="Card reward">
</p>

### 🚔 Police chase & action
Trigger-based **police catch** encounters with chase feedback, roadblocks and escape mechanics that
break up the idle loop with tense action moments.

<p align="center">
  <img src="docs/images/police-chase-gameplay.jpg" width="30%" alt="Police chase">
  <img src="docs/images/police-roadblock-gameplay.jpg" width="30%" alt="Roadblock">
  <img src="docs/images/police-chase-scene-view.jpg" width="30%" alt="Chase scene">
</p>

### 🔥 Boost / Nitro mode
A dedicated **Boost mode** with its own cinematic camera work, **post-processing** stack, VFX faders,
audio layer and collectible **Nitro coins** and **shield** pickups.

<p align="center">
  <img src="docs/images/nitro-boost-effect.jpg" width="30%" alt="Nitro boost">
  <img src="docs/images/nitro-shield-effect.jpg" width="30%" alt="Nitro shield">
  <img src="docs/images/nitro-coin-pickup.jpg" width="30%" alt="Nitro coin">
</p>

### 🎯 Blacklist missions
A tiered **Blacklist** progression (inspired by classic street-racing games): mission definitions,
tiers, reward tables, stat tracking and locked cars unlocked by clearing challenges.

<p align="center">
  <img src="docs/images/blacklist-missions-screen.jpg" width="30%" alt="Blacklist missions">
  <img src="docs/images/blacklist-5-missions-screen.jpg" width="30%" alt="Blacklist tier">
  <img src="docs/images/wanted-card-event.jpg" width="30%" alt="Wanted event">
</p>

### 🏆 Online leaderboard
Global ranking with score submission validated server-side by a **Supabase Edge Function**, plus a
shop, **daily offers**, ad integration hooks, an **environment** system (ambient/heat profiles) and a
guided **tutorial** flow.

<p align="center">
  <img src="docs/images/shop-screen.jpg" width="45%" alt="Shop">
  <img src="docs/images/car-purchase-confirmation-popup.jpg" width="45%" alt="Purchase confirmation">
</p>

---

## 🛠️ Tech stack & architecture

| Area | Details |
|------|---------|
| **Engine** | Unity `6000.2.13f1` (Unity 6.2) |
| **Rendering** | Universal Render Pipeline (URP) + custom shaders & post-processing |
| **Language** | C# (~150 scripts) |
| **Design pattern** | Heavily **ScriptableObject-driven** data (definitions, tiers, themes, tuning) |
| **Backend** | Supabase (PostgreSQL) + TypeScript **Edge Function** for score submission |
| **Persistence** | Custom per-system save data models |
| **Target** | Android / mobile (3D) |

**Gameplay systems** are organized under `Assets/Scripts/`:

```
Scripts/
├── Blacklist/     Tiered mission progression, rewards & stat tracking
├── Cinematic/     Car showcase director & cinematic shot definitions
├── Environment/   Ambient/heat environment profiles
├── Garage/        Cars, customization, evolution, shop popups
├── Ranking/       Online leaderboard client (Supabase)
├── ShaderScripts/ Runtime shader/VFX controllers
├── Tutorial/      Guided onboarding flow
└── (core)         Economy, chests, cards, boost/nitro, police, save, ads…
```

---

## 🗂️ Repository layout

This repo is a **lean showcase**, not a fully buildable clone. It intentionally versions only:

```
Assets/Scripts/     Game source code (C#)
Assets/Shaders/     Custom shaders
Assets/Editor/      Editor tooling
Assets/SO/          ScriptableObject definitions
ProjectSettings/    Unity project configuration
Packages/           Package manifest
supabase/           Backend edge function + leaderboard SQL/tests
docs/               Screenshots, design docs & tooling scripts
```

Heavy binary content — **3D models, textures, prefabs, scenes, audio, fonts, VFX, third-party
plugins** — is deliberately excluded via [`.gitignore`](.gitignore) and preserved in an offline
project backup. As a result the repository stays a few MB in size instead of ~500 MB.

---

## 🚧 Development status

**In active development.** Core gameplay systems are implemented and functional; current work focuses
on **polish and balancing**:

- 🎨 Lighting / visual design pass (in progress)
- 🔊 Audio design & implementation
- 💹 Economy tuning & balance
- 🧭 Tutorial and onboarding text
- 🖥️ UI refinement

Design notes, audits and system guides for these efforts live under [`docs/`](docs/).

---

## 📄 Documentation

The [`docs/`](docs/) folder collects working design & audit documents produced during development —
economy tuning, save-system and audio audits, blacklist/UI inspections, balance change logs and the
audio design document — alongside the screenshot gallery used above.

---

<p align="center"><sub>Built with Unity 6 &amp; C#. Screenshots are from in-development builds and may change.</sub></p>
