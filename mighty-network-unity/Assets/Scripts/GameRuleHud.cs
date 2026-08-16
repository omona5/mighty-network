using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// GameRuleHud: 좌상단 — 기루다 / 마이티 / 조커콜 / 주공 / 프렌드 / 주공팀·공약
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
    private const int LayoutRev = 6;
    private int builtRev;

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
    }

    public void Clear()
    {
        Set(new Info { visible = false });
    }

    private static string NullDash(string s)
    {
        return string.IsNullOrEmpty(s) ? "-" : s;
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
        root.anchoredPosition = new Vector2(16f, -16f);
        root.sizeDelta = new Vector2(360f, 340f);
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
        titRt.anchoredPosition = new Vector2(0f, -8f);
        titRt.sizeDelta = new Vector2(-20f, 36f);
        Text title = titleGo.GetComponent<Text>();
        title.font = GetFont();
        title.fontSize = UiFonts.Size(16);
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(1f, 0.9f, 0.45f, 1f);
        title.text = "판 정보";
        title.raycastTarget = false;

        Vector2 cardSz = IconSpriteAtlas.DisplayCard;
        Vector2 sqSz = IconSpriteAtlas.DisplaySquare;
        trumpIcon = MakeLabeledIcon(root, "Trump", "기루다", new Vector2(12f, -38f), sqSz);
        mightyIcon = MakeLabeledIcon(root, "Mighty", "마이티", new Vector2(12f, -98f), cardSz);
        jokerCallIcon = MakeLabeledIcon(root, "JokerCall", "조커콜", new Vector2(12f, -158f), cardSz);
        friendCardIcon = MakeLabeledIcon(root, "FriendCard", "프렌드카드", new Vector2(12f, -218f), cardSz);
        friendNoneText = MakeInlineLabel(friendCardIcon.transform.parent, "None", "없음", new Vector2(80f, 0f));
        friendNoneText.gameObject.SetActive(false);

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        bodyGo.transform.SetParent(root, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0f, 0f);
        bodyRt.anchorMax = new Vector2(1f, 0f);
        bodyRt.pivot = new Vector2(0.5f, 0f);
        bodyRt.anchoredPosition = new Vector2(0f, 10f);
        bodyRt.sizeDelta = new Vector2(-24f, 66f);
        bodyText = bodyGo.GetComponent<Text>();
        bodyText.font = GetFont();
        bodyText.fontSize = UiFonts.Size(15);
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.color = Color.white;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.raycastTarget = false;
        bodyText.lineSpacing = 1.1f;
        Shadow sh = bodyGo.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(1f, -1f);

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
        rt.sizeDelta = new Vector2(-24f, Mathf.Max(iconSize.y, 22f));

        GameObject labGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labGo.transform.SetParent(row.transform, false);
        RectTransform lrt = labGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0f, 1f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.anchoredPosition = Vector2.zero;
        lrt.sizeDelta = new Vector2(110f, 0f);
        Text t = labGo.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = UiFonts.Size(14);
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.text = label;
        t.raycastTarget = false;

        Image img = IconGui.MakeImage(row.transform, "Icon", default(IconSpriteAtlas.Slice), iconSize);
        RectTransform irt = img.rectTransform;
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(80f, 0f);
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
        rt.sizeDelta = new Vector2(110f, 36f);
        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = UiFonts.Size(15);
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        t.text = text;
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
