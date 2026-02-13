using UnityEngine;
using TMPro;

public class ClickTextSpawner : MonoBehaviour
{
    public static ClickTextSpawner Instance;

    public Canvas mainCanvas;
    public GameObject floatingTextPrefab;

    [Header("Performance")]
    public int maxActiveTexts = 30;   // Aynı anda ekranda en fazla kaç floating text
    private int activeTextCount = 0;


    private void Awake()
    {
        Instance = this;
    }

    public void SpawnClickText(Vector3 worldPosition, double amount)
    {
        if (mainCanvas == null || floatingTextPrefab == null || Camera.main == null)
            return;

        // Çok fazla text varsa yenisini spawn etme (FPS'i koru)
        if (activeTextCount >= maxActiveTexts)
            return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);


        GameObject go = Instantiate(floatingTextPrefab, mainCanvas.transform);
        activeTextCount++;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.position = screenPos;
        }

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = $"+{amount:0}";
        }

        FloatingTextLife ftl = go.GetComponent<FloatingTextLife>();
        if (ftl == null)
        {
            ftl = go.AddComponent<FloatingTextLife>();
        }

        ftl.lifeTime = 0.7f;
        ftl.moveOffset = new Vector3(0, 80f, 0);
    }

    // FloatingTextLife burayı çağıracak
    public void NotifyTextDestroyed()
    {
        activeTextCount = Mathf.Max(0, activeTextCount - 1);
    }
}
