using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using DG.Tweening;

/// <summary>
/// Police Catch minigame controller.
/// 
/// Flow: Idle → Enter → PromptLoop → Success/Fail → Exit → Idle
///
/// During the chase, TapInputRaycaster routes car taps to OnChaseTap()
/// instead of the normal economy path (tap isolation via isPoliceChaseActive flag).
/// </summary>
public class PoliceCatchController : MonoBehaviour
{
    public static PoliceCatchController Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    // ==================== SERIALIZED ====================

    [Header("Scene References")]
    [SerializeField] private Transform playerCar;
    [SerializeField] private Transform policeCar;

    [Header("UI")]
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image timerFill;
    [SerializeField] private TMP_Text wastedText;

    [Header("Prompt Settings")]
    [Tooltip("Total number of tap prompts per chase.")]
    [SerializeField] private int promptCount = 10;
    [Tooltip("Maximum failed prompts before losing.")]
    [SerializeField] private int maxWasted = 3;
    [Tooltip("Duration per required tap (seconds). E.g. 3x = 3 * 0.5 = 1.5s.")]
    [SerializeField] private float secondsPerTap = 0.5f;

    [Header("Car Z-Offset Pop")]
    [Tooltip("Z-axis local offset applied to player car when chase begins (positive = forward).")]
    [SerializeField] private float zPopOffset = 0.6f;
    [Tooltip("Duration of Z-offset enter/exit pop (seconds).")]
    [SerializeField] private float zPopDuration = 0.25f;
    [Tooltip("Ease curve for Z-offset pop. If empty/no keys, defaults to InOutSine.")]
    [SerializeField] private AnimationCurve zPopEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Log Z-pop enter/exit transitions to console.")]
    [SerializeField] private bool enableZPopLogs = false;

    [Header("Police Car Animation")]
    [Tooltip("Duration of police car enter/exit movement.")]
    [SerializeField] private float moveDuration = 0.6f;

    [Header("Police Car Positions")]
    [Tooltip("Local position of police car when hidden (below screen, negative Y).")]
    [SerializeField] private Vector3 policeHiddenLocalPos = new Vector3(0f, 0f, -8f);
    [Tooltip("Local position of police car when chasing (behind player, negative Y offset).")]
    [SerializeField] private Vector3 policeChaseLocalPos = new Vector3(0f, 0f, -2f);

    [Header("Police Sway")]
    [Tooltip("Horizontal sway amplitude during chase.")]
    [SerializeField] private float swayAmount = 0.15f;
    [Tooltip("Duration of one sway cycle (left-right).")]
    [SerializeField] private float swayDuration = 0.8f;

    [Header("Reward (Success)")]
    [Tooltip("Number of nitro coins spawned on success.")]
    [SerializeField] private int rewardCoinCount = 10;
    [Tooltip("Interval between reward coin spawns (seconds).")]
    [SerializeField] private float rewardCoinInterval = 0.12f;
    [SerializeField] private NitroCoinSpawner rewardSpawner;

    [Header("Penalty (Fail)")]
    [Tooltip("Money multiplier on fail (0.75 = lose 25%).")]
    [SerializeField] private float failMoneyMultiplier = 0.75f;

    // ==================== RUNTIME STATE ====================

    /// <summary>True while the minigame is active (any state except Idle).</summary>
    public bool IsChaseActive => _state != ChaseState.Idle;

    private enum ChaseState { Idle, Enter, PromptLoop, Success, Fail, Exit }
    private ChaseState _state = ChaseState.Idle;

    // Prompt data
    private int[] _promptTaps;       // required taps per prompt
    private float[] _promptDurations; // duration per prompt
    private int _currentPromptIndex;
    private int _currentTapCount;
    private int _wastedCount;
    private float _promptTimer;

    // Car original position (for restore)
    private Vector3 _playerCarOriginalLocal;
    private Vector3 _policeCarOriginalLocal;

    // Result-flash window — coroutine sets false, DOTween OnComplete sets true
    private bool _resultWindowDone;
    // Blocks taps during result flash and prevents Update from doing anything extra
    private bool _isShowingResult;
    private Color _timerFillDefaultColor;

    // Reference to TapInputRaycaster for chase flag
    private TapInputRaycaster _tapInput;

    // Z-pop tween handle (interruption-safe: kill before starting a new one)
    private Tweener _zPopTween;

