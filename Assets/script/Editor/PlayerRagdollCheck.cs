using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;
using System.Reflection;

public static class PlayerRagdollCheck
{
    [MenuItem("Tools/Tests/Player Death Ragdoll")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CheckPose("Idel", Vector3.zero);
        CheckPose("RUN", Vector3.forward);
        CheckPose("Attackcharge", Vector3.forward);
        Debug.Log("PASS: Player death ragdoll in idle, running and charged melee poses; 6 seconds of isolated physics each.");
    }

    private static void CheckPose(string pose, Vector3 impact)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        PlayerCammara cameraRig = Camera.main != null ? Camera.main.GetComponent<PlayerCammara>() : null;
        Transform previousTarget = cameraRig != null ? cameraRig.RagdollTarget : null;
        try
        {
            var root = new GameObject("Player Ragdoll Test");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.transform.position = Vector3.up * 1.5f;
            Rigidbody originalBody = root.AddComponent<Rigidbody>();
            originalBody.constraints = RigidbodyConstraints.FreezeRotation;
            originalBody.linearVelocity = Vector3.right;
            CapsuleCollider originalCollider = root.AddComponent<CapsuleCollider>();
            GameObject model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Model/ChPlayer.prefab"), root.transform);
            Animator animator = model.GetComponentInChildren<Animator>();
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Model/PlayerAnimations/{pose}.anim").SampleAnimation(animator.gameObject, 0.2f);
            var abilities = root.AddComponent<PlayerAbilities>();
            var movement = root.AddComponent<Playermovement>();
            var shoot = root.AddComponent<PlayerShoot>();
            var input = root.AddComponent<PlayerInput>();
            var animation = root.AddComponent<PlayerAnimation>();
            var health = root.AddComponent<PlayerConfig>();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(ground, preview);
            ground.transform.position = Vector3.down * 0.25f;
            ground.transform.localScale = new Vector3(30f, 0.5f, 30f);
            MethodInfo die = typeof(PlayerConfig).GetMethod("Die", BindingFlags.Instance | BindingFlags.NonPublic);
            die.Invoke(health, new object[] { impact });
            Assert.IsTrue(health.IsDead() && health.enabled, "Death must preserve the restart handler.");
            Assert.IsFalse(abilities.enabled || movement.enabled || shoot.enabled || input.enabled || animation.enabled || animator.enabled);
            Assert.IsTrue(originalBody.isKinematic && !originalCollider.enabled);
            Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>();
            CharacterJoint[] joints = model.GetComponentsInChildren<CharacterJoint>();
            Collider[] colliders = model.GetComponentsInChildren<Collider>();
            Assert.AreEqual(11, bodies.Length, "Every weighted chicken bone should have a body.");
            Assert.AreEqual(bodies.Length - 1, joints.Length);
            foreach (Rigidbody body in bodies)
            {
                Assert.IsFalse(body.isKinematic);
                Assert.AreEqual(Vector3.right, body.linearVelocity, "Keep the player's movement velocity.");
            }
            die.Invoke(health, new object[] { impact });
            health.TakeDamage(10f);
            Assert.AreEqual(bodies.Length, model.GetComponentsInChildren<Rigidbody>().Length, "Repeated damage must not build another ragdoll.");
            for (int i = 0; i < colliders.Length; i++)
                for (int j = i + 1; j < colliders.Length; j++)
                    Assert.IsTrue(Physics.GetIgnoreCollision(colliders[i], colliders[j]));
            Physics.SyncTransforms();
            PhysicsScene physics = preview.GetPhysicsScene();
            Assert.IsFalse(physics.Equals(Physics.defaultPhysicsScene), "Only simulate the isolated test scene.");
            for (int step = 0; step < 300; step++) physics.Simulate(0.02f);
            foreach (Rigidbody body in bodies)
                Assert.IsTrue(float.IsFinite(body.position.sqrMagnitude) && body.position.sqrMagnitude < 100f, "Ragdoll must remain stable.");
            foreach (Collider collider in colliders)
                Assert.IsTrue(collider.bounds.min.y > -0.1f, "Ragdoll must stay above the floor.");
            foreach (CharacterJoint joint in joints)
                Assert.IsTrue(Vector3.Distance(joint.transform.TransformPoint(joint.anchor),
                    joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) < 0.15f, "Bones must remain joined.");
            Assert.IsTrue(model.GetComponentInChildren<SkinnedMeshRenderer>().rootBone.position.y < 1f, "Ragdoll must fall under gravity.");
            Debug.Log($"PASS: {pose}, {bodies.Length} bodies, {joints.Length} joints, floor collision and joint stability.");
        }
        finally
        {
            if (cameraRig != null) cameraRig.RagdollTarget = previousTarget;
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }
}
