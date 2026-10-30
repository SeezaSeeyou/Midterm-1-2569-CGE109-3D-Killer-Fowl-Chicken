using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerShoot))]
public class PlayerShootEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        PlayerShoot shoot = (PlayerShoot)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime Debug (Play Mode)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Toggle("IsAiming", shoot.IsAiming);
            EditorGUILayout.Toggle("IsReloading", shoot.IsReloading);
            EditorGUILayout.IntField("Current Ammo", shoot.CurrentAmmo);
            EditorGUILayout.Slider("Reload Progress", shoot.ReloadProgress, 0f, 1f);
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