    // Scene-loaded subscription guard
    private bool _sceneLoadedSubscribed = false;

    // ==================== LIFECYCLE ====================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Ensure UI is disabled immediately — BEFORE any other script can see it
        ForceHideUI();
    }

    private void OnEnable()
    {
        // Every time this component enables (including scene load), force UI off
        ForceHideUI();
        SubscribeSceneLoaded();
    }

    private void Start()
    {
        _tapInput = FindFirstObjectByType<TapInputRaycaster>();
        if (_tapInput == null)
            Debug.LogWarning("[PoliceCatch] TapInputRaycaster not found in scene.");

        // Hide police car at start (disabled entirely when not chasing)
        if (policeCar != null)
        {
            policeCar.localPosition = policeHiddenLocalPos;
            policeCar.gameObject.SetActive(false);
        }

        // Capture default timer fill color for flash resets
        if (timerFill != null)
            _timerFillDefaultColor = timerFill.color;

        // Redundant safety: ensure UI is still off after all Awake/OnEnable ran
        ForceHideUI();
    }

    private void OnDisable()
    {
        UnsubscribeSceneLoaded();
    }

    private void OnDestroy()
    {
        // Clear singleton
        if (Instance == this)
            Instance = null;

        UnsubscribeSceneLoaded();

        // Safety: ensure tap isolation is cleared if this object is destroyed mid-chase
        if (_tapInput != null)
            _tapInput.isPoliceChaseActive = false;

        // Kill any lingering DOTween animations
        StopSway();
        _zPopTween?.Kill();
        _zPopTween = null;
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();
        if (timerFill != null)
        {
            timerFill.DOKill();
            timerFill.rectTransform.DOKill();
        }
    }

    // ==================== SCENE LOAD ====================

    private void SubscribeSceneLoaded()
    {
        if (!_sceneLoadedSubscribed)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneLoadedSubscribed = true;
        }
    }

    private void UnsubscribeSceneLoaded()
    {
        if (_sceneLoadedSubscribed)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneLoadedSubscribed = false;
        }
    }

    /// <summary>
    /// Called when any scene finishes loading. Ensures PoliceCatchUI is OFF
    /// unless an active chase is in progress (which should never happen on load).
    /// Also re-acquires scene references that were lost due to DontDestroyOnLoad.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[PoliceCatch] OnSceneLoaded({scene.name}) — state={_state}, uiRoot={(uiRoot != null ? uiRoot.name : "NULL")}");

        // Re-acquire scene references when Main loads (they go null after DDOL scene switch)
        if (scene.name == "Main")
        {
            RebindSceneReferences();
        }

        if (_state == ChaseState.Idle)
        {
            ForceHideUI();
            Debug.Log($"[PoliceCatch] OnSceneLoaded({scene.name}) — UI forced OFF (state=Idle).");
        }
    }

    /// <summary>
    /// Re-acquires serialized references that point to Main scene objects.
    /// Required because this component lives on a DontDestroyOnLoad GameManager,
    /// so after switching to ChestOpenScene and back, all scene references become null.
    /// </summary>
    private void RebindSceneReferences()
    {
        // --- uiRoot (PoliceCatchUI) ---
        if (uiRoot == null)
        {
            GameObject found = GameObject.Find("PoliceCatchUI");
            if (found != null)
            {
                uiRoot = found;
                Debug.Log("[PoliceCatch] RebindSceneReferences: uiRoot re-acquired by name.");
            }
            else
            {
                Debug.LogWarning("[PoliceCatch] RebindSceneReferences: PoliceCatchUI not found in scene!");
            }
        }

        // --- TapInputRaycaster ---
        if (_tapInput == null)
        {
            _tapInput = FindFirstObjectByType<TapInputRaycaster>();
            if (_tapInput != null)
                Debug.Log("[PoliceCatch] RebindSceneReferences: TapInputRaycaster re-acquired.");
        }

        // --- playerCar ---
        if (playerCar == null)
        {
            GameObject carObj = GameObject.FindWithTag("Car");
            if (carObj != null)
            {
                playerCar = carObj.transform;
                Debug.Log("[PoliceCatch] RebindSceneReferences: playerCar re-acquired via tag.");
            }
        }

        // --- policeCar ---
        if (policeCar == null)
        {
            GameObject policeObj = GameObject.Find("PoliceCar");
            if (policeObj != null)
            {
                policeCar = policeObj.transform;
                policeCar.localPosition = policeHiddenLocalPos;
                policeCar.gameObject.SetActive(false);
                Debug.Log("[PoliceCatch] RebindSceneReferences: policeCar re-acquired by name.");
            }
        }

        // --- UI sub-elements (search under uiRoot) ---
        if (uiRoot != null)
        {
            if (promptText == null)
            {
                var tmp = uiRoot.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (tmp != null)
                {
                    promptText = tmp;
                    Debug.Log("[PoliceCatch] RebindSceneReferences: promptText re-acquired.");
                }
            }
            if (timerFill == null)
            {
                var fill = uiRoot.GetComponentInChildren<Image>(true);
                // timerFill is a fill-type Image; find the one that isn't Dim_BG
                var images = uiRoot.GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.type == Image.Type.Filled)
                    {
                        timerFill = img;
                        _timerFillDefaultColor = timerFill.color;
                        Debug.Log("[PoliceCatch] RebindSceneReferences: timerFill re-acquired.");
                        break;
                    }
                }
            }
        }

        // Ensure tap isolation is off when idle
        if (_state == ChaseState.Idle && _tapInput != null)
            _tapInput.isPoliceChaseActive = false;
    }

    /// <summary>
    /// Unconditionally hides the police chase UI and police car.
    /// If uiRoot is null (lost after scene switch), attempts to find it by name.
    /// Also ensures CanvasGroup is non-blocking when hidden.
    /// Safe to call multiple times.
    /// </summary>
    private void ForceHideUI()
    {
        // If uiRoot reference was lost (DDOL scene switch), try to find it
        if (uiRoot == null)
        {
            GameObject found = GameObject.Find("PoliceCatchUI");
            if (found != null)
            {
                uiRoot = found;
                Debug.Log("[PoliceCatch] ForceHideUI — uiRoot was NULL, re-acquired by name.");
            }
        }

        if (uiRoot != null)
        {
            if (uiRoot.activeSelf)
            {
                uiRoot.SetActive(false);
                Debug.Log("[PoliceCatch] ForceHideUI — uiRoot disabled.");
            }

            // Ensure CanvasGroup does not block raycasts when hidden
            EnsureCanvasGroupNonBlocking(uiRoot);

            // Notify guard (handles Dim_BG image raycast targets)
            var guard = uiRoot.GetComponent<PoliceCatchUIGuard>();
            if (guard != null) guard.OnHide();
        }
    }

    /// <summary>
    /// If the given GameObject has a CanvasGroup (or we add one),
    /// set it to non-blocking and non-interactable.
    /// </summary>
    private void EnsureCanvasGroupNonBlocking(GameObject go)
    {
        if (go == null) return;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    /// <summary>
    /// Restores CanvasGroup to visible/interactive state when showing the UI.
    /// </summary>
    private void EnsureCanvasGroupBlocking(GameObject go)
    {
        if (go == null) return;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
    }

    private void Update()
    {
        if (_state != ChaseState.PromptLoop) return;
        if (_isShowingResult) return; // flash playing — freeze timer & fill

        if (_promptTimer > 0f)
        {
            _promptTimer -= Time.deltaTime;
            if (_promptTimer < 0f) _promptTimer = 0f;
        }

        // Update timer fill (charge bar: 0 → 1)
        if (timerFill != null)
        {
            float totalDuration = _promptDurations[_currentPromptIndex];
            timerFill.fillAmount = Mathf.Clamp01(1f - _promptTimer / totalDuration);
        }
        // NOTE: Evaluation is driven by the coroutine, NOT by Update.
    }

    // ==================== PUBLIC API ====================

    /// <summary>
    /// Called by TapInputRaycaster when the player taps the car during police chase.
    /// Completely isolated from economy — no money, no momentum, no magnet.
    /// </summary>
    public void OnChaseTap()
    {
        if (_state != ChaseState.PromptLoop) return;
        if (_isShowingResult) return; // ignore taps during result flash
        _currentTapCount++;
    }

    /// <summary>
    /// Start a police chase. Call this from whatever trigger system you build
    /// (popularity threshold, random event, etc.).
    /// </summary>
    public void StartChase()
    {
        if (_state != ChaseState.Idle)
        {
            Debug.LogWarning("[PoliceCatch] Chase already in progress.");
            return;
        }

        StartCoroutine(ChaseSequence());
    }

    // ==================== CHASE SEQUENCE ====================

    private IEnumerator ChaseSequence()
    {
        // ── ENTER ──
        _state = ChaseState.Enter;
        GeneratePrompts();
        _wastedCount = 0;

        // Enable tap isolation
        if (_tapInput != null)
            _tapInput.isPoliceChaseActive = true;

        // Save car positions
        if (playerCar != null)
            _playerCarOriginalLocal = playerCar.localPosition;
        if (policeCar != null)
        {
            _policeCarOriginalLocal = policeCar.localPosition;
            // Enable police car before enter animation
            policeCar.gameObject.SetActive(true);
        }

        // Animate player car forward + police car in
        yield return StartCoroutine(AnimateEnter());

        // Hide top bar (same animation as Shop & Cards)
        if (TopBarAnimator.Instance != null)
            TopBarAnimator.Instance.HideAnimated();

        // Show UI
        if (uiRoot != null)
        {
            uiRoot.SetActive(true);
            EnsureCanvasGroupBlocking(uiRoot);

            // Notify guard (restores Dim_BG image raycast targets)
            var guard = uiRoot.GetComponent<PoliceCatchUIGuard>();
            if (guard != null) guard.OnShow();

            Debug.Log("[PoliceCatch] Chase UI shown — CanvasGroup set to blocking.");
        }

        UpdateWastedUI();

        // ── PROMPT LOOP ──
        _state = ChaseState.PromptLoop;

        for (_currentPromptIndex = 0; _currentPromptIndex < promptCount; _currentPromptIndex++)
        {
            // ── Setup this prompt ──
            _currentTapCount = 0;
            _promptTimer = _promptDurations[_currentPromptIndex];
            _isShowingResult = false;
            _resultWindowDone = false;
            UpdatePromptUI();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PoliceCatch] OnStartPrompt index={_currentPromptIndex} required={_promptTaps[_currentPromptIndex]} duration={_promptDurations[_currentPromptIndex]:F2}s");
#endif

            // ── Wait for timer to expire (Update ticks it down) ──
            while (_promptTimer > 0f && _state == ChaseState.PromptLoop)
                yield return null;

            if (_state != ChaseState.PromptLoop)
                break;

            // ── Evaluate ONCE ──
            EvaluatePrompt();

            // If state changed (fail triggered), break out
            if (_state != ChaseState.PromptLoop)
                break;

            // ── Wait for result flash to finish ──
            while (!_resultWindowDone && _state == ChaseState.PromptLoop)
                yield return null;

            // Reset result state before next prompt
            _isShowingResult = false;

            if (_state != ChaseState.PromptLoop)
                break;
        }

        // If we completed all prompts without failing
        if (_state == ChaseState.PromptLoop)
        {
            _state = ChaseState.Success;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[PoliceCatch] OnChaseSuccess (all prompts completed)");
#endif
        }

        bool isFail = (_state == ChaseState.Fail);
        bool isSuccess = (_state == ChaseState.Success);
        double penaltyBefore = 0;
        double penaltyAfter = 0;

        // ── RESULT (TopBar stays hidden — money animations play after exit) ──
        // Enable suppress early so MPS/TAP earnings buffer during exit animations too.
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.suppressTopBarMoneyUpdates = true;
            CurrencyManager.Instance.bufferedEarnings = 0;
        }

        if (isSuccess)
        {
            Debug.Log("[PoliceCatch] SUCCESS — player escaped!");
            if (promptText != null)
                promptText.text = "ESCAPED!";
            yield return new WaitForSeconds(0.5f);
        }
        else if (isFail)
        {
            Debug.Log("[PoliceCatch] FAIL — caught by police!");
            if (promptText != null)
                promptText.text = "BUSTED!";

            // Apply penalty directly (no animation yet — that plays after TopBar shows)
            if (CurrencyManager.Instance != null)
            {
                var cm = CurrencyManager.Instance;
                penaltyBefore = cm.money;
                cm.money = penaltyBefore * failMoneyMultiplier;
                penaltyAfter = cm.money;
                Debug.Log($"[PoliceCatch] FAIL penalty applied: before={penaltyBefore:F0} after={penaltyAfter:F0}");
            }
            yield return new WaitForSeconds(0.5f);
        }

        // ── EXIT ──
        _state = ChaseState.Exit;

        if (uiRoot != null)
        {
            uiRoot.SetActive(false);
            EnsureCanvasGroupNonBlocking(uiRoot);

            // Notify guard
            var guard = uiRoot.GetComponent<PoliceCatchUIGuard>();
            if (guard != null) guard.OnHide();

            Debug.Log("[PoliceCatch] Chase UI hidden — CanvasGroup set to non-blocking.");
        }

        yield return StartCoroutine(AnimateExit());

        // Disable police car after exit animation completes
        if (policeCar != null)
            policeCar.gameObject.SetActive(false);

        // Disable tap isolation
        if (_tapInput != null)
            _tapInput.isPoliceChaseActive = false;

        // ── Show TopBar ONLY after exit + return animations complete ──
        Debug.Log("[PoliceCatch] Showing TopBar after exit+return");
        if (TopBarAnimator.Instance != null)
            TopBarAnimator.Instance.ShowAnimated();

        // Small delay to let TopBar animate in before showing money feedback
        yield return new WaitForSeconds(0.35f);

        // ── Post-exit money feedback (TopBar now visible) ──
        if (isFail && CurrencyManager.Instance != null)
        {
            var cm = CurrencyManager.Instance;

            // 1) Play penalty count-down animation (red blink)
            bool penaltyAnimDone = false;
            if (CurrencyUI.Instance != null)
            {
                CurrencyUI.Instance.PlayPenaltyAnimation(penaltyBefore, penaltyAfter, 1f, () =>
                {
                    penaltyAnimDone = true;
                });
            }
            else
            {
                penaltyAnimDone = true;
            }
            while (!penaltyAnimDone)
                yield return null;

            // 2) Apply buffered earnings with visible count-up
            double buffered = cm.bufferedEarnings;
            Debug.Log($"[PoliceCatch] FAIL before={penaltyBefore:F0} afterPenalty={penaltyAfter:F0} buffered={buffered:F0}");

            if (buffered > 0 && CurrencyUI.Instance != null)
            {
                bool buffAnimDone = false;
                CurrencyUI.Instance.PlayBufferedEarningsApplyAnimation(penaltyAfter, buffered, () =>
                {
                    cm.CommitBufferedEarnings();
                    buffAnimDone = true;
                });
                while (!buffAnimDone)
                    yield return null;
            }
            else
            {
                cm.CommitBufferedEarnings();
            }

            Debug.Log($"[PoliceCatch] FAIL complete, suppress OFF, money now={cm.money:F0}");
        }
        else if (isSuccess && CurrencyManager.Instance != null)
        {
            var cm = CurrencyManager.Instance;
            double before = cm.money;
            double reward = System.Math.Floor(before / 8.0);

            // Apply reward directly to money (bypassing buffer — this is not tap/MPS income)
            cm.money += reward;
            cm.totalMoneyEarned += reward;
            double after = cm.money;

            // 1) Play reward count-up animation (green blink)
            bool rewardAnimDone = false;
            if (CurrencyUI.Instance != null)
            {
                CurrencyUI.Instance.PlaySuccessRewardAnimation(before, after, 0.6f, () =>
                {
                    rewardAnimDone = true;
                });
            }
            else
            {
                rewardAnimDone = true;
            }
            while (!rewardAnimDone)
                yield return null;

            // 2) Apply buffered earnings with visible count-up
            double buffered = cm.bufferedEarnings;
            Debug.Log($"[PoliceCatch] SUCCESS reward={reward:F0} before={before:F0} afterReward={after:F0} buffered={buffered:F0}");

            if (buffered > 0 && CurrencyUI.Instance != null)
            {
                bool buffAnimDone = false;
                CurrencyUI.Instance.PlayBufferedEarningsApplyAnimation(after, buffered, () =>
                {
                    cm.CommitBufferedEarnings();
                    buffAnimDone = true;
                });
                while (!buffAnimDone)
                    yield return null;
            }
            else
            {
                cm.CommitBufferedEarnings();
            }

            Debug.Log($"[PoliceCatch] SUCCESS complete, suppress OFF, money now={cm.money:F0}");
        }
        else
        {
            // Safety: turn off suppress if somehow neither fail nor success
            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.suppressTopBarMoneyUpdates = false;
                CurrencyManager.Instance.bufferedEarnings = 0;
            }
        }

        _state = ChaseState.Idle;
        Debug.Log("[PoliceCatch] Chase ended, returning to Idle.");
    }

    // ==================== PROMPT LOGIC ====================

    private void GeneratePrompts()
    {
        _promptTaps = new int[promptCount];
        _promptDurations = new float[promptCount];

        for (int i = 0; i < promptCount; i++)
        {
            int n = Random.Range(1, 10); // 1–9 inclusive
            _promptTaps[i] = n;
            _promptDurations[i] = n * secondsPerTap;
        }
    }

    /// <summary>
    /// Called exactly once per prompt by the coroutine when the timer runs out.
    /// Exact-count rule: pass only if tapCount == required. Under or over = fail.
    /// Starts a green/red flash; sets _resultWindowDone = true when flash finishes.
    /// </summary>
    private void EvaluatePrompt()
    {
        _promptTimer = 0f; // clamp
        _isShowingResult = true;
        _resultWindowDone = false;

        // Ensure fill shows fully charged
        if (timerFill != null)
            timerFill.fillAmount = 1f;

        int required = _promptTaps[_currentPromptIndex];
        bool passed = (_currentTapCount == required);

        // Kill any lingering DOTween on the fill bar
        if (timerFill != null)
        {
            timerFill.DOKill();
            timerFill.rectTransform.DOKill();
            timerFill.rectTransform.localScale = Vector3.one;
        }

        if (passed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PoliceCatch] OnPromptEnd index={_currentPromptIndex} taps={_currentTapCount} required={required} PASS wasted={_wastedCount}");
#endif

            if (timerFill != null)
            {
                // Green flash: snap green → fade back over 0.35s
                timerFill.color = Color.green;
                timerFill.DOColor(_timerFillDefaultColor, 0.35f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() => _resultWindowDone = true);

                // Subtle scale punch for juice
                timerFill.rectTransform.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.5f);
            }
            else
            {
                _resultWindowDone = true;
            }
        }
        else
        {
            _wastedCount++;
            UpdateWastedUI();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PoliceCatch] OnPromptEnd index={_currentPromptIndex} taps={_currentTapCount} required={required} FAIL wasted={_wastedCount}/{maxWasted}");
