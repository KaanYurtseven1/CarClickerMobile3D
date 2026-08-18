using UnityEngine;

public class CarEvolution : MonoBehaviour
{
    public Renderer carRenderer;
    public Material stage0Material;
    public Material stage1Material;
    public Material stage2Material;

    public double stage1Threshold = 1000;
    public double stage2Threshold = 10000;

    private int currentStage = 0;

    private void Start()
    {
        ApplyStage(0);
    }

    private void Update()
    {
        if (CurrencyManager.Instance == null || carRenderer == null) return;

        double money = CurrencyManager.Instance.money;

        if (currentStage == 0 && money >= stage1Threshold)
        {
            ApplyStage(1);
        }
        else if (currentStage == 1 && money >= stage2Threshold)
        {
            ApplyStage(2);
        }
    }

    private void ApplyStage(int stage)
    {
        currentStage = stage;

        // T4: Car evolution stage change SFX
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayCarEvolution();

        switch (stage)
        {
            case 0:
                if (stage0Material != null)
                    carRenderer.material = stage0Material;
                break;
            case 1:
                if (stage1Material != null)
                    carRenderer.material = stage1Material;
                break;
            case 2:
                if (stage2Material != null)
                    carRenderer.material = stage2Material;
                break;
        }

        Debug.Log("[CarEvolution] Stage changed to: " + stage);
    }
}
