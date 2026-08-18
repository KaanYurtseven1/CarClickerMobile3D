using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database that maps CardType → CardPopupThemeSO.
/// Create one asset via: Assets → Create → Cards → Popup Theme Database.
/// Assign every per-card theme to the "themes" list in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "CardPopupThemeDatabase", menuName = "Cards/Popup Theme Database")]
public class CardPopupThemeDatabaseSO : ScriptableObject
{
    [Tooltip("One entry per card type. Duplicates are ignored (first wins).")]
    public List<CardPopupThemeSO> themes = new List<CardPopupThemeSO>();

    // Runtime lookup built on first query.
    private Dictionary<CardType, CardPopupThemeSO> _lookup;

    /// <summary>
    /// Returns the theme for the given card type if one exists.
    /// Builds the dictionary lazily on first call.
    /// </summary>
    public bool TryGetTheme(CardType type, out CardPopupThemeSO theme)
    {
        if (_lookup == null)
            BuildLookup();

        return _lookup.TryGetValue(type, out theme);
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<CardType, CardPopupThemeSO>();

        if (themes == null) return;

        foreach (var t in themes)
        {
            if (t == null) continue;

            if (!_lookup.ContainsKey(t.type))
            {
                _lookup.Add(t.type, t);
            }
            else
            {
                Debug.LogWarning($"[CardPopupThemeDB] Duplicate theme for {t.type} — keeping first entry.");
            }
        }
    }

    /// <summary>
    /// Forces the lookup dictionary to rebuild (call after hot-reloading themes in editor).
    /// </summary>
    public void InvalidateCache()
    {
        _lookup = null;
    }
}
