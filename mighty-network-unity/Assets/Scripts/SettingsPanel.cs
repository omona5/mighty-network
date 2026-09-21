using TMPro;
using UnityEngine;

public sealed class SettingsPanel : MonoBehaviour
{
    private static SettingsPanel active;
    public static bool IsOpen => active != null;
    private System.Action leaveRoom;
    private Canvas canvas;
    private RectTransform panel;
    private TextMeshProUGUI sfxVolumeLabel;
    private TextMeshProUGUI musicVolumeLabel;
    private UnityEngine.UI.Button korean;
    private UnityEngine.UI.Button english;

    public static void Open(Canvas canvas)
    {
        Create(canvas, null);
    }

    public static void OpenMenu(Canvas canvas, System.Action leaveRoom)
    {
        Create(canvas, leaveRoom);
    }

    private static void Create(Canvas canvas, System.Action leaveRoom)
    {
        if (canvas == null || IsOpen) return;
        GameSettings.Initialize();
        var overlay = new GameObject("SettingsOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster), typeof(SettingsPanel));
        overlay.transform.SetParent(canvas.transform, false);
        var rect = (RectTransform)overlay.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.8f);
        var view = overlay.GetComponent<SettingsPanel>();
        active = view;
        view.canvas = canvas;
        view.leaveRoom = leaveRoom;
        var overlayCanvas = overlay.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 30000;
        if (leaveRoom == null) view.Build();
        else view.BuildMenu();
    }

    private void BuildMenu()
    {
        panel = Box(transform, "GameMenu", new Vector2(350, 360), Vector2.zero, MightyTheme.Panel);
        Label(panel, "Heading", "메뉴", 28, new Vector2(300, 42), new Vector2(0, 120));
        Button(panel, "OpenSettings", "설정", new Vector2(0, 40), () =>
        {
            Destroy(panel.gameObject);
            Build();
        });
        Button(panel, "LeaveRoom", "방 나가기", new Vector2(0, -30), () =>
        {
            var action = leaveRoom;
            Close();
            action?.Invoke();
        });
        Button(panel, "Close", "닫기", new Vector2(0, -120), Close);
        LateUpdate();
    }

    private void Build()
    {
        panel = Box(transform, "SettingsPanel", new Vector2(350, 490), Vector2.zero, MightyTheme.Panel);
        Label(panel, "Heading", "설정", 28, new Vector2(300, 42), new Vector2(0, 200));
        sfxVolumeLabel = VolumeSlider("SfxVolume", "효과음", 130, GameSettings.SfxVolume, GameSettings.SetSfxVolume, GameSettings.SfxMuted, GameSettings.SetSfxMuted);
        musicVolumeLabel = VolumeSlider("MusicVolume", "배경음", 20, GameSettings.MusicVolume, GameSettings.SetMusicVolume, GameSettings.MusicMuted, GameSettings.SetMusicMuted);
        Label(panel, "LanguageTitle", "언어", 20, new Vector2(280, 32), new Vector2(0, -98));
        korean = Button(panel, "Korean", "한국어", new Vector2(-76, -142), () => Select("ko"));
        english = Button(panel, "English", "English", new Vector2(76, -142), () => Select("en"));
        Button(panel, "Close", "닫기", new Vector2(0, -204), Close);
        Refresh();
        LateUpdate();
    }

