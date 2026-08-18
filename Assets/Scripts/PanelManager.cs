using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public GameObject clickerRoot;
    public GameObject panelShopCards;
    public GameObject panelBank;
    public GameObject panelTimeWarp;
    public GameObject panelRanking;

    private void Start()
    {
        HideAllPanels();
        if (clickerRoot != null)
            clickerRoot.SetActive(true);

        // Ensure suppression is OFF on scene start (Clicker is the default panel)
        UIFlowState.IsContentPanelOpen = false;
    }

    public void OnClick_Clicker()
    {
        HideAllPanels();
        if (clickerRoot != null)
            clickerRoot.SetActive(true);

        UIFlowState.IsContentPanelOpen = false;
        Debug.Log($"[PanelManager] Switched to Clicker — suppression OFF");
    }

    public void OnClick_ShopCards()
    {
        HideAllPanels();
        if (panelShopCards != null)
            panelShopCards.SetActive(true);

        UIFlowState.IsContentPanelOpen = true;
        Debug.Log($"[PanelManager] Switched to ShopCards — suppression ON");
    }

    public void OnClick_Bank()
    {
        HideAllPanels();
        if (panelBank != null)
            panelBank.SetActive(true);

        UIFlowState.IsContentPanelOpen = true;
        Debug.Log($"[PanelManager] Switched to Bank — suppression ON");
    }

    public void OnClick_TimeWarp()
    {
        HideAllPanels();
        if (panelTimeWarp != null)
            panelTimeWarp.SetActive(true);

        UIFlowState.IsContentPanelOpen = true;
        Debug.Log($"[PanelManager] Switched to TimeWarp — suppression ON");
    }

    public void OnClick_Ranking()
    {
        HideAllPanels();
        if (panelRanking != null)
            panelRanking.SetActive(true);

        UIFlowState.IsContentPanelOpen = true;
        Debug.Log($"[PanelManager] Switched to Ranking — suppression ON");
    }

    public void OnClick_CloseShopCards()
    {
        if (panelShopCards != null)
            panelShopCards.SetActive(false);
    }

    public void OnClick_CloseBank()
    {
        if (panelBank != null)
            panelBank.SetActive(false);
    }

    public void OnClick_CloseTimeWarp()
    {
        if (panelTimeWarp != null)
            panelTimeWarp.SetActive(false);
    }

    public void OnClick_CloseRanking()
    {
        if (panelRanking != null)
            panelRanking.SetActive(false);
    }

    private void HideAllPanels()
    {
        if (panelShopCards != null) panelShopCards.SetActive(false);
        if (panelBank != null) panelBank.SetActive(false);
        if (panelTimeWarp != null) panelTimeWarp.SetActive(false);
        if (panelRanking != null) panelRanking.SetActive(false);
    }
}
