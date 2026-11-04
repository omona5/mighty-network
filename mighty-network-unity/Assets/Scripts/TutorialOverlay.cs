using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuse the game's Canvas: other views locate it with FindFirstObjectByType.
public sealed class TutorialOverlay : MonoBehaviour
{
    private GameObject root;
    private RectTransform panel;
    private TextMeshProUGUI heading, body, hint;
    private UnityEngine.UI.Button confirm, restart, exit, minimize;
    private UnityEngine.UI.Image dim;
    private bool modal;
    private bool inspecting;
    private bool collapsed;
    private bool showConfirm;
    private bool preparing;
    private Vector2 lastSize;
    private Canvas hostCanvas;
    private TMP_SpriteAsset roleSprites;
    public bool IsVisible => root != null && root.activeSelf;

    public void Show(string title, string text, string task, bool paused, bool complete,
        UnityEngine.Events.UnityAction acknowledge, UnityEngine.Events.UnityAction retry,
        UnityEngine.Events.UnityAction leave, UnityEngine.Events.UnityAction practice = null,
        bool inspectTable = false)
    {
        EnsureUi();
        preparing = false;
        Color panelColor = MightyTheme.Panel;
        panelColor.a = 1f;
        panel.GetComponent<UnityEngine.UI.Image>().color = panelColor;
        heading.color = MightyTheme.Accent;
        modal = paused;
        inspecting = paused && inspectTable;
        collapsed = false;
        showConfirm = paused && (!complete || practice != null);
        root.SetActive(true);
        dim.raycastTarget = paused;
        heading.text = RoleText(title);
        body.text = RoleText(text);
        hint.text = RoleText(task);
        LocalizedLabel.Bind(confirm.GetComponentInChildren<TextMeshProUGUI>(), complete ? "싱글플레이 시작" : "확인");
        foreach (var button in new[] { confirm, restart, exit }) button.onClick.RemoveAllListeners();
        confirm.onClick.AddListener(() => { Sfx.UiClick(); if (complete) practice?.Invoke(); else acknowledge?.Invoke(); });
        restart.onClick.AddListener(() => { Sfx.UiClick(); retry?.Invoke(); });
        exit.onClick.AddListener(() => { Sfx.UiClick(); leave?.Invoke(); });
        RefreshPresentation();
    }

