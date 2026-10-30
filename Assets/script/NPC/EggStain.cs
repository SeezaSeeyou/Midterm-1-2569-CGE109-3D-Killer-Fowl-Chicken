using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class EggStain : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer targetRenderer;
    [SerializeField] private Material stainMaterial;
    [SerializeField, Min(0.01f)] private float stainRadius = 0.3f;
    [SerializeField, Min(1)] private int maxStains = 12;

    private readonly List<(GameObject obj, Mesh mesh)> stains = new List<(GameObject, Mesh)>();
    private readonly List<Vector3> currentVertices = new List<Vector3>();
    private Mesh snapshot;

    public void Hit(Vector3 point, Vector3 direction)
    {
        if (!isActiveAndEnabled || stainMaterial == null || direction.sqrMagnitude < 0.0001f)
            return;
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (targetRenderer == null || targetRenderer.sharedMesh == null || !targetRenderer.sharedMesh.isReadable)
            return;

        if (snapshot == null)
            snapshot = new Mesh();
        targetRenderer.BakeMesh(snapshot);
        Vector3[] vertices = snapshot.vertices;
        Vector3[] normals = snapshot.normals;
        if (vertices.Length == 0 || normals.Length != vertices.Length)
            return;

        int nearest = 0;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = (targetRenderer.transform.TransformPoint(vertices[i]) - point).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = i;
                nearestDistance = distance;
            }
        }

        Vector3 center = targetRenderer.transform.TransformPoint(vertices[nearest]);
        Vector3 forward = direction.normalized;
        Vector3 right = Vector3.Cross(forward, Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up).normalized;
        Vector3 up = Vector3.Cross(right, forward).normalized;
        float radius = Mathf.Max(0.01f, stainRadius);
        Vector2[] uv = new Vector2[vertices.Length];
        float[] depth = new float[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 delta = targetRenderer.transform.TransformPoint(vertices[i]) - center;
            uv[i] = new Vector2(0.5f + Vector3.Dot(delta, right) / (radius * 2f),
                0.5f + Vector3.Dot(delta, up) / (radius * 2f));
            depth[i] = Vector3.Dot(delta, forward);
        }

        int[] sourceTriangles = targetRenderer.sharedMesh.triangles;
        List<int> triangles = new List<int>();
        for (int i = 0; i < sourceTriangles.Length; i += 3)
        {
            int a = sourceTriangles[i], b = sourceTriangles[i + 1], c = sourceTriangles[i + 2];
            if (Mathf.Min(uv[a].x, Mathf.Min(uv[b].x, uv[c].x)) > 1f || Mathf.Max(uv[a].x, Mathf.Max(uv[b].x, uv[c].x)) < 0f
                || Mathf.Min(uv[a].y, Mathf.Min(uv[b].y, uv[c].y)) > 1f || Mathf.Max(uv[a].y, Mathf.Max(uv[b].y, uv[c].y)) < 0f
                || Mathf.Abs((depth[a] + depth[b] + depth[c]) / 3f) > radius)
                continue;
            Vector3 faceNormal = targetRenderer.transform.TransformDirection(normals[a] + normals[b] + normals[c]);
            if (Vector3.Dot(faceNormal, forward) >= 0f)
                continue;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }
        if (triangles.Count == 0)
            return;

        while (stains.Count >= Mathf.Max(1, maxStains))
        {
            Destroy(stains[0].obj);
            Destroy(stains[0].mesh);
            stains.RemoveAt(0);
        }

        Mesh mesh = Instantiate(targetRenderer.sharedMesh);
        mesh.name = "Egg Stain Mesh";
        mesh.MarkDynamic();
        mesh.uv = uv;
        mesh.triangles = triangles.ToArray();
        GameObject obj = new GameObject("Egg Stain");
        obj.transform.SetParent(targetRenderer.transform, false);
        SkinnedMeshRenderer renderer = obj.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.sharedMaterial = stainMaterial;
        renderer.bones = targetRenderer.bones;
        renderer.rootBone = targetRenderer.rootBone;
        renderer.quality = targetRenderer.quality;
        renderer.localBounds = targetRenderer.localBounds;
        renderer.updateWhenOffscreen = targetRenderer.updateWhenOffscreen;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        stains.Add((obj, mesh));
    }

    private void LateUpdate()
    {
        if (stains.Count == 0 || targetRenderer == null || targetRenderer.sharedMesh == null)
            return;
        targetRenderer.sharedMesh.GetVertices(currentVertices);
        foreach (var stain in stains)
            if (stain.mesh != null && stain.mesh.vertexCount == currentVertices.Count)
                stain.mesh.SetVertices(currentVertices);
    }

    private void OnDestroy()
    {
        foreach (var stain in stains)
        {
            if (stain.obj != null) Destroy(stain.obj);
            if (stain.mesh != null) Destroy(stain.mesh);
        }
        if (snapshot != null) Destroy(snapshot);
    }
}
