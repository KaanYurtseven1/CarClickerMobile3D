using UnityEngine;

public class BuildingsPanelController : MonoBehaviour
{
    public GameObject buildingsPanel;

    private void Start()
    {
        if (buildingsPanel != null)
        {
            buildingsPanel.SetActive(false); // güvence olsun
        }
    }

    public void OpenPanel()
    {
        if (buildingsPanel != null)
            buildingsPanel.SetActive(true);
    }

    public void ClosePanel()
    {
        if (buildingsPanel != null)
            buildingsPanel.SetActive(false);
    }
}
