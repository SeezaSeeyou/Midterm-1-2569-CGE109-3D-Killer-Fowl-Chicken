using System.Collections.Generic;
using UnityEngine;

/// <summary>Fades scene objects between this camera and the player.</summary>
[RequireComponent(typeof(Camera))]
public class CameraOcclusionFade : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField, Min(0f)] private float targetHeight = 1.2f;
    [Tooltip("Fade the player model when the camera gets this close.")]
    [SerializeField, Min(0f)] private float playerFadeDistance = 2.5f;

    [Header("Fade")]
    [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.25f;
    [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;

    private readonly RaycastHit[] hits = new RaycastHit[32];
    private readonly Dictionary<Renderer, RendererFade> tracked = new Dictionary<Renderer, RendererFade>();
    private readonly HashSet<Renderer> visibleThisFrame = new HashSet<Renderer>();

    private sealed class RendererFade
    {
        public Material[] originals;
        public Material[] instances;
        public float alpha = 1f;
        public bool transparentSetup;
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        visibleThisFrame.Clear();
        Vector3 target = player.position + Vector3.up * targetHeight;
        Vector3 toTarget = target - transform.position;
        if (playerFadeDistance > 0f && toTarget.magnitude <= playerFadeDistance)
        {
            Renderer[] playerRenderers = player.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in playerRenderers)
            {
                if (renderer != null && renderer.enabled)
                {
                    visibleThisFrame.Add(renderer);
                    EnsureTracked(renderer);
                }
            }
        }
        float distance = toTarget.magnitude;
        if (distance > 0.001f)
        {
            int hitCount = Physics.RaycastNonAlloc(
                transform.position, toTarget / distance, hits, distance,
                obstructionLayers, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == null || hitTransform == player || hitTransform.IsChildOf(player))
                    continue;

                Renderer[] renderers = hitTransform.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null && renderer.enabled)
                    {
                        visibleThisFrame.Add(renderer);
                        EnsureTracked(renderer);
                    }
                }
            }
        }

        List<Renderer> finished = null;
        foreach (KeyValuePair<Renderer, RendererFade> entry in tracked)
        {
            Renderer renderer = entry.Key;
            RendererFade state = entry.Value;
            float targetAlpha = visibleThisFrame.Contains(renderer) ? fadedAlpha : 1f;
            state.alpha = Mathf.MoveTowards(state.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            ApplyAlpha(state, state.alpha);

            if (state.alpha >= 0.999f && targetAlpha >= 1f)
            {
                renderer.sharedMaterials = state.originals;
                if (finished == null) finished = new List<Renderer>();
                finished.Add(renderer);
            }
        }

        if (finished != null)
            foreach (Renderer renderer in finished) tracked.Remove(renderer);
    }

    private void EnsureTracked(Renderer renderer)
    {
        if (tracked.ContainsKey(renderer)) return;

        Material[] originals = renderer.sharedMaterials;
        Material[] instances = new Material[originals.Length];
        bool setup = false;
        for (int i = 0; i < originals.Length; i++)
        {
            if (originals[i] == null) continue;
            instances[i] = new Material(originals[i]);
            setup |= ConfigureTransparent(instances[i]);
        }

        renderer.materials = instances;
        tracked.Add(renderer, new RendererFade
        {
            originals = originals,
            instances = instances,
            alpha = 1f,
            transparentSetup = setup
        });
    }

    private static bool ConfigureTransparent(Material material)
    {
        if (material.HasProperty("_Surface")) // URP Lit/Unlit
        {
            material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return true;
        }

        if (material.HasProperty("_Mode")) // Built-in Standard shader
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return true;
        }

        return material.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void ApplyAlpha(RendererFade state, float alpha)
    {
        foreach (Material material in state.instances)
        {
            if (material == null) continue;
            string colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" :
                                  material.HasProperty("_Color") ? "_Color" : null;
            if (colorProperty == null) continue;
            Color color = material.GetColor(colorProperty);
            color.a = alpha;
            material.SetColor(colorProperty, color);
        }
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<Renderer, RendererFade> entry in tracked)
        {
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value.originals;
            foreach (Material material in entry.Value.instances)
                if (material != null) Destroy(material);
        }
        tracked.Clear();
    }
}




