using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Chase : MonoBehaviour
{
    private enum AttackStyle { Melee, Ranged }

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController animatorController;
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int BackFlip = Animator.StringToHash("BackFlip");
    private static readonly int IsRetreating = Animator.StringToHash("IsRetreating");

    [Header("Behavior")]
    [SerializeField] private AttackStyle attackStyle = AttackStyle.Melee;
    [SerializeField] private string targetTag = "Player";
    [SerializeField, Min(0f)] private float detectionRange = 25f;
    [SerializeField, Min(0.05f)] private float attackCooldown = 1f;

    [Header("Chase Movement")]
    [SerializeField, Min(0.01f)] private float accelerationTime = 0.1f;
    [SerializeField, Min(0f)] private float minimumTurnSpeed = 720f;
    [SerializeField, Min(0.01f)] private float destinationRefreshDistance = 0.5f;
    [Header("Charged Melee Knockback")]
    [SerializeField, Min(0f)] private float knockbackDistance = 0.7f;
    [SerializeField, Min(0.01f)] private float knockbackDuration = 0.2f;

    [Header("Melee")]
    [SerializeField, Min(0f)] private float meleeRange = 2f;
    [SerializeField, Min(0f)] private float meleeDamage = 15f;
    [Tooltip("Seconds after starting Attack before the melee hitbox deals damage.")]
    [SerializeField, Min(0f)] private float meleeHitDelay = 0.2f;
    [SerializeField] private Vector3 meleeHitboxSize = new Vector3(1.5f, 1.8f, 1.5f);
    [SerializeField] private Vector3 meleeHitboxOffset = new Vector3(0f, 1f, 1f);
    [SerializeField] private bool debugHitbox;

    [Header("Damage Sounds")]
    [SerializeField, Range(0f, 1f)] private float hitSoundChance = 0.5f;
    [SerializeField, Min(0f)] private float hitSoundCooldown = 0.75f;
    private float nextHitSoundTime;

    public void PlayDamageSound(bool died, bool meleeHit = false)
    {
        SoundManager sounds = SoundManager.Instance;
        if (sounds == null)
            return;

        if (died)
        {
            sounds.StopSFX(transform);
            sounds.PlaySFX(attackStyle == AttackStyle.Melee ? "BigKidDeath" : "KidDeath", transform);
            return;
        }

        if (Time.time < nextHitSoundTime)
            return;

        // Limit attempts too, so rapid hits cannot bypass the chance of silence.
        nextHitSoundTime = Time.time + hitSoundCooldown;
        if (Random.value < hitSoundChance)
            sounds.PlaySFX(attackStyle == AttackStyle.Melee ? "BigKidHit"
                : meleeHit ? "KidHitMelee" : "KidHit", transform);
    }

    [Header("Ranged Voice")]
    [SerializeField, Range(0f, 1f)] private float findSoundChance = 0.5f;
    [SerializeField, Min(0f)] private float findSoundCooldown = 4f;
    [SerializeField, Min(0f)] private float dodgeSoundCooldown = 1.5f;
    private float nextFindSoundTime;
    private float nextDodgeSoundTime;
    private bool wasApproaching;

    private void PlayRangedVoice(string id, float chance, float cooldown, ref float nextTime)
    {
        if (Time.time < nextTime)
            return;
        nextTime = Time.time + cooldown;
        SoundManager sounds = SoundManager.Instance;
        if (sounds != null && Random.value < chance)
            sounds.PlaySFX(id, transform);
    }

    [Header("Ranged")]
    [SerializeField, Min(0f)] private float preferredDistance = 6f;
    [SerializeField, Min(0f)] private float retreatBuffer = 0.5f;
    [SerializeField, Min(0.1f)] private float shootingRange = 15f;
    [SerializeField] private BulletTypeData bulletType;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform bulletContainer;
    private NavMeshAgent agent;
    private Transform target;
    private Transform targetAimPoint;
    private PlayerConfig targetHealth;
    private float nextAttackTime;
    private float meleeHitTime = -1f;
    private bool warnedMissingBullet;
    private bool wasRetreating;
    private bool hasBackFlip;
    private bool hasRetreatingParameter;
    private Vector3 lastDestination;
    private bool hasDestination;
    private Vector3 knockbackDirection;
    private float knockbackRemaining;
    


    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            if (animatorController != null)
                animator.runtimeAnimatorController = animatorController;
            if (animator.runtimeAnimatorController != null)
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.nameHash == BackFlip && parameter.type == AnimatorControllerParameterType.Trigger)
                        hasBackFlip = true;
                    if (parameter.nameHash == IsRetreating && parameter.type == AnimatorControllerParameterType.Bool)
                        hasRetreatingParameter = true;
                }
            // Navigation owns movement; animation only moves the model's bones.
            animator.applyRootMotion = false;
        }
        agent.autoBraking = true;
        // Keep fast NPCs responsive when the target dodges.
        agent.acceleration = Mathf.Max(agent.acceleration, agent.speed / Mathf.Max(0.01f, accelerationTime));
        agent.angularSpeed = Mathf.Max(agent.angularSpeed, minimumTurnSpeed);

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
            body.isKinematic = true;
    }

    private void Update()
    {
        if (target == null)
        {
            meleeHitTime = -1f;
            GameObject player = GameObject.FindGameObjectWithTag(targetTag);
            if (player != null)
            {
                target = player.transform;
                targetAimPoint = target.Find("AimPoint");
                targetHealth = player.GetComponent<PlayerConfig>();
            }
        }

        if (!agent.isOnNavMesh)
        {
            wasApproaching = false;
            meleeHitTime = -1f;
            SetRetreating(false);
            return;
        }

        if (knockbackRemaining > 0f)
        {
            float step = Mathf.Min(knockbackRemaining, knockbackDistance / Mathf.Max(0.01f, knockbackDuration) * Time.deltaTime);
            Vector3 displacement = knockbackDirection * step;
            if (agent.Raycast(agent.nextPosition + displacement, out NavMeshHit hit))
            {
                agent.Move(hit.position - agent.nextPosition);
                knockbackRemaining = 0f;
            }
            else
            {
                agent.Move(displacement);
                knockbackRemaining -= step;
            }
            if (knockbackRemaining <= 0f)
                agent.isStopped = false;
            return;
        }

        if (target == null || (targetHealth != null && targetHealth.IsDead()))
        {
            meleeHitTime = -1f;
            SetRetreating(false);
            StopMoving();
            return;
        }

        if (meleeHitTime >= 0f)
        {
            StopMoving();
            ResolveMeleeHit();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > detectionRange)
        {
            SetRetreating(false);
            StopMoving();
            return;
        }

        if (attackStyle == AttackStyle.Melee)
            UpdateMelee();
        else
            UpdateRanged(distance);
    }

    public void ApplyChargedKnockback(Vector3 direction)
    {
        direction.y = 0f;
        if (!isActiveAndEnabled || agent == null || !agent.isOnNavMesh || knockbackDistance <= 0f || direction.sqrMagnitude < 0.0001f)
            return;
        meleeHitTime = -1f;
        knockbackDirection = direction.normalized;
        knockbackRemaining = knockbackDistance;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetBool(IsRunning, agent.isActiveAndEnabled && agent.isOnNavMesh
                && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f);
    }

    private void OnDisable()
    {
        wasApproaching = false;
        meleeHitTime = -1f;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsRunning, false);
            animator.ResetTrigger(Attack);
        }
        SetRetreating(false);
    }

    private void UpdateMelee()
    {
        Vector3 separation = target.position - transform.position;
        separation.y = 0f;
        float distance = separation.magnitude;
        SetRetreating(false);
        agent.updateRotation = true;
        if (distance > meleeRange)
        {
            MoveTo(target.position, Mathf.Max(0f, meleeRange - 0.25f));
            return;
        }

        FaceTarget();
        Collider targetHit = null;
        HitboxService.CreateHitbox(transform, meleeHitboxSize, meleeHitboxOffset, hit =>
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
                targetHit = hit;
        });
        // The attack radius can exceed the hitbox; keep approaching until a hit can land.
        if (targetHit == null)
        {
            MoveTo(target.position, 0f);
            return;
        }

        StopMoving();
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.SetTrigger(Attack);
        meleeHitTime = Time.time + Mathf.Max(0f, meleeHitDelay);
        ResolveMeleeHit();
    }

    private void ResolveMeleeHit()
    {
        if (meleeHitTime < 0f || Time.time < meleeHitTime)
            return;

        meleeHitTime = -1f;
        if (target == null)
            return;
        HitboxService.CreateHitbox(transform, meleeHitboxSize, meleeHitboxOffset, hit =>
        {
            if (hit.transform == target || hit.transform.IsChildOf(target))
                ApplyDamage(hit, meleeDamage);
        }, debugHitbox);
    }

    private void UpdateRanged(float distance)
    {
        float maxRange = Mathf.Max(0.1f, shootingRange);
        float safeDistance = Mathf.Min(Mathf.Max(0f, preferredDistance), maxRange);
        float retreatEndDistance = Mathf.Min(maxRange, safeDistance + Mathf.Max(0f, retreatBuffer));
        bool retreating = distance < safeDistance || (wasRetreating && distance < retreatEndDistance);
        agent.updateRotation = distance > maxRange;
        if (!agent.updateRotation)
            FaceTarget();

        bool approaching = !retreating && distance > maxRange;
        if (approaching && !wasApproaching)
            PlayRangedVoice("KidFind", findSoundChance, findSoundCooldown, ref nextFindSoundTime);
        if (retreating && !wasRetreating)
            PlayRangedVoice("KidDodge", 1f, dodgeSoundCooldown, ref nextDodgeSoundTime);
        wasApproaching = approaching;
        SetRetreating(retreating);

        if (retreating)
        {
            Vector3 away = transform.position - target.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.001f)
                away = -transform.forward;

            MoveTo(transform.position + away.normalized * (retreatEndDistance - distance + 0.1f), 0.1f);
            return;
        }

        if (distance > maxRange)
        {
            MoveTo(target.position, Mathf.Max(0f, maxRange - 0.25f));
            return;
        }

        StopMoving();
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + (bulletType != null
            ? bulletType.GetFireCooldown(attackCooldown) : attackCooldown);
        if (bulletType == null)
        {
            if (!warnedMissingBullet)
            {
                Debug.LogWarning("Chase: assign a Bullet Type for ranged behavior.", this);
                warnedMissingBullet = true;
            }
            return;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up;
        Vector3 aimPoint = targetAimPoint != null ? targetAimPoint.position : target.position + Vector3.up;
        GameObject bullet = bulletType.Spawn(origin, aimPoint - origin, transform);

        if (bullet != null)
        {
            bullet.transform.SetParent(bulletContainer, true);
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetTrigger(Attack);
        }
    }

    private void MoveTo(Vector3 destination, float stoppingDistance)
    {
        agent.isStopped = false;
        agent.stoppingDistance = stoppingDistance;
        if (hasDestination
            && (destination - lastDestination).sqrMagnitude < destinationRefreshDistance * destinationRefreshDistance
            && ((destination - lastDestination).sqrMagnitude < 0.0001f
                || agent.pathPending || agent.remainingDistance > stoppingDistance + 0.05f))
            return;

        agent.SetDestination(destination);
        lastDestination = destination;
        hasDestination = true;
    }

    private void SetRetreating(bool retreating)
    {
        if (hasRetreatingParameter && animator != null)
            animator.SetBool(IsRetreating, retreating);
        if (hasBackFlip && animator != null)
        {
            if (retreating && !wasRetreating)
                animator.SetTrigger(BackFlip);
            else if (!retreating && wasRetreating)
                animator.ResetTrigger(BackFlip);
        }
        wasRetreating = retreating;
    }

    private void StopMoving()
    {
        wasApproaching = false;
        agent.isStopped = true;
        hasDestination = false;
    }

    private void FaceTarget()
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }

    private static void ApplyDamage(Collider hit, float damage)
    {
        foreach (MonoBehaviour component in hit.GetComponentsInParent<MonoBehaviour>())
        {
            if (component is IDamageable damageable)
            {
                damageable.TakeDamage(damage);
                return;
            }
        }
    }
}
