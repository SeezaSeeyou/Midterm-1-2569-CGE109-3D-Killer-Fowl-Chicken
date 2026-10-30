using UnityEngine;
using UnityEngine.InputSystem;


[DefaultExecutionOrder(100)]
public class PlayerShoot : MonoBehaviour
{
    [Header("Reload")]
    [SerializeField, Min(1)] private int magazineSize = 50;
    [SerializeField, Min(0.01f)] private float reloadDuration = 1.5f;
    [Header("Shooting")]
    [SerializeField] private BulletTypeData bulletType;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0f)] private float fireCooldown = 0.15f;
    [SerializeField, Min(0f)] private float m1SpreadAngle;

    [Header("Aim")]
    [SerializeField] private Camera aimCamera;
    [SerializeField, Min(1f)] private float aimFieldOfView = 45f;
    [SerializeField, Min(0f)] private float aimSmoothSpeed = 12f;
    [SerializeField] private Transform bulletContainer;
    private float normalFieldOfView;
    private float nextFireTime;
    private bool fireHeld;
    private bool fireRequested;
    private bool fireSequenceStarted;
    private bool shotWaitingForWindup;
    private float windupEndTime;
    private PlayerAnimation playerAnimation;
    private PlayerAbilities abilities;
    private Playermovement movement;
    private float reloadEndTime;
    [Header("Reload UI")]
    [SerializeField] private GameObject reloadUI;
    [SerializeField] private RectTransform reloadFill;

    public bool IsAiming { get; private set; }
    private bool IsMeleeBlockingShot => (abilities != null && abilities.IsChargingMelee)
        || (movement != null && movement.IsMeleeLunging)
        || (playerAnimation != null && playerAnimation.IsMeleeAttacking);

    public bool IsFiring => isActiveAndEnabled && fireHeld && !IsReloading && !IsMeleeBlockingShot && bulletType != null && firePoint != null;
    public bool IsWaitingForShotWindup => shotWaitingForWindup;
    public event System.Action ShotFired;
    public int CurrentAmmo { get; private set; }
    public int MagazineSize => magazineSize;
    public bool IsReloading { get; private set; }
    public float ReloadProgress => IsReloading
        ? Mathf.Clamp01(1f - (reloadEndTime - Time.time) / reloadDuration)
        : 0f;

    private void Awake()
    {
        OnValidate();
        playerAnimation = GetComponent<PlayerAnimation>();
        abilities = GetComponent<PlayerAbilities>();
        movement = GetComponent<Playermovement>();
        CurrentAmmo = magazineSize;
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera != null)
            normalFieldOfView = aimCamera.fieldOfView;

        if (reloadUI != null)
            reloadUI.SetActive(false);
    }

    private void Update()
    {
        if (IsReloading && Time.time >= reloadEndTime)
        {
            CurrentAmmo = magazineSize;
            IsReloading = false;
        }
        if (reloadUI != null)
            reloadUI.SetActive(IsReloading);
        if (reloadFill != null)
            reloadFill.anchorMax = new Vector2(ReloadProgress, 1f);

        if (aimCamera != null)
        {
            float targetFov = IsAiming ? aimFieldOfView : normalFieldOfView;
            aimCamera.fieldOfView = Mathf.Lerp(
                aimCamera.fieldOfView,
                targetFov,
                aimSmoothSpeed * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (IsMeleeBlockingShot)
        {
            fireRequested = false;
            fireSequenceStarted = false;
            shotWaitingForWindup = false;
            return;
        }

        // The camera must finish positioning before we resolve the centre-screen aim.
        if (shotWaitingForWindup)
        {
            if (playerAnimation == null || !playerAnimation.IsShotWindup)
                shotWaitingForWindup = false;
            else if (Time.time >= windupEndTime)
            {
                shotWaitingForWindup = false;
                fireSequenceStarted = fireHeld;
                FireOnce();
            }
        }
        else if ((fireHeld || fireRequested) && Time.time >= nextFireTime && !IsReloading)
        {
            if (!fireSequenceStarted && !IsAiming && bulletType != null && firePoint != null && playerAnimation != null)
            {
                float duration = playerAnimation.BeginShotWindup();
                if (duration > 0f)
                {
                    shotWaitingForWindup = true;
                    windupEndTime = Time.time + duration;
                    fireSequenceStarted = true;
                    fireRequested = false;
                    return;
                }
            }
            fireSequenceStarted = fireHeld;
            FireOnce();
        }
        fireRequested = false;
    }

    public void OnM1(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
        {
            fireHeld = true;
            fireRequested = true;
        }
        else if (context.canceled)
        {
            fireHeld = false;
            fireSequenceStarted = false;
        }
    }

    private void FireOnce()
    {
        if (IsReloading || Time.time < nextFireTime)
            return;

        if (bulletType == null || firePoint == null)
        {
            Debug.LogWarning("PlayerShoot: assign Bullet Type and Fire Point.", this);
            fireHeld = false;
            return;
        }

        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        Transform aimTransform = cameraToUse != null ? cameraToUse.transform : firePoint;
        Ray aimRay = cameraToUse != null
            ? cameraToUse.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
            : new Ray(firePoint.position, firePoint.forward);

        float currentSpreadAngle = m1SpreadAngle * (IsAiming ? 0.25f : 1f);
        if (currentSpreadAngle > 0f)
        {
            float spreadRadius = Mathf.Tan(currentSpreadAngle * Mathf.Deg2Rad);
            Vector2 spread = Random.insideUnitCircle * spreadRadius;
            aimRay.direction = (aimRay.direction
                + aimTransform.right * spread.x
                + aimTransform.up * spread.y).normalized;
        }

        float aimDistance = Mathf.Max(1f, bulletType.Speed * bulletType.Lifetime);
        Vector3 aimPoint = aimRay.GetPoint(aimDistance);
        foreach (RaycastHit hit in Physics.RaycastAll(aimRay, aimDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)
                || hit.transform.GetComponentInParent<BulletRuntime>() != null || hit.distance >= aimDistance)
                continue;
            aimDistance = hit.distance;
            aimPoint = hit.point;
        }
        Vector3 toAimPoint = aimPoint - firePoint.position;
        Vector3 shootDirection = Vector3.Dot(toAimPoint, aimRay.direction) > 0.001f
            ? toAimPoint.normalized : aimRay.direction;

        GameObject bullet = bulletType.Spawn(
            firePoint.position, shootDirection, transform);

        if (bullet != null)
        {
            bullet.transform.SetParent(bulletContainer, true);
            CurrentAmmo--;
            ShotFired?.Invoke();
            if (CurrentAmmo == 0)
            {
                IsReloading = true;
                fireSequenceStarted = false;
                reloadEndTime = Time.time + reloadDuration;
                if (reloadFill != null)
                    reloadFill.anchorMax = new Vector2(0f, 1f);
                if (reloadUI != null)
                    reloadUI.SetActive(true);
            }
        }

        nextFireTime = Time.time + bulletType.GetFireCooldown(fireCooldown);
    }

    public void OnM2(InputAction.CallbackContext context)
    {
        if (context.started || context.performed)
            IsAiming = true;
        else if (context.canceled)
            IsAiming = false;
    }

    private void OnDisable()
    {
        if (reloadUI != null)
            reloadUI.SetActive(false);
        fireHeld = false;
        fireRequested = false;
        fireSequenceStarted = false;
        shotWaitingForWindup = false;
        IsAiming = false;
        if (aimCamera != null)
            aimCamera.fieldOfView = normalFieldOfView;
    }

    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        reloadDuration = Mathf.Max(0.01f, reloadDuration);
    }

}
