using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class PlayerCammara : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;
    public Transform RagdollTarget { get; set; }

    [Header("Third-Person Camera")]
    [SerializeField, Min(0.1f)] private float distance = 3f;
    [SerializeField] private float lookHeight = 1.25f;
    [SerializeField, Min(0f)] private float shoulderOffset = 0.4f;
    [SerializeField, Min(0f)] private float framingOffset = 0.65f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float verticalSensitivity = 0.12f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Smoothing")]
    [Tooltip("Short value keeps mouse aiming responsive while removing sharp camera snaps.")]
    [SerializeField, Min(0f)] private float rotationSmoothTime = 0.045f;
    [SerializeField, Min(0f)] private float shoulderSwitchSmoothTime = 0.3f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.1f;

    [Header("Wall Collision")]
    [SerializeField, Min(0.01f)] private float cameraCollisionRadius = 0.2f;
    [SerializeField, Min(0f)] private float cameraCollisionBuffer = 0.05f;
    [SerializeField] private LayerMask collisionLayers = ~0;
    [SerializeField, Min(0f)] private float collisionReturnSmoothTime = 0.12f;

    private float yaw;
    private float targetYaw;
    private float pitch = 10f;
    private float targetPitch = 10f;
    private float yawVelocity;
    private float pitchVelocity;
    private int shoulderSide = 1;
    public bool IsLeftShoulder => shoulderSide < 0;
    private float shoulderBlend = 1f;
    private float shoulderBlendVelocity;
    private Vector3 followVelocity;
    private Vector3 smoothedPivot;
    private bool pivotInitialized;
    private float currentCameraDistance;
    private float cameraDistanceVelocity;
    private bool cameraDistanceInitialized;
    private readonly RaycastHit[] obstructionHits = new RaycastHit[32];
    private bool cursorLocked;

    private void Awake()
    {
        if (player == null)
            player = transform.parent;


        if (player != null)
        {
            yaw = player.eulerAngles.y;
            targetYaw = yaw;
        }
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            UnlockCursor();

        if (!cursorLocked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor();

        if (cursorLocked && Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            targetYaw += mouseDelta.x * mouseSensitivity;
            targetPitch = Mathf.Clamp(targetPitch - mouseDelta.y * verticalSensitivity, minPitch, maxPitch);
        }

        // Smooth the aim over a very short interval so mouse input still feels direct.
        yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, rotationSmoothTime);
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, rotationSmoothTime);
    }

    private void LateUpdate()
    {
        if (player == null)
            return;

        shoulderBlend = Mathf.SmoothDamp(
            shoulderBlend,
            shoulderSide,
            ref shoulderBlendVelocity,
            shoulderSwitchSmoothTime);

        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPivot = (RagdollTarget != null ? RagdollTarget : player).position + Vector3.up * lookHeight;
        if (!pivotInitialized)
        {
            smoothedPivot = targetPivot;
            pivotInitialized = true;
        }
        else
        {
            smoothedPivot = Vector3.SmoothDamp(smoothedPivot, targetPivot, ref followVelocity, followSmoothTime);
        }

        Vector3 orbitPivot = smoothedPivot;
        Vector3 shoulderDirection = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 desiredPosition = orbitPivot
            - orbitRotation * Vector3.forward * distance
            + shoulderDirection * (shoulderOffset * shoulderBlend);
        Vector3 framingPoint = orbitPivot
            + shoulderDirection * (framingOffset * shoulderBlend);

        Vector3 cameraOffset = desiredPosition - orbitPivot;
        float desiredDistance = cameraOffset.magnitude;
        Vector3 cameraDirection = cameraOffset / Mathf.Max(desiredDistance, 0.0001f);
        float safeDistance = GetSafeCameraDistance(orbitPivot, cameraDirection, desiredDistance);

        if (!cameraDistanceInitialized)
        {
            currentCameraDistance = safeDistance;
            cameraDistanceInitialized = true;
        }
        else if (safeDistance < currentCameraDistance)
        {
            // Move in immediately to avoid showing the camera through a wall.
            currentCameraDistance = safeDistance;
            cameraDistanceVelocity = 0f;
        }
        else
        {
            // Ease back out after the obstruction is gone.
            currentCameraDistance = Mathf.SmoothDamp(
                currentCameraDistance,
                safeDistance,
                ref cameraDistanceVelocity,
                collisionReturnSmoothTime);
        }

        transform.position = orbitPivot + cameraDirection * currentCameraDistance;
        transform.rotation = Quaternion.LookRotation(framingPoint - transform.position, Vector3.up);
    }


    private float GetSafeCameraDistance(Vector3 origin, Vector3 direction, float desiredDistance)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            cameraCollisionRadius,
            direction,
            obstructionHits,
            desiredDistance,
            collisionLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = desiredDistance;
        bool obstructionFound = false;
        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = obstructionHits[i].transform;
            if (hitTransform == null || hitTransform == player || hitTransform.IsChildOf(player))
                continue;

            NPCHP npc = hitTransform.GetComponentInParent<NPCHP>();
            if (npc != null)
                continue;

            if (obstructionHits[i].distance < nearestDistance)
            {
                nearestDistance = obstructionHits[i].distance;
                obstructionFound = true;
            }
        }

        return obstructionFound
            ? Mathf.Max(0.05f, nearestDistance - cameraCollisionBuffer)
            : desiredDistance;
    }
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            LockCursor();
        else
            UnlockCursor();
    }

    private void OnDisable()
    {
        UnlockCursor();
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }

    public void OnSwitch(InputAction.CallbackContext context)
    {
        if (context.performed)
            shoulderSide *= -1;
    }
}









