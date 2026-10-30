using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerAbilities))]
public class PlayerAbilitiesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PlayerAbilities abilities = (PlayerAbilities)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug (Play Mode)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("IsDash", abilities.IsDash);
            EditorGUILayout.Toggle("IsDouble", abilities.IsDouble);
            EditorGUILayout.Toggle("IsChargingMelee", abilities.IsChargingMelee);
            EditorGUILayout.EnumPopup("LastMeleeType", abilities.LastMeleeType);
            EditorGUILayout.IntField("MeleePerformed Count", abilities.MeleePerformedCount);
            EditorGUILayout.IntField("Current Ability Charges", abilities.CurrentAbilityCharges);
            EditorGUILayout.Slider("Charge Progress", abilities.AbilityChargeProgress, 0f, 1f);
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
