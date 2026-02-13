using UnityEngine;

public class BottomBarController : MonoBehaviour
{
    public BottomBarTabUI[] tabs;
    public int defaultTabIndex = 2; // örn: Clicker tab’i başlangıç

    private int currentIndex = -1;

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
        SetActiveTab(index);
    }



}
