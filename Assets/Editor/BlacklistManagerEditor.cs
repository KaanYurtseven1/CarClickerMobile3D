#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlacklistManager))]
public class BlacklistManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("Complete Next Mission", GUILayout.Height(36)))
        {
            int idx = ((BlacklistManager)target).DebugCompleteNextMission();
            if (idx >= 0)
                Debug.Log($"[Blacklist-DEBUG] Mission {idx} completed via Inspector button.");
        }

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use this button.", MessageType.Info);
        }
    }
}
#endif