    public void ShowPreparation(string message)
    {
        EnsureUi();
        preparing = true;
        modal = inspecting = collapsed = false;
        root.SetActive(true);
        dim.color = Color.clear;
        dim.raycastTarget = true;
        panel.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.92f);
        heading.color = Color.white;
        heading.text = L10n.Text(message);
        body.gameObject.SetActive(false);
        hint.gameObject.SetActive(false);
        foreach (var button in new[] { confirm, restart, exit, minimize }) button.gameObject.SetActive(false);
        Layout();
    }

    private void RefreshPresentation()
    {
        dim.color = modal && !inspecting && !collapsed ? new Color(0, 0, 0, 0.68f) : Color.clear;
        // Minimizing only changes presentation; the lesson still blocks game input.
        body.gameObject.SetActive(modal && !collapsed);
        hint.gameObject.SetActive(!modal);
        confirm.gameObject.SetActive(showConfirm && !collapsed);
        restart.gameObject.SetActive(modal && !collapsed);
        exit.gameObject.SetActive(!collapsed);
        minimize.gameObject.SetActive(modal && !inspecting);
        LocalizedLabel.Bind(minimize.GetComponentInChildren<TextMeshProUGUI>(), collapsed ? "펼치기" : "최소화");
        Layout();
    }

    public void Feedback(string message) { if (hint != null) hint.text = RoleText(message); }
    public void Hide() { if (root != null) root.SetActive(false); }
    private void OnDestroy()
    {
        if (root != null) Destroy(root);
        if (roleSprites != null) { Destroy(roleSprites.material); Destroy(roleSprites); }
    }
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
        CreateRoleSprites();
        heading = Label("Heading", 23, MightyTheme.Accent);
        body = Label("Body", 19, MightyTheme.Ink);
        hint = Label("Task", 17, MightyTheme.Ink);
        confirm = Button("Confirm", "확인");
        restart = Button("Restart", "처음부터");
        exit = Button("Exit", "타이틀로");
        minimize = Button("Minimize", "최소화");
        minimize.onClick.AddListener(() => { Sfx.UiClick(); collapsed = !collapsed; RefreshPresentation(); });
    }

    private TextMeshProUGUI Label(string name, int size, Color color)
    {
        var t = UiTmp.Create(panel, name, size, TextAnchor.UpperLeft, color);
        t.raycastTarget = false;
        t.enableAutoSizing = true;
        t.fontSizeMin = 13; t.fontSizeMax = size;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.richText = true;
        t.spriteAsset = roleSprites;
        return t;
    }

    private string RoleText(string text)
    {
        string value = L10n.Text(text) ?? "";
        // Result feedback combines independently localized paragraphs (counts,
        // actual winner and rule explanation), while authored lessons use full entries.
        if (value == text && text != null)
            value = string.Join("\n\n", System.Array.ConvertAll(
                text.Split(new[] { "\n\n" }, System.StringSplitOptions.None), L10n.Text));
        if (roleSprites == null) return value;
        // Inline glyphs wrap and scale with the sentence in either language.
        return System.Text.RegularExpressions.Regex.Replace(value,
            "회색 프렌드|\\b[Gg]ray friend\\b|주공|(?<!노)프렌드|프랜드|\\b[Dd]eclarer\\b|\\b[Ff]riend\\b", match =>
            {
                string word = match.Value;
                if (word == "회색 프렌드") return "회색 <sprite index=2>프렌드";
                if (word.ToLowerInvariant() == "gray friend") return word.Substring(0, 5) + "<sprite index=2>" + word.Substring(5);
                return "<sprite index=" + ((word == "주공" || word.ToLowerInvariant() == "declarer") ? "0" : "1") + ">" + word;
            });
    }

    private void CreateRoleSprites()
    {
        var slices = new[] { IconSpriteAtlas.GetDeclarer(), IconSpriteAtlas.GetFriend(), IconSpriteAtlas.GetFriendSecret() };
        if (!slices[0].IsValid || !slices[1].IsValid || !slices[2].IsValid) return;
        var defaultSprites = TMP_Settings.defaultSpriteAsset;
        if (defaultSprites == null || defaultSprites.material == null) return;
        // Clone a current-format asset: newly created TMP assets have an empty
        // version and trigger the legacy upgrader before glyphs can be added.
        roleSprites = Instantiate(defaultSprites);
        roleSprites.spriteCharacterTable.Clear();
        roleSprites.spriteGlyphTable.Clear();
        roleSprites.spriteSheet = slices[0].tex;
        // TMP's default sprite material retains the sprite shader in WebGL builds.
        roleSprites.material = new Material(defaultSprites.material);
        roleSprites.material.mainTexture = slices[0].tex;
        for (int i = 0; i < slices.Length; i++)
        {
            Rect r = slices[i].sprite.rect;
            var glyph = new TMP_SpriteGlyph((uint)i,
                new UnityEngine.TextCore.GlyphMetrics(r.width, r.height, 0, r.height * 0.85f, r.width),
                new UnityEngine.TextCore.GlyphRect((int)r.x, (int)r.y, (int)r.width, (int)r.height), 1, 0);
            roleSprites.spriteGlyphTable.Add(glyph);
            roleSprites.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)(0xE000 + i), glyph)
                { name = i == 0 ? "declarer" : i == 1 ? "friend" : "friend_secret", scale = 1 });
        }
        roleSprites.UpdateLookupTables();
    }

    private UnityEngine.UI.Button Button(string name, string text)
    {
        var rt = Box(panel, name, MightyTheme.Table);
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        var label = UiTmp.Create(rt, "Label", 17, TextAnchor.MiddleCenter, MightyTheme.Ink);
        LocalizedLabel.Bind(label, text); label.raycastTarget = false;
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
        bool sideInspection = inspecting && safe.width > safe.height * 1.2f;
        if (sideInspection) width = Mathf.Min(360f, safe.width * 0.27f / scale - 12f);
        float height = modal ? Mathf.Min(inspecting && !sideInspection ? 300f : 420f, safe.height / scale - 24f) : 104f;
        if (collapsed) height = 74f;
        if (preparing) height = 74f;
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
        float canvasScale = Mathf.Max(0.001f, hostCanvas.scaleFactor);
        panel.localScale = Vector3.one * scale / canvasScale;
        panel.sizeDelta = new Vector2(width, height);
        panel.anchoredPosition = modal && !inspecting && !collapsed ? safe.center - lastSize * 0.5f
            : new Vector2(safe.center.x - Screen.width * 0.5f, safe.yMax - Screen.height * 0.5f - height * scale * 0.5f - 8f);
        if (sideInspection) panel.anchoredPosition = new Vector2(
            safe.xMin + 8f + width * scale * 0.5f - Screen.width * 0.5f, safe.center.y - Screen.height * 0.5f);
        panel.anchoredPosition /= canvasScale;
        Place(heading.rectTransform, 18, -16, width - (modal && !inspecting ? 128 : 36), 42);
        Place((RectTransform)minimize.transform, width - 98, -14, 80, 44);
        Place(body.rectTransform, 18, -66, width - 36, Mathf.Max(1f, height - 142));
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
