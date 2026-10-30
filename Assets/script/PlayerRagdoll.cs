using System.Collections.Generic;
using UnityEngine;

public static class PlayerRagdoll
{
    public static Rigidbody Activate(PlayerConfig player, Vector3 direction)
    {
        SkinnedMeshRenderer renderer = player.GetComponentInChildren<SkinnedMeshRenderer>();
        if (renderer == null || renderer.sharedMesh == null || !renderer.sharedMesh.isReadable)
            return null;

        Mesh mesh = renderer.sharedMesh;
        Transform[] bones = renderer.bones;
        Vector3[] vertices = mesh.vertices;
        BoneWeight[] weights = mesh.boneWeights;
        Matrix4x4[] bindPoses = mesh.bindposes;
        if (bones.Length == 0 || weights.Length != vertices.Length || bindPoses.Length != bones.Length)
            return null;

        var bounds = new Bounds[bones.Length];
        var hasVertices = new bool[bones.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            BoneWeight weight = weights[i];
            int index = weight.boneIndex0;
            float strongest = weight.weight0;
            if (weight.weight1 > strongest) { index = weight.boneIndex1; strongest = weight.weight1; }
            if (weight.weight2 > strongest) { index = weight.boneIndex2; strongest = weight.weight2; }
            if (weight.weight3 > strongest) index = weight.boneIndex3;
            if (index < 0 || index >= bones.Length || bones[index] == null)
                continue;
            Vector3 point = bindPoses[index].MultiplyPoint3x4(vertices[i]);
            if (hasVertices[index]) bounds[index].Encapsulate(point);
            else { bounds[index] = new Bounds(point, Vector3.zero); hasVertices[index] = true; }
        }

        Rigidbody originalBody = player.GetComponent<Rigidbody>();
        Vector3 velocity = originalBody != null ? originalBody.linearVelocity : Vector3.zero;
        var bodies = new Dictionary<Transform, Rigidbody>();
        var colliders = new List<Collider>();
        Rigidbody mainBody = null;
        for (int i = 0; i < bones.Length; i++)
        {
            if (!hasVertices[i]) continue;
            Rigidbody body = bones[i].gameObject.AddComponent<Rigidbody>();
            body.mass = bones[i] == renderer.rootBone ? 1f : 0.25f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            body.linearVelocity = velocity;
            BoxCollider collider = bones[i].gameObject.AddComponent<BoxCollider>();
            collider.center = bounds[i].center;
            // FBX bones are scaled by ~130; keep even thin parts at least 2 cm wide in world space.
            Vector3 scale = bones[i].lossyScale;
            collider.size = Vector3.Max(bounds[i].size * 0.9f, new Vector3(
                0.02f / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                0.02f / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                0.02f / Mathf.Max(Mathf.Abs(scale.z), 0.0001f)));
            bodies.Add(bones[i], body);
            colliders.Add(collider);
            if (mainBody == null || bones[i] == renderer.rootBone) mainBody = body;
        }
        if (mainBody == null) return null;

        foreach (var pair in bodies)
        {
            if (pair.Value == mainBody) continue;
            Transform parent = pair.Key.parent;
            while (parent != null && !bodies.ContainsKey(parent)) parent = parent.parent;
            CharacterJoint joint = pair.Key.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent != null ? bodies[parent] : mainBody;
            joint.lowTwistLimit = new SoftJointLimit { limit = -30f };
            joint.highTwistLimit = new SoftJointLimit { limit = 30f };
            joint.swing1Limit = new SoftJointLimit { limit = 45f };
            joint.swing2Limit = new SoftJointLimit { limit = 45f };
            joint.enableProjection = true;
        }
        for (int i = 0; i < colliders.Count; i++)
            for (int j = i + 1; j < colliders.Count; j++)
                Physics.IgnoreCollision(colliders[i], colliders[j]);

        foreach (Collider collider in player.GetComponents<Collider>()) collider.enabled = false;
        if (originalBody != null) originalBody.isKinematic = true;
        renderer.updateWhenOffscreen = true;
        mainBody.AddTorque(player.transform.right * 2f, ForceMode.VelocityChange);
        if (direction.sqrMagnitude > 0.0001f)
            mainBody.AddForce(direction.normalized * 2f, ForceMode.VelocityChange);
        return mainBody;
    }
}
