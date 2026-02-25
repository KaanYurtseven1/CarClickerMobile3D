using UnityEngine;
using UnityEngine.SceneManagement;
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
        Summary,            // tap 6 result  wait for tap 7
        Exit                // tap 7 fires grant + scene change
    }

    // 
    //  INSPECTOR
    // 

    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject chestPrefab;

    [Header("Lid Settings")]
    [SerializeField] private Transform lidBone;
    [SerializeField] private string lidTransformName = "Cube.004";
    [SerializeField] private string lidSearchPath = "Empty/Cube.004";
    [SerializeField] private Vector3 lidClosedLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 lidOpenLocalEuler = new Vector3(-110f, 0, 0);

    [Header("Runtime Pivot Fix")]
    [SerializeField] private bool useRuntimePivotFix = false;
    [SerializeField] private Vector3 pivotOffset = new Vector3(0, 0.5f, -0.5f);

    [Header("Reward Reveal")]
    [Tooltip("ChestRewardRevealController on RewardRevealRoot")]
    [SerializeField] private ChestRewardRevealController revealController;

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

    // Bug #7: Cached chest data (read + cleared from PlayerPrefs on scene entry)
    private ChestInventoryManager.ChestData cachedChestData;

    // Bug #2: Animation watchdog
    [Header("Animation Safety")]
    [Tooltip("Max seconds isAnimating can stay true before auto-recovery")]
    [SerializeField] private float animationTimeout = 4f;
    private float animStartTime;
    private Phase animTargetPhase;

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

        DLog($"Start ENTRY — ChestInventoryManager.Instance={(ChestInventoryManager.Instance != null ? ChestInventoryManager.Instance.name + "#" + ChestInventoryManager.Instance.GetInstanceID() : "NULL")}");

        // ── Bug #7 fix: read pending chest ONCE, cache in-memory, clear PlayerPrefs key immediately ──
        cachedChestData = ReadAndClearPendingChest();
        if (cachedChestData == null)
        {
            Debug.LogError("[ChestOpenScene] No pending chest data on scene entry! Returning to Main safely.");
            DWarn("Start — cachedChestData is NULL after ReadAndClearPendingChest, loading Main");
            SceneManager.LoadScene("Main");
            return;
        }
        DLog($"Start — cachedChestData OK: name='{cachedChestData.chestName}' cardReward={cachedChestData.cardReward}");

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
    /// Bug #7: Read the pending chest from PlayerPrefs, cache it, and immediately
    /// delete the key so it can never leak into a future chest open if the app
    /// crashes or is force-quit mid-reveal.
    /// </summary>
    private ChestInventoryManager.ChestData ReadAndClearPendingChest()
    {
        DLog($"ReadAndClearPendingChest ENTRY — ChestInventoryManager.Instance={(ChestInventoryManager.Instance != null ? "EXISTS (" + ChestInventoryManager.Instance.name + "#" + ChestInventoryManager.Instance.GetInstanceID() + ")" : "NULL")}");

        if (ChestInventoryManager.Instance == null)
        {
            Debug.LogError("[ChestOpenScene] ChestInventoryManager.Instance is NULL!");
            if (debugLogs)
            {
                DWarn("ReadAndClearPendingChest — Instance is NULL, printing stack trace for diagnosis");
                Debug.LogWarning(System.Environment.StackTrace);
            }
            return null;
        }

        DLog("ReadAndClearPendingChest — calling GetPendingOpenChest()");
        var data = ChestInventoryManager.Instance.GetPendingOpenChest();
        DLog($"ReadAndClearPendingChest — GetPendingOpenChest returned {(data != null ? $"'{data.chestName}'" : "NULL")}");
        if (data != null)
        {
            // Clear immediately — this is the critical fix for Bug #7.
            ChestInventoryManager.Instance.ClearPendingOpenChest();
            PlayerPrefs.Save(); // force flush to disk
            Debug.Log($"[ChestOpenScene] Pending chest cached & PlayerPrefs key cleared: '{data.chestName}' cardReward={data.cardReward}");
        }
        return data;
    }

    // 
    //  CHEST SPAWN
    // 

    private void SpawnChest()
    {
        if (spawnPoint == null || chestPrefab == null)
        {
            Debug.LogError("[ChestOpenScene] spawnPoint or chestPrefab missing!");
            return;
        }

        chestGO = Instantiate(chestPrefab, spawnPoint.position, spawnPoint.rotation);
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
    //  INTRO ANIM
    // 

    private void PlayIntro()
    {
        if (chestTr == null) return;
        phase = Phase.Intro;
        chestTr.DOKill(true);

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
        });
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

        if (phase == Phase.Intro || phase == Phase.LidOpening) return;
        if (cam == null) return;
        if (isAnimating) return;

        bool tapDetected = false;
        Vector2 screenPos = Vector2.zero;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) { tapDetected = true; screenPos = Input.mousePosition; }
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        { tapDetected = true; screenPos = Input.GetTouch(0).position; }
#endif

        if (!tapDetected) return;

        // Post-lid phases: accept any screen tap (no raycast needed)
        if (phase >= Phase.Reveal_Money)
        {
            OnChestTapped();
            return;
        }

        // Pre-lid: require raycast on chest
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
            BeginAnimating(Phase.Reveal_Card);
            Debug.Log($"[ChestOpenScene] TAP #{globalTapCount} -> RevealCard");
            // Use full card art on CardBG renderer; format is "Nx" (e.g. "4x")
            Sprite cardBGSprite = rewardPackage?.cardArtSprite;
            string cardLabel = rewardPackage != null ? $"{rewardPackage.cardCopies}x" : "";
            SafeFadeOutAndSwap(null, cardLabel, () =>   // icon=null: CardBG mode handles visuals
            {
                if (revealController != null)
                    revealController.ShowCardInfo(() => EndAnim(Phase.Reveal_Card));
                else
                    EndAnim(Phase.Reveal_Card);
            }, cardBGSprite);
            return;
        }

        //  TAP 6  (Reveal_Card  Summary)  NO hop 
        if (phase == Phase.Reveal_Card)
        {
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

        //  TAP 7  (Summary  Exit) 
        if (phase == Phase.Summary)
        {
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

        // Kill all active tweens for this scene
        if (chestTr != null) chestTr.DOKill();
        if (lidBone != null) lidBone.DOKill();
        if (activeWorldCard != null)
        {
            activeWorldCard.transform.DOKill();
            Destroy(activeWorldCard.gameObject);
            activeWorldCard = null;
        }
        if (revealController != null) DOTween.Kill(revealController);

        // If rewards haven't been computed yet, bail to Main without granting
        if (rewardPackage == null)
        {
            Debug.LogError("[ChestOpenScene] WATCHDOG: No rewardPackage — returning to Main without rewards.");
            isAnimating = false;
            phase = Phase.Exit;
            SceneManager.LoadScene("Main");
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
        chestTr.DOKill(true);

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

        if (revealController != null)
            revealController.Initialize(rewardPackage);
        else
            Debug.LogError("[ChestOpenScene] revealController NULL  texts won't show.");

        // Auto money reveal (tap 3's result)
        BeginAnimating(Phase.Reveal_Money);
        Sprite moneyIcon = revealController != null ? revealController.MoneySprite : null;
        string moneyLabel = rewardPackage != null ? $"{rewardPackage.moneyMultiplier}x" : "?x";

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

        // Bug #7: Use the cached chest data (read + cleared from PlayerPrefs in Start)
        var chestData = cachedChestData;

        if (chestData == null)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[ChestOpenScene] No cached chest  DEBUG fallback.");
            chestData = new ChestInventoryManager.ChestData
            {
                chestName  = "DebugChest",
                minReward  = 500,
                maxReward  = 2000,
                cardReward = 1,
                turboMin   = 1,
                turboMax   = 3
            };
#else
            Debug.LogError("[ChestOpenScene] No cached chest data for reward computation!");
            return;
#endif
        }

        //  Money (multiplier-based on current balance) 
        double curMoney = CurrencyManager.Instance != null ? CurrencyManager.Instance.money : 1000;
        rewardPackage.currentMoney = curMoney;
        rewardPackage.moneyMultiplier = ChestRewardPackage.MoneyMultipliers[
            Random.Range(0, ChestRewardPackage.MoneyMultipliers.Length)];
        rewardPackage.moneyGained = curMoney * (rewardPackage.moneyMultiplier - 1);
        rewardPackage.finalMoneyShown = curMoney * rewardPackage.moneyMultiplier;

        //  Nitro (additive) 
        rewardPackage.nitroReward = ChestRewardPackage.NitroAmounts[
            Random.Range(0, ChestRewardPackage.NitroAmounts.Length)];
        rewardPackage.currentNitroCoins = CurrencyManager.Instance != null
            ? CurrencyManager.Instance.nitroCoins : 0;
        rewardPackage.finalNitroTotal = rewardPackage.currentNitroCoins + rewardPackage.nitroReward;

        //  Card (weighted rarity + level decay + dynamic multiplier) 
        if (CardManager.Instance != null &&
            CardManager.Instance.cards != null &&
            CardManager.Instance.cards.Length > 0)
        {
            var cards = CardManager.Instance.cards;
            var def = PickWeightedCard(cards);

            rewardPackage.cardType = def.type;
            rewardPackage.cardDisplayName = def.displayName;
            rewardPackage.cardIcon = def.icon;
            // Full card artwork for world-space reveal; fall back to icon if not set
            rewardPackage.cardArtSprite = def.cardArtSprite != null ? def.cardArtSprite : def.icon;
            rewardPackage.cardRarity = def.rarity;

            // C: Snapshot pre-reward copies for progress bar animation
            rewardPackage.preRewardCopiesOwned = Mathf.Clamp(def.copiesOwned, 0, CardDropTuning.SegmentsPerUpgrade);

            // Dynamic segment multiplier based on selected card's level
            int multiplier = CardDropTuning.GetCardDropMultiplier(def.currentLevel);
            rewardPackage.cardCopies = multiplier;  // segments granted

            // Simulated post-reward preview using canonical CardManager method
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
            Debug.LogWarning("[ChestOpenScene] No cards in CardManager  card reward skipped.");
        }

        Debug.Log($"[ChestOpenScene] Rewards: Money={rewardPackage.finalMoneyShown:N0} " +
                  $"({rewardPackage.moneyMultiplier}x of {rewardPackage.currentMoney:N0}), " +
                  $"Nitro=+{rewardPackage.nitroReward} (total {rewardPackage.finalNitroTotal}), " +
                  $"Card={rewardPackage.cardDisplayName} x{rewardPackage.cardCopies}");
    }

    // 
    //  CARD SELECTION (weighted rarity + level decay)
    // 

    /// <summary>
    /// Picks a card using:
    ///   weight(card) = rarityBaseWeight * LevelDecay(card.level)
    /// This makes high-level cards less likely but never impossible.
    /// </summary>
    private CardDefinition PickWeightedCard(CardDefinition[] cards)
    {
        float totalWeight = 0f;
        float[] weights = new float[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            float rarityW = CardDropTuning.RarityBaseWeights[(int)cards[i].rarity];
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

        bool ok = GrantChestRewards();
        Debug.Log($"[ChestOpenScene] Rewards granted: {ok}");

        // Bug #7: Safety net — clear pending key again. Normally already cleared in
        // Start(), but this guards against edge cases (e.g. PlayerPrefs.Save failed earlier).
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.ClearPendingOpenChest();

        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        SceneManager.LoadScene("Main");
    }

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