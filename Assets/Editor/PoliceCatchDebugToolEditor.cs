#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PoliceCatchDebugTool))]
public class PoliceCatchDebugToolEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("START POLICE CHASE (DEBUG)", GUILayout.Height(36)))
        {
            ((PoliceCatchDebugTool)target).TriggerChase();
        }

        GUI.enabled = true;

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use this button.", MessageType.Info);
        }
    }
}
#endif
