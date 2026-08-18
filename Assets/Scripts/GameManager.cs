using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // - Save/Load
    // - Global settings
    // - Sahne Geçişleri

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
