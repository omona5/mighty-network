using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================================
// TitleScreen: 타이틀 씬 — 브랜드 + 게임 시작 → 게임 씬 로드
// ============================================================================
public class TitleScreen : MonoBehaviour
{
    [SerializeField] private string gameSceneName = GameScenes.Game;

    private bool loading;
    private Canvas titleCanvas;
    private Vector2 lastViewport;
    private ServerConnection connection;
    private Button singleButton;
    private Button multiButton;
    private Button retryButton;
    private TextMeshProUGUI connectionLabel;

    private void LateUpdate()
    {
        if (titleCanvas == null) return;
        Vector2 viewport = new Vector2(ResponsiveCanvas.ViewWidth, ResponsiveCanvas.ViewHeight);
        if (viewport == lastViewport) return;
        lastViewport = viewport;
        Canvas.ForceUpdateCanvases();
        float unit = UiFonts.MenuScale / Mathf.Max(0.01f, titleCanvas.scaleFactor);
        bool portrait = ResponsiveCanvas.IsPortrait;
        float width = Mathf.Min(portrait ? 342f : 620f, ResponsiveCanvas.SafeArea.width / UiFonts.MenuScale - 32f);
        unit *= Mathf.Min(1f, ResponsiveCanvas.SafeArea.height / UiFonts.MenuScale / 610f);
        SetTitleElement("Glow", width, 590f, -32f, 0, unit);
        SetTitleElement("Eyebrow", width - 32f, 24f, 210f, 12, unit);
        SetTitleElement("Title", width - 32f, 88f, 154f, portrait ? 52 : 64, unit);
        SetTitleElement("Subtitle", width - 32f, 36f, 92f, 18, unit);
        string[] buttons = { "SinglePlayerButton", "MultiplayerButton", "TutorialButton", "SettingsButton" };
        for (int i = 0; i < buttons.Length; i++)
            SetTitleElement(buttons[i], portrait ? width - 48f : 280f, 48f, 28f - i * 60f, 18, unit);
        SetTitleElement("ConnectionStatus", width - 32f, 32f, -207f, 16, unit);
        SetTitleElement("RetryButton", portrait ? width - 48f : 280f, 42f, -250f, 16, unit);
    }

