using UnityEngine;

public class BottomBarController : MonoBehaviour
{
    public static BottomBarController Instance { get; private set; }

    public BottomBarTabUI[] tabs;
    public int defaultTabIndex = 2; // örn: Clicker tab'i başlangıç

    private int currentIndex = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        SetActiveTab(defaultTabIndex);
    }

    public void SetActiveTab(int index)
    {
        if (tabs == null || tabs.Length == 0) return;
        if (index < 0 || index >= tabs.Length) return;

        currentIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool selected = (i == index);
            if (tabs[i] != null)
                tabs[i].SetSelected(selected);
        }

        if (PanelTransitionManager.Instance != null)
            PanelTransitionManager.Instance.SwitchTo((BottomTab)index);
    }

    // Button OnClick'lerden çağırmak için
    public void OnTabButtonClicked(int index)
    {
        // U1: UI click SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayUIClick();

        SetActiveTab(index);
    }



}
