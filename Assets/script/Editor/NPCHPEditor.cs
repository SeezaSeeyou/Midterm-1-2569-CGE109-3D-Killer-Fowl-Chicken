using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NPCHP))]
public class NPCHPEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        NPCHP health = (NPCHP)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug (Play Mode)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Current HP", health.CurrentHP);
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
