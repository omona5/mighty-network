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

    private void Awake()
    {
        EnsureBasics();
        Sfx.Ensure();
        BuildUi();
    }

    public void StartGame()
    {
        if (loading) return;
        loading = true;
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
            cam.backgroundColor = new Color(0.04f, 0.22f, 0.14f, 1f);
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

        // 전체 배경
        Image bg = MakeStretchImage(canvas.transform, "Bg", new Color(0.04f, 0.18f, 0.12f, 1f));
        bg.raycastTarget = false;

        // 은은한 중앙 글로우 패널
        GameObject glowGo = new GameObject("Glow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glowGo.transform.SetParent(canvas.transform, false);
        RectTransform glowRt = glowGo.GetComponent<RectTransform>();
        glowRt.anchorMin = new Vector2(0.5f, 0.5f);
        glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.pivot = new Vector2(0.5f, 0.5f);
        glowRt.sizeDelta = new Vector2(UiFonts.Layout(720f), UiFonts.Layout(420f));
        glowRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(20f));
        Image glow = glowGo.GetComponent<Image>();
        glow.color = new Color(0f, 0f, 0f, 0.45f);
        glow.raycastTarget = false;

        TextMeshProUGUI title = UiTmp.Create(
            canvas.transform, "Title", UiFonts.Size(72), TextAnchor.MiddleCenter,
            new Color(1f, 0.92f, 0.55f, 1f));
        RectTransform titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(UiFonts.Layout(900f), UiFonts.Layout(110f));
        titleRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(90f));
        title.text = "Mighty";
        title.fontStyle = FontStyles.Bold;

        TextMeshProUGUI sub = UiTmp.Create(
            canvas.transform, "Subtitle", UiFonts.Size(22), TextAnchor.MiddleCenter,
            new Color(0.85f, 0.9f, 0.82f, 0.95f));
        RectTransform subRt = sub.rectTransform;
        subRt.anchorMin = new Vector2(0.5f, 0.5f);
        subRt.anchorMax = new Vector2(0.5f, 0.5f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.sizeDelta = new Vector2(UiFonts.Layout(700f), UiFonts.Layout(40f));
        subRt.anchoredPosition = new Vector2(0f, UiFonts.Layout(20f));
        sub.text = "5인 마이티";

        Button startBtn = MakeButton(
            canvas.transform,
            "게임 시작",
            new Vector2(UiFonts.Layout(280f), UiFonts.Layout(64f)),
            new Vector2(0f, UiFonts.Layout(-80f)),
            StartGame);

        startBtn.transform.SetAsLastSibling();
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
        Transform parent, string label, Vector2 size, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(
            "StartButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        Image img = go.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.12f, 0.92f);
        img.raycastTarget = true;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(0.22f, 0.22f, 0.18f, 1f);
        colors.pressedColor = new Color(0.08f, 0.08f, 0.08f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        TextMeshProUGUI text = UiTmp.Create(
            go.transform, "Label", UiFonts.Size(28), TextAnchor.MiddleCenter, Color.white);
        RectTransform trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        text.text = label;
        text.raycastTarget = false;

        return btn;
    }
}
