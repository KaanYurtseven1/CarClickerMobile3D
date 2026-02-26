// ════════════════════════════════════════════════════════════════
// PartsUIController.cs – Manages the 18 mod-part "Çerçeve" buttons.
//
// Inspector wiring:
//   framesParent → Canvas/Slide_Parts/Modifiye_Parts
// ════════════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartsUIController : MonoBehaviour
{
    // ─── Serialized ───
    [Header("References")]
    [Tooltip("Modifiye_Parts transform whose children are the 18 Çerçeve Buttons.")]
    [SerializeField] private Transform framesParent;

    [Header("Visuals")]
    [Tooltip("Tint for an ACTIVE (enabled) part frame.")]
    [SerializeField] private Color activeColor = Color.white;
    [Tooltip("Tint for an INACTIVE (disabled) part frame.")]
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.4f);

    // ─── Callback (set by GarageController) ───
    [NonSerialized] public Action<string> onPartToggled;

    // ─── Constants ───
    private const int PART_COUNT = 18;

    // ─── Cached ───
    private readonly Button[] _frameButtons = new Button[PART_COUNT];
    private readonly Image[] _frameImages = new Image[PART_COUNT];   // frame / background
    private readonly Image[] _iconImages = new Image[PART_COUNT];   // child icon
    private string[] _partKeys;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        ResolveFrames();
    }

    // ══════════════════ Frame Resolution ══════════════════

    private void ResolveFrames()
    {
        if (framesParent == null)
        {
            Debug.LogError("[PartsUIController] framesParent is not assigned.");
            return;
        }

        int childCount = Mathf.Min(framesParent.childCount, PART_COUNT);
        for (int i = 0; i < childCount; i++)
        {
            Transform frame = framesParent.GetChild(i);

            _frameImages[i] = frame.GetComponent<Image>();

            // Ensure a Button component exists
            _frameButtons[i] = frame.GetComponent<Button>();
            if (_frameButtons[i] == null)
                _frameButtons[i] = frame.gameObject.AddComponent<Button>();

            // The first child of each frame is the icon Image
            if (frame.childCount > 0)
                _iconImages[i] = frame.GetChild(0).GetComponent<Image>();

            int captured = i;
            _frameButtons[i].onClick.AddListener(() => OnFrameClicked(captured));
        }
    }

    private void OnFrameClicked(int index)
    {
        if (_partKeys == null || index >= _partKeys.Length) return;
        onPartToggled?.Invoke(_partKeys[index]);
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>
    /// Full refresh: sets per-car icons and updates highlight states.
    /// </summary>
    public void Refresh(CarDataSO data, HashSet<string> enabledParts, List<string> globalPartKeys)
    {
        if (data == null || globalPartKeys == null) return;

        _partKeys = globalPartKeys.ToArray();

        for (int i = 0; i < PART_COUNT; i++)
        {
            // Set icon from CarDataSO.partOptions
            if (i < data.partOptions.Count && _iconImages[i] != null)
            {
                Sprite ico = data.partOptions[i].icon;
                _iconImages[i].sprite = ico;
                _iconImages[i].enabled = ico != null;
            }

            // Highlight based on current enabled state
            string key = (i < _partKeys.Length) ? _partKeys[i] : null;
            bool active = key != null && enabledParts.Contains(key);
            SetFrameVisual(i, active);
        }
    }

    /// <summary>
    /// Updates the visual highlight for a single part by its key.
    /// </summary>
    public void UpdatePartHighlight(string partKey, bool active)
    {
        if (_partKeys == null) return;
        for (int i = 0; i < _partKeys.Length; i++)
        {
            if (_partKeys[i] == partKey)
            {
                SetFrameVisual(i, active);
                break;
            }
        }
    }

    // ══════════════════ Helpers ══════════════════

    private void SetFrameVisual(int index, bool active)
    {
        if (index < 0 || index >= PART_COUNT) return;
        if (_frameImages[index] != null)
            _frameImages[index].color = active ? activeColor : inactiveColor;
    }
}
