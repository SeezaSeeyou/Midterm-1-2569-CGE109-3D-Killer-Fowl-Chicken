using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCImpactDeform : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    [SerializeField, Min(0.01f)] private float impactRadius = 0.3f;
    [SerializeField, Min(0f)] private float impactDepth = 0.12f;
    [SerializeField, Min(0.01f)] private float impactDuration = 0.4f;

    private Mesh originalMesh;
    private Mesh deformMesh;
    private Mesh bakedMesh;
    private Vector3[] originalVertices;
    private Vector3[] workingVertices;
    private BoneWeight[] weights;
    private Matrix4x4[] bindPoses;
    private Bounds originalBounds;
    private readonly List<(float start, float duration, Vector3[] offsets)> impacts =
        new List<(float, float, Vector3[])>();

    public void Hit(Vector3 worldPoint, Vector3 worldDirection)
    {
        if (!isActiveAndEnabled || impactDepth <= 0f || worldDirection.sqrMagnitude < 0.0001f)
            return;
        if (deformMesh == null && !InitializeMesh())
            return;

        targetRenderer.BakeMesh(bakedMesh);
        Vector3[] posedVertices = bakedMesh.vertices;
        if (posedVertices.Length != originalVertices.Length || posedVertices.Length == 0)
            return;

        Matrix4x4 toWorld = targetRenderer.transform.localToWorldMatrix;
        int nearest = 0;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < posedVertices.Length; i++)
        {
            float distance = (toWorld.MultiplyPoint3x4(posedVertices[i]) - worldPoint).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        Vector3 center = toWorld.MultiplyPoint3x4(posedVertices[nearest]);
        Vector3 push = worldDirection.normalized * impactDepth;
        Transform[] bones = targetRenderer.bones;
        Matrix4x4[] worldToBind = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            worldToBind[i] = bones[i] != null && i < bindPoses.Length
                ? (bones[i].localToWorldMatrix * bindPoses[i]).inverse
                : targetRenderer.transform.worldToLocalMatrix;

        float radius = Mathf.Max(0.01f, impactRadius);
        Vector3[] offsets = new Vector3[originalVertices.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            float distance = Vector3.Distance(toWorld.MultiplyPoint3x4(posedVertices[i]), center);
            float falloff = Mathf.Clamp01(1f - distance / radius);
            if (falloff <= 0f)
                continue;
            falloff = falloff * falloff * (3f - 2f * falloff);
            if (weights.Length == offsets.Length && bones.Length > 0)
            {
                BoneWeight w = weights[i];
                offsets[i] = (worldToBind[w.boneIndex0].MultiplyVector(push) * w.weight0
                    + worldToBind[w.boneIndex1].MultiplyVector(push) * w.weight1
                    + worldToBind[w.boneIndex2].MultiplyVector(push) * w.weight2
                    + worldToBind[w.boneIndex3].MultiplyVector(push) * w.weight3) * falloff;
            }
            else
                offsets[i] = targetRenderer.transform.InverseTransformVector(push) * falloff;
        }
        impacts.Add((Time.time, Mathf.Max(0.01f, impactDuration), offsets));
    }

    private bool InitializeMesh()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (targetRenderer == null || targetRenderer.sharedMesh == null)
            return false;
        originalMesh = targetRenderer.sharedMesh;
        if (!originalMesh.isReadable)
        {
            Debug.LogWarning("NPCImpactDeform: enable Read/Write on the model's import settings.", this);
            return false;
        }
        originalVertices = originalMesh.vertices;
        workingVertices = new Vector3[originalVertices.Length];
        weights = originalMesh.boneWeights;
        bindPoses = originalMesh.bindposes;
        originalBounds = targetRenderer.localBounds;
        deformMesh = Instantiate(originalMesh);
        deformMesh.MarkDynamic();
        bakedMesh = new Mesh();
        targetRenderer.sharedMesh = deformMesh;
        Bounds bounds = originalBounds;
        bounds.Expand(impactDepth * 4f);
        targetRenderer.localBounds = bounds;
        return true;
    }

    private void LateUpdate()
    {
        if (deformMesh == null || impacts.Count == 0)
            return;
        System.Array.Copy(originalVertices, workingVertices, originalVertices.Length);
        for (int i = impacts.Count - 1; i >= 0; i--)
        {
            var impact = impacts[i];
            float progress = (Time.time - impact.start) / impact.duration;
            if (progress >= 1f)
            {
                impacts.RemoveAt(i);
                continue;
            }
            float amount = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI * 2f) * Mathf.Exp(-3f * progress);
            for (int v = 0; v < workingVertices.Length; v++)
                workingVertices[v] += impact.offsets[v] * amount;
        }
        deformMesh.vertices = workingVertices;
    }

    private void OnDisable()
    {
        impacts.Clear();
        if (deformMesh != null)
            deformMesh.vertices = originalVertices;
    }

    private void OnDestroy()
    {
        if (deformMesh != null && targetRenderer != null && targetRenderer.sharedMesh == deformMesh)
        {
            targetRenderer.sharedMesh = originalMesh;
            targetRenderer.localBounds = originalBounds;
        }
        if (deformMesh != null)
            Destroy(deformMesh);
        if (bakedMesh != null)
            Destroy(bakedMesh);
    }
}
