using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// GameRuleHud: 좌상단 — 기루다 / 마이티 / 조커콜 / 프렌드카드 / 주공팀·공약
// ============================================================================
public class GameRuleHud : MonoBehaviour
{
    public RectTransform root;

    private Text bodyText;
    private Image trumpIcon;
    private Image mightyIcon;
    private Image jokerCallIcon;
    private Image friendCardIcon;
    private Text friendNoneText;
    private Font uiFont;
    private CanvasGroup canvasGroup;
    private const int LayoutRev = 8;
    private int builtRev;

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
                friendNoneText.text = "없음";
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
                "주공팀  " + NullDash(info.teamScoreLabel) + "\n"
                + "공약    " + NullDash(info.bidLabel);
        }

        ApplyPanelSize(info.friendCardNone);
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
        go.transform.SetParent(canvas.transform, false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(12f, -12f);
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
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = false;

        GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleGo.transform.SetParent(root, false);
        RectTransform titRt = titleGo.GetComponent<RectTransform>();
        titRt.anchorMin = new Vector2(0f, 1f);
        titRt.anchorMax = new Vector2(1f, 1f);
        titRt.pivot = new Vector2(0.5f, 1f);
        titRt.anchoredPosition = new Vector2(0f, -6f);
        titRt.sizeDelta = new Vector2(-16f, UiFonts.Layout(26f));
        Text title = titleGo.GetComponent<Text>();
        title.font = GetFont();
        title.fontSize = UiFonts.Size(15);
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(1f, 0.9f, 0.45f, 1f);
        title.text = "판 정보";
        title.raycastTarget = false;

        Vector2 cardSz = IconSpriteAtlas.DisplayCard;
        Vector2 sqSz = IconSpriteAtlas.DisplaySquare;
        float rowH = Mathf.Max(cardSz.y, sqSz.y, UiFonts.Layout(22f)) + 8f;
        float y0 = -(UiFonts.Layout(26f) + 6f);
        trumpIcon = MakeLabeledIcon(root, "Trump", "기루다", new Vector2(PadL, y0), sqSz);
        mightyIcon = MakeLabeledIcon(root, "Mighty", "마이티", new Vector2(PadL, y0 - rowH), cardSz);
        jokerCallIcon = MakeLabeledIcon(root, "JokerCall", "조커콜", new Vector2(PadL, y0 - rowH * 2f), cardSz);
        friendCardIcon = MakeLabeledIcon(root, "FriendCard", "프렌드카드", new Vector2(PadL, y0 - rowH * 3f), cardSz);
        float noneX = LabelColW + 8f + cardSz.x + 8f;
        friendNoneText = MakeInlineLabel(friendCardIcon.transform.parent, "None", "없음", new Vector2(noneX, 0f));
        friendNoneText.gameObject.SetActive(false);

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        bodyGo.transform.SetParent(root, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 0f);
        bodyRt.pivot = new Vector2(0.5f, 0f);
        bodyRt.anchoredPosition = new Vector2(0f, 8f);
        bodyRt.sizeDelta = new Vector2(-20f, UiFonts.Layout(44f));
        bodyText = bodyGo.GetComponent<Text>();
        bodyText.font = GetFont();
        bodyText.fontSize = UiFonts.Size(14);
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.color = Color.white;
        bodyText.horizontalOverflow = HorizontalWrapMode.Overflow;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.raycastTarget = false;
        bodyText.lineSpacing = 1.05f;
        Shadow sh = bodyGo.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(1f, -1f);

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

        GameObject labGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labGo.transform.SetParent(row.transform, false);
        RectTransform lrt = labGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(LabelColW, 0f);
        Text t = labGo.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = UiFonts.Size(13);
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.text = label;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;

        Image img = IconGui.MakeImage(row.transform, "Icon", default(IconSpriteAtlas.Slice), iconSize);
        RectTransform irt = img.rectTransform;
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(LabelColW + 8f, 0f);
        return img;
    }

    private Text MakeInlineLabel(Transform parent, string name, string text, Vector2 pos)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(UiFonts.Layout(48f), UiFonts.Layout(24f));
        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = UiFonts.Size(14);
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.text = text;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private Font GetFont()
    {
        if (uiFont == null) uiFont = UiFonts.Primary;
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return uiFont;
    }
}
