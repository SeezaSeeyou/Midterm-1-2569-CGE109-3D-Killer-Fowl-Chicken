using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class EggGroundStain : MonoBehaviour, IBulletHitEffect
{
    [SerializeField] private Material stainMaterial;
    [SerializeField, Min(0.01f)] private float stainRadius = 0.3f;
    [SerializeField, Min(0.01f)] private float minSizeMultiplier = 0.75f;
    [SerializeField, Min(0.01f)] private float maxSizeMultiplier = 1.25f;
    [SerializeField] private Vector2 rotationRange = new Vector2(0f, 360f);
    [SerializeField, Min(1)] private int maxStains = 100;
    private static readonly Queue<GameObject> stains = new Queue<GameObject>();

    public void OnBulletHit(BulletRuntime bullet, GameObject hitObject, Vector3 point, Vector3 normal)
    {
        if (stainMaterial == null || hitObject == null || normal.y < 0.5f
            || hitObject.GetComponentInParent<NPCHP>() != null)
            return;

        while (stains.Count > 0 && (stains.Peek() == null || stains.Count >= Mathf.Max(1, maxStains)))
        {
            GameObject oldest = stains.Dequeue();
            if (oldest != null) Destroy(oldest);
        }

        // shortcut: a flat quad can float on uneven surfaces; use projected decals for irregular terrain.
        GameObject stain = GameObject.CreatePrimitive(PrimitiveType.Quad);
        stain.name = "Egg Ground Stain";
        Collider collider = stain.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        stain.transform.position = point + normal * 0.002f;
        float angle = Random.Range(Mathf.Min(rotationRange.x, rotationRange.y), Mathf.Max(rotationRange.x, rotationRange.y));
        float size = Random.Range(Mathf.Min(minSizeMultiplier, maxSizeMultiplier), Mathf.Max(minSizeMultiplier, maxSizeMultiplier));
        stain.transform.rotation = Quaternion.LookRotation(-normal) * Quaternion.Euler(0f, 0f, angle);
        stain.transform.localScale = Vector3.one * (Mathf.Max(0.01f, stainRadius) * 2f * Mathf.Max(0.01f, size));
        stain.transform.SetParent(hitObject.transform, true);
        MeshRenderer renderer = stain.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = stainMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        stains.Enqueue(stain);
    }
}
