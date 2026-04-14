using UnityEngine;

/// <summary>
/// BoostAudioController — Subscribes to BoostModeController events and plays
/// appropriate audio through SFXManager. Attach to same GameObject as BoostModeController.
/// </summary>
public class BoostAudioController : MonoBehaviour
{
    private bool _subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (!_subscribed)
            TrySubscribe();
    }

    private void TrySubscribe()
    {
        if (_subscribed) return;
        if (BoostModeController.Instance == null) return;

        BoostModeController.Instance.OnBoostReady += OnBoostReady;
        BoostModeController.Instance.OnBoostStarted += OnBoostStarted;
        BoostModeController.Instance.OnBoostEnded += OnBoostEnded;
        BoostModeController.Instance.OnNitroChargeAccepted += OnNitroDeposit;
        BoostModeController.Instance.OnStateChanged += OnStateChanged;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (BoostModeController.Instance == null) return;

        BoostModeController.Instance.OnBoostReady -= OnBoostReady;
        BoostModeController.Instance.OnBoostStarted -= OnBoostStarted;
        BoostModeController.Instance.OnBoostEnded -= OnBoostEnded;
        BoostModeController.Instance.OnNitroChargeAccepted -= OnNitroDeposit;
        BoostModeController.Instance.OnStateChanged -= OnStateChanged;
        _subscribed = false;
    }

    // B1: Fully charged ding
    private void OnBoostReady()
    {
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayBoostReady();
    }

    // B2: Turbo ignition + B3: Start boost loop
    private void OnBoostStarted(float duration)
    {
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayBoostActivate();
            SFXManager.Instance.StartBoostLoop();
        }
        // Duck music during boost
        if (MusicManager.Instance != null)
            MusicManager.Instance.DuckMusic(0.5f, 0.3f);
    }

    // B4: Power-down + stop boost loop
    private void OnBoostEnded()
    {
        if (SFXManager.Instance != null)
        {
            SFXManager.Instance.PlayBoostEnd();
            SFXManager.Instance.StopBoostLoop();
        }
        // Restore music
        if (MusicManager.Instance != null)
            MusicManager.Instance.RestoreMusic(0.8f);
    }

    // N4: Nitro deposit ding
    private void OnNitroDeposit()
    {
        if (SFXManager.Instance != null)
            SFXManager.Instance.PlayNitroDeposit();
    }

    // B5: Cooldown complete pip
    private void OnStateChanged(BoostModeController.BoostState newState)
    {
        if (newState == BoostModeController.BoostState.Charging)
        {
            // Cooldown just ended, now charging again
            if (SFXManager.Instance != null)
                SFXManager.Instance.PlayBoostCooldownComplete();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
