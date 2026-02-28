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
// whose name starts with "BarFillEmpty" (using GetComponentsInChildren),
// sort them by sibling index, and cache the first child of each
// (BarFillFull) for enable/disable.
//
// Inspector wiring:
//   barsGroupParent → Panel_CarUI/LayoutRoot/BarsGroup
// ════════════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

public class BarsUIController : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("The BarsGroup transform that parents the 3 bar containers.")]
    [SerializeField] private Transform barsGroupParent;

    private const int BAR_COUNT = 3;
    private const int SEGMENTS = 15;

    private const string SEGMENT_PREFIX = "BarFillEmpty";

    private static readonly string[] BarNames =
        { "Bar1_durability", "Bar2_acceleration", "Bar3_speed" };

    // _fillFull[bar][segment] → the BarFillFull GameObject to enable/disable
    private GameObject[][] _fillFull;
    private bool _resolved;

    // ══════════════════ Lifecycle ══════════════════

    private void Awake()
    {
        ResolveBars();
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
    /// <para>Bar order: 0 = durability, 1 = acceleration, 2 = speed.</para>
    /// </summary>
    public void Refresh(int durability, int acceleration, int speed)
    {
        if (!_resolved) ResolveBars();

        int[] values = { durability, acceleration, speed };

        for (int b = 0; b < BAR_COUNT; b++)
        {
            if (_fillFull[b] == null) continue;

            int filled = Mathf.Clamp(values[b], 0, SEGMENTS);
            for (int s = 0; s < SEGMENTS; s++)
            {
                if (_fillFull[b][s] != null)
                    _fillFull[b][s].SetActive(s < filled);
            }
        }
    }
}
