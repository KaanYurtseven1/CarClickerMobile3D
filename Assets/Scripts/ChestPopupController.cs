using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ChestPopupController : MonoBehaviour
{
    public static ChestPopupController Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Root")]
    [SerializeField] private GameObject popupRoot;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI titleText;

    [SerializeField] private GameObject timerTextObj;     // TimerText'in GameObject'i (SetActive yapacağız)
    [SerializeField] private TextMeshProUGUI timerText;   // TimerText TMP component

    [Header("State Label")]
    [SerializeField] private GameObject openGetRewardTextObj; // "Open & get your reward" yazısı (GameObject)

    [Header("Buttons / Objects")]
    [SerializeField] private GameObject openNowObj;
    [SerializeField] private GameObject startUnlockObj;
    [SerializeField] private GameObject skip20Obj;
    [SerializeField] private GameObject openObj;

    public bool IsPopupOpen => popupRoot != null && popupRoot.activeSelf;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (popupRoot != null) popupRoot.SetActive(false);
    }

    private void Update()
    {
        if (IsPopupOpen)
            RefreshUI();
    }

    public void ShowPopupFromInventory()
    {
        if (popupRoot != null) popupRoot.SetActive(true);
        RefreshUI();
    }

    public void ClosePopup()
    {
        if (popupRoot != null) popupRoot.SetActive(false);
    }

    private void RefreshUI()
    {
        if (ChestInventoryManager.Instance == null) return;

        var cd = ChestInventoryManager.Instance.GetChestToShowInPopup();
        if (cd == null)
        {
            ClosePopup();
            return;
        }

        if (titleText != null)
            titleText.text = cd.chestName;

        // ✅ Önce HER ŞEYİ kapat (state artığı kalmasın)
        SetActive(openGetRewardTextObj, false);
        SetActive(timerTextObj, false);
        SetActive(openNowObj, false);
        SetActive(startUnlockObj, false);
        SetActive(skip20Obj, false);
        SetActive(openObj, false);

        // ✅ Sonra state'e göre gerekenleri aç
        switch (cd.state)
        {
            case ChestState.Idle:
                // İlk açılış: "Open & get your reward" açık, timer kapalı
                SetActive(openGetRewardTextObj, true);
                SetActive(openNowObj, true);
                SetActive(startUnlockObj, true);
                break;

            case ChestState.Unlocking:
                // StartUnlock sonrası: "Open & get your reward" kapalı, timer açık
                SetActive(timerTextObj, true);
                SetActive(skip20Obj, !cd.skipUsed);

                if (timerText != null)
                    timerText.text = FormatTime(cd.remainingTime);
                break;

            case ChestState.ReadyToOpen:
                // Sayaç bitti: timer kapalı, "Open & get your reward" açık + Open butonu
                SetActive(openGetRewardTextObj, true);
                SetActive(openObj, true);
                break;
        }
    }

    // --- UI Button Events ---

    public void OnStartUnlockPressed()
    {
        if (ChestInventoryManager.Instance == null) return;

        // 1) UI'yi anında değiştir (kesin davranış)
        SetActive(timerTextObj, true);
        SetActive(openGetRewardTextObj, false);

        SetActive(openNowObj, false);
        SetActive(startUnlockObj, false);
        SetActive(skip20Obj, true);

        // 2) Unlock başlat
        ChestInventoryManager.Instance.StartUnlockOldest();

        // 3) UI'yı state'e göre tekrar doğrula
        RefreshUI();

        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();
    }

    public void OnSkip20Pressed()
    {
        if (ChestInventoryManager.Instance == null) return;

        bool ok = ChestInventoryManager.Instance.ApplySkip20Minutes();
        if (ok)
        {
            RefreshUI();

            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();
        }
    }

    // ✅ NITRO COIN ile beklemeden açma (15 coin — increased from 3)
    public void OnOpenNowPressed()
    {
        if (ChestInventoryManager.Instance == null) return;

        bool ok = ChestInventoryManager.Instance.OpenNowByNitro(15);
        if (ok)
        {
            RefreshUI();

            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();
        }
        else
        {
            Debug.Log("[ChestPopup] OpenNow failed (not enough nitro or no chest).");
        }
    }

    public void OnOpenPressed()
    {
        if (ChestInventoryManager.Instance == null) return;

        // 1) Get the chest data BEFORE consuming it
        var chestData = ChestInventoryManager.Instance.GetActiveChest();
        if (chestData == null)
        {
            Debug.LogWarning("[ChestPopup] OnOpenPressed: No active chest to open!");
            return;
        }

        // 2) Persist the chest data for ChestOpenScene
        ChestInventoryManager.Instance.SetPendingOpenChest(chestData);
        Debug.Log($"[ChestPopup] OnOpenPressed: Persisted chest '{chestData.chestName}' for opening.");

        // 3) Consume chest from inventory (removes from list)
        bool ok = ChestInventoryManager.Instance.ConsumeReadyChestForOpening();
        if (!ok)
        {
            Debug.LogWarning("[ChestPopup] OnOpenPressed: ConsumeReadyChestForOpening failed!");
            ChestInventoryManager.Instance.ClearPendingOpenChest();
            return;
        }

        // 4) UI'yi hemen güncelle
        if (ChestShownUI.Instance != null)
            ChestShownUI.Instance.RefreshVisibilityAndCount();

        // 5) Save al
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();

        // 6) Sahneye geç
        UnityEngine.SceneManagement.SceneManager.LoadScene("ChestOpenScene");
    }

    private void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    private string FormatTime(float seconds)
    {
        if (seconds < 0) seconds = 0;

        int s = Mathf.CeilToInt(seconds);
        int m = s / 60;
        int r = s % 60;

        return $"{m:00}m {r:00}s";
    }
}
