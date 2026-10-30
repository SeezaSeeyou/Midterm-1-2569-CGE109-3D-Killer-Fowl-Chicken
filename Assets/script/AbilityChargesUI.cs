using UnityEngine;
using UnityEngine.UI;

public class AbilityChargesUI : MonoBehaviour
{
    [SerializeField] private PlayerAbilities abilities;
    [SerializeField] private Image[] chargeSlots = new Image[4];
    [SerializeField] private Color filledColor = new Color(0.2f, 0.85f, 1f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private void Update()
    {
        if (abilities == null || chargeSlots == null)
            return;

        int charges = abilities.CurrentAbilityCharges;
        float progress = abilities.AbilityChargeProgress;
        for (int i = 0; i < chargeSlots.Length; i++)
        {
            if (chargeSlots[i] == null)
                continue;

            chargeSlots[i].color = i < charges
                ? filledColor
                : i == charges ? Color.Lerp(emptyColor, filledColor, progress) : emptyColor;
        }
    }
}
