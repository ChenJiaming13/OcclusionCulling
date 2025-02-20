using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(PrefabGenerator))]
    public class PrefabGeneratorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var prefabGenerator = (PrefabGenerator)target;
            if (GUILayout.Button("Spawn"))
            {
                prefabGenerator.SpawnPrefabs();
            }
            if (GUILayout.Button("Clear"))
            {
                prefabGenerator.ClearPrefabs();
            }
        }
    }
}