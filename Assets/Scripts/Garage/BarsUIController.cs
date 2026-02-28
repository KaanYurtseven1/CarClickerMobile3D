// ════════════════════════════════════════════════════════════════
// BarsUIController.cs – Manages the 3 stat bars in BarsGroup.
//
// Hierarchy variants handled:
//   (A) Direct children:
//       Bar1_durability / BarFillEmpty, BarFillEmpty (1) … (14)
//   (B) Nested under a visual container:
//       Bar3_speed / Bar3_Visual / BarFillEmpty, BarFillEmpty (1) … (14)
//
// Resolution strategy: for each bar root, find ALL descendants
// whose name starts with "BarFillEmpty" (using recursive CollectSegments),
// sort them by sibling index, and cache the first child of each
// (BarFillFull) for enable/disable.
//
// Animation: on Refresh, segments are activated one-by-one from 0
// to the target count using a DOTween Sequence (step delay tunable).
// If a new Refresh arrives mid-animation the previous sequence is
// killed and a fresh one starts from 0.
//
// Inspector wiring:
//   barsGroupParent → Panel_CarUI/LayoutRoot/BarsGroup
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class BarsUIController : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("The BarsGroup transform that parents the 3 bar containers.")]
    [SerializeField] private Transform barsGroupParent;

    // ─── Animation Settings (inspector-tunable) ───
    [Header("Fill Animation")]
    [Tooltip("Delay before the fill animation begins (seconds).")]
    [SerializeField] private float startDelay = 0.05f;
    [Tooltip("Time between activating successive segments (seconds).")]
    [SerializeField] private float stepDelay = 0.04f;

    private const int BAR_COUNT = 3;
    private const int SEGMENTS = 15;

    private const string SEGMENT_PREFIX = "BarFillEmpty";

    private static readonly string[] BarNames =
        { "Bar1_durability", "Bar2_acceleration", "Bar3_speed" };

    // _fillFull[bar][segment] → the BarFillFull GameObject to enable/disable
    private GameObject[][] _fillFull;
    private bool _resolved;

    // One tween sequence per bar so they can be killed independently
    private Sequence[] _barSequences;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        ResolveBars();
    }

    private void OnDestroy()
    {
        KillAllSequences();
    }

    // ══════════════════ Resolution ══════════════════

    private void ResolveBars()
    {
        if (_resolved) return;
        if (barsGroupParent == null)
        {
            Debug.LogError("[BarsUIController] barsGroupParent is not assigned.");
            return;
        }

        _fillFull = new GameObject[BAR_COUNT][];
        _barSequences = new Sequence[BAR_COUNT];

        for (int b = 0; b < BAR_COUNT; b++)
        {
            _fillFull[b] = new GameObject[SEGMENTS];

            Transform bar = barsGroupParent.Find(BarNames[b]);
            if (bar == null)
            {
                Debug.LogError($"[BarsUIController] '{BarNames[b]}' not found under " +
                               $"'{barsGroupParent.name}'.");
                continue;
            }

            // Collect every descendant Transform whose name starts with "BarFillEmpty".
            // This works whether the segments are direct children or nested inside
            // an intermediate container (e.g. Bar3_Visual).
            List<Transform> segments = new List<Transform>();
            CollectSegments(bar, segments);

            // Sort by sibling index so the visual left→right order is preserved.
            segments.Sort((a, b2) => a.GetSiblingIndex().CompareTo(b2.GetSiblingIndex()));

            int count = Mathf.Min(segments.Count, SEGMENTS);
            if (segments.Count != SEGMENTS)
            {
                Debug.LogWarning($"[BarsUIController] '{BarNames[b]}' has {segments.Count} " +
                                 $"BarFillEmpty segments (expected {SEGMENTS}).");
            }

            for (int s = 0; s < count; s++)
            {
                Transform empty = segments[s];
                if (empty.childCount > 0)
                {
                    _fillFull[b][s] = empty.GetChild(0).gameObject; // BarFillFull
                }
                else
                {
                    Debug.LogWarning($"[BarsUIController] '{BarNames[b]}' segment {s} " +
                                     $"('{empty.name}') has no BarFillFull child.");
                }
            }
        }

        _resolved = true;
    }

    /// <summary>
    /// Recursively collects all descendants of <paramref name="parent"/>
    /// whose name starts with <see cref="SEGMENT_PREFIX"/>.
    /// </summary>
    private static void CollectSegments(Transform parent, List<Transform> results)
    {
        int childCount = parent.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(SEGMENT_PREFIX))
            {
                results.Add(child);
            }
            else
            {
                // Recurse into non-segment containers (e.g. Bar3_Visual)
                CollectSegments(child, results);
            }
        }
    }

    // ══════════════════ Public API ══════════════════

    /// <summary>
    /// Updates the three bars.  Values are clamped to [0, 15].
    /// When <paramref name="animate"/> is true (default), segments are
    /// activated one-by-one via a DOTween Sequence.  When false, bars
    /// snap instantly (used for part-toggle feedback).
    /// If called again while animating, previous animation is killed
    /// cleanly and a new one starts from 0.
    /// <para>Bar order: 0 = durability, 1 = acceleration, 2 = speed.</para>
    /// </summary>
    public void Refresh(int durability, int acceleration, int speed, bool animate = true)
    {
        if (!_resolved) ResolveBars();
        if (_fillFull == null) return;

        int[] values = { durability, acceleration, speed };

        for (int b = 0; b < BAR_COUNT; b++)
        {
            if (_fillFull[b] == null) continue;

            int filled = Mathf.Clamp(values[b], 0, SEGMENTS);

            if (animate)
            {
                AnimateBar(b, filled);
            }
            else
            {
                SnapBar(b, filled);
            }
        }
    }

    // ══════════════════ Instant Snap ══════════════════

    /// <summary>
    /// Instantly sets bar segments to the target count with no animation.
    /// Kills any in-flight sequence for this bar first.
    /// </summary>
    private void SnapBar(int barIndex, int target)
    {
        // Kill any in-flight sequence for this bar
        if (_barSequences[barIndex] != null && _barSequences[barIndex].IsActive())
        {
            _barSequences[barIndex].Kill();
            _barSequences[barIndex] = null;
        }

        for (int s = 0; s < SEGMENTS; s++)
        {
            if (_fillFull[barIndex][s] != null)
                _fillFull[barIndex][s].SetActive(s < target);
        }
    }

    // ══════════════════ Sequential Fill Animation ══════════════════

    /// <summary>
    /// Animates a single bar: turns all segments off, then activates
    /// segments 0 → target-1 one at a time using a DOTween Sequence.
    /// </summary>
    private void AnimateBar(int barIndex, int target)
    {
        // Kill any in-flight sequence for this bar
        if (_barSequences[barIndex] != null && _barSequences[barIndex].IsActive())
        {
            _barSequences[barIndex].Kill();
            _barSequences[barIndex] = null;
        }

        // Immediately turn all segments off (start from empty)
        for (int s = 0; s < SEGMENTS; s++)
        {
            if (_fillFull[barIndex][s] != null)
                _fillFull[barIndex][s].SetActive(false);
        }

        // Nothing to animate if target is 0
        if (target <= 0) return;

        // Build a new sequence that enables segments one-by-one
        Sequence seq = DOTween.Sequence()
            .SetUpdate(true)           // unscaled time
            .SetTarget(this);          // tied to this MonoBehaviour

        // Optional initial pause
        if (startDelay > 0f)
            seq.AppendInterval(startDelay);

        for (int s = 0; s < target; s++)
        {
            GameObject fill = _fillFull[barIndex][s];
            if (fill == null) continue;

            // AppendCallback activates the next segment after stepDelay
            seq.AppendCallback(() => fill.SetActive(true));

            // Add a gap before the next segment (skip after the last one)
            if (s < target - 1)
                seq.AppendInterval(stepDelay);
        }

        _barSequences[barIndex] = seq;
    }

    // ══════════════════ Cleanup ══════════════════

    private void KillAllSequences()
    {
        if (_barSequences == null) return;
        for (int b = 0; b < BAR_COUNT; b++)
        {
            if (_barSequences[b] != null && _barSequences[b].IsActive())
            {
                _barSequences[b].Kill();
                _barSequences[b] = null;
            }
        }
    }
}
