using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(PlayerAbilities))]
public class Playermovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField, Range(0f, 1f)] private float backwardSpeedMultiplier = 0.65f;
    [SerializeField, Range(0f, 1f)] private float strafeSpeedMultiplier = 0.8f;
    [SerializeField, Range(0f, 1f)] private float aimingSpeedMultiplier = 0.7f;
    [SerializeField] private PlayerShoot playerShoot;
    [SerializeField] private Transform movementCamera;
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0f)] private float turnSpeed = 720f;

    [Header("Jump")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private Vector3 groundCheckSize = new Vector3(0.45f, 0.12f, 0.45f);
    [SerializeField, Min(0f)] private float groundCheckOffset = 0.03f;
    [SerializeField, Min(0f)] private float airControlAcceleration = 12f;

    private Rigidbody rb;
    private Vector2 moveInput;
    private Quaternion visualRotationOffset;
    private bool groundContact;
    private Vector3 pendingMeleeLunge;
    private bool meleeLunging;
    private PlayerAbilities abilities;
    private Collider playerCollider;
    private readonly Collider[] groundHits = new Collider[8];
    private readonly List<Vector3> wallNormals = new List<Vector3>();

    public bool IsMeleeLunging => isActiveAndEnabled && meleeLunging;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        abilities = GetComponent<PlayerAbilities>();
        if (playerShoot == null)
            playerShoot = GetComponent<PlayerShoot>();
        playerCollider = GetComponent<Collider>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
        if (visualRoot != null)
            visualRotationOffset = Quaternion.Inverse(transform.rotation) * visualRoot.rotation;

        if (movementCamera == null && Camera.main != null)
            movementCamera = Camera.main.transform;
    }


    public bool IsGroundedForAbilities()
    {
        if (groundContact)
            return true;
        float feetY = playerCollider != null ? playerCollider.bounds.min.y : transform.position.y;
        Vector3 checkCenter = new Vector3(transform.position.x, feetY + groundCheckOffset, transform.position.z);
        int hitCount = Physics.OverlapBoxNonAlloc(
            checkCenter,
            groundCheckSize * 0.5f,
            groundHits,
            Quaternion.identity,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = groundHits[i];
            if (hit != null && hit.attachedRigidbody != rb && hit.transform != transform && !hit.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }
    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        Debug.Log("Move Input: " + moveInput);
    }

    private void FixedUpdate()
    {
        bool grounded = IsGroundedForAbilities();
        groundContact = false;

        Vector3 forward = movementCamera != null ? movementCamera.forward : Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        Vector3 right = movementCamera != null ? movementCamera.right : Vector3.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1f);

        // Remove input into walls so the player slides down instead of pushing against them.
        for (int i = 0; i < wallNormals.Count; i++)
        {
            if (Vector3.Dot(moveDirection, wallNormals[i]) < 0f)
                moveDirection = Vector3.ProjectOnPlane(moveDirection, wallNormals[i]);
        }
        wallNormals.Clear();

        float speedMultiplier = 1f;
        if (moveInput.y < -0.1f)
            speedMultiplier = backwardSpeedMultiplier;
        else if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
            speedMultiplier = strafeSpeedMultiplier;

        if (playerShoot != null && playerShoot.IsAiming)
            speedMultiplier *= aimingSpeedMultiplier;

        Vector3 targetHorizontalVelocity = moveDirection * (moveSpeed * speedMultiplier);
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if (abilities != null && abilities.IsDashing)
        {
            horizontalVelocity = abilities.DashVelocity;
        }
        else if (grounded && !meleeLunging)
        {
            horizontalVelocity = targetHorizontalVelocity;
        }
        else
        {
            float acceleration = meleeLunging && airControlAcceleration <= 0f ? 12f : airControlAcceleration;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                targetHorizontalVelocity,
                acceleration * Time.fixedDeltaTime);
        }

        horizontalVelocity += pendingMeleeLunge;
        pendingMeleeLunge = Vector3.zero;
        // Ground movement must not erase the punch momentum on the next physics step.
        meleeLunging = meleeLunging
            && (horizontalVelocity - targetHorizontalVelocity).sqrMagnitude > 0.0001f;

        // Preserve jump velocity at walls; gravity naturally brings the player back down.
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);

        Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);
        if (visualRoot != null)
        {
            Quaternion visualTarget = targetRotation * visualRotationOffset;
            visualRoot.rotation = Quaternion.RotateTowards(
                visualRoot.rotation,
                visualTarget,
                turnSpeed * Time.fixedDeltaTime);
        }
    }

    public void ApplyJump(float jumpForce)
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
    }

    public Vector3 GetDashDirection()
    {
        Vector3 forward = movementCamera != null ? movementCamera.forward : transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        Vector3 right = movementCamera != null ? movementCamera.right : transform.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

        Vector3 direction = forward * moveInput.y + right * moveInput.x;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : forward;
    }

    public Vector3 GetFacingDirection()
    {
        Vector3 forward = movementCamera != null ? movementCamera.forward : transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    public void ApplyMeleeLunge(Vector3 direction, float speed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            pendingMeleeLunge = direction.normalized * speed;
            meleeLunging = speed > 0f;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        CacheWallNormals(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        CacheWallNormals(collision);
    }

    private void CacheWallNormals(Collision collision)
    {
        NPCHP npc = collision.rigidbody != null ? collision.rigidbody.GetComponentInParent<NPCHP>() : null;
        bool pushableRagdoll = npc != null && npc.IsRagdoll;
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 normal = contact.normal;
            if (Vector3.Dot(normal, rb.worldCenterOfMass - contact.point) < 0f)
                normal = -normal;

            bool contactAtFeet = playerCollider != null
                && contact.point.y <= playerCollider.bounds.min.y + 0.08f
                && rb.linearVelocity.y <= 0.1f;
            if (normal.y >= 0.5f || contactAtFeet)
            {
                groundContact = true;
                continue;
            }
            if (normal.y <= -0.5f)
                continue;

            if (!pushableRagdoll)
                wallNormals.Add(normal);
        }
    }
}





















