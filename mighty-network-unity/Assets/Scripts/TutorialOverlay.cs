using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuse the game's Canvas: other views locate it with FindFirstObjectByType.
public sealed class TutorialOverlay : MonoBehaviour
{
    private GameObject root;
    private RectTransform panel;
    private TextMeshProUGUI heading, body, hint;
    private UnityEngine.UI.Button confirm, restart, exit;
    private UnityEngine.UI.Image dim;
    private bool modal;
    private Vector2 lastSize;
    private Canvas hostCanvas;
    public bool IsVisible => root != null && root.activeSelf;

    public void Show(string title, string text, string task, bool paused, bool complete,
        UnityEngine.Events.UnityAction acknowledge, UnityEngine.Events.UnityAction retry,
        UnityEngine.Events.UnityAction leave)
    {
        EnsureUi();
        modal = paused;
        root.SetActive(true);
        dim.color = paused ? new Color(0, 0, 0, 0.68f) : Color.clear;
        dim.raycastTarget = paused;
        heading.text = title;
        body.text = text;
        hint.text = task;
        body.gameObject.SetActive(paused);
        hint.gameObject.SetActive(!paused);
        confirm.gameObject.SetActive(paused && !complete);
        restart.gameObject.SetActive(paused);
        foreach (var button in new[] { confirm, restart, exit }) button.onClick.RemoveAllListeners();
        confirm.onClick.AddListener(acknowledge);
        restart.onClick.AddListener(retry);
        exit.onClick.AddListener(leave);
        Layout();
    }

    public void Feedback(string message) { if (hint != null) hint.text = message; }
    public void Hide() { if (root != null) root.SetActive(false); }
    private void OnDestroy() { if (root != null) Destroy(root); }
    private void Update()
    {
        if (IsVisible && lastSize != new Vector2(Screen.width, Screen.height)) Layout();
    }
    private void LateUpdate() { if (IsVisible) root.transform.SetAsLastSibling(); }

    private void EnsureUi()
    {
        if (root != null) return;
        hostCanvas = FindFirstObjectByType<Canvas>();
        root = new GameObject("TutorialOverlay", typeof(RectTransform));
        root.transform.SetParent(hostCanvas.transform, false);
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        dim = Box(root.transform, "Dim", Color.clear).GetComponent<UnityEngine.UI.Image>();
        var dr = dim.rectTransform;
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = dr.offsetMax = Vector2.zero;
        Color panelColor = MightyTheme.Panel;
        panelColor.a = 1f;
        panel = Box(root.transform, "Lesson", panelColor);
        heading = Label("Heading", 23, MightyTheme.Accent);
        body = Label("Body", 19, MightyTheme.Ink);
        hint = Label("Task", 17, MightyTheme.Ink);
        confirm = Button("Confirm", "확인");
        restart = Button("Restart", "처음부터");
        exit = Button("Exit", "타이틀로");
    }

    private TextMeshProUGUI Label(string name, int size, Color color)
    {
        var t = UiTmp.Create(panel, name, size, TextAnchor.UpperLeft, color);
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = 13; t.fontSizeMax = size;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private UnityEngine.UI.Button Button(string name, string text)
    {
        var rt = Box(panel, name, MightyTheme.Table);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        var label = UiTmp.Create(rt, "Label", 17, TextAnchor.MiddleCenter, MightyTheme.Ink);
        label.text = text; label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static RectTransform Box(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<UnityEngine.UI.Image>().color = color;
        return (RectTransform)go.transform;
    }

    private void Layout()
    {
        lastSize = new Vector2(Screen.width, Screen.height);
        Rect safe = Screen.safeArea;
        float scale = Mathf.Clamp(Mathf.Min(safe.width / 390f, safe.height / 550f), 0.6f, 1.4f);
        float width = Mathf.Min(620f, safe.width / scale - 24f);
        float height = modal ? Mathf.Min(420f, safe.height / scale - 24f) : 104f;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        float canvasScale = Mathf.Max(0.001f, hostCanvas.scaleFactor);
        panel.localScale = Vector3.one * scale / canvasScale;
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = modal ? safe.center - lastSize * 0.5f
            : new Vector2(safe.center.x - Screen.width * 0.5f, safe.yMax - Screen.height * 0.5f - height * scale * 0.5f - 8f);
        panel.anchoredPosition /= canvasScale;
        Place(heading.rectTransform, 18, -16, width - 36, 42);
        Place(body.rectTransform, 18, -66, width - 36, height - 142);
        Place(hint.rectTransform, 18, -52, width - 132, 44);
        float buttonWidth = (width - 52) / 3;
        Place((RectTransform)confirm.transform, 18, -height + 62, buttonWidth, 44);
        Place((RectTransform)restart.transform, 26 + buttonWidth, -height + 62, buttonWidth, 44);
        Place((RectTransform)exit.transform, modal ? 34 + buttonWidth * 2 : width - 108,
            modal ? -height + 62 : -52, modal ? buttonWidth : 90, 44);
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }
}
