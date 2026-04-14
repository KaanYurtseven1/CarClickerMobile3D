using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DebugChestSpawner))]
public class DebugChestSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        DebugChestSpawner spawner = (DebugChestSpawner)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Spawn Buttons (Play Mode Only)", EditorStyles.boldLabel);

        GUI.enabled = Application.isPlaying;

        if (GUILayout.Button("Spawn Common Chest", GUILayout.Height(30)))
            spawner.SpawnCommon();

        if (GUILayout.Button("Spawn Rare Chest", GUILayout.Height(30)))
            spawner.SpawnRare();

        if (GUILayout.Button("Spawn Legendary Chest", GUILayout.Height(30)))
            spawner.SpawnLegendary();

        GUI.enabled = true;

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to use spawn buttons.", MessageType.Info);
    }
}
