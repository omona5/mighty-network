using System;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// PlayChoicePopup: 조커 리드 무늬 / 조커콜 여부 중앙 팝업 (uGUI)
// ============================================================================
public class PlayChoicePopup : MonoBehaviour
{
    private RectTransform root;
    private CanvasGroup group;
    private Text title;
    private RectTransform row;
    private Action onCancel;
    private bool built;

    public void Configure(Canvas canvas)
    {
        if (built || canvas == null) return;

        GameObject overlay = new GameObject("PlayChoicePopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        overlay.transform.SetParent(canvas.transform, false);
        root = overlay.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.55f);
        dim.raycastTarget = true;
        Button dimBtn = overlay.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(Cancel);

        group = overlay.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(640f, 260f);
        prt.anchoredPosition = new Vector2(0f, 40f);
        Image bg = panel.GetComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;

        title = MakeText(panel.transform, "Title", UiFonts.Size(32), TextAnchor.MiddleCenter);
        RectTransform trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-32f, 56f);
        trt.anchoredPosition = new Vector2(0f, -12f);

        GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(panel.transform, false);
        row = rowGo.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0.5f, 0.5f);
        row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(500f, 72f);
        row.anchoredPosition = new Vector2(0f, -8f);
        HorizontalLayoutGroup h = rowGo.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 12f;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;

        Button cancel = MakeTextButton(panel.transform, "취소", 130f, 56f, Cancel);
        RectTransform crt = cancel.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0f);
        crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 16f);

        built = true;
        Hide();
    }

    public void Hide()
    {
        onCancel = null;
        if (root != null) root.gameObject.SetActive(false);
        if (group != null) group.alpha = 0f;
        ClearRow();
    }

    public void ShowSuitPick(Action<string> onPick, Action cancel)
    {
        EnsureReady();
        onCancel = cancel;
        title.text = "조커 리드 — 따라낼 무늬";
        ClearRow();
        string[] suits = { "SPADE", "DIAMOND", "HEART", "CLUB" };
        for (int i = 0; i < suits.Length; i++)
        {
            string suit = suits[i];
            MakeIconButton(row, IconSpriteAtlas.GetSuit(suit), IconSpriteAtlas.DisplaySquare, () =>
            {
                Hide();
                if (onPick != null) onPick(suit);
            });
        }
        Open();
    }

    public void ShowJokerCallPick(string cardId, Action<bool> onPick, Action cancel)
    {
        EnsureReady();
        onCancel = cancel;
        title.text = "조커콜을 사용할까요?";
        ClearRow();
        Image preview = IconGui.MakeImage(row, "CallCard",
            IconSpriteAtlas.GetCard(cardId), IconSpriteAtlas.DisplayCard);
        LayoutElement ple = preview.gameObject.AddComponent<LayoutElement>();
        ple.preferredWidth = IconSpriteAtlas.DisplayCard.x;
        ple.preferredHeight = IconSpriteAtlas.DisplayCard.y;
        MakeTextButton(row, "조커콜 사용", 150f, 52f, () =>
        {
            Hide();
            if (onPick != null) onPick(true);
        });
        MakeTextButton(row, "일반으로 내기", 150f, 52f, () =>
        {
            Hide();
            if (onPick != null) onPick(false);
        });
        Open();
    }

    private void Open()
    {
        if (root == null) return;
        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
        if (group != null) group.alpha = 1f;
    }

    private void Cancel()
    {
        Action cb = onCancel;
        Hide();
        if (cb != null) cb();
    }

    private void EnsureReady()
    {
        if (built) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Configure(canvas);
    }

    private void ClearRow()
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--)
            Destroy(row.GetChild(i).gameObject);
    }

    private static Text MakeText(Transform parent, string name, int fontSize, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        Font font = UiFonts.Primary;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.font = font;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static Button MakeTextButton(Transform parent, string label, float w, float h, Action click)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        img.raycastTarget = true;
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.minWidth = w;
        le.minHeight = h;
        Text t = MakeText(go.transform, "Label", UiFonts.Size(22), TextAnchor.MiddleCenter);
        RectTransform trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        t.text = label;
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => { if (click != null) click(); });
        return btn;
    }

    private static void MakeIconButton(Transform parent, IconSpriteAtlas.Slice slice, Vector2 size, Action click)
    {
        Image img = IconGui.MakeImage(parent, "IconBtn", slice, size);
        img.raycastTarget = true;
        LayoutElement le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size.x + 8f;
        le.preferredHeight = size.y + 8f;
        le.minWidth = size.x;
        le.minHeight = size.y;
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => { if (click != null) click(); });
    }
}
