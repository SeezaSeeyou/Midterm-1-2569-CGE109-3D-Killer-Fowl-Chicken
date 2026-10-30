using System;
using System.Collections.Generic;
using UnityEngine;

public static class HitboxService
{
    public static void CreateHitbox(
        Transform attacker,
        Vector3 size,
        Vector3 offset,
        Action<Collider> onHit,
        bool debug = false)
    {
        if (attacker == null)
            return;

        CreateHitbox(attacker, size, attacker.TransformPoint(offset), attacker.rotation, onHit, debug);
    }

    public static void CreateHitbox(
        Transform attacker,
        Vector3 size,
        Vector3 center,
        Quaternion rotation,
        Action<Collider> onHit,
        bool debug = false)
    {
        if (attacker == null || onHit == null || size.x <= 0f || size.y <= 0f || size.z <= 0f)
            return;

        Vector3 halfExtents = size * 0.5f;

        if (debug)
            DrawDebugBox(center, halfExtents, rotation);

        // shortcut: OverlapBox allocates per call, switch to NonAlloc if called continuously.
        Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation);
        HashSet<Transform> hitTargets = new HashSet<Transform>();

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform == attacker || hit.transform.IsChildOf(attacker))
                continue;

            Transform target = hit.attachedRigidbody != null
                ? hit.attachedRigidbody.transform
                : hit.transform.root;

            if (hitTargets.Add(target))
                onHit(hit);
        }
    }

    public static void CreateSweptHitbox(Transform attacker, Vector3 size,
        Vector3 start, Vector3 end, Quaternion rotation, Action<Collider> onHit, bool debug = false)
    {
        CreateHitbox(attacker, size, start, rotation, onHit, debug);
        if (attacker == null || onHit == null || size.x <= 0f || size.y <= 0f || size.z <= 0f)
            return;

        Vector3 travel = end - start;
        if (travel.sqrMagnitude > 0.000001f)
        {
            foreach (RaycastHit hit in Physics.BoxCastAll(start, size * 0.5f,
                travel.normalized, rotation, travel.magnitude))
            {
                if (hit.collider != null && hit.transform != attacker && !hit.transform.IsChildOf(attacker))
                    onHit(hit.collider);
            }
            CreateHitbox(attacker, size, end, rotation, onHit, debug);
        }
    }

    private static void DrawDebugBox(Vector3 center, Vector3 halfExtents, Quaternion rotation)
    {
        Vector3 right = rotation * Vector3.right * halfExtents.x;
        Vector3 up = rotation * Vector3.up * halfExtents.y;
        Vector3 forward = rotation * Vector3.forward * halfExtents.z;

        Vector3 p0 = center - right - up - forward;
        Vector3 p1 = center + right - up - forward;
        Vector3 p2 = center + right + up - forward;
        Vector3 p3 = center - right + up - forward;
        Vector3 p4 = center - right - up + forward;
        Vector3 p5 = center + right - up + forward;
        Vector3 p6 = center + right + up + forward;
        Vector3 p7 = center - right + up + forward;

        Debug.DrawLine(p0, p1, Color.yellow, 0.1f);
        Debug.DrawLine(p1, p2, Color.yellow, 0.1f);
        Debug.DrawLine(p2, p3, Color.yellow, 0.1f);
        Debug.DrawLine(p3, p0, Color.yellow, 0.1f);
        Debug.DrawLine(p4, p5, Color.yellow, 0.1f);
        Debug.DrawLine(p5, p6, Color.yellow, 0.1f);
        Debug.DrawLine(p6, p7, Color.yellow, 0.1f);
        Debug.DrawLine(p7, p4, Color.yellow, 0.1f);
        Debug.DrawLine(p0, p4, Color.yellow, 0.1f);
        Debug.DrawLine(p1, p5, Color.yellow, 0.1f);
        Debug.DrawLine(p2, p6, Color.yellow, 0.1f);
        Debug.DrawLine(p3, p7, Color.yellow, 0.1f);
    }
}