#endif

            if (_wastedCount >= maxWasted)
            {
                // Chase lost — signal immediately, no flash needed
                _resultWindowDone = true;
                _isShowingResult = false;
                _state = ChaseState.Fail;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log("[PoliceCatch] OnChaseFail (wasted limit reached)");
#endif
                return;
            }

            if (timerFill != null)
            {
                // Red blink: red→default→red→default (2 blinks, 0.4s total)
                Sequence s = DOTween.Sequence();
                s.Append(timerFill.DOColor(Color.red, 0.1f));
                s.Append(timerFill.DOColor(_timerFillDefaultColor, 0.1f));
                s.Append(timerFill.DOColor(Color.red, 0.1f));
                s.Append(timerFill.DOColor(_timerFillDefaultColor, 0.1f));
                s.OnComplete(() => _resultWindowDone = true);

                // Subtle scale punch for juice
                timerFill.rectTransform.DOPunchScale(Vector3.one * 0.06f, 0.4f, 8, 0.5f);
            }
            else
            {
                _resultWindowDone = true;
            }
        }
    }

    // ==================== UI ====================

    private void UpdatePromptUI()
    {
        if (promptText != null)
            promptText.text = $"{_promptTaps[_currentPromptIndex]}x";

        if (timerFill != null)
        {
            timerFill.DOKill(); // kill any lingering flash tween
            timerFill.rectTransform.DOKill(); // kill any lingering scale tween
            timerFill.rectTransform.localScale = Vector3.one;
            timerFill.color = _timerFillDefaultColor;
            timerFill.fillAmount = 0f; // charge bar starts empty
        }
    }

    private void UpdateWastedUI()
    {
        if (wastedText != null)
            wastedText.text = $"{_wastedCount} / {maxWasted}";
    }

    // ==================== ANIMATION (DOTween) ====================

    private IEnumerator AnimateEnter()
    {
        // Kill any prior tweens on these transforms
        _zPopTween?.Kill();
        _zPopTween = null;
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();

        Sequence seq = DOTween.Sequence();

        // Player car Z-pop (only Z axis — preserves X/Y)
        if (playerCar != null)
        {
            float targetZ = _playerCarOriginalLocal.z + zPopOffset;
            _zPopTween = playerCar.DOLocalMoveZ(targetZ, zPopDuration);
            if (zPopEaseCurve != null && zPopEaseCurve.length > 0)
                _zPopTween.SetEase(zPopEaseCurve);
            else
                _zPopTween.SetEase(Ease.InOutSine);
            seq.Join(_zPopTween);

            if (enableZPopLogs)
                Debug.Log($"[PoliceCatch Z-Pop] ENTER: Z {_playerCarOriginalLocal.z:F3} → {targetZ:F3} over {zPopDuration:F2}s");
        }

        // Police car slides in
        if (policeCar != null)
            seq.Join(policeCar.DOLocalMove(policeChaseLocalPos, moveDuration).SetEase(Ease.OutCubic));

        yield return seq.WaitForCompletion();
        _zPopTween = null;

        // Start police sway
        StartSway();
    }

    private IEnumerator AnimateExit()
    {
        // Stop sway first
        StopSway();

        _zPopTween?.Kill();
        _zPopTween = null;
        if (playerCar != null) playerCar.DOKill();
        if (policeCar != null) policeCar.DOKill();

        // Build a sequential exit: police car retreats FIRST, then player car Z-pop returns.
        Sequence seq = DOTween.Sequence();

        // Phase 1: Police car slides out to hidden position
        if (policeCar != null)
        {
            seq.Append(policeCar.DOLocalMove(_policeCarOriginalLocal, moveDuration).SetEase(Ease.InCubic));

            if (enableZPopLogs)
                Debug.Log($"[PoliceCatch Exit] Police car sliding to hidden pos over {moveDuration:F2}s");
        }

        // Phase 2: After police car finishes, player car Z-pop returns to original Z
        if (playerCar != null)
        {
            float currentZ = playerCar.localPosition.z;
            float targetZ = _playerCarOriginalLocal.z;

            seq.AppendCallback(() =>
            {
                if (enableZPopLogs)
                    Debug.Log($"[PoliceCatch Z-Pop] EXIT: Z {playerCar.localPosition.z:F3} → {targetZ:F3} over {zPopDuration:F2}s");

                // Kill any lingering Z-pop tween before starting return
                _zPopTween?.Kill();
                _zPopTween = playerCar.DOLocalMoveZ(targetZ, zPopDuration);
                if (zPopEaseCurve != null && zPopEaseCurve.length > 0)
                    _zPopTween.SetEase(zPopEaseCurve);
                else
                    _zPopTween.SetEase(Ease.InOutSine);
            });
            seq.AppendInterval(zPopDuration); // wait for Z-pop to finish
        }

        yield return seq.WaitForCompletion();
        _zPopTween = null;
    }

    private void StartSway()
    {
        if (policeCar == null || swayAmount <= 0f) return;

        float baseX = policeCar.localPosition.x;
        policeCar.DOLocalMoveX(baseX + swayAmount, swayDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetId("PoliceSway");
    }

    private void StopSway()
    {
        DOTween.Kill("PoliceSway");
    }

    // ==================== REWARD ====================

    private IEnumerator SpawnRewardCoins()
    {
        if (rewardSpawner == null || rewardSpawner.nitroCoinPrefab == null)
        {
            Debug.LogWarning("[PoliceCatch] rewardSpawner or its prefab is null — skipping reward.");
            yield break;
        }

        for (int i = 0; i < rewardCoinCount; i++)
        {
            float x = Random.Range(rewardSpawner.minX, rewardSpawner.maxX);
            Vector3 pos = new Vector3(x, rewardSpawner.spawnTop.position.y, rewardSpawner.spawnTop.position.z);

            GameObject obj = Object.Instantiate(rewardSpawner.nitroCoinPrefab, pos, Quaternion.identity);
            NitroCoin coin = obj.GetComponent<NitroCoin>();
            if (coin != null)
                coin.despawnZ = rewardSpawner.spawnBottom.position.z;

            yield return new WaitForSeconds(rewardCoinInterval);
        }
    }

    // ==================== EDITOR TEST ====================

#if UNITY_EDITOR
    [ContextMenu("TEST: Start Police Chase")]
    private void DebugStartChase()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PoliceCatch] Must be in Play Mode.");
            return;
        }
        StartChase();
    }
#endif
}
