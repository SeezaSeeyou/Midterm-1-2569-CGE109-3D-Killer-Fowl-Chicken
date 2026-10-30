using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerConfig : MonoBehaviour, IImpactDamageable
{
    public float HP = 100f;
    public float MaxHP = 100f;
    [Header("Damage Numbers")]
    [SerializeField] private bool showDamageNumbers = true;
    [SerializeField] private Color damageNumberColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField, Min(0.05f)] private float damageNumberDuration = 0.9f;
    [SerializeField, Min(1)] private int damageNumberFontSize = 40;
    [SerializeField] private RectTransform damageNumberAnchor;

    private bool deathHandled;
    private bool restarting;

    private void Update()
    {
        if (IsDead() && !deathHandled)
            Die(Vector3.zero);
        if (deathHandled && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            RestartGame();
    }

    public bool IsDead()
    {
        return HP <= 0f;
    }

    public void TakeDamage(float damage)
    {
        takeDamage(damage);
    }

    public void takeDamage(float damage)
    {
        TakeDamage(damage, transform.position, Vector3.zero);
    }

    public void TakeDamage(float damage, Vector3 point, Vector3 direction)
    {
        if (damage <= 0f || float.IsNaN(damage) || IsDead())
            return;

        HP = Mathf.Max(0f, HP - damage);
        SoundManager sounds = SoundManager.Instance;
        if (sounds != null)
            sounds.PlaySFX("ChickenHit", transform);
        if (showDamageNumbers)
            DamageNumber.Show(transform, damage, damageNumberColor, damageNumberDuration, damageNumberFontSize, true, damageNumberAnchor);
        if (IsDead())
            Die(direction);
    }
    private void Die(Vector3 direction)
    {
        if (deathHandled)
            return;
        deathHandled = true;
        HP = 0f;
        if (TryGetComponent(out PlayerAbilities abilities)) abilities.enabled = false;
        if (TryGetComponent(out PlayerShoot shoot)) shoot.enabled = false;
        if (TryGetComponent(out Playermovement movement)) movement.enabled = false;
        if (TryGetComponent(out PlayerInput input)) input.enabled = false;
        if (TryGetComponent(out PlayerAnimation animation)) animation.enabled = false;
        foreach (Animator animator in GetComponentsInChildren<Animator>())
            animator.enabled = false;

        Rigidbody ragdoll = PlayerRagdoll.Activate(this, direction);
        if (ragdoll != null)
        {
            if (Camera.main != null && Camera.main.TryGetComponent(out PlayerCammara cameraRig))
                cameraRig.RagdollTarget = ragdoll.transform;
            return;
        }

        if (TryGetComponent(out Rigidbody body))
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.None;
            body.AddTorque(transform.right * 2f, ForceMode.VelocityChange);
            if (direction.sqrMagnitude > 0.0001f)
                body.AddForce(direction.normalized * 2f, ForceMode.VelocityChange);
        }
    }

    public void RestartGame()
    {
        if (restarting)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex < 0)
        {
            Debug.LogError("RestartGame: add the active scene to the build scene list.", this);
            return;
        }
        restarting = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene.buildIndex);
    }
}
