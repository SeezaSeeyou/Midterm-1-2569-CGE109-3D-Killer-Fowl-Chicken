using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerConfig health;
    [SerializeField] private PlayerShoot shoot;
    [SerializeField] private Text hpText;
    [SerializeField] private Text ammoText;
    [SerializeField] private RectTransform hpFill;

    private void LateUpdate()
    {
        if (health != null && hpText != null && hpFill != null)
        {
            hpText.text = health.IsDead() ? "DEAD <size=20>R: Restart</size>"
                : $"{Mathf.CeilToInt(Mathf.Max(0f, health.HP))} <size=20>/ {Mathf.CeilToInt(health.MaxHP)}</size>";
            float ratio = health.MaxHP > 0f ? Mathf.Clamp01(health.HP / health.MaxHP) : 0f;
            hpFill.anchorMax = new Vector2(1f, ratio);
        }
        if (shoot != null)
        {
            if (ammoText != null)
                ammoText.text = $"{shoot.CurrentAmmo} <color=#AAA797>/ {shoot.MagazineSize}</color>";
        }
    }
}
