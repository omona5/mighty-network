using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// GameRuleHud: 좌상단 — 기루다 / 마이티 / 조커콜 / 프렌드카드 / 주공팀·공약
// ============================================================================
public class GameRuleHud : MonoBehaviour
{
    public RectTransform root;

    private TextMeshProUGUI bodyText;
    private Image trumpIcon;
    private Image mightyIcon;
    private Image jokerCallIcon;
    private Image friendCardIcon;
    private TextMeshProUGUI friendNoneText;
    private CanvasGroup canvasGroup;
    private const int LayoutRev = 9;
    private int builtRev;
    private Info currentInfo;
    private Vector2 layoutScreen;
    private Rect layoutSafeArea;
    private float topClearancePixels;

    // IMGUI corner controls use screen pixels; convert their occupied height
    // into canvas units so portrait layouts keep the same gap at any resolution.
    public void SetTopClearance(float screenPixels)
    {
        if (Mathf.Approximately(topClearancePixels, screenPixels)) return;
        topClearancePixels = screenPixels;
        if (root != null && root.gameObject.activeSelf) ApplyResponsiveLayout();
    }

    private void LateUpdate()
    {
        if (root != null && root.gameObject.activeSelf
            && (layoutScreen != new Vector2(ResponsiveCanvas.ViewWidth, ResponsiveCanvas.ViewHeight) || layoutSafeArea != ResponsiveCanvas.SafeArea))
            ApplyResponsiveLayout();
    }

    private const float PadL = 12f;
    private const float PadR = 12f;
    private const float LabelColW = 118f; // "프렌드카드" 한 줄

    public struct Info
    {
        public string trumpSuit;
        public bool noTrump;
        public string mightyCardId;
        public string jokerCallCardId;
        public string declarerLabel;
        public string friendLabel;
        public string friendCardId;
        public bool friendCardNone;
        public string teamScoreLabel;
        public string bidLabel;
        public bool visible;
    }

    public void Set(Info info)
    {
        currentInfo = info;
        EnsureUi();
        if (root == null) return;

        if (!info.visible)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            root.gameObject.SetActive(false);
            return;
        }

        root.gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        IconGui.Apply(trumpIcon, IconSpriteAtlas.GetTrump(info.noTrump, info.trumpSuit));
        IconGui.Apply(mightyIcon, IconSpriteAtlas.GetCard(info.mightyCardId));
        IconGui.Apply(jokerCallIcon, IconSpriteAtlas.GetCard(info.jokerCallCardId));
        if (info.friendCardNone)
        {
            IconGui.Apply(friendCardIcon, default(IconSpriteAtlas.Slice));
            if (friendNoneText != null)
            {
                friendNoneText.gameObject.SetActive(true);
                LocalizedLabel.Bind(friendNoneText, "없음");
            }
        }
        else
        {
            IconGui.Apply(friendCardIcon, IconSpriteAtlas.GetCard(info.friendCardId));
            if (friendNoneText != null)
                friendNoneText.gameObject.SetActive(false);
        }

        if (bodyText != null)
        {
            bodyText.text =
                L10n.Text("주공팀  ") + NullDash(info.teamScoreLabel) + "\n"
                + L10n.Text("공약    ") + NullDash(info.bidLabel);
        }

