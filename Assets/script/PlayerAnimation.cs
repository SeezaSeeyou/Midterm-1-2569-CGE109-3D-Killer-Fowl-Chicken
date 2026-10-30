using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    private Animator animator;
    private PlayerShoot shoot;
    private PlayerAbilities abilities;
    private Playermovement movement;
    private Rigidbody body;
    private PlayerCammara cameraRig;
    private string state;
    private float holdUntil;
    private bool grounded;
    private bool aiming;
    private bool dashing;
    private bool doubleJump;
    private bool left;
    private bool shotPending;

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        shoot = GetComponent<PlayerShoot>();
        abilities = GetComponent<PlayerAbilities>();
        movement = GetComponent<Playermovement>();
        body = GetComponent<Rigidbody>();
        cameraRig = Camera.main != null ? Camera.main.GetComponent<PlayerCammara>() : null;
        if (animator != null) animator.applyRootMotion = false;
    }

    private void OnEnable()
    {
        if (shoot != null) shoot.ShotFired += OnShot;
        if (abilities != null) abilities.MeleePerformed += OnMelee;
    }

    private void OnDisable()
    {
        if (shoot != null) shoot.ShotFired -= OnShot;
        if (abilities != null) abilities.MeleePerformed -= OnMelee;
        shotPending = false;
    }

    private string Side => cameraRig != null && cameraRig.IsLeftShoulder ? "_L" : "_R";

    public bool IsShotWindup => isActiveAndEnabled && animator != null && animator.enabled && state == "StatdShoot";
    public bool IsMeleeAttacking => isActiveAndEnabled && animator != null && animator.enabled
        && (state == "Attack1" || state == "Attackcharge") && Time.time < holdUntil;

    public float BeginShotWindup()
    {
        if (!isActiveAndEnabled || animator == null || !animator.enabled || animator.runtimeAnimatorController == null
            || !movement.isActiveAndEnabled || !movement.IsGroundedForAbilities() || body.linearVelocity.y > 0.1f
            || abilities.IsDash || abilities.IsChargingMelee || movement.IsMeleeLunging
            || IsMeleeAttacking)
            return 0f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name != "StatdShoot")
                continue;
            Play("StatdShoot", true);
            return clip.length;
        }
        return 0f;
    }

    private void OnShot()
    {
        if (abilities.IsDash || (!grounded && body.linearVelocity.y >= 0f)
            || abilities.IsChargingMelee || movement.IsMeleeLunging) return;
        if (!shoot.IsAiming && (state == "StatdShoot" || (shoot.IsFiring && state == "Shooting"))) return;
        shotPending = true;
    }

    private void OnMelee(PlayerAbilities.MeleeType type)
    {
        shotPending = false;
        Play(type == PlayerAbilities.MeleeType.Charged ? "Attackcharge" : "Attack1", true);
    }

    private void LateUpdate()
    {
        if (animator == null || !animator.enabled || !movement.enabled) return;
        bool onGround = movement.IsGroundedForAbilities() && body.linearVelocity.y <= 0.1f;
        bool dash = abilities.IsDash;
        bool secondJump = abilities.IsDouble;
        bool aim = shoot.IsAiming;
        bool cameraLeft = cameraRig != null && cameraRig.IsLeftShoulder;
        if (!dash && state != null && state.StartsWith("dodge_"))
            holdUntil = 0f;
        if (IsMeleeAttacking)
        {
            shotPending = false;
            grounded = onGround; dashing = dash; doubleJump = secondJump; aiming = aim; left = cameraLeft;
            return;
        }
        if (abilities.IsChargingMelee)
        {
            shotPending = false;
            if (state != "StatdCharge") Play("StatdCharge", true);
            grounded = onGround; dashing = dash; doubleJump = secondJump; aiming = aim; left = cameraLeft;
            return;
        }
        if (onGround && !dash && !aim && !movement.IsMeleeLunging
            && shoot.IsWaitingForShotWindup && state == "StatdShoot")
        {
            grounded = onGround; dashing = dash; doubleJump = secondJump; aiming = aim; left = cameraLeft;
            return;
        }
        bool falling = !onGround && body.linearVelocity.y < 0f;
        if (falling && !dash && !movement.IsMeleeLunging && (aim || shotPending || shoot.IsFiring))
        {
            if (state == "JUMP" || aim != aiming || left != cameraLeft)
                holdUntil = 0f;
            grounded = false; dashing = dash; doubleJump = secondJump; aiming = aim; left = cameraLeft;
            if (shotPending && Time.time >= holdUntil)
            {
                shotPending = false;
                Play(aim ? "StatdScopShoot" + Side : "Shooting", true);
            }
            else if (Time.time >= holdUntil)
                Play(aim ? "StatdScopIdel" + Side : "Shooting");
            return;
        }
        if (dash && !dashing)
        {
            Vector3 direction = Quaternion.Inverse(Quaternion.LookRotation(movement.GetFacingDirection())) * abilities.DashVelocity;
            Play("dodge_" + (Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? (direction.x < 0 ? "L" : "R") : (direction.z < 0 ? "B" : "F")), true);
        }
        else if (!dash && ((!onGround && grounded) || (secondJump && !doubleJump))) Play("JUMP", true);
        else if (!dash && onGround && !grounded) Play("Landing", true);
        else if (onGround && !dash && aim && (!aiming || left != cameraLeft)) Play("StatdScop" + Side, true);
        else if (!aim && aiming) holdUntil = 0f;
        grounded = onGround; dashing = dash; doubleJump = secondJump; aiming = aim; left = cameraLeft;
        if (dash || !onGround || abilities.IsChargingMelee)
        {
            shotPending = false;
            if (!dash && !onGround && Time.time >= holdUntil) Play("JUMP");
            return;
        }
        if (Time.time < holdUntil) return;
        Vector3 velocity = body.linearVelocity; velocity.y = 0;
        if (!aim && shoot.IsFiring && (state == "StatdShoot" || state == "Shooting"))
        {
            shotPending = false;
            Play("Shooting");
            return;
        }
        if (shotPending)
        {
            shotPending = false;
            Play(aim ? "StatdScopShoot" + Side
                : velocity.sqrMagnitude > 0.05f ? "Shooting" : "StatdShoot", true);
            return;
        }
        Play(aim ? "StatdScopIdel" + Side : velocity.sqrMagnitude > 0.05f ? "RUN" : "Idel");
    }

    private void Play(string name, bool once = false)
    {
        if (animator == null || !animator.enabled || (!once && state == name)) return;
        state = name;
        holdUntil = 0f;
        animator.CrossFadeInFixedTime(name, 0.06f, 0, 0);
        if (once)
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == name) { holdUntil = Time.time + clip.length; break; }
    }
}
