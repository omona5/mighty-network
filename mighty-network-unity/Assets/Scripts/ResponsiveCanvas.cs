using UnityEngine;
using UnityEngine.UI;

// Fill the browser between 9:16 and 16:9; letterbox only outside those limits.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class ResponsiveCanvas : MonoBehaviour
{
    public static readonly Vector2 LandscapeReference = new Vector2(1920f, 1080f);
    public static readonly Vector2 PortraitReference = new Vector2(1080f, 1920f);
    private CanvasScaler scaler;
    private RectTransform content;
    private RectTransform backdrop;
    private int lastWidth;
    private int lastHeight;

    public static bool IsPortrait => Screen.height > Screen.width;
    public static Rect Viewport
    {
        get { return CalculateViewport(Screen.width, Screen.height); }
    }
    public static Rect CalculateViewport(float width, float height)
    {
        width = Mathf.Max(1f, width);
        height = Mathf.Max(1f, height);
        float aspect = Mathf.Clamp(width / height, 9f / 16f, 16f / 9f);
        float w = Mathf.Min(width, height * aspect);
        float h = Mathf.Min(height, w / aspect);
        return new Rect((width - w) * 0.5f, (height - h) * 0.5f, w, h);
    }
    public static float ViewWidth => Viewport.width;
    public static float ViewHeight => Viewport.height;
    // Safe-area coordinates are local to the game viewport, with a bottom-left origin.
    public static Rect SafeArea
    {
        get
        {
            Rect v = Viewport;
            Rect safe = Screen.safeArea;
            float left = Mathf.Clamp(safe.xMin - v.xMin, 0f, v.width);
            float bottom = Mathf.Clamp(safe.yMin - v.yMin, 0f, v.height);
            float right = Mathf.Clamp(safe.xMax - v.xMin, left, v.width);
            float top = Mathf.Clamp(safe.yMax - v.yMin, bottom, v.height);
            return Rect.MinMaxRect(left, bottom, right, top);
        }
    }

    public static ResponsiveCanvas Ensure(Canvas canvas)
    {
        if (canvas == null) return null;
        ResponsiveCanvas responsive = canvas.GetComponent<ResponsiveCanvas>();
        if (responsive == null) responsive = canvas.gameObject.AddComponent<ResponsiveCanvas>();
        responsive.Apply();
        return responsive;
    }

    public static RectTransform Content(Canvas canvas)
    {
        if (canvas == null) return null;
        ResponsiveCanvas responsive = canvas.GetComponent<ResponsiveCanvas>();
        if (responsive == null) responsive = Ensure(canvas);
        if (responsive.content == null) responsive.Apply();
        return responsive.content;
    }

    private void Awake() { Apply(); }
    private void Update()
    {
        if (lastWidth != Screen.width || lastHeight != Screen.height) Apply();
    }

    private void Apply()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) return;
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        Vector2 reference = IsPortrait ? PortraitReference : LandscapeReference;
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        // Fit the authored controls without stretching cards. Extra space belongs
        // to the layout, rather than becoming black bars around a fixed frame.
        scaler.scaleFactor = Mathf.Min(ViewWidth / reference.x, ViewHeight / reference.y);
        canvas.scaleFactor = scaler.scaleFactor;

        if (content == null) content = transform.Find("GameViewport") as RectTransform;
        if (backdrop == null) backdrop = transform.Find("LetterboxBackground") as RectTransform;
        if (backdrop == null)
        {
            GameObject bg = new GameObject("LetterboxBackground", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            backdrop = bg.GetComponent<RectTransform>();
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            Image image = bg.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            bg.transform.SetAsFirstSibling();
        }
        if (content == null)
        {
            GameObject viewport = new GameObject("GameViewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(transform, false);
            content = viewport.GetComponent<RectTransform>();
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
            // Preserve authored hierarchy and references; runtime UI uses Content(canvas).
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child == content || child == backdrop) continue;
                child.SetParent(content, false);
                child.SetAsFirstSibling();
            }
        }
        backdrop.gameObject.SetActive(true);
        content.sizeDelta = new Vector2(ViewWidth, ViewHeight) / scaler.scaleFactor;
        content.anchoredPosition = Vector2.zero;
        lastWidth = Screen.width;
        lastHeight = Screen.height;
    }
}
