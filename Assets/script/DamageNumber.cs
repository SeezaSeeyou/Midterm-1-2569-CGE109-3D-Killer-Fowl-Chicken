using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class DamageNumber : MonoBehaviour
{
    private static Canvas canvas;
    private Text label;
    private Vector3 worldPosition;
    private float startTime;
    private float duration;
    private Color color;
    private bool playerDamage;
    private Vector2 screenOffset;
    private RectTransform uiAnchor;

    public static void Show(Transform target, float damage, Color color, float duration, int fontSize, bool playerDamage = false, RectTransform anchor = null)
    {
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Damage Numbers", typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
        }

        // shortcut: one UI object per hit, use pooling if large battles cause allocation spikes.
        GameObject number = new GameObject("Damage Number", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(DamageNumber));
        number.transform.SetParent(anchor != null ? anchor : canvas.transform, false);
        RectTransform rect = number.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(180f, 60f);
        if (anchor != null)
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        Text text = number.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = damage.ToString("0.##", CultureInfo.InvariantCulture);
        text.fontSize = Mathf.Max(1, fontSize);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        Outline outline = number.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        DamageNumber popup = number.GetComponent<DamageNumber>();
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        popup.worldPosition = target.position;
        if (targetCollider != null)
            popup.worldPosition.y = targetCollider.bounds.max.y;
        popup.worldPosition += Vector3.up * 0.3f + Vector3.right * Random.Range(-0.2f, 0.2f);
        popup.label = text;
        popup.color = color;
        popup.playerDamage = playerDamage;
        popup.uiAnchor = anchor;
        popup.screenOffset = new Vector2(Random.Range(-35f, 35f), Random.Range(-12f, 12f));
        popup.duration = Mathf.Max(0.05f, duration);
        popup.startTime = Time.time;
    }

    private void LateUpdate()
    {
        float progress = (Time.time - startTime) / duration;
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (canvas == null)
        {
            label.enabled = false;
            return;
        }

        if (uiAnchor != null)
        {
            ((RectTransform)transform).anchoredPosition = new Vector2(35f, progress * 45f) + screenOffset * 0.2f;
            label.enabled = true;
        }
        else if (playerDamage)
        {
            RectTransform canvasRect = (RectTransform)canvas.transform;
            ((RectTransform)transform).anchoredPosition = new Vector2(canvasRect.rect.width * 0.15f,
                canvasRect.rect.height * 0.08f + progress * 80f) + screenOffset;
            label.enabled = true;
        }
        else
        {
            Camera cameraToUse = Camera.main;
            if (cameraToUse == null)
            {
                label.enabled = false;
                return;
            }
            Vector3 screen = cameraToUse.WorldToScreenPoint(worldPosition + Vector3.up * (progress * 0.8f));
            label.enabled = screen.z > 0f;
            if (!label.enabled)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, null, out Vector2 position);
            ((RectTransform)transform).anchoredPosition = position;
        }
        label.color = new Color(color.r, color.g, color.b, color.a * (1f - progress));
    }
}
