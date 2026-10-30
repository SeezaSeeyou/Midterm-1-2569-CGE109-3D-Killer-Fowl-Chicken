using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount);
}

public interface IImpactDamageable : IDamageable
{
    void TakeDamage(float amount, Vector3 point, Vector3 direction);
}

public interface IBulletHitEffect
{
    void OnBulletHit(BulletRuntime bullet, GameObject hitObject, Vector3 point, Vector3 normal);
}

/// <summary>Reusable runtime component for projectile prefabs.</summary>
public class BulletRuntime : MonoBehaviour
{
    [SerializeField] private string hitSoundId = string.Empty;
    [SerializeField, Range(0.1f, 3f)] private float hitSoundMinPitch = 1f;
    [SerializeField, Range(0.1f, 3f)] private float hitSoundMaxPitch = 1f;
    public float Damage { get; private set; }
    public float Speed { get; private set; }
    public float Lifetime { get; private set; }

    private bool hasHit;
    private bool isNpcOwned;
    private Vector3 flightDirection;

    public void Initialize(Vector3 direction, float damage, float speed, float lifetime, Transform owner)
    {
        Damage = damage;
        Speed = speed;
        Lifetime = lifetime;
        flightDirection = direction.normalized;
        isNpcOwned = owner != null && owner.GetComponentInParent<Chase>() != null;

        Collider[] bulletColliders = GetComponentsInChildren<Collider>();
        if (owner != null)
        {
            Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>();
            foreach (Collider bulletCollider in bulletColliders)
            {
                foreach (Collider ownerCollider in ownerColliders)
                {
                    if (bulletCollider != null && ownerCollider != null)
                        Physics.IgnoreCollision(bulletCollider, ownerCollider);
                }
            }
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
        }

        body.linearVelocity = direction.normalized * speed;
        Destroy(gameObject, Mathf.Max(0.01f, lifetime));
    }

    private void OnCollisionEnter(Collision collision)
    {
        Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        Vector3 normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -flightDirection;
        HandleHit(collision.gameObject, point, normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 previousPosition = transform.position - flightDirection * (Mathf.Max(0f, Speed) * Time.fixedDeltaTime + 0.1f);
        Vector3 point = other.ClosestPoint(previousPosition);
        Vector3 normal = Vector3.zero;
        if (other.Raycast(new Ray(previousPosition, flightDirection), out RaycastHit hit,
            Vector3.Distance(previousPosition, transform.position) + 0.5f))
        {
            point = hit.point;
            normal = hit.normal;
        }
        else if (other.Raycast(new Ray(point + Vector3.up * 0.2f, Vector3.down), out hit, 0.4f))
        {
            point = hit.point;
            normal = hit.normal;
        }
        HandleHit(other.gameObject, point, normal);
    }

    private void HandleHit(GameObject hitObject, Vector3 point, Vector3 normal)
    {
        if (hasHit || hitObject == null)
            return;

        // shortcut: all Chase NPCs share one team, add team IDs if factions need friendly fire.
        Chase friendlyNpc = isNpcOwned ? hitObject.GetComponentInParent<Chase>() : null;
        if (friendlyNpc != null)
        {
            IgnoreCollisionsWith(friendlyNpc);
            return;
        }

        hasHit = true;

        if (!string.IsNullOrWhiteSpace(hitSoundId))
        {
            SoundManager sounds = SoundManager.Instance;
            if (sounds != null)
                sounds.PlaySFX(hitSoundId, point, Random.Range(
                    Mathf.Min(hitSoundMinPitch, hitSoundMaxPitch),
                    Mathf.Max(hitSoundMinPitch, hitSoundMaxPitch)));
        }

        MonoBehaviour[] hitBehaviours = hitObject.GetComponentsInParent<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in hitBehaviours)
        {
            if (behaviour is IDamageable damageable)
            {
                if (damageable is IImpactDamageable impact)
                    impact.TakeDamage(Damage, point, flightDirection);
                else
                    damageable.TakeDamage(Damage);
                break;
            }
        }

        MonoBehaviour[] effects = GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour effect in effects)
        {
            if (effect is IBulletHitEffect hitEffect)
                hitEffect.OnBulletHit(this, hitObject, point, normal);
        }

        Destroy(gameObject);
    }

    private void IgnoreCollisionsWith(Chase npc)
    {
        Collider[] bulletColliders = GetComponentsInChildren<Collider>();
        Collider[] npcColliders = npc.GetComponentsInChildren<Collider>();

        foreach (Collider bulletCollider in bulletColliders)
        {
            foreach (Collider npcCollider in npcColliders)
            {
                if (bulletCollider != null && npcCollider != null)
                    Physics.IgnoreCollision(bulletCollider, npcCollider);
            }
        }
    }
}
