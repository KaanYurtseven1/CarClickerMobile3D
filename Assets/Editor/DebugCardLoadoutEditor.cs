using UnityEngine;
using UnityEditor;
using System;

/// <summary>
/// Custom Inspector for DebugCardLoadout.
/// Renders a clean card table with checkboxes + level/segment fields,
/// and action buttons at the bottom.
/// </summary>
[CustomEditor(typeof(DebugCardLoadout))]
public class DebugCardLoadoutEditor : Editor
{
    private SerializedProperty entriesProp;
    private SerializedProperty applyOnPlayStartProp;

    private static readonly Color HeaderColor = new Color(0.2f, 0.7f, 1f, 0.3f);
    private static readonly Color ActiveColor = new Color(0.2f, 1f, 0.3f, 0.15f);

    private void OnEnable()
    {
        entriesProp = serializedObject.FindProperty("entries");
        applyOnPlayStartProp = serializedObject.FindProperty("applyOnPlayStart");

        // Ensure all card types are represented
        var loadout = (DebugCardLoadout)target;
        loadout.EnsureAllCardTypes();
        EditorUtility.SetDirty(loadout);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var loadout = (DebugCardLoadout)target;

        // ── Header ──
        EditorGUILayout.Space(4);
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
        EditorGUILayout.LabelField("Debug Card Loadout", headerStyle);
        EditorGUILayout.Space(2);

        // Status badge
        if (Application.isPlaying && loadout.IsDebugActive)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            EditorGUILayout.HelpBox("DEBUG CARDS ACTIVE (session-only)", MessageType.Info);
            GUI.backgroundColor = prev;
        }
        else if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Debug cards not active. Use buttons below to apply.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Configure cards below. They will be applied when you enter Play Mode.", MessageType.None);
        }

        EditorGUILayout.Space(4);

        // ── Options ──
        EditorGUILayout.PropertyField(applyOnPlayStartProp, new GUIContent("Auto-Apply on Play Start"));

        EditorGUILayout.Space(8);

        // ── Card Table ──
        // Header row
        var rect = EditorGUILayout.GetControlRect(false, 20);
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = HeaderColor;
        GUI.Box(rect, GUIContent.none);
        GUI.backgroundColor = prevBg;

        float x = rect.x;
        float checkW = 40f;
        float nameW = rect.width * 0.35f;
        float levelW = 60f;
        float segW = 60f;

        EditorGUI.LabelField(new Rect(x, rect.y, checkW, rect.height), "Own", EditorStyles.miniLabel);
        x += checkW;
        EditorGUI.LabelField(new Rect(x, rect.y, nameW, rect.height), "Card", EditorStyles.miniBoldLabel);
        x += nameW;
        EditorGUI.LabelField(new Rect(x, rect.y, levelW, rect.height), "Level", EditorStyles.miniLabel);
        x += levelW;
        EditorGUI.LabelField(new Rect(x, rect.y, segW, rect.height), "Segments", EditorStyles.miniLabel);

        // Card rows
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var entryProp = entriesProp.GetArrayElementAtIndex(i);
            var ownProp = entryProp.FindPropertyRelative("own");
            var levelProp = entryProp.FindPropertyRelative("level");
            var segmentsProp = entryProp.FindPropertyRelative("segments");
            var typeProp = entryProp.FindPropertyRelative("cardType");

            bool isOwned = ownProp.boolValue;

            rect = EditorGUILayout.GetControlRect(false, 20);

            // Highlight active rows
            if (isOwned)
            {
                prevBg = GUI.backgroundColor;
                GUI.backgroundColor = ActiveColor;
                GUI.Box(rect, GUIContent.none);
                GUI.backgroundColor = prevBg;
            }

            x = rect.x;

            // Checkbox
            ownProp.boolValue = EditorGUI.Toggle(new Rect(x + 10, rect.y, checkW - 10, rect.height), ownProp.boolValue);
            x += checkW;

            // Card name (read-only label)
            string cardName = ((CardType)typeProp.enumValueIndex).ToString();

            // Try to get display name from CardManager if available
            if (Application.isPlaying && CardManager.Instance != null)
            {
                var def = CardManager.Instance.GetCard((CardType)typeProp.enumValueIndex);
                if (def != null && !string.IsNullOrEmpty(def.displayName))
                    cardName = def.displayName;
            }

            var labelStyle = isOwned ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUI.LabelField(new Rect(x, rect.y, nameW, rect.height), cardName, labelStyle);
            x += nameW;

            // Level & Segments — editable only when owned
            using (new EditorGUI.DisabledScope(!isOwned))
            {
                levelProp.intValue = Mathf.Max(0, EditorGUI.IntField(new Rect(x, rect.y, levelW - 4, rect.height), levelProp.intValue));
                x += levelW;
                segmentsProp.intValue = Mathf.Max(0, EditorGUI.IntField(new Rect(x, rect.y, segW - 4, rect.height), segmentsProp.intValue));
            }

            // Show real vs debug info during play
            if (Application.isPlaying && loadout.IsDebugActive && isOwned && CardManager.Instance != null)
            {
                x += segW;
                float infoW = rect.width - (x - rect.x);
                if (infoW > 30)
                {
                    var card = CardManager.Instance.GetCard((CardType)typeProp.enumValueIndex);
                    if (card != null)
                    {
                        EditorGUI.LabelField(
                            new Rect(x, rect.y, infoW, rect.height),
                            $"(Live: L{card.currentLevel} S{card.copiesOwned})",
                            EditorStyles.miniLabel
                        );
                    }
                }
            }
        }

        EditorGUILayout.Space(12);

        // ── Action Buttons ──
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Apply Debug Ownership", GUILayout.Height(28)))
            {
                loadout.ApplyDebugOwnership();
            }

            if (GUILayout.Button("Clear Debug Cards", GUILayout.Height(28)))
            {
                loadout.ClearDebugCards();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Magnet + Rain combo debug trigger
            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            if (GUILayout.Button("\u26a1 Start Nitro Magnet + Nitro Rain Now", GUILayout.Height(30)))
            {
                loadout.DebugStartMagnetAndRain();
            }
            GUI.backgroundColor = prevColor;

            EditorGUILayout.Space(2);

            // Boost Mode debug trigger
            prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.9f, 1f);
            if (GUILayout.Button("\ud83d\ude80 Start Boost Mode Now", GUILayout.Height(30)))
            {
                loadout.DebugStartBoostMode();
            }
            GUI.backgroundColor = prevColor;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Buttons are active only in Play Mode.", MessageType.None);
        }

        // ── Quick-select helpers ──
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Select All", EditorStyles.miniButton))
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
                entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("own").boolValue = true;
        }

        if (GUILayout.Button("Select None", EditorStyles.miniButton))
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
                entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("own").boolValue = false;
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }
}
