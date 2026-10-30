using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class MainMenu : MonoBehaviour
{
    [SerializeField] private Texture2D background;
    [SerializeField] private Texture2D logo;
    [SerializeField] private string gameScene = "SampleScene";
    private bool entering;
    private Text prompt;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RawImage bg = CreateImage("Background", background, Vector2.zero, Vector2.one);
        bg.color = background != null ? Color.white : Color.black;
        RawImage title = CreateImage("Logo", logo, new Vector2(0.2f, 0.52f), new Vector2(0.8f, 0.95f));
        if (logo != null)
        {
            AspectRatioFitter fit = title.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)logo.width / logo.height;
        }
        GameObject label = new GameObject("Press Any Key", typeof(RectTransform));
        label.transform.SetParent(transform, false);
        prompt = label.AddComponent<Text>();
        prompt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        prompt.text = "Press any key to enter";
        prompt.fontSize = 48;
        prompt.alignment = TextAnchor.MiddleCenter;
        prompt.color = Color.white;
        prompt.raycastTarget = false;
        RectTransform rect = prompt.rectTransform;
        rect.anchorMin = new Vector2(0.1f, 0.13f);
        rect.anchorMax = new Vector2(0.9f, 0.25f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
    }

    private RawImage CreateImage(string name, Texture texture, Vector2 min, Vector2 max)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(transform, false);
        RawImage image = child.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
        image.rectTransform.anchorMin = min;
        image.rectTransform.anchorMax = max;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        return image;
    }

    private void Update()
    {
        bool pressed = (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            || (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed))
            || (Gamepad.current != null && (Gamepad.current.buttonSouth.isPressed || Gamepad.current.startButton.isPressed));
        if (pressed) entering = true;
        // Wait for release so the start key cannot also shoot or trigger an ability.
        if (entering && !pressed)
        {
            entering = false;
            if (!Application.CanStreamedLevelBeLoaded(gameScene))
            {
                Debug.LogError($"MainMenu: add {gameScene} to the build scene list.", this);
                return;
            }
            enabled = false;
            SceneManager.LoadScene(gameScene);
            return;
        }
        prompt.color = new Color(1, 1, 1, 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 2f));
    }

}
