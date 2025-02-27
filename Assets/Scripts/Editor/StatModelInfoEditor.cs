using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(StatModelInfo))]
    public class StatModelInfoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var statModelInfo = (StatModelInfo)target;
            if (GUILayout.Button("Stat Vert & Tri Info"))
                statModelInfo.StatVertAndTriInfo();
        }
    }
}