using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// MusicManager — Persistent singleton that handles background music across all scenes.
/// Crossfades between scene-specific tracks on scene load.
/// Volume controlled via PlayerPrefs ("MusicVolume", 0–1).
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }

    [Header("Music Clips")]
    [Tooltip("Chill lo-fi / electronic loop for Main scene")]
    public AudioClip mainSceneMusic;
    [Tooltip("Mystical ambient pad for ChestOpenScene")]
    public AudioClip chestSceneMusic;
    [Tooltip("Showroom ambient loop for NewGarage scene")]
    public AudioClip garageSceneMusic;
    [Tooltip("Dramatic pump-up track for TakeTheCarScene cinematic")]
    public AudioClip cinematicSceneMusic;

    [Header("Settings")]
    [Tooltip("Crossfade duration when switching tracks (seconds)")]
    [SerializeField] private float crossfadeDuration = 1.0f;
    [Tooltip("Base music volume (0–1)")]
    [SerializeField] private float baseVolume = 0.35f;

    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource activeSource;
    private AudioClip currentClip;
    private float _userVolume = 1f;

    private const string VolumeKey = "MusicVolume";

    public float UserVolume
    {
        get => _userVolume;
        set
        {
            _userVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, _userVolume);
            if (activeSource != null)
                activeSource.volume = baseVolume * _userVolume;
        }
    }

    public bool IsMuted => _userVolume <= 0.01f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupAudioSources();
            _userVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void SetupAudioSources()
    {
        musicSourceA = gameObject.AddComponent<AudioSource>();
        musicSourceA.loop = true;
        musicSourceA.playOnAwake = false;
        musicSourceA.volume = 0f;
        musicSourceA.priority = 64;

        musicSourceB = gameObject.AddComponent<AudioSource>();
        musicSourceB.loop = true;
        musicSourceB.playOnAwake = false;
        musicSourceB.volume = 0f;
        musicSourceB.priority = 64;

        activeSource = musicSourceA;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Play music for the initial scene
        PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        AudioClip clip = GetClipForScene(sceneName);
        PlayMusic(clip);
    }

    private AudioClip GetClipForScene(string sceneName)
    {
        switch (sceneName)
        {
            case "Main": return mainSceneMusic;
            case "ChestOpenScene": return chestSceneMusic;
            case "NewGarage": return garageSceneMusic;
            case "TakeTheCarScene": return cinematicSceneMusic;
            default: return mainSceneMusic;
        }
    }

    /// <summary>
    /// Crossfade to a new music clip. If clip is null, fades out current music.
    /// If same clip is already playing, does nothing.
    /// </summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == currentClip && activeSource != null && activeSource.isPlaying)
            return;

        currentClip = clip;

        AudioSource fadeOut = activeSource;
        AudioSource fadeIn = (activeSource == musicSourceA) ? musicSourceB : musicSourceA;
        activeSource = fadeIn;

        float targetVol = baseVolume * _userVolume;

        // Fade out old
        if (fadeOut != null && fadeOut.isPlaying)
        {
            fadeOut.DOKill();
            fadeOut.DOFade(0f, crossfadeDuration).OnComplete(() =>
            {
                fadeOut.Stop();
                fadeOut.clip = null;
            });
        }

        // Fade in new
        if (clip != null)
        {
            fadeIn.DOKill();
            fadeIn.clip = clip;
            fadeIn.volume = 0f;
            fadeIn.Play();
            fadeIn.DOFade(targetVol, crossfadeDuration);
        }
    }

    /// <summary>
    /// Temporarily duck music volume (e.g., during police chase or reward stingers).
    /// </summary>
    public void DuckMusic(float duckVolume01, float fadeDuration = 0.4f)
    {
        if (activeSource == null) return;
        activeSource.DOKill();
        activeSource.DOFade(baseVolume * _userVolume * duckVolume01, fadeDuration);
    }

    /// <summary>
    /// Restore music volume after ducking.
    /// </summary>
    public void RestoreMusic(float fadeDuration = 1.0f)
    {
        if (activeSource == null) return;
        activeSource.DOKill();
        activeSource.DOFade(baseVolume * _userVolume, fadeDuration);
    }

    /// <summary>Stop all music immediately.</summary>
    public void StopMusic()
    {
        if (musicSourceA != null) { musicSourceA.DOKill(); musicSourceA.Stop(); }
        if (musicSourceB != null) { musicSourceB.DOKill(); musicSourceB.Stop(); }
        currentClip = null;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
