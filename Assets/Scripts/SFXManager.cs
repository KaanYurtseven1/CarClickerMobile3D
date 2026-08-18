using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Audio Sources")]
    public AudioSource sfxSource;

    [Header("Loop Audio Source (auto-created if null)")]
    [Tooltip("Dedicated AudioSource for looping SFX (rain, boost hum, etc). Auto-created at runtime if not assigned.")]
    public AudioSource loopSFXSource;

    // ═══════════════════════════════════════════════════
    // EXISTING CLIPS
    // ═══════════════════════════════════════════════════

    [Header("Tap SFX (multiple)")]
    public AudioClip[] carTapClips;

    [Header("Single SFX")]
    public AudioClip buildingBuyClip;
    public AudioClip goalCompleteClip;
    public AudioClip upgradeClip;

    // ═══════════════════════════════════════════════════
    // CHEST OPEN SCENE (C1-C10)
    // ═══════════════════════════════════════════════════

    [Header("Chest Open SFX")]
    [Tooltip("C1: Magical whoosh + thud on intro drop")]
    public AudioClip chestDropClip;
    [Tooltip("C2: Wooden/metallic tap + rattle for pre-open hops")]
    public AudioClip chestHopClip;
    [Tooltip("C3: Hinge creak + golden burst for lid opening")]
    public AudioClip chestLidOpenClip;
    [Tooltip("C4: Coin cascade + fanfare for money reveal")]
    public AudioClip rewardMoneyClip;
    [Tooltip("C5: Electric/tech chime for nitro reveal")]
    public AudioClip rewardNitroClip;
    [Tooltip("C6: Card flip + shimmer for card reveal")]
    public AudioClip rewardCardClip;
    [Tooltip("C7: Sticker slap + sparkle for sticker reveal")]
    public AudioClip rewardStickerClip;
    [Tooltip("C8: Soft completion jingle for summary")]
    public AudioClip chestSummaryClip;
    [Tooltip("C9: Quick swoosh-out for exit")]
    public AudioClip chestExitClip;
    [Tooltip("C10: Card swoosh/slide for world card animation")]
    public AudioClip worldCardSwooshClip;

    // ═══════════════════════════════════════════════════
    // NITRO SYSTEMS (N1-N12)
    // ═══════════════════════════════════════════════════

    [Header("Nitro SFX")]
    [Tooltip("N1: Bell/chime pling for tap-collected coin")]
    public AudioClip nitroCoinCollectClip;
    [Tooltip("N2: Softer pling for magnet-collected coin")]
    public AudioClip nitroCoinMagnetClip;
    [Tooltip("N4: Electronic deposit ding for boost charge")]
    public AudioClip nitroDepositClip;
    [Tooltip("N5: Distant thunder rumble for rain delay")]
    public AudioClip nitroRainDelayClip;
    [Tooltip("N6: Release whoosh for rain start")]
    public AudioClip nitroRainStartClip;
    [Tooltip("N7: Soft rain patter loop (assign to loopSFXSource)")]
    public AudioClip nitroRainLoopClip;
    [Tooltip("N8: Fade-out whoosh for rain end")]
    public AudioClip nitroRainEndClip;
    [Tooltip("N9: Energy field hum-on for magnet activation")]
    public AudioClip magnetActivateClip;
    [Tooltip("N10: Magnetic whoosh/zip per coin pull")]
    public AudioClip magnetPullClip;
    [Tooltip("N11: Energy field power-down for magnet deactivation")]
    public AudioClip magnetDeactivateClip;

    // ═══════════════════════════════════════════════════
    // BOOST MODE (B1-B5)
    // ═══════════════════════════════════════════════════

    [Header("Boost SFX")]
    [Tooltip("B1: Bright fully-charged ding")]
    public AudioClip boostReadyClip;
    [Tooltip("B2: Turbo ignition whoosh")]
    public AudioClip boostActivateClip;
    [Tooltip("B3: Sustained turbo hum loop")]
    public AudioClip boostActiveLoopClip;
    [Tooltip("B4: Power-down whoosh")]
    public AudioClip boostEndClip;
    [Tooltip("B5: Ready-to-charge pip")]
    public AudioClip boostCooldownCompleteClip;

    // ═══════════════════════════════════════════════════
    // POLICE / RADAR / POPULARITY (P6-P12)
    // ═══════════════════════════════════════════════════

    [Header("Police & Radar SFX")]
    [Tooltip("P6: Victory sting for chase success")]
    public AudioClip chaseSuccessClip;
    [Tooltip("P7: Failure sting for chase fail")]
    public AudioClip chaseFailClip;
    [Tooltip("P8: Electronic zap for radar defuse")]
    public AudioClip radarDefuseClip;
    [Tooltip("P9: Camera shutter + alarm for radar miss")]
    public AudioClip radarMissClip;
    [Tooltip("P10: Photo develop sound for radar popup")]
    public AudioClip radarPopupClip;
    [Tooltip("P11: Escalation tone for popularity stage up")]
    public AudioClip popularityStageUpClip;

    // ═══════════════════════════════════════════════════
    // TURBO / GARAGE MGR / MOMENTUM / EVOLUTION (F1-F6, T1-T4)
    // ═══════════════════════════════════════════════════

    [Header("Systems SFX")]
    [Tooltip("F1: Finger snap + power-up for Turbo Finger")]
    public AudioClip turboFingerActivateClip;
    [Tooltip("F2: Power-down for Turbo Finger")]
    public AudioClip turboFingerDeactivateClip;
    [Tooltip("F3: Wrench clink for Garage Manager activate")]
    public AudioClip garageManagerActivateClip;
    [Tooltip("F4: Soft wind-down for Garage Manager deactivate")]
    public AudioClip garageManagerDeactivateClip;
    [Tooltip("F5: Cash register for PitStop offline earnings")]
    public AudioClip pitStopEarningsClip;
    [Tooltip("T1: Ascending tick for momentum stack gain")]
    public AudioClip momentumTickClip;
    [Tooltip("T2: Descending tone for momentum reset")]
    public AudioClip momentumResetClip;
    [Tooltip("T4: Level-up fanfare for car evolution")]
    public AudioClip carEvolutionClip;
    [Tooltip("T5: Soft cha-ching for small investment cashback")]
    public AudioClip cashbackClip;

    // ═══════════════════════════════════════════════════
    // UI / PANELS / POPUPS (U1-U15)
    // ═══════════════════════════════════════════════════

    [Header("UI SFX")]
    [Tooltip("U1/U11: Soft tok click for tab switch")]
    public AudioClip uiClickClip;
    [Tooltip("U2: Gentle fwip for panel open")]
    public AudioClip panelOpenClip;
    [Tooltip("U3: Softer reverse fwip for panel close")]
    public AudioClip panelCloseClip;
    [Tooltip("U4: Soft popup pop")]
    public AudioClip popupAppearClip;
    [Tooltip("U5: Mechanical tick for start unlock")]
    public AudioClip startUnlockClip;
    [Tooltip("U6: Coin spend + whoosh for open now")]
    public AudioClip openNowClip;
    [Tooltip("U7: Soft ding for chest timer complete")]
    public AudioClip chestTimerDoneClip;
    [Tooltip("U8: Gift pling for daily free reward")]
    public AudioClip dailyFreeClaimClip;
    [Tooltip("U9: Purchase confirm for daily pack buy")]
    public AudioClip dailyPackBuyClip;
    [Tooltip("U12: Satisfying collect sting")]
    public AudioClip rewardCollectClip;
    [Tooltip("U13: Short achievement jingle for mission complete")]
    public AudioClip missionCompleteClip;
    [Tooltip("U14: Ascending fanfare for blacklist tier advance")]
    public AudioClip tierAdvanceClip;
    [Tooltip("U15: Brief fanfare for reward popup appears")]
    public AudioClip rewardPopupAppearClip;
    [Tooltip("U10: Card flip/reveal for card popup")]
    public AudioClip cardPopupOpenClip;

    // ═══════════════════════════════════════════════════
    // GARAGE SCENE (G1-G10)
    // ═══════════════════════════════════════════════════

    [Header("Garage SFX")]
    [Tooltip("G1: Digital glitch swoosh for car switch")]
    public AudioClip garageCarSwitchClip;
    [Tooltip("G2: Paint spray whoosh for color apply")]
    public AudioClip garageColorClip;
    [Tooltip("G3: Vinyl slap for sticker apply")]
    public AudioClip garageStickerClip;
    [Tooltip("G4: Mechanical snap for part toggle on")]
    public AudioClip garagePartOnClip;
    [Tooltip("G5: Reverse click for part toggle off")]
    public AudioClip garagePartOffClip;
    [Tooltip("G6: Cash register for garage purchase")]
    public AudioClip garagePurchaseClip;
    [Tooltip("G7: Soft error buzz for purchase fail")]
    public AudioClip garagePurchaseFailClip;
    [Tooltip("G8: Camera zoom-in for focus mode enter")]
    public AudioClip garageFocusInClip;
    [Tooltip("G9: Camera zoom-out for focus mode exit")]
    public AudioClip garageFocusOutClip;
    [Tooltip("G10: Lock rattle for locked car shake")]
    public AudioClip garageLockedClip;

    // ═══════════════════════════════════════════════════
    // CINEMATIC (K1-K5)
    // ═══════════════════════════════════════════════════

    [Header("Cinematic SFX")]
    [Tooltip("K1: Dramatic reveal sting for showcase start")]
    public AudioClip cinematicRevealClip;
    [Tooltip("K3: Text impact whoosh for car name reveal")]
    public AudioClip cinematicNameRevealClip;
    [Tooltip("K5: Cinematic fade-out drone")]
    public AudioClip cinematicFadeOutClip;

    // ═══════════════════════════════════════════════════
    // WORLD COLLECTIBLES
    // ═══════════════════════════════════════════════════

    [Header("World Collectibles")]
    [Tooltip("World chest tapped on road")]
    public AudioClip worldChestCollectClip;

    // ═══════════════════════════════════════════════════
    // SETTINGS
    // ═══════════════════════════════════════════════════

    [Header("Settings")]
    public bool sfxEnabled = true;

    private int lastTapIndex = -1;
    private float _userVolume = 1f;
    private const string VolumeKey = "SFXVolume";

    // Rate-limiting
    private float _lastCoinCollectTime;
    private int _coinVoiceCount;
    private const int MaxCoinVoices = 3;
    private const float CoinCooldown = 0.05f;

    private float _lastMomentumTickTime;
    private const float MomentumTickCooldown = 0.12f;

    private float _lastBuildingBuyTime;
    private const float BuildingBuyCooldown = 0.15f;

    public float UserVolume
    {
        get => _userVolume;
        set
        {
            _userVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, _userVolume);
            if (sfxSource != null)
                sfxSource.volume = _userVolume;
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _userVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            if (sfxSource != null)
                sfxSource.volume = _userVolume;
            EnsureLoopSource();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureLoopSource()
    {
        if (loopSFXSource == null)
        {
            loopSFXSource = gameObject.AddComponent<AudioSource>();
            loopSFXSource.loop = true;
            loopSFXSource.playOnAwake = false;
            loopSFXSource.volume = 0f;
        }
    }

    // ═══════════════════════════════════════════════════
    // EXISTING METHODS (preserved)
    // ═══════════════════════════════════════════════════

    public void PlayCarTap()
    {
        if (!sfxEnabled || sfxSource == null || carTapClips == null || carTapClips.Length == 0)
            return;

        int index;
        if (carTapClips.Length == 1)
        {
            index = 0;
        }
        else
        {
            do { index = Random.Range(0, carTapClips.Length); }
            while (index == lastTapIndex);
        }
        lastTapIndex = index;

        // Pitch variation ±5%
        sfxSource.pitch = Random.Range(0.95f, 1.05f);
        sfxSource.PlayOneShot(carTapClips[index]);
        sfxSource.pitch = 1f;
    }

    public void PlayBuildingBuy()
    {
        if (Time.unscaledTime - _lastBuildingBuyTime < BuildingBuyCooldown) return;
        _lastBuildingBuyTime = Time.unscaledTime;
        PlayOneShot(buildingBuyClip);
    }

    public void PlayUpgrade()
    {
        PlayOneShot(upgradeClip);
    }

    public void PlayGoalComplete()
    {
        PlayOneShot(goalCompleteClip);
    }

    // ═══════════════════════════════════════════════════
    // CHEST OPEN SCENE METHODS
    // ═══════════════════════════════════════════════════

    public void PlayChestDrop() { PlayOneShot(chestDropClip); }
    public void PlayChestHop() { PlayOneShotPitchVariation(chestHopClip, 0.95f, 1.10f); }
    public void PlayChestLidOpen() { PlayOneShot(chestLidOpenClip); }
    public void PlayRewardMoney() { PlayOneShot(rewardMoneyClip); }
    public void PlayRewardNitro() { PlayOneShot(rewardNitroClip); }
    public void PlayRewardCard() { PlayOneShot(rewardCardClip); }
    public void PlayRewardSticker() { PlayOneShot(rewardStickerClip); }
    public void PlayChestSummary() { PlayOneShot(chestSummaryClip); }
    public void PlayChestExit() { PlayOneShot(chestExitClip); }
    public void PlayWorldCardSwoosh() { PlayOneShot(worldCardSwooshClip); }

    // ═══════════════════════════════════════════════════
    // NITRO METHODS
    // ═══════════════════════════════════════════════════

    public void PlayNitroCoinCollect()
    {
        float now = Time.unscaledTime;
        if (now - _lastCoinCollectTime < CoinCooldown) return;
        _lastCoinCollectTime = now;
        PlayOneShotPitchVariation(nitroCoinCollectClip, 0.90f, 1.10f);
    }

    public void PlayNitroCoinMagnet()
    {
        float now = Time.unscaledTime;
        if (now - _lastCoinCollectTime < CoinCooldown) return;
        _lastCoinCollectTime = now;
        PlayOneShotVolume(nitroCoinMagnetClip, 0.6f);
    }

    public void PlayNitroDeposit() { PlayOneShot(nitroDepositClip); }
    public void PlayNitroRainDelay() { PlayOneShot(nitroRainDelayClip); }
    public void PlayNitroRainStart() { PlayOneShot(nitroRainStartClip); }
    public void PlayNitroRainEnd() { PlayOneShot(nitroRainEndClip); }
    public void PlayMagnetActivate() { PlayOneShot(magnetActivateClip); }
    public void PlayMagnetPull() { PlayOneShotVolume(magnetPullClip, 0.5f); }
    public void PlayMagnetDeactivate() { PlayOneShot(magnetDeactivateClip); }

    /// <summary>Start the rain ambient loop. Fades in over 0.5s.</summary>
    public void StartRainLoop()
    {
        EnsureLoopSource();
        if (nitroRainLoopClip == null) return;
        loopSFXSource.clip = nitroRainLoopClip;
        loopSFXSource.volume = 0f;
        loopSFXSource.Play();
        DG.Tweening.DOTween.Kill(loopSFXSource);
        DG.Tweening.DOTweenModuleAudio.DOFade(loopSFXSource, 0.3f * _userVolume, 0.5f);
    }

    /// <summary>Stop the rain ambient loop. Fades out over 0.5s.</summary>
    public void StopRainLoop()
    {
        if (loopSFXSource == null || !loopSFXSource.isPlaying) return;
        DG.Tweening.DOTween.Kill(loopSFXSource);
        DG.Tweening.TweenSettingsExtensions.OnComplete(
            DG.Tweening.DOTweenModuleAudio.DOFade(loopSFXSource, 0f, 0.5f),
            () => { loopSFXSource.Stop(); loopSFXSource.clip = null; });
    }

    // ═══════════════════════════════════════════════════
    // BOOST METHODS
    // ═══════════════════════════════════════════════════

    public void PlayBoostReady() { PlayOneShot(boostReadyClip); }
    public void PlayBoostActivate() { PlayOneShot(boostActivateClip); }
    public void PlayBoostEnd() { PlayOneShot(boostEndClip); }
    public void PlayBoostCooldownComplete() { PlayOneShot(boostCooldownCompleteClip); }

    /// <summary>Start the boost active hum loop.</summary>
    public void StartBoostLoop()
    {
        EnsureLoopSource();
        if (boostActiveLoopClip == null) return;
        loopSFXSource.clip = boostActiveLoopClip;
        loopSFXSource.volume = 0f;
        loopSFXSource.Play();
        DG.Tweening.DOTween.Kill(loopSFXSource);
        DG.Tweening.DOTweenModuleAudio.DOFade(loopSFXSource, 0.25f * _userVolume, 0.3f);
    }

    /// <summary>Stop the boost active hum loop.</summary>
    public void StopBoostLoop()
    {
        if (loopSFXSource == null || !loopSFXSource.isPlaying) return;
        if (loopSFXSource.clip != boostActiveLoopClip) return;
        DG.Tweening.DOTween.Kill(loopSFXSource);
        DG.Tweening.TweenSettingsExtensions.OnComplete(
            DG.Tweening.DOTweenModuleAudio.DOFade(loopSFXSource, 0f, 0.3f),
            () => { loopSFXSource.Stop(); loopSFXSource.clip = null; });
    }

    // ═══════════════════════════════════════════════════
    // POLICE / RADAR METHODS
    // ═══════════════════════════════════════════════════

    public void PlayChaseSuccess() { PlayOneShot(chaseSuccessClip); }
    public void PlayChaseFail() { PlayOneShot(chaseFailClip); }
    public void PlayRadarDefuse() { PlayOneShot(radarDefuseClip); }
    public void PlayRadarMiss() { PlayOneShot(radarMissClip); }
    public void PlayRadarPopup() { PlayOneShot(radarPopupClip); }
    public void PlayPopularityStageUp() { PlayOneShot(popularityStageUpClip); }

    // ═══════════════════════════════════════════════════
    // SYSTEMS METHODS
    // ═══════════════════════════════════════════════════

    public void PlayTurboFingerActivate() { PlayOneShot(turboFingerActivateClip); }
    public void PlayTurboFingerDeactivate() { PlayOneShot(turboFingerDeactivateClip); }
    public void PlayGarageManagerActivate() { PlayOneShot(garageManagerActivateClip); }
    public void PlayGarageManagerDeactivate() { PlayOneShot(garageManagerDeactivateClip); }
    public void PlayPitStopEarnings() { PlayOneShot(pitStopEarningsClip); }
    public void PlayCarEvolution() { PlayOneShot(carEvolutionClip); }
    public void PlayCashback() { PlayOneShot(cashbackClip); }

    public void PlayMomentumTick(int currentStacks, int stackCap)
    {
        // Only play every 5th stack to avoid spam
        if (currentStacks % 5 != 0 || currentStacks == 0) return;
        float now = Time.unscaledTime;
        if (now - _lastMomentumTickTime < MomentumTickCooldown) return;
        _lastMomentumTickTime = now;
        // Pitch rises with stack progress
        float pitch = Mathf.Lerp(0.9f, 1.3f, (float)currentStacks / Mathf.Max(1, stackCap));
        PlayOneShotPitch(momentumTickClip, pitch);
    }

    public void PlayMomentumReset() { PlayOneShot(momentumResetClip); }

    // ═══════════════════════════════════════════════════
    // UI METHODS
    // ═══════════════════════════════════════════════════

    public void PlayUIClick() { PlayOneShotVolume(uiClickClip, 0.7f); }
    public void PlayPanelOpen() { PlayOneShot(panelOpenClip); }
    public void PlayPanelClose() { PlayOneShot(panelCloseClip); }
    public void PlayPopupAppear() { PlayOneShot(popupAppearClip); }
    public void PlayStartUnlock() { PlayOneShot(startUnlockClip); }
    public void PlayOpenNow() { PlayOneShot(openNowClip); }
    public void PlayChestTimerDone() { PlayOneShot(chestTimerDoneClip); }
    public void PlayDailyFreeClaim() { PlayOneShot(dailyFreeClaimClip); }
    public void PlayDailyPackBuy() { PlayOneShot(dailyPackBuyClip); }
    public void PlayRewardCollect() { PlayOneShot(rewardCollectClip); }
    public void PlayMissionComplete() { PlayOneShot(missionCompleteClip); }
    public void PlayTierAdvance() { PlayOneShot(tierAdvanceClip); }
    public void PlayRewardPopupAppear() { PlayOneShot(rewardPopupAppearClip); }
    public void PlayCardPopupOpen() { PlayOneShot(cardPopupOpenClip); }

    // ═══════════════════════════════════════════════════
    // GARAGE METHODS
    // ═══════════════════════════════════════════════════

    public void PlayGarageCarSwitch() { PlayOneShot(garageCarSwitchClip); }
    public void PlayGarageColor() { PlayOneShot(garageColorClip); }
    public void PlayGarageSticker() { PlayOneShot(garageStickerClip); }
    public void PlayGaragePartOn() { PlayOneShot(garagePartOnClip); }
    public void PlayGaragePartOff() { PlayOneShot(garagePartOffClip); }
    public void PlayGaragePurchase() { PlayOneShot(garagePurchaseClip); }
    public void PlayGaragePurchaseFail() { PlayOneShot(garagePurchaseFailClip); }
    public void PlayGarageFocusIn() { PlayOneShot(garageFocusInClip); }
    public void PlayGarageFocusOut() { PlayOneShot(garageFocusOutClip); }
    public void PlayGarageLocked() { PlayOneShot(garageLockedClip); }

    // ═══════════════════════════════════════════════════
    // CINEMATIC METHODS
    // ═══════════════════════════════════════════════════

    public void PlayCinematicReveal() { PlayOneShot(cinematicRevealClip); }
    public void PlayCinematicNameReveal() { PlayOneShot(cinematicNameRevealClip); }
    public void PlayCinematicFadeOut() { PlayOneShot(cinematicFadeOutClip); }

    // ═══════════════════════════════════════════════════
    // WORLD COLLECTIBLES
    // ═══════════════════════════════════════════════════

    public void PlayWorldChestCollect() { PlayOneShot(worldChestCollectClip); }

    // ═══════════════════════════════════════════════════
    // INTERNAL HELPERS
    // ═══════════════════════════════════════════════════

    private void PlayOneShot(AudioClip clip)
    {
        if (!sfxEnabled || clip == null || sfxSource == null)
            return;
        sfxSource.PlayOneShot(clip, _userVolume);
    }

    private void PlayOneShotVolume(AudioClip clip, float volumeScale)
    {
        if (!sfxEnabled || clip == null || sfxSource == null)
            return;
        sfxSource.PlayOneShot(clip, _userVolume * volumeScale);
    }

    private void PlayOneShotPitchVariation(AudioClip clip, float minPitch, float maxPitch)
    {
        if (!sfxEnabled || clip == null || sfxSource == null)
            return;
        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, _userVolume);
        sfxSource.pitch = 1f;
    }

    private void PlayOneShotPitch(AudioClip clip, float pitch)
    {
        if (!sfxEnabled || clip == null || sfxSource == null)
            return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, _userVolume);
        sfxSource.pitch = 1f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
