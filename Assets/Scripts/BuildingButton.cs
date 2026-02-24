using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildingButton : MonoBehaviour
{
    [Header("Setup")]
    public BuildingType buildingType;
    public TextMeshProUGUI labelText;
    public Button button;

    private double currentCost;
    private double lastCost = -1;
    private int lastOwned = -1;

    private bool _initialized;        // Start logic ran successfully
    private bool _diagnosticLogged;   // one-shot diagnostic flag (no spam)
    private bool _boundSuccessfully;  // first successful bind logged

    // ───────────────────── lifecycle ─────────────────────

    private void OnEnable()
    {
        // Panel was re-activated → try to bind immediately if managers are ready.
        if (_initialized)
            TryRefreshData(true);
    }

    private void Start()
    {
        // ── resolve Button reference ──
        if (button == null)
            button = GetComponent<Button>();

        if (button == null)
        {
            Debug.LogError($"[BuildingButton] '{gameObject.name}' (type={buildingType}) — " +
                           "no Button component found. Disabling script.", this);
            enabled = false;
            return;
        }

        // ── one-time diagnostic: check inspector refs ──
        if (labelText == null)
        {
            Debug.LogWarning($"[BuildingButton] '{gameObject.name}' (type={buildingType}) — " +
                             "labelText is NOT assigned in Inspector. Text will not update.", this);
        }

        button.onClick.AddListener(OnClickBuy);
        _initialized = true;

        // First refresh — may silently skip if managers aren't ready yet.
        TryRefreshData(true);
    }

    private void Update()
    {
        if (!_initialized) return;

        TryRefreshData(false);
    }

    // ───────────────────── singleton recovery ─────────────────────

    /// <summary>
    /// If the static Instance field is null but a live manager object exists in
    /// the scene, recover the reference. This handles race conditions where
    /// ResetStatics or OnDestroy clears the static after Awake already ran.
    /// Logs a warning so we can trace why the static was lost.
    /// </summary>
    private bool EnsureManagers()
    {
        bool recovered = false;

        if (BuildingManager.Instance == null)
        {
            var found = FindObjectsByType<BuildingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length > 0)
            {
                BuildingManager.Instance = found[0];
                found[0].InitializeLookup(); // ensure dictionary is ready
                recovered = true;
                Debug.LogWarning($"[BuildingButton] RECOVERED BuildingManager.Instance " +
                                 $"via FindObjectsByType (ID={found[0].GetInstanceID()}, " +
                                 $"name='{found[0].gameObject.name}', count={found.Length}). " +
                                 "Static field was null — possible ResetStatics/OnDestroy race.", this);
                if (found.Length > 1)
                {
                    for (int i = 0; i < found.Length; i++)
                        Debug.LogWarning($"  [BuildingButton] BuildingManager #{i}: " +
                                         $"ID={found[i].GetInstanceID()}, name='{found[i].gameObject.name}', " +
                                         $"scene={found[i].gameObject.scene.name}", this);
                }
            }
        }

        if (CurrencyManager.Instance == null)
        {
            var found = FindObjectsByType<CurrencyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length > 0)
            {
                CurrencyManager.Instance = found[0];
                recovered = true;
                Debug.LogWarning($"[BuildingButton] RECOVERED CurrencyManager.Instance " +
                                 $"via FindObjectsByType (ID={found[0].GetInstanceID()}, " +
                                 $"name='{found[0].gameObject.name}', count={found.Length}). " +
                                 "Static field was null — possible ResetStatics/OnDestroy race.", this);
                if (found.Length > 1)
                {
                    for (int i = 0; i < found.Length; i++)
                        Debug.LogWarning($"  [BuildingButton] CurrencyManager #{i}: " +
                                         $"ID={found[i].GetInstanceID()}, name='{found[i].gameObject.name}', " +
                                         $"scene={found[i].gameObject.scene.name}", this);
                }
            }
        }

        return BuildingManager.Instance != null && CurrencyManager.Instance != null;
    }

    // ───────────────────── safe refresh ─────────────────────

    private void TryRefreshData(bool forceLabel)
    {
        // ── 1. Manager singletons ready? Try recovery if static is null ──
        if (BuildingManager.Instance == null || CurrencyManager.Instance == null)
        {
            if (!EnsureManagers())
            {
                // Log once so we can trace timing issues
                if (!_diagnosticLogged && forceLabel)
                {
                    _diagnosticLogged = true;

                    // Deep scan: report what actually exists in the scene
                    var bmAll = FindObjectsByType<BuildingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    var cmAll = FindObjectsByType<CurrencyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                    Debug.LogWarning($"[BuildingButton] '{gameObject.name}' (type={buildingType}) — " +
                                     $"managers NOT available. " +
                                     $"BuildingManager.Instance={(BuildingManager.Instance != null ? "set" : "NULL")} " +
                                     $"(scene objects found: {bmAll.Length}), " +
                                     $"CurrencyManager.Instance={(CurrencyManager.Instance != null ? "set" : "NULL")} " +
                                     $"(scene objects found: {cmAll.Length}). " +
                                     "Will retry each frame.", this);

                    for (int i = 0; i < bmAll.Length; i++)
                        Debug.LogWarning($"  BuildingManager #{i}: ID={bmAll[i].GetInstanceID()}, " +
                                         $"name='{bmAll[i].gameObject.name}', active={bmAll[i].gameObject.activeInHierarchy}, " +
                                         $"scene={bmAll[i].gameObject.scene.name}", this);
                    for (int i = 0; i < cmAll.Length; i++)
                        Debug.LogWarning($"  CurrencyManager #{i}: ID={cmAll[i].GetInstanceID()}, " +
                                         $"name='{cmAll[i].gameObject.name}', active={cmAll[i].gameObject.activeInHierarchy}, " +
                                         $"scene={cmAll[i].gameObject.scene.name}", this);
                }
                return;
            }
        }

        // ── 2. Building definition exists? ──
        BuildingDefinition b = BuildingManager.Instance.GetBuilding(buildingType);
        if (b == null)
        {
            if (!_diagnosticLogged)
            {
                _diagnosticLogged = true;
                Debug.LogWarning($"[BuildingButton] '{gameObject.name}' — " +
                                 $"BuildingManager.GetBuilding({buildingType}) returned NULL. " +
                                 $"Check BuildingManager.buildings[] array in Inspector " +
                                 $"(length={BuildingManager.Instance.buildings?.Length ?? 0}).", this);
            }
            return;
        }

        // ── 3. All good — log first successful bind ──
        if (!_boundSuccessfully)
        {
            _boundSuccessfully = true;
            _diagnosticLogged = true;
            Debug.Log($"[BuildingButton] '{gameObject.name}' bound OK — " +
                      $"type={buildingType}, definition='{b.displayName}', " +
                      $"BuildingManager.ID={BuildingManager.Instance.GetInstanceID()}, " +
                      $"CurrencyManager.ID={CurrencyManager.Instance.GetInstanceID()}", this);
        }

        // ── 4. Refresh cost / interactable ──
        currentCost = BuildingManager.Instance.GetCurrentCost(buildingType);
        bool canAfford = CurrencyManager.Instance.money >= currentCost;
        button.interactable = canAfford;

        // Sadece cost veya owned değiştiyse label güncelle
        if (forceLabel || System.Math.Abs(currentCost - lastCost) > 0.0001 || b.count != lastOwned)
        {
            lastCost = currentCost;
            lastOwned = b.count;
            UpdateLabel(b);
        }
    }

    // ───────────────────── buy ─────────────────────

    private void OnClickBuy()
    {
        if (BuildingManager.Instance == null && !EnsureManagers()) return;

        bool success = BuildingManager.Instance.TryBuyBuilding(buildingType);

        if (success)
        {
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayBuildingBuy();

            TryRefreshData(true);
        }
    }

    // ───────────────────── label ─────────────────────

    private void UpdateLabel(BuildingDefinition b)
    {
        if (labelText == null) return;

        labelText.text =
            $"{b.displayName}\n" +
            $"Owned: {b.count}\n" +
            $"Cost: {currentCost:0}";
    }
}
