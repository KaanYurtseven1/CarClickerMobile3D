using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Audio Source")]
    public AudioSource sfxSource;

    [Header("Tap SFX (multiple)")]
    public AudioClip[] carTapClips;   // 11 tap sesini buraya koyacağız

    [Header("Single SFX")]
    public AudioClip buildingBuyClip;
    public AudioClip goalCompleteClip;
    public AudioClip upgradeClip;

    [Header("Settings")]
    public bool sfxEnabled = true;

    private int lastTapIndex = -1;    // aynı sesi üst üste çalmamak için

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    // ========= PUBLIC METHODS =========

    public void PlayCarTap()
    {
        if (!sfxEnabled || sfxSource == null || carTapClips == null || carTapClips.Length == 0)
            return;

        int index;

        if (carTapClips.Length == 1)
        {
            index = 0;
        }
        else
        {
            // Aynı index'i üst üste seçmemeye çalış
            do
            {
                index = Random.Range(0, carTapClips.Length);
            }
            while (index == lastTapIndex);
        }

        lastTapIndex = index;
        sfxSource.PlayOneShot(carTapClips[index]);
    }

    public void PlayBuildingBuy()
    {
        PlayOneShot(buildingBuyClip);
    }

    public void PlayUpgrade()
    {
        PlayOneShot(upgradeClip);
    }

    public void PlayGoalComplete()
    {
        PlayOneShot(goalCompleteClip);
    }

    // ========= INTERNAL =========

    private void PlayOneShot(AudioClip clip)
    {
        if (!sfxEnabled || clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