    private TextMeshProUGUI VolumeSlider(string name, string title, float y, float value, UnityEngine.Events.UnityAction<float> changed, bool muted, UnityEngine.Events.UnityAction<bool> muteChanged)
    {
        Label(panel, name + "Title", title, 20, new Vector2(140, 32), new Vector2(-70, y));
        var muteRow = Box(panel, name + "Mute", new Vector2(130, 40), new Vector2(85, y), Color.clear);
        var toggle = muteRow.gameObject.AddComponent<UnityEngine.UI.Toggle>();
        var checkBox = Box(muteRow, "CheckBox", new Vector2(30, 30), new Vector2(-46, 0), MightyTheme.PanelSoft);
        var check = Label(checkBox, "Check", "X", 24, new Vector2(30, 30), Vector2.zero);
        check.raycastTarget = false;
        toggle.targetGraphic = checkBox.GetComponent<UnityEngine.UI.Image>();
        toggle.graphic = check;
        toggle.SetIsOnWithoutNotify(muted);
        toggle.onValueChanged.AddListener(muteChanged);
        Label(muteRow, "Label", "음소거", 18, new Vector2(90, 36), new Vector2(19, 0));
        var valueLabel = Label(panel, name + "Value", "", 18, new Vector2(80, 32), new Vector2(110, y - 36));
        var track = Box(panel, name, new Vector2(200, 36), new Vector2(-36, y - 36), MightyTheme.PanelSoft);
        var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>();
        var fillArea = Box(track, "FillArea", new Vector2(180, 8), Vector2.zero, Color.clear);
        fillArea.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var fill = Box(fillArea, "Fill", Vector2.zero, Vector2.zero, MightyTheme.Accent);
        fill.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var handleArea = Box(track, "HandleArea", new Vector2(180, 36), Vector2.zero, Color.clear);
        handleArea.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var handle = Box(handleArea, "Handle", new Vector2(20, 32), Vector2.zero, MightyTheme.Ink);
        slider.fillRect = fill;
        slider.handleRect = handle;
        // Slider drives vertical stretch anchors; subtract the four-pixel inset
        // rather than adding handle height to the parent's 36-pixel height.
        handle.sizeDelta = new Vector2(20, -4);
        slider.targetGraphic = handle.GetComponent<UnityEngine.UI.Image>();
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v => { changed(v); Refresh(); });
        return valueLabel;
    }

    private void Select(string language) { GameSettings.SetLanguage(language); Refresh(); Sfx.UiClick(); }
    private void Close() { GameSettings.Save(); Sfx.UiClick(); Destroy(gameObject); }
    private void OnDestroy()
    {
        if (active == this) active = null;
        GameSettings.Save();
    }
    private void Refresh()
    {
        sfxVolumeLabel.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100) + "%";
        musicVolumeLabel.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100) + "%";
        korean.interactable = GameSettings.Language != "ko";
        english.interactable = GameSettings.Language != "en";
        korean.GetComponent<UnityEngine.UI.Image>().color = GameSettings.Language == "ko" ? MightyTheme.Accent : MightyTheme.Primary;
        english.GetComponent<UnityEngine.UI.Image>().color = GameSettings.Language == "en" ? MightyTheme.Accent : MightyTheme.Primary;
    }

    private void LateUpdate()
    {
        if (panel == null || canvas == null) return;
        Rect safe = ResponsiveCanvas.SafeArea;
        float pixels = Mathf.Min(UiFonts.MenuScale, Mathf.Min(safe.width / 374f, safe.height / (panel.sizeDelta.y + 24f)));
        panel.localScale = Vector3.one * pixels / Mathf.Max(0.01f, canvas.scaleFactor);
        panel.anchoredPosition = (safe.center - new Vector2(ResponsiveCanvas.ViewWidth, ResponsiveCanvas.ViewHeight) * 0.5f)
            / Mathf.Max(0.01f, canvas.scaleFactor);
    }

    private static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        go.GetComponent<UnityEngine.UI.Image>().color = color;
        return rect;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string source, int size, Vector2 dimensions, Vector2 position)
    {
        var text = UiTmp.Create(parent, name, size, TextAnchor.MiddleCenter, MightyTheme.Ink);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = dimensions;
        text.rectTransform.anchoredPosition = position;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14;
        text.fontSizeMax = size;
        LocalizedLabel.Bind(text, source);
        return text;
    }

    private static UnityEngine.UI.Button Button(Transform parent, string name, string source, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var rect = Box(parent, name, new Vector2(140, 44), position, MightyTheme.Primary);
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rect.GetComponent<UnityEngine.UI.Image>();
        button.onClick.AddListener(action);
        Label(rect, "Label", source, 18, new Vector2(132, 40), Vector2.zero);
        return button;
    }
}
