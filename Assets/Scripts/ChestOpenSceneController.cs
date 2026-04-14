using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class ChestOpenSceneController : MonoBehaviour
{
    // ── Debug Instrumentation ─────────────────────────────────────────
    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void DLog(string msg)
    {
        if (!debugLogs) return;
        Debug.Log($"[ChestOpenCtrl][{name}#{GetInstanceID()}] t={Time.time:F2} rt={Time.realtimeSinceStartup:F2} f={Time.frameCount} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} | {msg}");
    }

    private void DWarn(string msg)
    {
        Debug.LogWarning($"[ChestOpenCtrl][{name}#{GetInstanceID()}] t={Time.time:F2} rt={Time.realtimeSinceStartup:F2} f={Time.frameCount} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} | {msg}");
    }
    // ──────────────────────────────────────────────────────────────────

    // 
    //  PHASE STATE MACHINE
    // 

    private enum Phase
    {
        Intro,              // chest drops in  no input
        Closed_TapToOpen,   // taps 1-2: hop only  |  tap 3: hop + lid + money
        LidOpening,         // lid animating  no input
        Reveal_Money,       // money card parked, texts shown  wait for tap 4
        Reveal_Nitro,       // tap 4 result  wait for tap 5
        Reveal_Card,        // tap 5 result  wait for tap 6
        Reveal_Sticker,     // tap 6 result (Rare/Legendary only)  wait for tap 7
        Summary,            // tap 6 or 7 result  wait for final tap
        Exit                // final tap fires grant + scene change
    }

    // 
    //  INSPECTOR
    // 

    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private GameObject commonChestPrefab;
    [SerializeField] private GameObject rareChestPrefab;
    [SerializeField] private GameObject legendaryChestPrefab;

    [Header("Lid Settings")]
    [SerializeField] private Transform lidBone;
    [SerializeField] private string lidTransformName = "Cube.004";
    [SerializeField] private string lidSearchPath = "Empty/Cube.004";
    [SerializeField] private Vector3 lidClosedLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 lidOpenLocalEuler = new Vector3(-110f, 0, 0);

    [Header("Runtime Pivot Fix")]
    [SerializeField] private bool useRuntimePivotFix = false;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0, 0.5f, -0.5f);

    [Header("Money-Based Chest Gold")]
    [Tooltip("Minimum percentage of current money for chest gold reward (0.05 = 5%).")]
    [SerializeField] private float chestGoldPercentMin = 0.05f;
    [Tooltip("Maximum percentage of current money for chest gold reward (0.15 = 15%).")]
    [SerializeField] private float chestGoldPercentMax = 0.15f;

    [Header("Nitro-Based Chest Nitro")]
    [Tooltip("Minimum percentage of current nitro for chest nitro reward (0.05 = 5%).")]
    [SerializeField] private float chestNitroPercentMin = 0.05f;
    [Tooltip("Maximum percentage of current nitro for chest nitro reward (0.20 = 20%).")]
    [SerializeField] private float chestNitroPercentMax = 0.20f;

    [Header("Reward Reveal")]
    [Tooltip("ChestRewardRevealController on RewardRevealRoot")]
    [SerializeField] private ChestRewardRevealController revealController;

    [Tooltip("Drag the GarageDatabase SO here (Assets/SO/Garage/GarageDatabase). Required for sticker rewards.")]
    [SerializeField] private GarageDatabaseSO garageDatabase;

    [Tooltip("WorldRewardCardPrefab_TMP prefab")]
    [SerializeField] private GameObject worldCardPrefab;

    [Tooltip("CardParkAnchor transform in scene (where parked card goes)")]
    [SerializeField] private Transform parkAnchor;

    [Tooltip("Child name on chest prefab for card emerge origin")]
    [SerializeField] private string cardMouthChildName = "CardMouthAnchor";

    [Header("Tap Settings")]
    [SerializeField] private int tapsToOpen = 3;
    [SerializeField] private LayerMask chestLayerMask = ~0;

    [Header("Intro Anim")]
    [SerializeField] private float introMoveTime = 0.55f;
    [SerializeField] private float introStartYOffset = 0.55f;
    [SerializeField] private float introStartScale = 0.25f;

    [Header("Tap Feedback")]
    [SerializeField] private float tapJumpPower = 0.25f;
    [SerializeField] private float tapJumpDuration = 0.22f;

    [Header("Lid Open Anim")]
    [SerializeField] private float lidOpenDuration = 0.35f;
    [SerializeField] private Ease lidEase = Ease.OutCubic;

    [Header("Background per Chest Type")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Sprite bgCommon;
    [SerializeField] private Sprite bgRare;
    [SerializeField] private Sprite bgLegendary;

    // 
    //  RUNTIME STATE
    // 

    private Phase phase = Phase.Intro;
    private int tapCount;
    private int globalTapCount;

    private GameObject chestGO;
    private Transform chestTr;
    private Vector3 basePos;
    private Vector3 baseScale;
    private Transform cardMouthAnchor;

    private WorldRewardCardController activeWorldCard;
    private bool rewardsGranted;
    private bool isAnimating;

    private ChestRewardPackage rewardPackage;

    // Resolved chest type for per-type reward scaling
    private ChestType cachedChestType = ChestType.Common;

    // Bug #7: Cached chest data (read + cleared from PlayerPrefs on scene entry)
    private ChestInventoryManager.ChestData cachedChestData;

    // Bug #2: Animation watchdog
    [Header("Animation Safety")]
    [Tooltip("Max seconds isAnimating can stay true before auto-recovery")]
    [SerializeField] private float animationTimeout = 4f;
    private float animStartTime;
    private Phase animTargetPhase;

    // 
    //  BACKGROUND
    // 

    private void ApplyBackground()
    {
        if (backgroundImage == null) return;
        backgroundImage.sprite = cachedChestType switch
        {
            ChestType.Common => bgCommon,
            ChestType.Rare => bgRare,
            ChestType.Legendary => bgLegendary,
            _ => bgCommon
        };
    }

    // 
    //  LIFECYCLE
    // 

    private void OnDestroy()
    {
        if (chestTr != null) chestTr.DOKill();
        if (lidBone != null) lidBone.DOKill();
        if (activeWorldCard != null)
        {
            activeWorldCard.transform.DOKill();
            Destroy(activeWorldCard.gameObject);
            activeWorldCard = null;
        }
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        // ── Ensure critical managers are alive (bootstrap safety net) ──
        ChestSessionManager.EnsureInstance();
        ChestInventoryManager.EnsureInstance();

        DLog($"Start ENTRY — ChestSessionManager.Instance={(ChestSessionManager.Instance != null ? "EXISTS" : "NULL")} ChestInventoryManager.Instance={(ChestInventoryManager.Instance != null ? ChestInventoryManager.Instance.name + "#" + ChestInventoryManager.Instance.GetInstanceID() : "NULL")}");

        // ── Phase 1 Hardening: Primary handoff via runtime session, PlayerPrefs as fallback ──
        cachedChestData = ResolveChestData();
        if (cachedChestData == null)
        {
            Debug.LogError("[ChestOpenScene] No chest data available (neither session nor PlayerPrefs)! Returning to Main safely.");
            DWarn("Start — cachedChestData is NULL, loading Main");
            SceneManager.LoadScene("Main");
            return;
        }
        DLog($"Start — cachedChestData OK: name='{cachedChestData.chestName}' chestType={cachedChestData.chestType}");

        // Cache the chest type for reward scaling
        cachedChestType = cachedChestData.chestType;
        ApplyBackground();
        // Clear legacy pending key (no longer needed for the new flow, but clean up)
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.ClearPendingOpenChest();

        //  Validation 
        if (revealController == null)
            Debug.LogError("[ChestOpenScene] revealController is NULL! Assign in Inspector.");
        if (worldCardPrefab == null)
            Debug.LogError("[ChestOpenScene] worldCardPrefab is NULL! Assign in Inspector.");
        if (parkAnchor == null)
            Debug.LogWarning("[ChestOpenScene] parkAnchor is NULL  default will be computed.");

        SpawnChest();
        PlayIntro();
    }

    /// <summary>
    /// Resolves chest data using the best available source:
    ///   1. Runtime session from ChestSessionManager (primary, survives scene load)
    ///   2. Persisted session JSON read directly from PlayerPrefs (manager-independent)
    ///   3. Legacy pending chest key via ChestInventoryManager
    ///   4. Legacy pending chest key read directly from PlayerPrefs (last resort)
    /// </summary>
    private ChestInventoryManager.ChestData ResolveChestData()
    {
        // 1) Runtime session (best case — normal flow)
        if (ChestSessionManager.Instance != null && ChestSessionManager.Instance.HasActiveSession)
        {
            var session = ChestSessionManager.Instance.ActiveSession;
            if (session.chestData != null)
            {
                Debug.Log($"[ChestOpenScene] Chest data resolved from RUNTIME SESSION: '{session.chestData.chestName}'");
                return session.chestData;
            }
        }

        // 2) Persisted session JSON (manager may exist but have no in-memory session,
        //    or may have been freshly bootstrapped — the session was persisted to PlayerPrefs)
        DLog("ResolveChestData — no runtime session, trying persisted session JSON");
        var fromSession = TryLoadSessionFromPlayerPrefs();
        if (fromSession != null)
        {
            // If ChestSessionManager exists but has no active session, restore it
            if (ChestSessionManager.Instance != null && !ChestSessionManager.Instance.HasActiveSession)
            {
                ChestSessionManager.Instance.RestoreSessionFromPersisted(fromSession);
                Debug.Log($"[ChestOpenScene] Restored persisted session into ChestSessionManager: '{fromSession.chestData.chestName}'");
            }
            return fromSession.chestData;
        }

        // 3) Legacy pending key via manager
        DLog("ResolveChestData — no persisted session, trying legacy PlayerPrefs key");
        return ReadAndClearPendingChest();
    }

    /// <summary>
    /// Reads the persisted ChestOpeningSession directly from PlayerPrefs.
    /// Does NOT require ChestSessionManager or ChestInventoryManager to be alive.
    /// </summary>
    private ChestOpeningSession TryLoadSessionFromPlayerPrefs()
    {
        const string key = "Save_ChestOpeningSession";
        if (!PlayerPrefs.HasKey(key)) return null;

        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var session = JsonUtility.FromJson<ChestOpeningSession>(json);
            if (session != null && session.chestData != null)
            {
                Debug.Log($"[ChestOpenScene] Loaded persisted session from PlayerPrefs: id={session.sessionId} chest='{session.chestData.chestName}' committed={session.rewardsCommitted}");
                return session;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChestOpenScene] Failed to parse persisted session: {e.Message}");
        }
        return null;
    }

    /// <summary>
    /// Bug #7: Read the pending chest from PlayerPrefs, cache it, and immediately
    /// delete the key so it can never leak into a future chest open if the app
    /// crashes or is force-quit mid-reveal.
    /// </summary>
    private ChestInventoryManager.ChestData ReadAndClearPendingChest()
    {
        DLog($"ReadAndClearPendingChest ENTRY — ChestInventoryManager.Instance={(ChestInventoryManager.Instance != null ? "EXISTS (" + ChestInventoryManager.Instance.name + "#" + ChestInventoryManager.Instance.GetInstanceID() + ")" : "NULL")}");

        // Try via manager first (has proper logging and key management)
        if (ChestInventoryManager.Instance != null)
        {
            DLog("ReadAndClearPendingChest — calling GetPendingOpenChest()");
            var data = ChestInventoryManager.Instance.GetPendingOpenChest();
            DLog($"ReadAndClearPendingChest — GetPendingOpenChest returned {(data != null ? $"'{data.chestName}'" : "NULL")}");
            if (data != null)
            {
                ChestInventoryManager.Instance.ClearPendingOpenChest();
                PlayerPrefs.Save();
                Debug.Log($"[ChestOpenScene] Pending chest cached & PlayerPrefs key cleared: '{data.chestName}' type={data.chestType}");
            }
            return data;
        }

        // Last resort: read legacy key directly from PlayerPrefs (no manager needed)
        Debug.LogWarning("[ChestOpenScene] ChestInventoryManager.Instance is NULL — reading legacy key directly from PlayerPrefs.");
        return ReadLegacyPendingChestDirect();
    }

    /// <summary>
    /// Reads the legacy pending chest key directly from PlayerPrefs without
    /// depending on ChestInventoryManager. Last-resort fallback.
    /// </summary>
    private ChestInventoryManager.ChestData ReadLegacyPendingChestDirect()
    {
        const string key = "Save_PendingOpenChest";
        if (!PlayerPrefs.HasKey(key)) return null;

        string json = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            var data = JsonUtility.FromJson<ChestInventoryManager.ChestData>(json);
            if (data != null)
            {
                // Clear immediately to prevent double-use
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Debug.Log($"[ChestOpenScene] Legacy pending chest read directly from PlayerPrefs: '{data.chestName}'");
                return data;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ChestOpenScene] Failed to parse legacy pending chest: {e.Message}");
        }
        return null;
    }

    // 
    //  CHEST SPAWN
    // 

    private void SpawnChest()
    {
        if (spawnPoint == null)
        {
            Debug.LogError("[ChestOpenScene] spawnPoint missing!");
            return;
        }

        // Select prefab by chest type, fall back to generic chestPrefab
        GameObject prefab = GetPrefabForType(cachedChestType);
        if (prefab == null)
        {
            Debug.LogError("[ChestOpenScene] No chest prefab available!");
            return;
        }

        chestGO = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        chestTr = chestGO.transform;
        basePos = chestTr.position;
        baseScale = chestTr.localScale;

        // Lid auto-find
        if (lidBone == null)
            lidBone = FindLidTransform(chestTr);

        if (lidBone != null)
        {
            Debug.Log($"[ChestOpenScene] lidBone found: {GetTransformPath(lidBone)}");
            if (useRuntimePivotFix) lidBone = CreateRuntimePivot(lidBone);
            lidClosedLocalEuler = lidBone.localEulerAngles;
        }
        else
        {
            Debug.LogError("[ChestOpenScene] lidBone not found!");
            PrintHierarchy(chestTr);
        }

        // Resolve card mouth anchor
        if (!string.IsNullOrEmpty(cardMouthChildName))
            cardMouthAnchor = FindChildRecursive(chestTr, cardMouthChildName);

        if (cardMouthAnchor == null)
        {
            cardMouthAnchor = lidBone != null ? lidBone : spawnPoint;
            Debug.Log($"[ChestOpenScene] CardMouthAnchor fallback -> {cardMouthAnchor.name}");
        }
        else
        {
            Debug.Log($"[ChestOpenScene] CardMouthAnchor resolved: {cardMouthAnchor.name} pos={cardMouthAnchor.position}");
        }

        // Collider check
        if (chestGO.GetComponentInChildren<Collider>() == null)
            Debug.LogWarning("[ChestOpenScene] No Collider on chest prefab  taps won't work!");
    }

    // 
    //  CHEST PREFAB SELECTION
    // 

    private GameObject GetPrefabForType(ChestType type)
    {
        switch (type)
        {
            case ChestType.Rare:
                return rareChestPrefab != null ? rareChestPrefab : chestPrefab;
            case ChestType.Legendary:
                return legendaryChestPrefab != null ? legendaryChestPrefab : chestPrefab;
            default:
                return commonChestPrefab != null ? commonChestPrefab : chestPrefab;
        }
    }

    // 
    //  INTRO ANIM
    // 

    private void PlayIntro()
    {
        if (chestTr == null) return;
        phase = Phase.Intro;
        chestTr.DOKill(true);

        // C1: Chest drop intro SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayChestDrop();

        chestTr.position = basePos + Vector3.down * introStartYOffset;
        chestTr.localScale = baseScale * introStartScale;

        Sequence s = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        s.Append(chestTr.DOMove(basePos, introMoveTime).SetEase(Ease.OutCubic));
        s.Join(chestTr.DOScale(baseScale, introMoveTime).SetEase(Ease.OutCubic));
        s.Append(chestTr.DOMoveY(basePos.y + 0.06f, 0.16f).SetEase(Ease.OutSine));
        s.Append(chestTr.DOMoveY(basePos.y, 0.16f).SetEase(Ease.InOutSine));

        s.OnComplete(() =>
        {
            phase = Phase.Closed_TapToOpen;
            tapCount = 0;
            Debug.Log("[ChestOpenScene] Phase -> Closed_TapToOpen");
            StartIdleMotion();
        });
    }

    /// <summary>Gentle idle hover: Y oscillation + slow Y rotation. Killed on first tap feedback.</summary>
    private void StartIdleMotion()
    {
        if (chestTr == null) return;
        chestTr.DOMoveY(basePos.y + 0.08f, 1.2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetId("ChestIdle")
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        chestTr.DORotate(new Vector3(0, 360, 0), 12f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Restart)
            .SetId("ChestIdle")
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
    }

    // 
    //  INPUT
    // 

    private void Update()
    {
        // ── Bug #2 fix: Animation watchdog (checked BEFORE phase guard so it
        //    catches isAnimating stuck during LidOpening→Reveal transition too) ──
        if (isAnimating && Time.unscaledTime - animStartTime > animationTimeout)
        {
            WatchdogRecover();
            return;
        }

        // Intro: fully non-interactive
        if (phase == Phase.Intro) return;
        if (cam == null) return;

        bool tapDetected = false;
        Vector2 screenPos = Vector2.zero;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) { tapDetected = true; screenPos = Input.mousePosition; }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        { tapDetected = true; screenPos = Input.GetTouch(0).position; }
#endif

        if (!tapDetected) return;

        // ── Spam-tap: skip lid opening instantly ──
        if (phase == Phase.LidOpening)
        {
            // Complete the lid rotation (fires OnLidOpened → computes+commits rewards)
            if (lidBone != null) lidBone.DOComplete();
            // After OnLidOpened, isAnimating is true (money reveal started).
            // Fall through to the isAnimating skip below.
        }

        // ── Spam-tap: skip current animation and advance phase ──
        if (isAnimating)
        {
            SkipCurrentAnimation();
            // phase is now advanced, isAnimating is false — fall through
        }

        // Post-lid phases: accept any screen tap (no raycast needed)
        if (phase >= Phase.Reveal_Money)
        {
            OnChestTapped();
            return;
        }

        // Pre-lid: require raycast on chest
        if (phase == Phase.Closed_TapToOpen)
            TryTap(screenPos);
    }

    private void TryTap(Vector2 screenPos)
    {
        if (chestTr == null) return;
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, chestLayerMask))
        {
            if (hit.transform != null && hit.transform.IsChildOf(chestTr))
                OnChestTapped();
        }
    }

    // 
    //  TAP HANDLER  exact 7-tap flow
    // 

    private void OnChestTapped()
    {
        globalTapCount++;
        Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} phase={phase} isAnimating={isAnimating}");

        //  TAPS 1-3  (Closed_TapToOpen) 
        if (phase == Phase.Closed_TapToOpen)
        {
            tapCount++;
            PlayTapFeedback();
            Debug.Log($"[ChestOpenScene] Closed tap {tapCount}/{tapsToOpen}");

            if (tapCount >= tapsToOpen)
                OpenLid();     // tap 3  lid opens  auto money reveal
            return;
        }

        //  TAP 4  (Reveal_Money  Nitro) 
        if (phase == Phase.Reveal_Money)
        {
            PlayTapFeedback(openedFeel: true);
            // C5: Nitro reveal SFX
            if (SFXManager.Instance != null) SFXManager.Instance.PlayRewardNitro();
            BeginAnimating(Phase.Reveal_Nitro);
            Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> RevealNitro");
            Sprite nitroIcon = revealController != null ? revealController.NitroSprite : null;
            string nitroLabel = rewardPackage != null ? $"+{rewardPackage.nitroReward}" : "+0";
            SafeFadeOutAndSwap(nitroIcon, nitroLabel, () =>
            {
                if (revealController != null)
                    revealController.ShowNitroInfo(() => EndAnim(Phase.Reveal_Nitro));
                else
                    EndAnim(Phase.Reveal_Nitro);
            });
            return;
        }

        //  TAP 5  (Reveal_Nitro  Reveal_Card) 
        if (phase == Phase.Reveal_Nitro)
        {
            PlayTapFeedback(openedFeel: true);
            // C6: Card reveal SFX
            if (SFXManager.Instance != null) SFXManager.Instance.PlayRewardCard();
            BeginAnimating(Phase.Reveal_Card);
            Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> RevealCard");
            // Use full card art on CardBG renderer; format is "Nx" (e.g. "4x")
            Sprite cardBGSprite = rewardPackage?.cardArtSprite;
            string cardLabel = rewardPackage != null ? $"{rewardPackage.cardCopies}x" : "";
            Debug.Log($"[ChestOpenScene] RevealCard: bgSprite={(cardBGSprite != null ? cardBGSprite.name : "NULL")} " +
                      $"label='{cardLabel}' displayName='{rewardPackage?.cardDisplayName}' rarity={rewardPackage?.cardRarity}");
            SafeFadeOutAndSwap(null, cardLabel, () =>   // icon=null: CardBG mode handles visuals
            {
                if (revealController != null)
                    revealController.ShowCardInfo(() => EndAnim(Phase.Reveal_Card));
                else
                    EndAnim(Phase.Reveal_Card);
            }, cardBGSprite);
            return;
        }

        //  TAP 6  (Reveal_Card  Sticker or Summary)  NO hop 
        if (phase == Phase.Reveal_Card)
        {
            // If sticker reward exists, go to Reveal_Sticker; otherwise skip to Summary
            if (rewardPackage != null && rewardPackage.hasStickerReward)
            {
                PlayTapFeedback(openedFeel: true);
                // C7: Sticker reveal SFX
                if (SFXManager.Instance != null) SFXManager.Instance.PlayRewardSticker();
                BeginAnimating(Phase.Reveal_Sticker);
                Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> RevealSticker");
                SafeFadeOutAndSwap(revealController != null ? revealController.StickerSprite : null,
                    rewardPackage.stickerCarDisplayName ?? "Sticker", () =>
                {
                    if (revealController != null)
                        revealController.ShowStickerInfo(() => EndAnim(Phase.Reveal_Sticker));
                    else
                        EndAnim(Phase.Reveal_Sticker);
                });
            }
            else
            {
                // C8: Summary shown SFX (no sticker path)
                if (SFXManager.Instance != null) SFXManager.Instance.PlayChestSummary();
                BeginAnimating(Phase.Summary);
                Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> Summary (no sticker)");
                SafeFadeOut(() =>
                {
                    HideActiveWorldCard(() =>
                    {
                        if (revealController != null)
                            revealController.ShowSummary(() => EndAnim(Phase.Summary));
                        else
                            EndAnim(Phase.Summary);
                    });
                });
            }
            return;
        }

        //  TAP 7  (Reveal_Sticker  Summary)  NO hop 
        if (phase == Phase.Reveal_Sticker)
        {
            // C8: Summary shown SFX
            if (SFXManager.Instance != null) SFXManager.Instance.PlayChestSummary();
            BeginAnimating(Phase.Summary);
            Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> Summary");
            SafeFadeOut(() =>
            {
                HideActiveWorldCard(() =>
                {
                    if (revealController != null)
                        revealController.ShowSummary(() => EndAnim(Phase.Summary));
                    else
                        EndAnim(Phase.Summary);
                });
            });
            return;
        }

        //  TAP 7 or 8  (Summary  Exit) 
        if (phase == Phase.Summary)
        {
            // C9: Exit swoosh SFX
            if (SFXManager.Instance != null) SFXManager.Instance.PlayChestExit();
            Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> EXIT");
            FinishAndReturnToMain();
            return;
        }

        if (phase == Phase.Exit)
        {
            FinishAndReturnToMain();
        }
    }

    /// <summary>
    /// Helper: sets isAnimating=false and transitions to next phase.
    /// Guarantees the flow never gets stuck.
    /// </summary>
    private void EndAnim(Phase next)
    {
        isAnimating = false;
        phase = next;
        Debug.Log($"[ChestOpenScene] Phase -> {next}");
    }

    /// <summary>
    /// Bug #2: Sets isAnimating=true and records the start time + target phase
    /// so the watchdog in Update() can auto-recover if the tween chain breaks.
    /// </summary>
    private void BeginAnimating(Phase targetPhase)
    {
        isAnimating = true;
        animStartTime = Time.unscaledTime;
        animTargetPhase = targetPhase;
        Debug.Log($"[ChestOpenScene] BeginAnimating target={targetPhase}");
    }

    /// <summary>
    /// Bug #2: Called by Update() when isAnimating has been true longer than animationTimeout.
    /// Kills all active tweens, destroys stale world-cards, and force-transitions
    /// to the intended next phase (or Summary / Exit as fallback).
    /// </summary>
    private void WatchdogRecover()
    {
        Debug.LogError($"[ChestOpenScene] WATCHDOG TRIGGERED: isAnimating stuck for >{animationTimeout}s! " +
                       $"phase={phase} targetPhase={animTargetPhase} globalTap={globalTapCount}");

        KillAllSceneTweens();

        // If rewards haven't been computed yet, bail to Main without granting.
        // Session recovery will revert chest to ReadyToOpen on next launch.
        if (rewardPackage == null)
        {
            Debug.LogError("[ChestOpenScene] WATCHDOG: No rewardPackage — returning to Main. Session recovery will handle the chest.");
            isAnimating = false;
            phase = Phase.Exit;
            FinishAndReturnToMain();
            return;
        }

        // Determine recovery phase: use the intended target if it's a valid reveal+ phase,
        // otherwise fall back to Summary so the player can still tap to exit.
        Phase recovery = animTargetPhase;
        if ((int)recovery < (int)Phase.Reveal_Money)
            recovery = Phase.Summary;

        Debug.LogWarning($"[ChestOpenScene] WATCHDOG: Recovering to phase {recovery}");
        EndAnim(recovery);
    }

    /// <summary>
    /// Kills every DOTween in the chest-open scene.
    /// Shared by SkipCurrentAnimation and WatchdogRecover.
    /// </summary>
    private void KillAllSceneTweens()
    {
        if (chestTr != null) chestTr.DOKill();
        if (lidBone != null) lidBone.DOKill();
        DOTween.Kill(this);

        if (revealController != null)
            revealController.SnapKillAllTweens();

        if (activeWorldCard != null)
        {
            activeWorldCard.transform.DOKill();
            DOTween.Kill(activeWorldCard);
            Destroy(activeWorldCard.gameObject);
            activeWorldCard = null;
        }
    }

    /// <summary>
    /// Instantly completes the current animation transition.
    /// Kills all running tweens, cleans up, resets chest visuals, and advances phase.
    /// Called when the player taps during an animation (spam-tap support).
    /// </summary>
    private void SkipCurrentAnimation()
    {
        Debug.Log($"[ChestOpenScene] SPAM-TAP SKIP: killing tweens, advancing {phase}→{animTargetPhase}");
        KillAllSceneTweens();

        // Reset chest transform so the next PlayTapFeedback starts from a clean state
        if (chestTr != null)
        {
            chestTr.position = basePos;
            chestTr.localScale = baseScale;
        }

        EndAnim(animTargetPhase);
    }

    /// <summary>
    /// Fade out texts  hide old card  spawn new card  invoke onParked callback.
    /// If revealController is null, skips text fade.
    /// </summary>
    private void SafeFadeOutAndSwap(Sprite icon, string label, System.Action onParked, Sprite cardBGSprite = null)
    {
        SafeFadeOut(() =>
        {
            HideActiveWorldCard(() =>
            {
                SpawnWorldCard(icon, label, onParked, cardBGSprite);
            });
        });
    }

    /// <summary>
    /// Fade out info texts if revealController exists, otherwise invoke immediately.
    /// </summary>
    private void SafeFadeOut(System.Action onDone)
    {
        if (revealController != null)
            revealController.FadeOutInfoTexts(onDone);
        else
            onDone?.Invoke();
    }

    // 
    //  TAP FEEDBACK (chest hop)
    // 

    private void PlayTapFeedback(bool openedFeel = false)
    {
        if (chestTr == null) return;
        DOTween.Kill("ChestIdle");
        chestTr.DOKill(true);

        // C2: Chest hop tap SFX (pitch rising per tap handled by SFXManager)
        if (SFXManager.Instance != null) SFXManager.Instance.PlayChestHop();

        Sequence s = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        s.Append(chestTr.DOScale(
            new Vector3(baseScale.x * 1.06f, baseScale.y * 0.94f, baseScale.z * 1.06f), 0.07f)
            .SetEase(Ease.OutSine));
        s.Append(chestTr.DOJump(basePos, tapJumpPower, 1, tapJumpDuration).SetEase(Ease.OutQuad));
        s.Join(chestTr.DOScale(baseScale, 0.10f).SetEase(Ease.OutSine));

        if (openedFeel)
            s.Join(chestTr.DOShakeRotation(0.20f, new Vector3(0f, 7f, 0f), 14, 90f));
        else
            s.Join(chestTr.DOShakeRotation(0.22f, new Vector3(0f, 10f, 0f), 18, 90f));
    }

    // 
    //  WORLD CARD SPAWN / HIDE
    // 

    private void SpawnWorldCard(Sprite icon, string overlayText, System.Action onParked, Sprite cardBGSprite = null)
    {
        if (worldCardPrefab == null)
        {
            Debug.LogError("[ChestOpenScene] worldCardPrefab NULL!");
            onParked?.Invoke();
            return;
        }

        // Park anchor fallback
        Transform resolvedPark = parkAnchor;
        if (resolvedPark == null && cam != null)
        {
            Vector3 def = basePos + Vector3.up * 2.5f + Vector3.left * 1.8f;
            var go = new GameObject("_DefaultParkAnchor");
            go.transform.position = def;
            resolvedPark = go.transform;
            Debug.Log($"[ChestOpenScene] parkAnchor fallback -> {def}");
        }

        Vector3 mouthPos = cardMouthAnchor != null ? cardMouthAnchor.position : basePos;
        GameObject cardGO = Instantiate(worldCardPrefab, mouthPos, Quaternion.identity);
        activeWorldCard = cardGO.GetComponent<WorldRewardCardController>();

        if (activeWorldCard == null)
        {
            Debug.LogError("[ChestOpenScene] WorldCardPrefab missing WorldRewardCardController!");
            Destroy(cardGO);
            onParked?.Invoke();
            return;
        }

        activeWorldCard.SetAnchors(cam, cardMouthAnchor, resolvedPark);
        activeWorldCard.ShowWorldCard(icon, overlayText, onParked, cardBGSprite);
        // C10: World card swoosh SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayWorldCardSwoosh();
        Debug.Log($"[ChestOpenScene] WorldCard spawned icon={icon?.name} label='{overlayText}'");
    }

    private void HideActiveWorldCard(System.Action onHidden)
    {
        if (activeWorldCard != null)
        {
            activeWorldCard.HideWorldCard(() =>
            {
                activeWorldCard = null;
                onHidden?.Invoke();
            });
        }
        else
        {
            onHidden?.Invoke();
        }
    }

    // 
    //  LID OPEN  AUTO MONEY REVEAL
    // 

    private void OpenLid()
    {
        if (lidBone == null)
        {
            Debug.LogWarning("[ChestOpenScene] OpenLid  no lidBone, skipping anim.");
            OnLidOpened();
            return;
        }

        phase = Phase.LidOpening;
        lidBone.DOKill(true);
        lidBone.localEulerAngles = lidClosedLocalEuler;

        // C3: Chest lid open SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayChestLidOpen();

        Debug.Log($"[ChestOpenScene] Opening lid {lidClosedLocalEuler} -> {lidOpenLocalEuler}");

        lidBone.DOLocalRotate(lidOpenLocalEuler, lidOpenDuration, RotateMode.Fast)
            .SetEase(lidEase)
            .SetLink(lidBone.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(OnLidOpened);
    }

    private void OnLidOpened()
    {
        Debug.Log("[ChestOpenScene] Lid opened  computing rewards, auto-revealing Money.");
        ComputeRewards();

        // ── Phase 1 Hardening: COMMIT rewards immediately so they are safe ──
        if (rewardPackage != null && ChestSessionManager.Instance != null)
        {
            ChestSessionManager.Instance.CommitRewards(rewardPackage);
            Debug.Log("[ChestOpenScene] Rewards COMMITTED via session manager. Reward is now safe from loss.");
        }
        else if (rewardPackage != null)
        {
            // Fallback: no session manager — grant directly (legacy behavior)
            Debug.LogWarning("[ChestOpenScene] No ChestSessionManager! Falling back to immediate grant.");
            GrantChestRewardsLegacy();
        }

        if (revealController != null)
            revealController.Initialize(rewardPackage);
        else
            Debug.LogError("[ChestOpenScene] revealController NULL  texts won't show.");

        // C4: Money reveal SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayRewardMoney();

        // Auto money reveal (tap 3's result)
        BeginAnimating(Phase.Reveal_Money);
        Sprite moneyIcon = revealController != null ? revealController.MoneySprite : null;
        string moneyLabel = rewardPackage != null ? $"+{rewardPackage.moneyGained:N0}" : "?";

        SpawnWorldCard(moneyIcon, moneyLabel, () =>
        {
            if (revealController != null)
                revealController.ShowMoneyInfo(() => EndAnim(Phase.Reveal_Money));
            else
                EndAnim(Phase.Reveal_Money);
        });
    }

    // 
    //  COMPUTE REWARDS
    // 

    private void ComputeRewards()
    {
        rewardPackage = new ChestRewardPackage();
        rewardPackage.chestType = cachedChestType;

        // Bug #7: Use the cached chest data (read + cleared from PlayerPrefs in Start)
        var chestData = cachedChestData;

        if (chestData == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[ChestOpenScene] No cached chest  DEBUG fallback.");
            chestData = new ChestInventoryManager.ChestData
            {
                chestType  = ChestType.Common,
                chestName  = "DebugChest",
                unlockDurationSeconds = 0f,
                state      = ChestState.Idle
            };
#else
            Debug.LogError("[ChestOpenScene] No cached chest data for reward computation!");
            return;
#endif
        }

        //  Money — per-type percentage of current balance, rounded to clean 10-based values.
        ChestTypeConfig.GetMoneyPercentRange(cachedChestType, out float goldMin, out float goldMax);
        double curMoney = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 0;
        double rawGold = curMoney * Random.Range(goldMin, goldMax);
        double goldReward = RoundToClean10(rawGold);
        rewardPackage.currentMoney = curMoney;
        rewardPackage.moneyMultiplier = 1;
        rewardPackage.moneyGained = goldReward;
        rewardPackage.finalMoneyShown = curMoney + goldReward;

        //  Nitro — per-type percentage of current balance, rounded to nearest 5, min 5.
        ChestTypeConfig.GetNitroPercentRange(cachedChestType, out float nitroMin, out float nitroMax);
        int curNitro = CurrencyManager.Instance != null ? CurrencyManager.Instance.nitroCoins : 0;
        int nitroRaw = Mathf.RoundToInt(curNitro * Random.Range(nitroMin, nitroMax));
        int nitroReward = Mathf.Max(5, Mathf.RoundToInt(nitroRaw / 5f) * 5);
        rewardPackage.nitroReward = nitroReward;
        rewardPackage.currentNitroCoins = curNitro;
        rewardPackage.finalNitroTotal = curNitro + nitroReward;

        //  Card (per-type rarity weights + level decay + dynamic multiplier) 
        bool cmExists = CardManager.Instance != null;
        bool cmHasCards = cmExists && CardManager.Instance.cards != null && CardManager.Instance.cards.Length > 0;
        Debug.Log($"[ChestOpenScene] Card guard: CardManager.Instance={(cmExists ? "EXISTS" : "NULL")} " +
                  $"cards={(cmHasCards ? CardManager.Instance.cards.Length.ToString() : "EMPTY/NULL")}");

        if (cmHasCards)
        {
            var cards = CardManager.Instance.cards;
            float[] typeWeights = ChestTypeConfig.GetCardRarityWeights(cachedChestType);
            var def = PickWeightedCard(cards, typeWeights);

            rewardPackage.cardType = def.type;
            rewardPackage.cardDisplayName = def.displayName;
            rewardPackage.cardIcon = def.icon;
            rewardPackage.cardArtSprite = def.cardArtSprite != null ? def.cardArtSprite : def.icon;
            rewardPackage.cardRarity = def.rarity;

            Debug.Log($"[ChestOpenScene] CardSelected: type={def.type} name='{def.displayName}' " +
                      $"rarity={def.rarity} icon={(def.icon != null ? def.icon.name : "NULL")} " +
                      $"artSprite={(def.cardArtSprite != null ? def.cardArtSprite.name : "NULL")} " +
                      $"copiesOwned={def.copiesOwned} level={def.currentLevel}");

            rewardPackage.preRewardCopiesOwned = Mathf.Clamp(def.copiesOwned, 0, CardDropTuning.SegmentsPerUpgrade);

            int multiplier = CardDropTuning.GetCardDropMultiplier(def.currentLevel);
            rewardPackage.cardCopies = multiplier;

            var progress = CardManager.Instance.GetCardProgress(def.type, rewardPackage.cardCopies);
            rewardPackage.postRewardLevel = progress.level;
            rewardPackage.postRewardCopiesOwned = progress.currentCopies;
            rewardPackage.copiesNeededForNext = progress.requiredCopies;
            rewardPackage.upgradeProgress01 = progress.fill01;

            Debug.Log($"[ChestOpenScene] CardProgress preview: {def.type} " +
                      $"baseSegments={def.copiesOwned} +{rewardPackage.cardCopies} => " +
                      $"{progress.currentCopies}/{progress.requiredCopies} " +
                      $"L{progress.level} fill01={progress.fill01:F3}");
        }
        else
        {
            Debug.LogError($"[ChestOpenScene] CARD REWARD SKIPPED! CardManager.Instance={(cmExists ? "EXISTS but cards null/empty" : "NULL")}.");
        }

        //  Sticker (4th reward, Rare/Legendary only) 
        Debug.Log($"[ChestOpenScene] 4th-reward check: chestType={cachedChestType}, eligible={ChestTypeConfig.CanHaveStickerReward(cachedChestType)}, dbAssigned={garageDatabase != null}");
        if (ChestTypeConfig.CanHaveStickerReward(cachedChestType))
        {
            if (StickerRewardHelper.TryPickRandomSticker(garageDatabase, out var sticker))
            {
                rewardPackage.hasStickerReward = true;
                rewardPackage.stickerCarId = sticker.carId;
                rewardPackage.stickerCarDisplayName = sticker.carDisplayName;
                rewardPackage.stickerIndex = sticker.stickerIndex;
                Debug.Log($"[ChestOpenScene] Sticker reward: car={sticker.carId} idx={sticker.stickerIndex} name={sticker.carDisplayName}");
            }
            else
            {
                Debug.Log("[ChestOpenScene] No unowned stickers available — 3-reward chest.");
            }
        }

        Debug.Log($"[ChestOpenScene] Rewards: Money=+{rewardPackage.moneyGained:N0} " +
                  $"(from {rewardPackage.currentMoney:N0}), " +
                  $"Nitro=+{rewardPackage.nitroReward} (total {rewardPackage.finalNitroTotal}), " +
                  $"Card={rewardPackage.cardDisplayName} x{rewardPackage.cardCopies}, " +
                  $"Sticker={rewardPackage.hasStickerReward} type={cachedChestType}");
    }

    /// <summary>
    /// Rounds a value to the nearest "clean" multiple of its order of magnitude.
    /// e.g. 75→80, 340→300, 2700→3000. Minimum return is 10.
    /// </summary>
    private static double RoundToClean10(double value)
    {
        if (value < 10) return 10;
        double magnitude = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(value)));
        return System.Math.Max(10, System.Math.Round(value / magnitude, System.MidpointRounding.AwayFromZero) * magnitude);
    }

    // 
    //  CARD SELECTION (weighted rarity + level decay)
    // 

    /// <summary>
    /// Picks a card using per-type rarity weights + level decay.
    /// </summary>
    private CardDefinition PickWeightedCard(CardDefinition[] cards, float[] rarityWeights)
    {
        float totalWeight = 0f;
        float[] weights = new float[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            int rarityIdx = Mathf.Clamp((int)cards[i].rarity, 0, rarityWeights.Length - 1);
            float rarityW = rarityWeights[rarityIdx];
            float decay = CardDropTuning.LevelDecay(cards[i].currentLevel);
            weights[i] = rarityW * decay;
            totalWeight += weights[i];
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[ChestOpenScene] PickWeightedCard: zero total weight, uniform random.");
            return cards[Random.Range(0, cards.Length)];
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < cards.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
            {
                Debug.Log($"[ChestOpenScene] PickWeightedCard: {cards[i].rarity} L{cards[i].currentLevel} -> {cards[i].displayName} (w={weights[i]:F2})");
                return cards[i];
            }
        }

        // Fallback (floating-point edge case)
        return cards[cards.Length - 1];
    }

    // 
    //  EXIT  GRANT & RETURN
    // 

    private void FinishAndReturnToMain()
    {
        if (rewardsGranted) return;
        rewardsGranted = true;

        // ── Phase 1 Hardening: Rewards already committed in OnLidOpened(). ──
        // Tap 7 just finalizes the session and returns to Main.

        // Complete the session (clears persisted session data)
        if (ChestSessionManager.Instance != null)
        {
            ChestSessionManager.Instance.CompleteSession();
            Debug.Log("[ChestOpenScene] Session completed.");
        }

        // Clear legacy pending key (safety net)
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.ClearPendingOpenChest();

        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        SceneManager.LoadScene("Main");
    }

    /// <summary>
    /// Legacy fallback: grant rewards directly if ChestSessionManager is unavailable.
    /// Only called when the session manager singleton is missing.
    /// </summary>
    private void GrantChestRewardsLegacy()
    {
        if (rewardPackage == null)
        {
            Debug.LogError("[ChestOpenScene] rewardPackage NULL!");
            return;
        }

        if (CurrencyManager.Instance != null && rewardPackage.moneyGained > 0)
            CurrencyManager.Instance.AddMoney(rewardPackage.moneyGained);

        if (CurrencyManager.Instance != null && rewardPackage.nitroReward > 0)
            CurrencyManager.Instance.AddNitroCoins(rewardPackage.nitroReward);

        if (CardManager.Instance != null && rewardPackage.cardCopies > 0)
            CardManager.Instance.AddCardCopies(rewardPackage.cardType, rewardPackage.cardCopies);

        if (rewardPackage.hasStickerReward)
        {
            StickerRewardHelper.GrantSticker(new StickerRewardHelper.StickerReward
            {
                carId = rewardPackage.stickerCarId,
                stickerIndex = rewardPackage.stickerIndex
            });
        }

        // Also remove chest from inventory (legacy path: it may still be OpeningInProgress)
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.RemoveOpeningChest();

        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        Debug.Log("[ChestOpenScene] Legacy reward grant completed.");
    }

    // kept for backward compat; no longer the primary grant path
    private bool GrantChestRewards()
    {
        if (rewardPackage == null)
        {
            Debug.LogError("[ChestOpenScene] rewardPackage NULL!");
            return false;
        }

        // Money  add the GAINED amount (not the multiplied total)
        if (CurrencyManager.Instance != null && rewardPackage.moneyGained > 0)
        {
            CurrencyManager.Instance.AddMoney(rewardPackage.moneyGained);
            Debug.Log($"[ChestReward] Money +{rewardPackage.moneyGained:N0} " +
                      $"({rewardPackage.moneyMultiplier}x of {rewardPackage.currentMoney:N0})");
        }

        // Nitro
        if (CurrencyManager.Instance != null && rewardPackage.nitroReward > 0)
        {
            CurrencyManager.Instance.AddNitroCoins(rewardPackage.nitroReward);
            Debug.Log($"[ChestReward] NitroCoins +{rewardPackage.nitroReward}");
        }

        // Card copies
        if (CardManager.Instance != null && rewardPackage.cardCopies > 0)
        {
            CardManager.Instance.AddCardCopies(rewardPackage.cardType, rewardPackage.cardCopies);
            Debug.Log($"[ChestReward] Card +{rewardPackage.cardCopies} {rewardPackage.cardType}");
        }

        return true;
    }

    // 
    //  LID FINDING UTILITIES
    // 

    private Transform FindLidTransform(Transform root)
    {
        if (!string.IsNullOrEmpty(lidSearchPath))
        {
            var t = root.Find(lidSearchPath);
            if (t != null) { Debug.Log($"[ChestOpenScene] Lid via path: {lidSearchPath}"); return t; }
        }
        var legacy = root.Find("body/bone001");
        if (legacy != null) { Debug.Log("[ChestOpenScene] Lid via legacy path"); return legacy; }
        if (!string.IsNullOrEmpty(lidTransformName))
        {
            var f = FindChildRecursive(root, lidTransformName);
            if (f != null) { Debug.Log($"[ChestOpenScene] Lid via recursive: {lidTransformName}"); return f; }
        }
        return null;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var f = FindChildRecursive(child, name);
            if (f != null) return f;
        }
        return null;
    }

    private Transform CreateRuntimePivot(Transform originalLid)
    {
        Transform lidParent = originalLid.parent;
        GameObject pivotGO = new GameObject("LidPivot_Runtime");
        Transform pivot = pivotGO.transform;
        pivot.SetParent(lidParent, false);

        Renderer r = originalLid.GetComponent<Renderer>();
        pivot.position = r != null
            ? originalLid.TransformPoint(pivotOffset)
            : originalLid.position + originalLid.TransformDirection(pivotOffset);
        pivot.rotation = originalLid.rotation;
        originalLid.SetParent(pivot, true);

        Debug.Log($"[ChestOpenScene] Runtime pivot at {pivot.position}");
        return pivot;
    }

    private string GetTransformPath(Transform t)
    {
        string path = t.name;
        Transform cur = t.parent;
        while (cur != null && cur != chestTr) { path = cur.name + "/" + path; cur = cur.parent; }
        return path;
    }

    private void PrintHierarchy(Transform root, int indent = 0)
    {
        Debug.Log($"{new string(' ', indent * 2)}- {root.name}");
        foreach (Transform child in root) PrintHierarchy(child, indent + 1);
    }
}