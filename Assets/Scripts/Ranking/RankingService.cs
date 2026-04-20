using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Singleton service that manages the connection to Supabase for the ranking system.
/// Phase 1: Anonymous authentication + player identity.
/// Phase 2: Score computation + submission via Edge Function.
/// Phase 3: Leaderboard query — 25 above + self + 25 below.
/// Phase 4: Unity UI — panel, entries, scroll-to-self.
/// Phase 5: Polish — event-driven auto-submit, app-lifecycle hooks, production logging.
/// Phase 6 (Hardening): Bootstrap-created singleton, scene-aware, load-guarded.
///
/// Setup: This object is created by CriticalManagerBootstrap at app start.
/// Configuration comes from a RankingConfig ScriptableObject at Resources/RankingConfig.
/// Do NOT place this as a GameObject in any scene.
/// </summary>
public class RankingService : MonoBehaviour
{
    // ─── Singleton ───

    public static RankingService Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    // ─── Configuration (loaded from RankingConfig ScriptableObject) ───

    private string supabaseUrl = "";
    private string supabaseAnonKey = "";

    // ─── Public State ───

    /// <summary>True after successful authentication with Supabase.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>The player's unique UUID from Supabase auth.</summary>
    public string UserId => _userId;

    /// <summary>The player's display name, e.g. "#R42".</summary>
    public string DisplayName => _displayName;

    /// <summary>Fired once when authentication completes successfully.</summary>
    public event Action OnAuthCompleted;

    /// <summary>Fired after a score submission succeeds. Parameter is the new racer score.</summary>
    public event Action<long> OnScoreSubmitted;

    /// <summary>The last successfully submitted racer score.</summary>
    public long LastSubmittedScore { get; private set; }

    /// <summary>Fired when a leaderboard fetch completes. Carries the result.</summary>
    public event Action<RankingDataModel.LeaderboardResult> OnLeaderboardFetched;

    /// <summary>The most recently fetched leaderboard data. Null until first fetch.</summary>
    public RankingDataModel.LeaderboardResult LastLeaderboardResult { get; private set; }

    /// <summary>The player's current global rank. 0 means unranked.</summary>
    public int PlayerRank { get; private set; }

    /// <summary>Fired once when the player first receives a valid rank (rank > 0).</summary>
    public event Action OnPlayerRanked;

    /// <summary>Maximum number of entries to show in the leaderboard window.</summary>
    public const int MaxVisibleEntries = 50;

    // ─── Score Submission Settings (loaded from RankingConfig) ───

    private float autoSubmitInterval = 120f;
    private long minimumScoreDelta = 10;
    private float eventSubmitCooldown = 10f;

    // ─── Internal State ───

    /// <summary>Tracks what triggered a score submission (for diagnostics).</summary>
    private enum SubmitSource { AutoTimer, EventBuilding, EventCard, EventBlacklist, Pause, Quit, Manual }

    private string _accessToken;
    private string _refreshToken;
    private string _userId;
    private string _displayName;
    private bool _authInProgress;
    private bool _submitInProgress;
    private bool _leaderboardFetchInProgress;
    private bool _refreshInProgress;
    private float _autoSubmitTimer;
    private float _lastEventSubmitTime = -999f;
    private float _lastSubmitTime = -999f;
    private bool _eventHooksRegistered;
    private bool _isMainScene;
    private bool _isLoadInProgress;

    // ─── PlayerPrefs Keys ───

    private const string PREF_ACCESS_TOKEN  = "Ranking_AccessToken";
    private const string PREF_REFRESH_TOKEN = "Ranking_RefreshToken";
    private const string PREF_USER_ID       = "Ranking_UserID";
    private const string PREF_DISPLAY_NAME  = "Ranking_DisplayName";

    // ─── Lifecycle ───

    private bool _isDuplicate;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.Log($"[RankingService] Duplicate detected, destroying {name}.");
            _isDuplicate = true;
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // DontDestroyOnLoad requires root-level GameObject
        if (transform.parent != null)
            transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // Load config from ScriptableObject
        LoadConfig();

