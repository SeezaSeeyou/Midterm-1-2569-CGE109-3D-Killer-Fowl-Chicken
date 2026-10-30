using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

public static class PlayerAnimationCheck
{
    [MenuItem("Tools/Tests/Player Animation Recovery")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Animation Check");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<Rigidbody>();
            var abilities = root.AddComponent<PlayerAbilities>();
            var movement = root.AddComponent<Playermovement>();
            var shoot = root.AddComponent<PlayerShoot>();
            Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/ChPlayer.prefab"), root.transform);
            var animation = root.AddComponent<PlayerAnimation>();
            Call(abilities, "Awake");
            Call(movement, "Awake");
            Call(shoot, "Awake");
            Call(animation, "Awake");
            Set(movement, "groundContact", true);
            Set(abilities, "dashEndTime", -1f);
            foreach (string state in new[] { "Attack1", "Attackcharge", "dodge_F", "dodge_B", "dodge_L", "dodge_R" })
            {
                Set(animation, "state", state);
                Set(animation, "grounded", true);
                Set(animation, "holdUntil", state.StartsWith("dodge_") ? Time.time + 10f : -1f);
                Set(movement, "meleeLunging", true);
                Call(animation, "LateUpdate");
                Assert.AreEqual("Idel", (string)Field(animation, "state").GetValue(animation), state + " must return to idle.");
            }
            Debug.Log("PASS: melee and all four dash animations return to idle, including residual melee momentum.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
}
