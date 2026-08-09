using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// GameRuleHud: 좌상단 — 기루다 / 마이티 / 조커콜 / 주공 / 프렌드 / 주공팀·공약
// ============================================================================
public class GameRuleHud : MonoBehaviour
{
    public RectTransform root;

    private Text bodyText;
    private Font uiFont;
    private CanvasGroup canvasGroup;

    public struct Info
    {
        public string trumpLabel;      // 기루다 or 노기루
        public string mightyLabel;     // 카드 짧은 표기
        public string jokerCallLabel;
        public string declarerLabel;
        public string friendLabel;
        public string teamScoreLabel;  // 주공팀 현재 점수 (마이티 전=주공만, 후=합산)
        public string bidLabel;        // 공약 N점
        public bool visible;
    }

    public void Set(Info info)
    {
        EnsureUi();
        if (root == null || bodyText == null) return;

        if (!info.visible)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (root != null) root.gameObject.SetActive(false);
            return;
        }

        root.gameObject.SetActive(true);
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        bodyText.text =
            "기루다  " + NullDash(info.trumpLabel) + "\n"
            + "마이티  " + NullDash(info.mightyLabel) + "\n"
            + "조커콜  " + NullDash(info.jokerCallLabel) + "\n"
            + "주공    " + NullDash(info.declarerLabel) + "\n"
            + "프렌드  " + NullDash(info.friendLabel) + "\n"
            + "주공팀  " + NullDash(info.teamScoreLabel) + "\n"
            + "공약    " + NullDash(info.bidLabel);
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
        if (root != null && bodyText != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[GameRuleHud] Canvas를 찾을 수 없습니다.");
            return;
        }

        GameObject go = new GameObject("GameRuleHud", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(16f, -16f);
        root.sizeDelta = new Vector2(280f, 220f);
        canvasGroup = go.GetComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        // 카드/상대 손패보다 위에 보이도록
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
        titRt.sizeDelta = new Vector2(-20f, 28f);
        Text title = titleGo.GetComponent<Text>();
        title.font = GetFont();
        title.fontSize = 16;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(1f, 0.9f, 0.45f, 1f);
        title.text = "판 정보";
        title.raycastTarget = false;

        GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        bodyGo.transform.SetParent(root, false);
        RectTransform bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(12f, 10f);
        bodyRt.offsetMax = new Vector2(-12f, -36f);
        bodyText = bodyGo.GetComponent<Text>();
        bodyText.font = GetFont();
        bodyText.fontSize = 15;
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.color = Color.white;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.raycastTarget = false;
        bodyText.lineSpacing = 1.15f;

        Shadow sh = bodyGo.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
        sh.effectDistance = new Vector2(1f, -1f);

        root.gameObject.SetActive(false);
    }

    private Font GetFont()
    {
        if (uiFont == null) uiFont = UiFonts.Primary;
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return uiFont;
    }
}
