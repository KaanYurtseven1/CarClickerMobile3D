using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class ChestOpenSceneController : MonoBehaviour
{
    private enum Phase
    {
        Intro,
        Closed_TapToOpen,   // 3 tap -> kapak aç
        LidOpening,         // anim oynuyor
        Opened_TapLoot,     // 3 tap (şimdilik loot yok, sadece tap feedback)
        Done_TapToExit      // 1 tap -> main
    }

    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject chestPrefab;

    [Header("Lid Settings")]
    [Tooltip("Chest kapağı olan transform. Boş bırakırsan otomatik bulunur.")]
    [SerializeField] private Transform lidBone;

    [Tooltip("Lid transform adı (örn: Cube.004). Otomatik arama için kullanılır.")]
    [SerializeField] private string lidTransformName = "Cube.004";

    [Tooltip("Lid için öncelikli arama yolu (örn: Empty/Cube.004). Boşsa recursive arama yapılır.")]
    [SerializeField] private string lidSearchPath = "Empty/Cube.004";

    [Tooltip("Kapağın kapalı local euler (prefab zaten kapalıysa otomatik alınır)")]
    [SerializeField] private Vector3 lidClosedLocalEuler = new Vector3(0, 0, 0);

    [Tooltip("Kapağın açık local euler. Açılış yönüne göre ayarla.")]
    [SerializeField] private Vector3 lidOpenLocalEuler = new Vector3(-110f, 0, 0);

    [Header("Runtime Pivot Fix")]
    [Tooltip("Eğer lid yanlış pivot etrafında dönüyorsa, runtime'da pivot oluştur.")]
    [SerializeField] private bool useRuntimePivotFix = false;

    [Tooltip("Pivot pozisyonu: lid'in üst-arka kenarına göre offset (local space).")]
    [SerializeField] private Vector3 pivotOffset = new Vector3(0, 0.5f, -0.5f);

    [Header("Tap Settings")]
    [SerializeField] private int tapsToOpen = 3;
    [SerializeField] private int afterOpenTapCount = 3; // kapak açıldıktan sonra kaç tap
    [SerializeField] private LayerMask chestLayerMask = ~0;

    [Header("Intro Anim (smooth)")]
    [SerializeField] private float introMoveTime = 0.55f;
    [SerializeField] private float introStartYOffset = 0.55f;
    [SerializeField] private float introStartScale = 0.25f;

    [Header("Tap Feedback")]
    [SerializeField] private float tapJumpPower = 0.25f;
    [SerializeField] private float tapJumpDuration = 0.22f;

    [Header("Lid Open Anim")]
    [SerializeField] private float lidOpenDuration = 0.35f;
    [SerializeField] private Ease lidEase = Ease.OutCubic;

    private Phase phase = Phase.Intro;

    private int tapCount = 0;
    private int afterOpenTapped = 0;

    private GameObject chestGO;
    private Transform chestTr;
    private Vector3 basePos;
    private Vector3 baseScale;

    // Guard to prevent double-reward if FinishAndReturnToMain fires twice
    private bool rewardsGranted = false;

    private void OnDestroy()
    {
        // Kill all tweens on chest and lid to prevent callbacks after scene unload
        if (chestTr != null)
        {
            chestTr.DOKill();
        }
        if (lidBone != null)
        {
            lidBone.DOKill();
        }
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        SpawnChest();
        PlayIntro();
    }

    private void SpawnChest()
    {
        if (spawnPoint == null || chestPrefab == null)
        {
            Debug.LogError("[ChestOpenScene] spawnPoint/chestPrefab eksik!");
            return;
        }

        chestGO = Instantiate(chestPrefab, spawnPoint.position, spawnPoint.rotation);
        chestTr = chestGO.transform;



        basePos = chestTr.position;
        baseScale = chestTr.localScale;

        // Lid otomatik bulma (Inspector'a bağlamadıysan)
        if (lidBone == null)
        {
            lidBone = FindLidTransform(chestTr);
        }

        if (lidBone != null)
        {
            Debug.Log($"[ChestOpenScene] lidBone bulundu: {GetTransformPath(lidBone)}");

            // Runtime pivot fix (eğer lid yanlış pivot etrafında dönüyorsa)
            if (useRuntimePivotFix)
            {
                lidBone = CreateRuntimePivot(lidBone);
            }

            // kapalı rotasyonu otomatik al (prefab zaten kapalıysa en güzeli)
            lidClosedLocalEuler = lidBone.localEulerAngles;
        }
        else
        {
            Debug.LogError($"[ChestOpenScene] lidBone bulunamadı! Chest: {chestGO.name}");
            Debug.LogError($"[ChestOpenScene] Aranan yol: '{lidSearchPath}' veya isim: '{lidTransformName}'");
            PrintHierarchy(chestTr);
        }

        // Collider kontrol
        var col = chestGO.GetComponentInChildren<Collider>();
        if (col == null)
            Debug.LogWarning("[ChestOpenScene] Chest prefabında Collider yok! Tıklama çalışmaz.");
    }

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

        // yumuşak settle
        s.Append(chestTr.DOMoveY(basePos.y + 0.06f, 0.16f).SetEase(Ease.OutSine));
        s.Append(chestTr.DOMoveY(basePos.y, 0.16f).SetEase(Ease.InOutSine));

        s.OnComplete(() =>
        {
            phase = Phase.Closed_TapToOpen;
            tapCount = 0;
            afterOpenTapped = 0;
        });
    }

    private void Update()
    {
        if (phase == Phase.Intro || phase == Phase.LidOpening) return;
        if (cam == null || chestTr == null) return;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0)) TryTap(Input.mousePosition);