        ApplyPanelSize(info.friendCardNone);
        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        layoutScreen = new Vector2(ResponsiveCanvas.ViewWidth, ResponsiveCanvas.ViewHeight);
        layoutSafeArea = ResponsiveCanvas.SafeArea;
        bool portrait = ResponsiveCanvas.IsPortrait;
        Canvas canvas = root.GetComponentInParent<Canvas>();
        float scale = Mathf.Max(0.001f, canvas.scaleFactor);
        Rect safe = ResponsiveCanvas.SafeArea;
        float width = portrait ? Mathf.Min(840f, safe.width / scale - 240f) : 280f;
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(portrait ? 0.5f : 0f, 1f);
        root.localScale = Vector3.one;
        root.anchoredPosition = new Vector2(portrait
            ? (safe.center.x - ResponsiveCanvas.ViewWidth * 0.5f) / scale : safe.xMin / scale + 20f,
            -(ResponsiveCanvas.ViewHeight - safe.yMax) / scale
                - (portrait ? Mathf.Max(156f, topClearancePixels / scale) : 20f));
        root.sizeDelta = new Vector2(width, portrait ? 256f : 280f);
        TextMeshProUGUI heading = root.Find("Title").GetComponent<TextMeshProUGUI>();
        heading.fontSize = portrait ? 34 : 26;
        heading.alignment = TextAlignmentOptions.Center;
        heading.rectTransform.sizeDelta = new Vector2(-24f, 44f);
        bodyText.text = L10n.Text("주공팀 ") + NullDash(currentInfo.teamScoreLabel)
            + (portrait ? L10n.Text("    ·    공약 ") : L10n.Text("\n공약 ")) + NullDash(currentInfo.bidLabel);
        bodyText.fontSize = portrait ? 36 : 24;
        bodyText.alignment = TextAlignmentOptions.Center;
        bodyText.rectTransform.sizeDelta = new Vector2(-24f, portrait ? 54f : 64f);
        Image[] icons = { trumpIcon, mightyIcon, jokerCallIcon, friendCardIcon };
        for (int i = 0; i < icons.Length; i++)
        {
            RectTransform row = (RectTransform)icons[i].transform.parent;
            row.anchorMin = row.anchorMax = new Vector2(0f, 1f);
            row.anchoredPosition = portrait ? new Vector2(i * width / 4f, -54f) : new Vector2(12f + (i % 2) * 128f, -50f - (i / 2) * 72f);
            row.sizeDelta = new Vector2(portrait ? width / 4f : 128f, portrait ? 134f : 72f);
            TextMeshProUGUI label = row.Find("Label").GetComponent<TextMeshProUGUI>();
            label.fontSize = portrait ? 30 : 22;
            label.enableAutoSizing = true;
            label.fontSizeMax = portrait ? 30 : 22;
            label.fontSizeMin = portrait ? 20 : 16;
            label.alignment = TextAlignmentOptions.Center;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = Vector2.zero;
            lr.sizeDelta = new Vector2(portrait ? width / 4f : 128f, portrait ? 40f : 28f);
            RectTransform ir = icons[i].rectTransform;
            ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
            ir.anchoredPosition = new Vector2(0f, portrait ? -26f : -14f);
            ir.sizeDelta = (i == 0 ? new Vector2(56f, 56f) : new Vector2(100f, 50f)) * (portrait ? 1f : 0.72f);
        }
        friendNoneText.fontSize = portrait ? 32 : 26;
        friendNoneText.rectTransform.anchorMin = friendNoneText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        friendNoneText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        friendNoneText.rectTransform.anchoredPosition = new Vector2(0f, portrait ? -26f : -14f);
        friendNoneText.rectTransform.sizeDelta = new Vector2(80f, 50f);
    }

    public void Clear()
    {
        Set(new Info { visible = false });
    }

    private static string NullDash(string s)
    {
        return string.IsNullOrEmpty(s) ? "-" : s;
    }

    private void ApplyPanelSize(bool friendNone)
    {
        if (root == null) return;
        Vector2 cardSz = IconSpriteAtlas.DisplayCard;
        float iconRight = PadL + LabelColW + 8f + cardSz.x;
        float w = friendNone
            ? iconRight + 8f + UiFonts.Layout(48f) + PadR // "없음"
            : iconRight + PadR;

        Vector2 sqSz = IconSpriteAtlas.DisplaySquare;
        float rowH = Mathf.Max(cardSz.y, sqSz.y, UiFonts.Layout(22f)) + 8f;
        float titleH = UiFonts.Layout(26f);
        float bodyH = UiFonts.Layout(44f);
        float y0 = -(titleH + 6f);
        float h = -y0 + rowH * 4f + bodyH + 16f;
        root.sizeDelta = new Vector2(w, h);
    }

    private void EnsureUi()
    {
        if (root != null && trumpIcon != null && bodyText != null && builtRev == LayoutRev) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[GameRuleHud] Canvas를 찾을 수 없습니다.");
            return;
        }

        if (root != null) Destroy(root.gameObject);

        GameObject go = new GameObject("GameRuleHud", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        bool portrait = ResponsiveCanvas.IsPortrait;
        root.anchoredPosition = new Vector2(UiFonts.Layout(portrait ? 8f : 12f), -UiFonts.Layout(portrait ? 8f : 12f));
        root.localScale = Vector3.one * (portrait ? 0.82f : 1f);
        canvasGroup = go.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        root.SetAsLastSibling();

        GameObject bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bgGo.transform.SetParent(root, false);
        RectTransform bgrt = bgGo.GetComponent<RectTransform>();
        bgrt.anchorMin = Vector2.zero;
        bgrt.anchorMax = Vector2.one;
        bgrt.offsetMin = Vector2.zero;
        bgrt.offsetMax = Vector2.zero;
        Image bg = bgGo.GetComponent<Image>();
        bg.color = MightyTheme.Panel;
        bg.raycastTarget = false;

        TextMeshProUGUI title = UiTmp.Create(
            root, "Title", UiFonts.Size(15), TextAnchor.MiddleLeft,
            MightyTheme.Accent);
        RectTransform titRt = title.rectTransform;
        titRt.anchorMin = new Vector2(0f, 1f);
        titRt.anchorMax = new Vector2(1f, 1f);
        titRt.pivot = new Vector2(0.5f, 1f);
        titRt.anchoredPosition = new Vector2(0f, -6f);
        titRt.sizeDelta = new Vector2(-16f, UiFonts.Layout(26f));
        LocalizedLabel.Bind(title, "판 정보");

        Vector2 cardSz = IconSpriteAtlas.DisplayCard;
        Vector2 sqSz = IconSpriteAtlas.DisplaySquare;
        float rowH = Mathf.Max(cardSz.y, sqSz.y, UiFonts.Layout(22f)) + 8f;
        float y0 = -(UiFonts.Layout(26f) + 6f);
        trumpIcon = MakeLabeledIcon(root, "Trump", "기루다", new Vector2(PadL, y0), sqSz);
        mightyIcon = MakeLabeledIcon(root, "Mighty", "마이티", new Vector2(PadL, y0 - rowH), cardSz);
        jokerCallIcon = MakeLabeledIcon(root, "JokerCall", "조커콜", new Vector2(PadL, y0 - rowH * 2f), cardSz);
        friendCardIcon = MakeLabeledIcon(root, "FriendCard", "프렌드카드", new Vector2(PadL, y0 - rowH * 3f), cardSz);
        float noneX = LabelColW + 8f + cardSz.x + 8f;
        friendNoneText = MakeInlineLabel(friendCardIcon.transform.parent, "None", L10n.Text("없음"), new Vector2(noneX, 0f));
        friendNoneText.gameObject.SetActive(false);

        bodyText = UiTmp.Create(
            root, "Body", UiFonts.Size(14), TextAnchor.UpperLeft, MightyTheme.Ink);
        RectTransform bodyRt = bodyText.rectTransform;
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 0f);
        bodyRt.pivot = new Vector2(0.5f, 0f);
        bodyRt.anchoredPosition = new Vector2(0f, 8f);
        bodyRt.sizeDelta = new Vector2(-20f, UiFonts.Layout(44f));
        bodyText.lineSpacing = 1.05f;

        ApplyPanelSize(false);
        root.gameObject.SetActive(false);
        builtRev = LayoutRev;
    }

    private Image MakeLabeledIcon(RectTransform parent, string name, string label, Vector2 pos, Vector2 iconSize)
    {
        GameObject row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rt = row.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(-PadL - PadR, Mathf.Max(iconSize.y, UiFonts.Layout(22f)));

        TextMeshProUGUI t = UiTmp.Create(
            row.transform, "Label", UiFonts.Size(13), TextAnchor.MiddleLeft, MightyTheme.Muted);
        RectTransform lrt = t.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(LabelColW, 0f);
        LocalizedLabel.Bind(t, label);

        Image img = IconGui.MakeImage(row.transform, "Icon", default(IconSpriteAtlas.Slice), iconSize);
        RectTransform irt = img.rectTransform;
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(LabelColW + 8f, 0f);
        return img;
    }

    private TextMeshProUGUI MakeInlineLabel(Transform parent, string name, string text, Vector2 pos)
    {
        TextMeshProUGUI t = UiTmp.Create(
            parent, name, UiFonts.Size(14), TextAnchor.MiddleLeft, MightyTheme.Ink);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(UiFonts.Layout(48f), UiFonts.Layout(24f));
        t.text = text;
        return t;
    }
}