    private void SetTitleElement(string name, float width, float height, float y, int fontSize, float unit)
    {
        RectTransform element = ResponsiveCanvas.Content(titleCanvas).Find(name) as RectTransform;
        if (element == null) return;
        element.sizeDelta = new Vector2(width, height) * unit;
        // The complete panel is centered at y=-32 in the authored layout.
        // Center that panel (including connection controls) in landscape.
        float centerOffset = ResponsiveCanvas.IsPortrait ? 0f : 32f;
        element.anchoredPosition = new Vector2(0f, (y + centerOffset) * unit);
        TextMeshProUGUI text = element.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null && fontSize > 0)
        {
            text.fontSize = fontSize * unit;
            if (text.enableAutoSizing)
            {
                text.fontSizeMax = fontSize * unit;
                text.fontSizeMin = fontSize * unit * 0.7f;
            }
        }
    }

    private void Awake()
    {
        GameSettings.Initialize();
        EnsureBasics();
        Sfx.Ensure();
        BuildUi();
        connection = ServerConnection.Ensure();
        // Entering the title is an intentional exit, never a room resume.
        PlayerPrefs.DeleteKey("reconnectToken");
        PlayerPrefs.DeleteKey("roomId");
        PlayerPrefs.Save();
        if (connection.IsReady) connection.Send("{\"type\":\"leave_room\",\"data\":{}}");
        connection.Changed += RefreshConnection;
        GameSettings.LanguageChanged += RefreshConnection;
        RefreshConnection();
    }

    private void OnDestroy()
    {
        if (connection != null) connection.Changed -= RefreshConnection;
        GameSettings.LanguageChanged -= RefreshConnection;
    }

    private void RefreshConnection()
    {
        bool ready = connection != null && connection.IsReady;
        singleButton.interactable = multiButton.interactable = ready && !loading;
        retryButton.interactable = !ready && connection != null;
        connectionLabel.text = L10n.Text(ready ? "서버 연결됨" : connection != null && connection.IsConnecting
            ? "서버 연결 중..." : "서버 연결 안 됨 · 5초마다 재시도");
        connectionLabel.color = ready ? MightyTheme.Ink : MightyTheme.Muted;
        foreach (var button in new[] { singleButton, multiButton })
            button.GetComponentInChildren<TextMeshProUGUI>().color = button.interactable ? MightyTheme.Ink : MightyTheme.Muted;
    }

    public void StartGame()
    {
        LoadGame(false);
    }

    public void StartSinglePlayer()
    {
        LoadGame(true);
    }

    private void LoadGame(bool singlePlayer)
    {
        if (loading || connection == null || !connection.IsReady) return;
        loading = true;
        RefreshConnection();
        GameScenes.StartSinglePlayer = singlePlayer;
        Sfx.Title();
        string scene = string.IsNullOrEmpty(gameSceneName) ? GameScenes.Game : gameSceneName;
        SceneManager.LoadScene(scene);
    }

    private void EnsureBasics()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(null);
        }

        if (Camera.main == null && FindFirstObjectByType<Camera>() == null)
        {
            GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            Camera cam = camGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = MightyTheme.Table;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
        }
    }

    private void BuildUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject(
                "TitleCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels =
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;
        }
        else
        {
            canvas.additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;
        }

        ResponsiveCanvas.Ensure(canvas);
        titleCanvas = canvas;
        Canvas.ForceUpdateCanvases();
        RectTransform canvasRect = ResponsiveCanvas.Content(canvas) as RectTransform;
        float viewportW = canvasRect != null && canvasRect.rect.width > 1f
            ? canvasRect.rect.width : ResponsiveCanvas.LandscapeReference.x;
        float viewportH = canvasRect != null && canvasRect.rect.height > 1f
            ? canvasRect.rect.height : ResponsiveCanvas.LandscapeReference.y;
        float contentW = Mathf.Max(320f, viewportW - UiFonts.Layout(48f));
        float contentH = Mathf.Max(480f, viewportH - UiFonts.Layout(48f));

        // 전체 배경
        Image bg = MakeStretchImage(ResponsiveCanvas.Content(canvas), "Bg", MightyTheme.Table);
        bg.raycastTarget = false;

        // 은은한 중앙 글로우 패널
        GameObject glowGo = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGo.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        RectTransform glowRt = glowGo.GetComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0.5f, 0.5f);
        glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.pivot = new Vector2(0.5f, 0.5f);
        glowRt.sizeDelta = new Vector2(
            Mathf.Min(UiFonts.Layout(720f), contentW),
            Mathf.Min(UiFonts.Layout(420f), contentH));
        glowRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(20f));
        Image glow = glowGo.GetComponent<Image>();
        glow.color = MightyTheme.Panel;
        glow.raycastTarget = false;

        TextMeshProUGUI eyebrow = UiTmp.Create(
            ResponsiveCanvas.Content(canvas), "Eyebrow", UiFonts.Size(14), TextAnchor.MiddleCenter,
            MightyTheme.Muted);
        RectTransform eyebrowRt = eyebrow.rectTransform;
        eyebrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        eyebrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        eyebrowRt.pivot = new Vector2(0.5f, 0.5f);
        eyebrowRt.sizeDelta = new Vector2(contentW - UiFonts.Layout(32f), UiFonts.Layout(30f));
        eyebrowRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(150f));
        eyebrow.text = "ONLINE CARD TABLE";

        TextMeshProUGUI title = UiTmp.Create(
            ResponsiveCanvas.Content(canvas), "Title", UiFonts.Size(72), TextAnchor.MiddleCenter,
            MightyTheme.Accent);
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(
            Mathf.Min(UiFonts.Layout(900f), contentW - UiFonts.Layout(32f)),
            UiFonts.Layout(110f));
        titleRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(90f));
        title.text = "MIGHTY";
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI sub = UiTmp.Create(
            ResponsiveCanvas.Content(canvas), "Subtitle", UiFonts.Size(22), TextAnchor.MiddleCenter,
            MightyTheme.Ink);
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.sizeDelta = new Vector2(
            Mathf.Min(UiFonts.Layout(700f), contentW - UiFonts.Layout(32f)),
            UiFonts.Layout(40f));
        subRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(20f));
        LocalizedLabel.Bind(sub, "5인용 전략 카드 게임");

        Transform menuParent = ResponsiveCanvas.Content(canvas);
        Vector2 menuSize = new Vector2(280f, 48f);
        singleButton = MakeButton(menuParent, "SinglePlayerButton", "싱글플레이", menuSize, Vector2.zero, StartSinglePlayer);
        multiButton = MakeButton(menuParent, "MultiplayerButton", "멀티플레이", menuSize, Vector2.zero, StartGame);
        MakeButton(menuParent, "TutorialButton", "튜토리얼 (준비 중)", menuSize, Vector2.zero, null);
        MakeButton(menuParent, "SettingsButton", "설정", menuSize, Vector2.zero, () => SettingsPanel.Open(titleCanvas));
        connectionLabel = UiTmp.Create(menuParent, "ConnectionStatus", 16, TextAnchor.MiddleCenter, MightyTheme.Muted);
        connectionLabel.rectTransform.anchorMin = connectionLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        connectionLabel.enableAutoSizing = true;
        retryButton = MakeButton(menuParent, "RetryButton", "재접속 시도", menuSize, Vector2.zero, () => connection?.RetryNow());
        lastViewport = new Vector2(-1f, -1f);
        LateUpdate();
    }

    private static Image MakeStretchImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static Button MakeButton(
        Transform parent, string name, string label, Vector2 size, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(
            name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image img = go.GetComponent<Image>();
        img.color = MightyTheme.Primary;
        img.raycastTarget = true;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = MightyTheme.PrimaryHover;
        colors.pressedColor = MightyTheme.Accent;
        btn.colors = colors;
        btn.interactable = onClick != null;
        if (onClick != null) btn.onClick.AddListener(onClick);

        TextMeshProUGUI text = UiTmp.Create(
            go.transform, "Label", UiFonts.Size(28), TextAnchor.MiddleCenter, Color.white);
        RectTransform trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        LocalizedLabel.Bind(text, label);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = 28;
        if (!btn.interactable) text.color = MightyTheme.Muted;
        text.raycastTarget = false;

        return btn;
    }
}
