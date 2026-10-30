using UnityEngine;

/// <summary>Reusable profile containing a bullet prefab and its default stats.</summary>
[CreateAssetMenu(fileName = "NewBulletType", menuName = "Combat/Bullet Type")]
public class BulletTypeData : ScriptableObject
{
    [Header("Bullet")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("Shooting")]
    [Tooltip("Shots per second for PlayerShoot and ranged Chase. 0 uses the shooter's cooldown.")]
    [SerializeField, Min(0f)] private float fireRate;

    [Header("Default Stats")]
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Min(0f)] private float speed = 30f;
    [SerializeField, Min(0.01f)] private float lifetime = 5f;

    public GameObject ProjectilePrefab => projectilePrefab;
    public float Damage => damage;
    public float Speed => speed;
    public float Lifetime => lifetime;

    public float GetFireCooldown(float fallback)
    {
        return fireRate > 0f && !float.IsInfinity(fireRate) ? 1f / fireRate : fallback;
    }

    /// <summary>Spawns this type. Negative stat overrides use this profile's values.</summary>
    public GameObject Spawn(Vector3 position, Vector3 direction, Transform owner = null,
        float damageOverride = -1f, float speedOverride = -1f, float lifetimeOverride = -1f)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError($"Bullet Type '{name}' has no Prefab assigned.", this);
            return null;
        }

        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        GameObject bullet = Instantiate(projectilePrefab, position, Quaternion.LookRotation(direction));
        BulletRuntime runtime = bullet.GetComponent<BulletRuntime>();
        if (runtime == null)
        {
            Debug.LogError($"Bullet Prefab '{projectilePrefab.name}' needs a BulletRuntime component.", bullet);
            Destroy(bullet);
            return null;
        }

        runtime.Initialize(direction,
            damageOverride >= 0f ? damageOverride : damage,
            speedOverride >= 0f ? speedOverride : speed,
            lifetimeOverride >= 0f ? lifetimeOverride : lifetime,
            owner);

        return bullet;
    }
}