        // Load cached auth data from previous session
        _accessToken  = PlayerPrefs.GetString(PREF_ACCESS_TOKEN, "");
        _refreshToken = PlayerPrefs.GetString(PREF_REFRESH_TOKEN, "");
        _userId       = PlayerPrefs.GetString(PREF_USER_ID, "");
        _displayName  = PlayerPrefs.GetString(PREF_DISPLAY_NAME, "");
    }

    private void Start()
    {
        // Guard: if this instance is marked as duplicate, do NOT run any logic.
        // Unity calls Start() even on objects that are pending Destroy() (end of frame).
        if (_isDuplicate) return;

        // ── One-time fix: clear stale auth from before the DB was fixed ──
        // After the trigger is recreated, old tokens point to a user that no longer exists.
        // This block detects that case and forces a fresh signup.
        const string PREF_DB_VERSION = "Ranking_DBVersion";
        const int CURRENT_DB_VERSION = 2; // bump this number every time you recreate the DB
        if (PlayerPrefs.GetInt(PREF_DB_VERSION, 0) < CURRENT_DB_VERSION)
        {
            Debug.Log("[RankingService] DB version changed — clearing old auth cache.");
            ClearSavedAuth();
            PlayerPrefs.SetInt(PREF_DB_VERSION, CURRENT_DB_VERSION);
            PlayerPrefs.Save();
        }

        StartCoroutine(InitializeAuth());
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SaveSystem.OnGameLoaded += OnGameLoadCompleted;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SaveSystem.OnGameLoaded -= OnGameLoadCompleted;
    }

    private void OnDestroy()
    {
        UnregisterEventHooks();
        if (Instance == this) Instance = null;
    }

    // ─── Scene & Load Awareness (Hardening Phase 2) ───

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool wasMain = _isMainScene;
        _isMainScene = scene.name == "Main";

        if (_isMainScene)
        {
            // LoadGame() is about to run (SaveSystem.LoadAfterScene coroutine).
            // Block all submissions until OnGameLoaded fires.
            _isLoadInProgress = true;
            Debug.Log("[RankingService] Main scene loaded — submissions blocked until LoadGame completes.");
        }
        else
        {
            Debug.Log($"[RankingService] Non-Main scene loaded ({scene.name}) — submissions paused.");
        }
    }

    private void OnGameLoadCompleted()
    {
        _isLoadInProgress = false;

        // Reset stale state so the first post-load submit uses a fresh baseline.
        LastSubmittedScore = 0;
        _autoSubmitTimer = 0f;

        Debug.Log("[RankingService] LoadGame completed — submissions unblocked, score baseline reset.");
    }

    /// <summary>True when submissions are allowed: authenticated, in Main scene, load finished.</summary>
    private bool CanSubmit => IsAuthenticated && _isMainScene && !_isLoadInProgress;

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && CanSubmit && !_submitInProgress)
        {
            Debug.Log("[RankingService] App pausing — submitting score.");
            ForceSubmitScoreInternal(SubmitSource.Pause);
        }
    }

    private void OnApplicationQuit()
    {
        // Best-effort submit on quit \u2014 coroutine may not complete, but it starts the request.
        if (CanSubmit && !_submitInProgress)
        {
            Debug.Log("[RankingService] App quitting \u2014 submitting score.");
            ForceSubmitScoreInternal(SubmitSource.Quit);
        }
    }

    private void Update()
    {
        if (!CanSubmit || autoSubmitInterval <= 0f) return;

        _autoSubmitTimer += Time.deltaTime;
        if (_autoSubmitTimer >= autoSubmitInterval)
        {
            _autoSubmitTimer = 0f;
            SubmitScoreInternal(SubmitSource.AutoTimer);
        }
    }

    // ─── Event-Driven Score Submission (Phase 5) ───

    private void RegisterEventHooks()
    {
        if (_eventHooksRegistered) return;
        _eventHooksRegistered = true;

        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingPurchased += OnBuildingPurchased;

        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsChanged += OnCardsChanged;

        if (BlacklistManager.Instance != null)
            BlacklistManager.Instance.OnTierChanged += OnBlacklistTierChanged;

        Debug.Log("[RankingService] Event hooks registered (building/card/blacklist).");
    }

    private void UnregisterEventHooks()
    {
        if (!_eventHooksRegistered) return;
        _eventHooksRegistered = false;

        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingPurchased -= OnBuildingPurchased;

        if (CardManager.Instance != null)
            CardManager.Instance.OnCardsChanged -= OnCardsChanged;

        if (BlacklistManager.Instance != null)
            BlacklistManager.Instance.OnTierChanged -= OnBlacklistTierChanged;
    }

    private void OnBuildingPurchased(BuildingType type, int count)
    {
        TryEventSubmit(SubmitSource.EventBuilding);
    }

    private void OnCardsChanged()
    {
        TryEventSubmit(SubmitSource.EventCard);
    }

    private void OnBlacklistTierChanged()
    {
        if (!CanSubmit)
        {
            Debug.Log("[RankingService] Blacklist tier changed — but submit blocked (scene/load guard).");
            return;
        }

        // Tier change is high-priority — force submit regardless of cooldown.
        Debug.Log("[RankingService] Blacklist tier changed — force submitting score.");
        _lastEventSubmitTime = Time.unscaledTime;
        ForceSubmitScoreInternal(SubmitSource.EventBlacklist);
    }

    private void TryEventSubmit(SubmitSource source)
    {
        if (!CanSubmit || _submitInProgress) return;

        if (Time.unscaledTime - _lastEventSubmitTime < eventSubmitCooldown) return;

        _lastEventSubmitTime = Time.unscaledTime;
        SubmitScoreInternal(source);
    }

    // ─── Score Submission (Phase 2) ───

    /// <summary>
    /// Computes the current score and submits it to the server.
    /// Skips if not authenticated, already submitting, or score hasn't changed enough.
    /// </summary>
    public void SubmitScore()
    {
        SubmitScoreInternal(SubmitSource.Manual);
    }

    private void SubmitScoreInternal(SubmitSource source)
    {
        if (!IsAuthenticated)
        {
            Debug.LogWarning("[RankingService] Cannot submit score — not authenticated.");
            return;
        }

        if (_submitInProgress)
        {
            Debug.Log("[RankingService] Score submission already in progress, skipping.");
            return;
        }

        // Global cooldown: no two submits within 5 seconds (any source).
        if (Time.unscaledTime - _lastSubmitTime < 5f)
        {
            Debug.Log("[RankingService] Global submit cooldown active, skipping.");
            return;
        }

        var components = RankingScoreComputer.Compute();

        if (Math.Abs(components.racerScore - LastSubmittedScore) < minimumScoreDelta)
        {
            Debug.Log($"[RankingService] Score delta too small ({components.racerScore} vs {LastSubmittedScore}), skipping.");
            return;
        }

        StartCoroutine(SubmitScoreCoroutine(components, source, 0));
    }

    /// <summary>
    /// Force-submits the score regardless of delta. Use for important moments
    /// (e.g. building purchased, blacklist tier completed).
    /// </summary>
    public void ForceSubmitScore()
    {
        ForceSubmitScoreInternal(SubmitSource.Manual);
    }

    private void ForceSubmitScoreInternal(SubmitSource source)
    {
        if (!IsAuthenticated || _submitInProgress) return;

        // Global cooldown still applies to force-submits.
        if (Time.unscaledTime - _lastSubmitTime < 5f)
        {
            Debug.Log("[RankingService] Global submit cooldown active (force), skipping.");
            return;
        }

        StartCoroutine(SubmitScoreCoroutine(RankingScoreComputer.Compute(), source, 0));
    }

    private IEnumerator SubmitScoreCoroutine(RankingScoreComputer.ScoreComponents components, SubmitSource source, int attempt = 0)
    {
        _submitInProgress = true;
        _lastSubmitTime = Time.unscaledTime;

        string activeScene = SceneManager.GetActiveScene().name;

        string url = supabaseUrl.TrimEnd('/') + "/functions/v1/submit-score";

        Debug.Log($"[RankingService] SUBMIT | source={source} | scene={activeScene} | attempt={attempt + 1} " +
                  $"| score={components.racerScore} | money={components.totalMoneyEarned:F0} " +
                  $"| buildings={components.totalBuildingCount} | cards={components.cardLevelSum} " +
                  $"| highTier={components.highestBuildingTier} | blTiers={components.blacklistTiersCompleted} " +
                  $"| lastSubmitted={LastSubmittedScore}");

        string body = JsonUtility.ToJson(new ScoreSubmitPayload
        {
            total_money_earned       = components.totalMoneyEarned,
            total_building_count     = components.totalBuildingCount,
            card_level_sum           = components.cardLevelSum,
            highest_building_tier    = components.highestBuildingTier,
            blacklist_tiers_completed = components.blacklistTiersCompleted
        });

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // IMPORTANT: The Authorization header carries the anon key (HS256)
            // so the Supabase Edge Functions gateway/relay can verify it.
            // The user's ES256 access token goes in x-user-token, and our
            // Edge Function validates it server-side via GoTrue.
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", "Bearer " + supabaseAnonKey);
            request.SetRequestHeader("x-user-token", _accessToken);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string responseBody = request.downloadHandler?.text ?? "";
                Debug.LogWarning($"[RankingService] Score submit failed (attempt {attempt + 1}): " +
                                 $"{request.error}\nHTTP {request.responseCode}\nResponse: {responseBody}");

                // Only retry once on 401, and only if we haven't already retried
                if (request.responseCode == 401 && attempt < 1)
                {
                    Debug.Log("[RankingService] 401 received — refreshing token and retrying (1 retry max)...");
                    _submitInProgress = false;
                    yield return RefreshAccessToken();
                    if (IsAuthenticated)
                        StartCoroutine(SubmitScoreCoroutine(components, source, attempt + 1));
                    yield break;
                }

                if (request.responseCode == 401 && attempt >= 1)
                {
                    Debug.LogError("[RankingService] Score submit still failing after token refresh. " +
                                   "This is NOT an expired-token issue. Check Edge Function deployment. " +
                                   "Response: " + responseBody);
                }

                _submitInProgress = false;
                yield break;
            }

            string json = request.downloadHandler.text;
            var result = JsonUtility.FromJson<ScoreSubmitResponse>(json);

            if (result != null && result.ok)
            {
                LastSubmittedScore = result.racer_score;
                _autoSubmitTimer = 0f; // Reset timer after successful submit

                // Update rank from submit response
                bool wasUnranked = PlayerRank <= 0;
                if (result.rank > 0)
                    PlayerRank = result.rank;
                if (wasUnranked && PlayerRank > 0)
                    OnPlayerRanked?.Invoke();

                Debug.Log($"[RankingService] Score submitted OK (attempt {attempt + 1})! " +
                          $"Racer Score: {result.racer_score} | Rank: {result.rank}");
                OnScoreSubmitted?.Invoke(result.racer_score);
            }
            else
            {
                Debug.LogWarning($"[RankingService] Server rejected score: {json}");
            }
        }

        _submitInProgress = false;
    }

    // ─── Leaderboard Query (Phase 3) ───

    /// <summary>
    /// Fetches a windowed leaderboard (max 50 entries) centered on the current player.
    /// The result is stored in LastLeaderboardResult and fired via OnLeaderboardFetched.
    /// </summary>
    public void FetchLeaderboard()
    {
        if (!IsAuthenticated)
        {
            Debug.LogWarning("[RankingService] Cannot fetch leaderboard — not authenticated.");
            return;
        }

        if (_leaderboardFetchInProgress)
        {
            Debug.Log("[RankingService] Leaderboard fetch already in progress, skipping.");
            return;
        }

        StartCoroutine(FetchLeaderboardCoroutine(0));
    }

    /// <summary>
    /// Computes the 1-based start and end ranks for the visible window.
    /// </summary>
    public static void ComputeWindow(int totalPlayers, int playerRank, int maxVisible,
                                     out int windowStart, out int windowEnd)
    {
        if (totalPlayers <= maxVisible)
        {
            windowStart = 1;
            windowEnd = totalPlayers;
            return;
        }

        // Center the player: place ~24 above, self, ~25 below
        int halfAbove = (maxVisible / 2) - 1; // 24
        windowStart = playerRank - halfAbove;
        windowEnd = windowStart + maxVisible - 1;

        // Clamp to valid range
        if (windowStart < 1)
        {
            windowStart = 1;
            windowEnd = maxVisible;
        }

        if (windowEnd > totalPlayers)
        {
            windowEnd = totalPlayers;
            windowStart = totalPlayers - maxVisible + 1;
        }
    }

    private IEnumerator FetchLeaderboardCoroutine(int attempt)
    {
        _leaderboardFetchInProgress = true;

        // Call the PostgreSQL RPC function
        string url = supabaseUrl.TrimEnd('/') +
                     "/rest/v1/rpc/get_leaderboard_window";

        string body = JsonUtility.ToJson(new LeaderboardWindowPayload
        {
            target_player_id = _userId,
            max_visible = MaxVisibleEntries
        });

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", "Bearer " + _accessToken);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RankingService] Leaderboard fetch failed: {request.error}" +
                                 $"\nResponse: {request.downloadHandler?.text}");

                if (request.responseCode == 401)
                {
                    Debug.Log("[RankingService] Token may be expired. Refreshing...");
                    _leaderboardFetchInProgress = false;

                    if (attempt >= 1)
                    {
                        Debug.LogError("[RankingService] Leaderboard fetch still failing after token refresh. Giving up.");
                        yield break;
                    }

                    yield return RefreshAccessToken();
                    if (IsAuthenticated)
                        StartCoroutine(FetchLeaderboardCoroutine(attempt + 1));
                    yield break;
                }

                _leaderboardFetchInProgress = false;
                OnLeaderboardFetched?.Invoke(null);
                yield break;
            }

            string json = request.downloadHandler.text;

            // The RPC returns a single JSON object (wrapped in an array by Supabase REST).
            // Structure: [{"entries": [...], "total_players": N, "self_rank": R}]
            var wrapper = JsonUtility.FromJson<RankingDataModel.WindowedLeaderboardResponseWrapper>(
                "{\"items\":" + json + "}");

            if (wrapper == null || wrapper.items == null || wrapper.items.Length == 0)
            {
                Debug.LogWarning("[RankingService] Leaderboard response was empty: " + json);
                _leaderboardFetchInProgress = false;
                OnLeaderboardFetched?.Invoke(null);
                yield break;
            }

            var rpcResult = wrapper.items[0];
            var entries = rpcResult.entries ?? new RankingDataModel.LeaderboardEntry[0];
            int totalPlayers = rpcResult.total_players;
            int selfRank = rpcResult.self_rank;

            // Find self index within the returned window
            int selfIdx = -1;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].player_id == _userId)
                {
                    selfIdx = i;
                    break;
                }
            }

            var result = new RankingDataModel.LeaderboardResult
            {
                entries = entries,
                selfIndex = selfIdx,
                selfRank = selfRank,
                totalPlayers = totalPlayers
            };

            LastLeaderboardResult = result;

            // Update PlayerRank and fire event if this is the first time
            bool wasUnranked = PlayerRank <= 0;
            PlayerRank = selfRank;
            if (wasUnranked && selfRank > 0)
                OnPlayerRanked?.Invoke();

            Debug.Log($"[RankingService] Leaderboard fetched: {entries.Length} entries, " +
                      $"totalPlayers={totalPlayers}, selfRank={selfRank}, selfIndex={selfIdx}");

            OnLeaderboardFetched?.Invoke(result);
        }

        _leaderboardFetchInProgress = false;
    }

    // ─── Auth Flow ───

    private IEnumerator InitializeAuth()
    {
        // Guard: don't start auth if config is missing
        if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseAnonKey))
        {
            Debug.LogError("[RankingService] Supabase URL or Anon Key is not set! " +
                           "Select the RankingService GameObject and fill in the Inspector fields.");
            yield break;
        }

        _authInProgress = true;

        if (string.IsNullOrEmpty(_userId))
        {
            // First launch ever — create a new anonymous identity
            Debug.Log("[RankingService] No saved user found. Creating anonymous identity...");
            yield return SignUpAnonymous();
        }
        else
        {
            // Returning player — refresh the auth token
            Debug.Log($"[RankingService] Saved user found: {_userId}. Refreshing token...");
            yield return RefreshAccessToken();
        }

        _authInProgress = false;
    }

    /// <summary>
    /// Creates a new anonymous user on Supabase.
    /// The database trigger automatically creates the player_profile and leaderboard_score rows.
    /// </summary>
    private IEnumerator SignUpAnonymous()
    {
        string url = supabaseUrl.TrimEnd('/') + "/auth/v1/signup";

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes("{}");
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[RankingService] Anonymous signup FAILED: {request.error}" +
                               $"\nResponse: {request.downloadHandler?.text}");
                yield break;
            }

            string json = request.downloadHandler.text;
            var response = JsonUtility.FromJson<AuthResponse>(json);

            if (response == null || string.IsNullOrEmpty(response.access_token) ||
                response.user == null || string.IsNullOrEmpty(response.user.id))
            {
                Debug.LogError("[RankingService] Signup response was invalid: " + json);
                yield break;
            }

            SaveAuthTokens(response);
            Debug.Log($"[RankingService] Anonymous signup OK. User ID: {_userId}");
        }

        // The database trigger has now created our profile row.
        // Fetch it to get the auto-generated display name (#R1, #R2, etc.)
        yield return FetchOwnProfile();
    }

    /// <summary>
    /// Refreshes the access token using the stored refresh token.
    /// If refresh fails, falls back to creating a new anonymous identity.
    /// </summary>
    private IEnumerator RefreshAccessToken()
    {
        // Serialize concurrent refresh requests: if one is already running, wait for it.
        if (_refreshInProgress)
        {
            Debug.Log("[RankingService] Token refresh already in progress \u2014 waiting...");
            yield return new WaitUntil(() => !_refreshInProgress);
            yield break;
        }

        _refreshInProgress = true;

        string url = supabaseUrl.TrimEnd('/') + "/auth/v1/token?grant_type=refresh_token";
        string body = JsonUtility.ToJson(new RefreshBody { refresh_token = _refreshToken });

        using (var request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RankingService] Token refresh failed: {request.error}. " +
                                 "Creating new anonymous identity...");
                _refreshInProgress = false;
                ClearSavedAuth();
                yield return SignUpAnonymous();
                yield break;
            }

            string json = request.downloadHandler.text;
            var response = JsonUtility.FromJson<AuthResponse>(json);

            if (response == null || string.IsNullOrEmpty(response.access_token))
            {
                Debug.LogWarning("[RankingService] Refresh response was invalid. " +
                                 "Creating new anonymous identity...");
                _refreshInProgress = false;
                ClearSavedAuth();
                yield return SignUpAnonymous();
                yield break;
            }

            SaveAuthTokens(response);
            Debug.Log($"[RankingService] Token refreshed OK for user {_userId}");
        }

        _refreshInProgress = false;

        // If we already have a display name cached, we're done.
        // Otherwise fetch it from the server.
        if (!string.IsNullOrEmpty(_displayName))
        {
            MarkAuthenticated();
        }
        else
        {
            yield return FetchOwnProfile();
        }
    }

    /// <summary>
    /// Fetches this player's profile row to get the display name assigned by the trigger.
    /// </summary>
    private IEnumerator FetchOwnProfile()
    {
        string url = supabaseUrl.TrimEnd('/') +
                     "/rest/v1/player_profiles" +
                     "?id=eq." + _userId +
                     "&select=display_name,sequential_id";

        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("apikey", supabaseAnonKey);
            request.SetRequestHeader("Authorization", "Bearer " + _accessToken);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RankingService] Profile fetch failed: {request.error}. " +
                                 "Continuing with cached name (may be empty).");
                MarkAuthenticated();
                yield break;
            }

            string json = request.downloadHandler.text;

            // Supabase REST returns a JSON array: [{"display_name":"#R1","sequential_id":1}]
            // JsonUtility cannot parse root arrays, so we wrap it in an object.
            var wrapper = JsonUtility.FromJson<ProfileArrayWrapper>("{\"items\":" + json + "}");

            if (wrapper != null && wrapper.items != null && wrapper.items.Length > 0)
            {
                _displayName = wrapper.items[0].display_name;
                PlayerPrefs.SetString(PREF_DISPLAY_NAME, _displayName);
                PlayerPrefs.Save();
                Debug.Log($"[RankingService] Profile loaded: {_displayName}");
            }
            else
            {
                Debug.LogWarning("[RankingService] Profile response was empty: " + json);
            }
        }

        MarkAuthenticated();
    }

    // ─── Helpers ───

    private void MarkAuthenticated()
    {
        IsAuthenticated = true;
        Debug.Log($"[RankingService] *** READY *** | Name: {_displayName} | ID: {_userId}");
        RegisterEventHooks();
        OnAuthCompleted?.Invoke();
    }

    private void SaveAuthTokens(AuthResponse response)
    {
        _accessToken  = response.access_token;
        _refreshToken = response.refresh_token;
        _userId       = response.user.id;

        PlayerPrefs.SetString(PREF_ACCESS_TOKEN, _accessToken);
        PlayerPrefs.SetString(PREF_REFRESH_TOKEN, _refreshToken);
        PlayerPrefs.SetString(PREF_USER_ID, _userId);
        PlayerPrefs.Save();
    }

    private void ClearSavedAuth()
    {
        _accessToken  = "";
        _refreshToken = "";
        _userId       = "";
        _displayName  = "";
        IsAuthenticated = false;

        PlayerPrefs.DeleteKey(PREF_ACCESS_TOKEN);
        PlayerPrefs.DeleteKey(PREF_REFRESH_TOKEN);
        PlayerPrefs.DeleteKey(PREF_USER_ID);
        PlayerPrefs.DeleteKey(PREF_DISPLAY_NAME);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Returns the current access token for use in HTTP headers.
    /// Usage: request.SetRequestHeader("Authorization", RankingService.Instance.GetBearerToken());
    /// </summary>
    public string GetBearerToken()
    {
        return "Bearer " + _accessToken;
    }

    /// <summary>Returns the configured Supabase project URL.</summary>
    public string GetSupabaseUrl() => supabaseUrl.TrimEnd('/');

    /// <summary>Returns the configured Supabase anon key.</summary>
    public string GetAnonKey() => supabaseAnonKey;

    // ─── Config Loading ───

    private void LoadConfig()
    {
        var config = Resources.Load<RankingConfig>("RankingConfig");
        if (config == null)
        {
            Debug.LogError("[RankingService] RankingConfig not found at Resources/RankingConfig! " +
                           "Create it via: Right-click in Project → Create → Ranking → RankingConfig, " +
                           "then move it to Assets/Resources/RankingConfig.asset.");
            return;
        }

        supabaseUrl          = config.supabaseUrl;
        supabaseAnonKey      = config.supabaseAnonKey;
        autoSubmitInterval   = config.autoSubmitInterval;
        minimumScoreDelta    = config.minimumScoreDelta;
        eventSubmitCooldown  = config.eventSubmitCooldown;

        Debug.Log($"[RankingService] Config loaded from ScriptableObject. URL={supabaseUrl}");
    }

    // ─── JSON Data Transfer Objects ───
    // These classes map to Supabase Auth API JSON responses.
    // JsonUtility requires [Serializable] and public fields.

    [Serializable]
    private class AuthResponse
    {
        public string access_token;
        public string token_type;
        public int    expires_in;
        public string refresh_token;
        public AuthUser user;
    }

    [Serializable]
    private class AuthUser
    {
        public string id;
        public bool   is_anonymous;
    }

    [Serializable]
    private class RefreshBody
    {
        public string refresh_token;
    }

    [Serializable]
    private class ProfileData
    {
        public string display_name;
        public long   sequential_id;
    }

    [Serializable]
    private class ProfileArrayWrapper
    {
        public ProfileData[] items;
    }

    // ── Score Submission DTOs ──

    [Serializable]
    private class ScoreSubmitPayload
    {
        public double total_money_earned;
        public int    total_building_count;
        public int    card_level_sum;
        public int    highest_building_tier;
        public int    blacklist_tiers_completed;
    }

    [Serializable]
    private class ScoreSubmitResponse
    {
        public bool ok;
        public long racer_score;
        public int  rank;
        public string error;
    }

    // ── Leaderboard Query DTOs ──

    [Serializable]
    private class LeaderboardWindowPayload
    {
        public string target_player_id;
        public int    max_visible;
    }

}
