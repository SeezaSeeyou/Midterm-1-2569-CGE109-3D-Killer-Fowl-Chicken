using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NPCHP : MonoBehaviour, IImpactDamageable
{
    [SerializeField, Min(1f)] private float maxHP = 100f;
    [Header("Damage Numbers")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private Color damageNumberColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField, Min(0.05f)] private float damageNumberDuration = 0.9f;
    [SerializeField, Min(1)] private int damageNumberFontSize = 40;
    [Header("Hit Feedback")]
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.15f;
    [SerializeField] private Transform hitVisual;
    [SerializeField, Min(0f)] private float hitTwistAngle = 12f;
    [SerializeField, Min(0f)] private float hitTwistDuration = 0.2f;
    [Header("Ragdoll")]
    [SerializeField] private bool ragdollOnDeath;
    [SerializeField, Min(0f)] private float ragdollLifetime = 8f;
    [SerializeField, Min(0.01f)] private float ragdollBodyMass = 1f;
    [SerializeField, Min(0f)] private float ragdollImpactForce = 3f;

    public float CurrentHP { get; private set; }
    public float MaxHP => maxHP;
    public bool IsRagdoll => isDead && ragdollOnDeath;

    private bool isDead;
    private bool twisting;
    private float twistStartTime;
    private Quaternion visualRestRotation;
    private Coroutine hitFlash;
    private readonly List<(Renderer renderer, int index, MaterialPropertyBlock original)> flashMaterials =
        new List<(Renderer, int, MaterialPropertyBlock)>();

    private void Awake()
    {
        maxHP = Mathf.Max(1f, maxHP);
        CurrentHP = maxHP;
        if (hitVisual == null)
        {
            Animator animator = GetComponentInChildren<Animator>();
            Renderer visual = animator == null ? GetComponentInChildren<Renderer>() : null;
            hitVisual = animator != null ? animator.transform : visual != null ? visual.transform : null;
        }
        if (hitVisual == transform || (hitVisual != null && !hitVisual.IsChildOf(transform)))
        {
            Debug.LogWarning("NPCHP: Hit Visual must be a child of the NPC.", this);
            hitVisual = null;
        }
    }

    public void TakeDamage(float amount)
    {
        ApplyDamage(amount, Vector3.zero, Vector3.zero, false);
    }

    private void ApplyDamage(float amount, Vector3 point, Vector3 direction, bool hasImpact, bool meleeHit = false)
    {
        if (isDead || amount <= 0f || float.IsNaN(amount))
            return;

        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        if (TryGetComponent(out Chase chase))
            chase.PlayDamageSound(CurrentHP <= 0f, meleeHit);
        if (showDamageNumbers)
            DamageNumber.Show(transform, amount, damageNumberColor, damageNumberDuration, damageNumberFontSize);
        if (CurrentHP > 0f)
        {
            if (hitVisual != null && hitTwistDuration > 0f && hitTwistAngle > 0f)
            {
                if (!twisting)
                    visualRestRotation = hitVisual.localRotation;
                twisting = true;
                twistStartTime = Time.time;
            }
            if (hitFlashDuration > 0f)
            {
                if (hitFlash != null)
                    StopCoroutine(hitFlash);
                else
                    ApplyHitFlash();
                hitFlash = StartCoroutine(EndHitFlash());
            }
            return;
        }

        isDead = true;
        if (ragdollOnDeath && BigKidRagdoll.Activate(this, point, direction, hasImpact, ragdollBodyMass, ragdollImpactForce))
            Destroy(gameObject, ragdollLifetime);
        else
            Destroy(gameObject);
    }

    public void TakeDamage(float amount, Vector3 point, Vector3 direction)
    {
        ApplyImpactDamage(amount, point, direction, false);
    }

    private void ApplyImpactDamage(float amount, Vector3 point, Vector3 direction, bool meleeHit)
    {
        if (amount <= 0f || float.IsNaN(amount))
            return;

        if (isDead)
        {
            if (IsRagdoll && !enabled)
            {
                BigKidRagdoll.ApplyImpact(this, point, direction, ragdollImpactForce);
                ApplyImpactEffects(point, direction);
            }
            return;
        }

        ApplyDamage(amount, point, direction, true, meleeHit);
        if (!isDead || (IsRagdoll && !enabled))
            ApplyImpactEffects(point, direction);
    }

    public void TakeChargedMelee(float amount, Vector3 point, Vector3 direction)
    {
        TakeMelee(amount, point, direction, true);
    }

    public void TakeMelee(float amount, Vector3 point, Vector3 direction, bool charged)
    {
        if (!charged)
        {
            ApplyDamage(amount, point, direction, false, true);
            return;
        }
        ApplyImpactDamage(amount, point, direction, true);
        if (!isDead && amount > 0f && !float.IsNaN(amount) && TryGetComponent(out Chase chase))
            chase.ApplyChargedKnockback(direction);
    }

    private void ApplyImpactEffects(Vector3 point, Vector3 direction)
    {
        if (TryGetComponent(out NPCImpactDeform impact))
            impact.Hit(point, direction);
        if (TryGetComponent(out EggStain stain))
            stain.Hit(point, direction);
    }

    private void LateUpdate()
    {
        if (!twisting || hitVisual == null)
            return;

        float progress = Mathf.Clamp01((Time.time - twistStartTime) / Mathf.Max(0.001f, hitTwistDuration));
        float angle = Mathf.Sin(progress * Mathf.PI * 3f) * (1f - progress) * hitTwistAngle;
        hitVisual.localRotation = visualRestRotation * Quaternion.Euler(0f, 0f, angle);
        if (progress >= 1f)
            twisting = false;
    }

    private void ApplyHitFlash()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null)
                    continue;
                int colorProperty = material.HasProperty("_BaseColor") ? Shader.PropertyToID("_BaseColor") :
                    material.HasProperty("_Color") ? Shader.PropertyToID("_Color") : -1;
                if (colorProperty == -1)
                    continue;

                MaterialPropertyBlock original = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(original, i);
                flashMaterials.Add((renderer, i, original));
                MaterialPropertyBlock flash = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(flash, i);
                Color color = hitColor;
                color.a = material.GetColor(colorProperty).a;
                flash.SetColor(colorProperty, color);
                renderer.SetPropertyBlock(flash, i);
            }
        }
    }

    private IEnumerator EndHitFlash()
    {
        yield return new WaitForSeconds(hitFlashDuration);
        RestoreHitFlash();
        hitFlash = null;
    }

    private void RestoreHitFlash()
    {
        foreach (var item in flashMaterials)
            if (item.renderer != null)
                item.renderer.SetPropertyBlock(item.original, item.index);
        flashMaterials.Clear();
    }

    private void OnDisable()
    {
        if (twisting && hitVisual != null)
            hitVisual.localRotation = visualRestRotation;
        twisting = false;
        if (hitFlash != null)
            StopCoroutine(hitFlash);
        hitFlash = null;
        RestoreHitFlash();
    }
}