#else
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began) TryTap(Input.GetTouch(0).position);
#endif
    }

    private void TryTap(Vector2 screenPos)
    {
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, chestLayerMask))
        {
            if (hit.transform != null && hit.transform.IsChildOf(chestTr))
            {
                OnChestTapped();
            }
        }
    }

    private void OnChestTapped()
    {
        if (phase == Phase.Closed_TapToOpen)
        {
            tapCount++;
            PlayTapFeedback();

            if (tapCount >= tapsToOpen)
                OpenLid();

            return;
        }

        if (phase == Phase.Opened_TapLoot)
        {
            afterOpenTapped++;
            PlayTapFeedback(openedFeel: true);

            if (afterOpenTapped >= afterOpenTapCount)
                phase = Phase.Done_TapToExit;

            return;
        }

        if (phase == Phase.Done_TapToExit)
        {
            FinishAndReturnToMain();
        }
    }

    private void PlayTapFeedback(bool openedFeel = false)
    {
        chestTr.DOKill(true);

        Sequence s = DOTween.Sequence().SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        // squash
        s.Append(chestTr.DOScale(new Vector3(baseScale.x * 1.06f, baseScale.y * 0.94f, baseScale.z * 1.06f), 0.07f)
            .SetEase(Ease.OutSine));

        // jump
        s.Append(chestTr.DOJump(basePos, tapJumpPower, 1, tapJumpDuration).SetEase(Ease.OutQuad));

        // restore
        s.Join(chestTr.DOScale(baseScale, 0.10f).SetEase(Ease.OutSine));

        // openedken daha yumuşak shake
        if (openedFeel)
            s.Join(chestTr.DOShakeRotation(0.20f, new Vector3(0f, 7f, 0f), 14, 90f));
        else
            s.Join(chestTr.DOShakeRotation(0.22f, new Vector3(0f, 10f, 0f), 18, 90f));
    }

    private void OpenLid()
    {
        if (lidBone == null)
        {
            // lid yoksa direkt sonraki faz
            Debug.LogWarning("[ChestOpenScene] OpenLid called but lidBone is null! Skipping animation.");
            phase = Phase.Opened_TapLoot;
            return;
        }

        phase = Phase.LidOpening;

        // Kill any existing tweens on lidBone to prevent overlap
        lidBone.DOKill(true);
        lidBone.localEulerAngles = lidClosedLocalEuler;

        Debug.Log($"[ChestOpenScene] Opening lid from {lidClosedLocalEuler} to {lidOpenLocalEuler}");

        lidBone.DOLocalRotate(lidOpenLocalEuler, lidOpenDuration, RotateMode.Fast)
            .SetEase(lidEase)
            .SetLink(lidBone.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                phase = Phase.Opened_TapLoot;
                afterOpenTapped = 0;
            });
    }

    #region Lid Finding Utilities

    /// <summary>
    /// Lid transform'unu bul: önce path ile dene, sonra recursive isim araması yap.
    /// </summary>
    private Transform FindLidTransform(Transform root)
    {
        // 1) Öncelikli yol ile dene (örn: "Empty/Cube.004")
        if (!string.IsNullOrEmpty(lidSearchPath))
        {
            var t = root.Find(lidSearchPath);
            if (t != null)
            {
                Debug.Log($"[ChestOpenScene] Lid found via path: {lidSearchPath}");
                return t;
            }
        }

        // 2) Eski yol ile dene (geriye uyumluluk: body/bone001)
        var legacyPath = root.Find("body/bone001");
        if (legacyPath != null)
        {
            Debug.Log("[ChestOpenScene] Lid found via legacy path: body/bone001");
            return legacyPath;
        }

        // 3) Recursive isim araması
        if (!string.IsNullOrEmpty(lidTransformName))
        {
            var found = FindChildRecursive(root, lidTransformName);
            if (found != null)
            {
                Debug.Log($"[ChestOpenScene] Lid found via recursive search: {lidTransformName}");
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Transform'u isimle recursive olarak ara.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            var found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Runtime'da pivot GameObject oluştur ve lid'i ona parent yap.
    /// Bu sayede lid doğru nokta etrafında döner (menteşe efekti).
    /// </summary>
    private Transform CreateRuntimePivot(Transform originalLid)
    {
        // Lid'in parent'ını al
        Transform lidParent = originalLid.parent;

        // Yeni pivot objesi oluştur
        GameObject pivotGO = new GameObject("LidPivot_Runtime");
        Transform pivot = pivotGO.transform;

        // Pivot'u lid'in parent'ına child yap
        pivot.SetParent(lidParent, false);

        // Pivot pozisyonunu hesapla (lid'in bounds'una göre arka-üst kenar)
        Renderer lidRenderer = originalLid.GetComponent<Renderer>();
        if (lidRenderer != null)
        {
            Bounds bounds = lidRenderer.bounds;
            // Menteşe pozisyonu: üst-arka kenar (local space offset kullan)
            pivot.position = originalLid.TransformPoint(pivotOffset);
        }
        else
        {
            // Renderer yoksa, lid pozisyonuna offset ekle
            pivot.position = originalLid.position + originalLid.TransformDirection(pivotOffset);
        }

        // Pivot rotasyonunu lid'in rotasyonuna eşitle
        pivot.rotation = originalLid.rotation;

        // Lid'i pivot'un child'ı yap (world position koru)
        originalLid.SetParent(pivot, true);

        Debug.Log($"[ChestOpenScene] Runtime pivot created at {pivot.position}");

        return pivot;
    }

    /// <summary>
    /// Transform'un tam yolunu al (debug için).
    /// </summary>
    private string GetTransformPath(Transform t)
    {
        string path = t.name;
        Transform current = t.parent;
        while (current != null && current != chestTr)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    /// <summary>
    /// Chest hierarchy'sini konsola yazdır (debug için).
    /// </summary>
    private void PrintHierarchy(Transform root, int indent = 0)
    {
        string prefix = new string(' ', indent * 2);
        Debug.Log($"{prefix}- {root.name}");
        foreach (Transform child in root)
        {
            PrintHierarchy(child, indent + 1);
        }
    }

    #endregion

    private void FinishAndReturnToMain()
    {
        // Guard: prevent double execution (set immediately)
        if (rewardsGranted) return;
        rewardsGranted = true;

        // 1) Grant rewards from pending chest
        bool granted = GrantChestRewards();
        Debug.Log($"[ChestOpenScene] Rewards granted: {granted}");

        // 2) Clear pending chest (already consumed from inventory in ChestPopupController)
        if (ChestInventoryManager.Instance != null)
        {
            ChestInventoryManager.Instance.ClearPendingOpenChest();
            Debug.Log("[ChestOpenScene] Pending chest cleared.");
        }

        // 3) Save
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        // 4) Return to Main
        SceneManager.LoadScene("Main");
    }

    /// <summary>
    /// Grants rewards from the pending chest: money, card copies, and nitro coins.
    /// Must be called BEFORE consuming the chest.
    /// Returns true if rewards were granted, false if no chest data available.
    /// </summary>
    private bool GrantChestRewards()
    {
        if (ChestInventoryManager.Instance == null)
        {
            Debug.LogWarning("[ChestOpenScene] ChestInventoryManager is null.");
            return false;
        }

        // Get the persisted pending chest (stored before scene transition)
        var chestData = ChestInventoryManager.Instance.GetPendingOpenChest();
        if (chestData == null)
        {
            Debug.LogError("[ChestOpenScene] No pending chest to grant rewards from! Make sure SetPendingOpenChest was called before scene load.");
            return false;
        }

        Debug.Log($"[ChestOpenScene] Granting rewards from chest: {chestData.chestName}, cardReward={chestData.cardReward}, minReward={chestData.minReward}, maxReward={chestData.maxReward}");

        // --- Money reward (double-safe, no float cast, min/max swap if needed) ---
        double minR = chestData.minReward;
        double maxR = chestData.maxReward;
        if (minR > maxR)
        {
            // Swap if inspector values are inverted
            double tmp = minR;
            minR = maxR;
            maxR = tmp;
        }
        double moneyReward = minR + (maxR - minR) * Random.value;
        if (CurrencyManager.Instance != null && moneyReward > 0)
        {
            CurrencyManager.Instance.AddMoney(moneyReward);
            Debug.Log($"[ChestReward] Money: +{moneyReward:N0}");
        }

        // --- Card copies reward ---
        // Debug: Check CardManager state
        Debug.Log($"[ChestOpenScene] CardManager.Instance != null: {CardManager.Instance != null}");
        if (CardManager.Instance != null)
        {
            Debug.Log($"[ChestOpenScene] CardManager.Instance.cards != null: {CardManager.Instance.cards != null}, Length: {CardManager.Instance.cards?.Length ?? 0}");
        }
        Debug.Log($"[ChestOpenScene] chestData.cardReward: {chestData.cardReward}");

        if (chestData.cardReward > 0 && CardManager.Instance != null)
        {
            // Pick a random card type from CardManager's defined cards only
            var definedCards = CardManager.Instance.cards;
            if (definedCards != null && definedCards.Length > 0)
            {
                int randomIndex = Random.Range(0, definedCards.Length);
                CardType randomType = definedCards[randomIndex].type;
                CardManager.Instance.AddCardCopies(randomType, chestData.cardReward);
                Debug.Log($"[ChestReward] Cards: +{chestData.cardReward} {randomType}");
            }
            else
            {
                Debug.LogWarning("[ChestReward] No cards defined in CardManager!");
            }
        }

        // --- Turbo reward (mapped to NitroCoins, clamped & non-negative) ---
        int turboMin = Mathf.Max(0, chestData.turboMin);
        int turboMax = Mathf.Max(turboMin, chestData.turboMax);
        int nitroReward = Random.Range(turboMin, turboMax + 1); // +1 for inclusive max
        if (CurrencyManager.Instance != null && nitroReward > 0)
        {
            CurrencyManager.Instance.AddNitroCoins(nitroReward);
            Debug.Log($"[ChestReward] NitroCoins: +{nitroReward}");
        }

        return true;
    }
}
