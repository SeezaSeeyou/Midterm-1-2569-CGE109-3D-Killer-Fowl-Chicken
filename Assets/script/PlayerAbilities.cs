using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class PlayerAbilities : MonoBehaviour
{
    public enum MeleeType { None, Normal, Charged }

    [Header("Ability Charges")]
    [SerializeField, Min(1)] private int maxAbilityCharges = 4;
    [SerializeField, Min(0.01f)] private float abilityChargeRechargeTime = 3f;

    [Header("Double Jump")]
    [SerializeField, Min(0f)] private float jumpForce = 6.5f;
    [SerializeField, Min(0f)] private float doubleJumpForce = 8.5f;
    [SerializeField, Min(0)] private int extraJumps = 1;
    [SerializeField, Min(0f)] private float coyoteTime = 0.2f;

    [Header("Dash")]
    [SerializeField, Min(0f)] private float dashSpeed = 14f;
    [SerializeField, Min(0.01f)] private float dashDuration = 0.18f;
    [SerializeField, Min(0f)] private float dashCooldown = 0.6f;

    [Header("Melee")]
    [SerializeField, Min(0.1f)] private float meleeChargeDuration = 2f;
    [SerializeField, Min(0f)] private float normalMeleeDamage = 5f;
    [SerializeField, Min(0f)] private float chargedMeleeDamage = 10f;
    [SerializeField, Min(0.05f)] private float normalMeleeCooldown = 0.3f;
    [SerializeField, Min(0.05f)] private float chargedMeleeCooldown = 1f;
    [SerializeField] private Vector3 meleeHitboxSize = new Vector3(1.5f, 1.8f, 1.5f);
    [SerializeField] private Vector3 meleeHitboxOffset = new Vector3(0f, 0.2f, 1f);
    [SerializeField] private bool debugMeleeHitbox;
    [SerializeField, Min(0f)] private float chargedLungeSpeed = 10f;

    [Header("Debug")]
    [SerializeField] private bool debugAbilities;

    [Header("Events")]
    [SerializeField] private UnityEvent<MeleeType> onMeleePerformed = new UnityEvent<MeleeType>();

    private Rigidbody rb;
    private Playermovement movement;
    private InputAction meleeAction;
    private int jumpsUsed;
    private float lastGroundedTime = -10f;
    private float dashEndTime;
    private float nextDashTime;
    private float nextMeleeTime;
    private float nextAbilityChargeTime;
    private float meleeChargeStartedAt;
    private int abilityCharges;
    private bool chargingMelee;
    private bool pendingChargedMelee;
    private Vector3 chargedMeleeDirection;
    private Vector3 previousChargedMeleePosition;
    private readonly HashSet<IDamageable> chargedMeleeTargets = new HashSet<IDamageable>();
    private Vector3 dashDirection;

    public bool IsDashing => Time.time < dashEndTime;
    public bool IsDash => isActiveAndEnabled && IsDashing;
    // Remains true after the extra jump until landing resets jumpsUsed.
    public bool IsDouble => isActiveAndEnabled && jumpsUsed > 1;
    public bool IsChargingMelee => isActiveAndEnabled && chargingMelee;
    public MeleeType LastMeleeType { get; private set; }
    public event System.Action<MeleeType> MeleePerformed;
    public int MeleePerformedCount { get; private set; }
    public Vector3 DashVelocity => dashDirection * dashSpeed;
    public int CurrentAbilityCharges => abilityCharges;
    public float AbilityChargeProgress => abilityCharges < maxAbilityCharges
        ? Mathf.Clamp01(1f - (nextAbilityChargeTime - Time.time) / abilityChargeRechargeTime)
        : 0f;

    private void Awake()
    {
        OnValidate();
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<Playermovement>();
        abilityCharges = maxAbilityCharges;
    }

    private void OnEnable()
    {
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
            return;

        meleeAction = playerInput.actions.FindAction("Player/Melee", false);
        if (meleeAction == null)
        {
            Debug.LogWarning("PlayerAbilities: Player/Melee action was not found.", this);
            return;
        }

        meleeAction.started += OnMeleeStarted;
        meleeAction.canceled += OnMeleeCanceled;
    }

    private void OnDisable()
    {
        if (meleeAction != null)
        {
            meleeAction.started -= OnMeleeStarted;
            meleeAction.canceled -= OnMeleeCanceled;
            meleeAction = null;
        }

        chargingMelee = false;
        pendingChargedMelee = false;
    }

    private void Update()
    {
        if (abilityCharges < maxAbilityCharges && Time.time >= nextAbilityChargeTime)
        {
            abilityCharges++;
            nextAbilityChargeTime = abilityCharges < maxAbilityCharges
                ? nextAbilityChargeTime + abilityChargeRechargeTime
                : 0f;
        }

        if (chargingMelee && Time.time - meleeChargeStartedAt >= meleeChargeDuration)
        {
            chargingMelee = false;
            PerformMelee(true);
        }

        CheckChargedMelee();

        if (movement.IsGroundedForAbilities() && rb.linearVelocity.y <= 0.05f)
        {
            jumpsUsed = 0;
            lastGroundedTime = Time.time;
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;

        bool grounded = movement.IsGroundedForAbilities();
        if (grounded && rb.linearVelocity.y <= 0.05f)
        {
            jumpsUsed = 0;
            lastGroundedTime = Time.time;
        }

        bool canUseGroundJump = jumpsUsed == 0
            && (grounded || Time.time - lastGroundedTime <= coyoteTime);
        bool canUseExtraJump = jumpsUsed > 0 && jumpsUsed <= extraJumps && abilityCharges > 0;

        if (!canUseGroundJump && !canUseExtraJump)
            return;

        bool isDoubleJump = jumpsUsed > 0;
        if (isDoubleJump && !TrySpendAbilityCharge())
            return;

        jumpsUsed++;
        movement.ApplyJump(isDoubleJump ? doubleJumpForce : jumpForce);
        if (debugAbilities)
            Debug.Log(isDoubleJump ? "Player: Double Jump" : "Player: Jump", this);
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed || Time.time < nextDashTime || !TrySpendAbilityCharge())
            return;

        dashDirection = movement.GetDashDirection();
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;
        if (debugAbilities)
            Debug.Log("Player: Dash", this);
    }

    private bool TrySpendAbilityCharge()
    {
        if (abilityCharges <= 0)
            return false;

        abilityCharges--;
        if (nextAbilityChargeTime <= 0f)
            nextAbilityChargeTime = Time.time + abilityChargeRechargeTime;
        return true;
    }

    private void OnMeleeStarted(InputAction.CallbackContext context)
    {
        if (pendingChargedMelee || Time.time < nextMeleeTime)
            return;

        chargingMelee = true;
        meleeChargeStartedAt = Time.time;
    }

    private void OnMeleeCanceled(InputAction.CallbackContext context)
    {
        if (!chargingMelee || !isActiveAndEnabled)
            return;

        bool charged = Time.time - meleeChargeStartedAt >= meleeChargeDuration;
        chargingMelee = false;
        PerformMelee(charged);
    }

    private void PerformMelee(bool charged)
    {
        float damage = charged ? chargedMeleeDamage : normalMeleeDamage;
        nextMeleeTime = Time.time + (charged ? chargedMeleeCooldown : normalMeleeCooldown);

        Vector3 direction = movement != null ? movement.GetFacingDirection() : transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        if (charged && movement != null && movement.isActiveAndEnabled && chargedLungeSpeed > 0f)
        {
            chargedMeleeDirection = direction;
            previousChargedMeleePosition = rb.position;
            chargedMeleeTargets.Clear();
            pendingChargedMelee = true;
            movement.ApplyMeleeLunge(direction, chargedLungeSpeed);
            CheckChargedMelee();
        }
        else
            CreateMeleeHitbox(direction, damage, transform.position, charged);

        LastMeleeType = charged ? MeleeType.Charged : MeleeType.Normal;
        if (debugAbilities)
            Debug.Log($"Player: Melee {LastMeleeType}", this);
        MeleePerformedCount++;
        MeleePerformed?.Invoke(LastMeleeType);
        onMeleePerformed.Invoke(LastMeleeType);
    }

    private void FixedUpdate()
    {
        CheckChargedMelee();
    }

    private void CheckChargedMelee()
    {
        if (!pendingChargedMelee)
            return;

        Vector3 position = rb.position;
        Quaternion rotation = Quaternion.LookRotation(chargedMeleeDirection);
        Vector3 offset = rotation * meleeHitboxOffset;
        HitboxService.CreateSweptHitbox(transform, meleeHitboxSize,
            previousChargedMeleePosition + offset, position + offset, rotation,
            hit => ApplyDamage(hit, chargedMeleeDamage, true, chargedMeleeDirection,
                position, chargedMeleeTargets), debugMeleeHitbox);
        previousChargedMeleePosition = position;
        if (movement == null || !movement.IsMeleeLunging)
            pendingChargedMelee = false;
    }

    private void CreateMeleeHitbox(Vector3 direction, float damage, Vector3 position, bool charged)
    {
        Quaternion rotation = Quaternion.LookRotation(direction);
        Vector3 center = position + rotation * meleeHitboxOffset;
        HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        HitboxService.CreateHitbox(transform, meleeHitboxSize, center, rotation,
            hit =>
            {
                ApplyDamage(hit, damage, charged, direction, position, hitTargets);
            }, debugMeleeHitbox);
    }

    private void OnValidate()
    {
        maxAbilityCharges = Mathf.Max(1, maxAbilityCharges);
        abilityChargeRechargeTime = Mathf.Max(0.01f, abilityChargeRechargeTime);
    }

    private static void ApplyDamage(Collider hit, float damage, bool charged, Vector3 direction,
        Vector3 position, HashSet<IDamageable> hitTargets)
    {
        foreach (MonoBehaviour component in hit.GetComponentsInParent<MonoBehaviour>())
        {
            if (component is IDamageable damageable)
            {
                if (!hitTargets.Add(damageable))
                    return;
                if (damageable is NPCHP npc)
                {
                    Vector3 point = hit.ClosestPoint(position);
                    if (damage > 0f && !float.IsNaN(damage) && npc.CurrentHP > 0f)
                    {
                        SoundManager sounds = SoundManager.Instance;
                        if (sounds != null)
                            sounds.PlaySFX(charged ? "M1charge" : "M1", point);
                    }
                    npc.TakeMelee(damage, point, direction, charged);
                }
                else if (charged && damageable is IImpactDamageable impact)
                    impact.TakeDamage(damage, hit.ClosestPoint(position), direction);
                else
                    damageable.TakeDamage(damage);
                return;
            }
        }
    }
}



