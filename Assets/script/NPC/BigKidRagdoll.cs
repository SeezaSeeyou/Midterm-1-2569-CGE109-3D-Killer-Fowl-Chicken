using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// BIGKID's imported rig has one body bone and three child bones, rather than a humanoid skeleton.
public static class BigKidRagdoll
{
    public static bool Activate(NPCHP health, Vector3 point, Vector3 direction, bool hasImpact, float bodyMass, float impactForce)
    {
        Animator animator = health.GetComponentInChildren<Animator>();
        SkinnedMeshRenderer mesh = health.GetComponentInChildren<SkinnedMeshRenderer>();
        if (animator == null || mesh == null)
            return false;

        Transform bodyBone = null;
        foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
            if (bone.name == "Bone") { bodyBone = bone; break; }
        if (bodyBone == null || bodyBone.childCount == 0)
            return false;

        Collider originalCollider = health.GetComponent<Collider>();
        Bounds bounds = originalCollider != null && originalCollider.enabled ? originalCollider.bounds : mesh.bounds;
        health.enabled = false;
        if (health.TryGetComponent(out Chase chase)) chase.enabled = false;
        if (health.TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
        animator.enabled = false;
        foreach (Collider collider in health.GetComponents<Collider>()) collider.enabled = false;

        Rigidbody body = bodyBone.gameObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(0.01f, bodyMass);
        BoxCollider bodyCollider = bodyBone.gameObject.AddComponent<BoxCollider>();
        Bounds localBounds = new Bounds(bodyBone.InverseTransformPoint(bounds.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    localBounds.Encapsulate(bodyBone.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z))));
        bodyCollider.center = localBounds.center;
        bodyCollider.size = localBounds.size * 0.65f;

        var colliders = new List<Collider> { bodyCollider };
        foreach (Transform bone in bodyBone)
        {
            if (!bone.name.StartsWith("Bone.") || bone.childCount == 0)
                continue;
            Vector3 end = bone.InverseTransformPoint(bone.GetChild(0).position);
            float length = end.magnitude;
            if (length < 0.001f)
                continue;

            Rigidbody limb = bone.gameObject.AddComponent<Rigidbody>();
            limb.mass = Mathf.Max(0.01f, body.mass * 0.25f);
            CapsuleCollider collider = bone.gameObject.AddComponent<CapsuleCollider>();
            Vector3 axis = new Vector3(Mathf.Abs(end.x), Mathf.Abs(end.y), Mathf.Abs(end.z));
            collider.direction = axis.x > axis.y && axis.x > axis.z ? 0 : axis.y > axis.z ? 1 : 2;
            collider.center = end * 0.5f;
            collider.radius = length * 0.16f;
            collider.height = length;
            colliders.Add(collider);
            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = body;
            joint.enableProjection = true;
        }

        for (int i = 0; i < colliders.Count; i++)
            for (int j = i + 1; j < colliders.Count; j++)
                Physics.IgnoreCollision(colliders[i], colliders[j]);

        if (hasImpact)
            ApplyImpact(health, point, direction, impactForce);
        return true;
    }

    public static void ApplyImpact(NPCHP health, Vector3 point, Vector3 direction, float impactForce)
    {
        if (direction.sqrMagnitude <= 0.0001f || impactForce <= 0f)
            return;

        Rigidbody nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Rigidbody body in health.GetComponentsInChildren<Rigidbody>())
        {
            if (body.isKinematic || !body.TryGetComponent(out Collider collider) || !collider.enabled)
                continue;
            float distance = (collider.ClosestPoint(point) - point).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = body;
                nearestDistance = distance;
            }
        }
        if (nearest != null)
        {
            Collider collider = nearest.GetComponent<Collider>();
            nearest.AddForceAtPosition(direction.normalized * impactForce * nearest.mass,
                collider.ClosestPoint(point), ForceMode.Impulse);
        }
    }
}
