using TMPro;
using UnityEngine;

public class ChestShownUI : MonoBehaviour
{
    public static ChestShownUI Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [Header("Root (hide/show)")]
    [SerializeField] private GameObject root; // ChestShown parent (ikon + text)

    [SerializeField] private TextMeshProUGUI countText;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        RefreshVisibilityAndCount();
    }

    private void OnEnable()
    {
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.OnInventoryChanged += RefreshVisibilityAndCount;
    }

    private void OnDisable()
    {
        if (ChestInventoryManager.Instance != null)
            ChestInventoryManager.Instance.OnInventoryChanged -= RefreshVisibilityAndCount;
    }

    public void RefreshVisibilityAndCount()
    {
        // During police chase, ChestShown must stay hidden
        if (PoliceCatchController.Instance != null && PoliceCatchController.Instance.IsChaseActive)
        {
            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
            return;
        }

        // During radar popup, ChestShown must stay hidden
        if (RadarPopupController.Instance != null && RadarPopupController.Instance.IsPopupOpen)
        {
            if (root != null)
                root.SetActive(false);
            else
                gameObject.SetActive(false);
            return;
        }

        int count = 0;
        if (ChestInventoryManager.Instance != null)
            count = ChestInventoryManager.Instance.GetUnopenedCount(); // toplam chest

        if (countText != null)
            countText.text = count.ToString();

        if (root != null)
            root.SetActive(count > 0);
        else
            gameObject.SetActive(count > 0);
    }


    // ChestShown ikonuna tıklama (Button OnClick)
    public void OnChestShownTapped()
    {
        Debug.Log("[ChestShownUI] Trying to open popup...");

        Debug.Log("[ChestShownUI] ChestPopupController.Instance = " + (ChestPopupController.Instance != null));

        Debug.Log("[ChestShownUI] ChestInventoryManager.Instance = " + (ChestInventoryManager.Instance != null));


        Debug.Log("[ChestShownUI] OnChestShownTapped fired");
        // Chest yoksa popup açma
        if (ChestInventoryManager.Instance == null) return;
        if (ChestInventoryManager.Instance.GetUnopenedCount() <= 0) return;

        Debug.Log("[ChestShownUI] BEFORE open call");
        if (ChestPopupController.Instance != null)
            ChestPopupController.Instance.ShowPopupFromInventory();
        Debug.Log("[ChestShownUI] AFTER open call");
    }
}
