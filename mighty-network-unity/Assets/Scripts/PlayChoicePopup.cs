using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// PlayChoicePopup: 조커 리드 무늬 / 조커콜 여부 중앙 팝업 (TMP)
// ============================================================================
public class PlayChoicePopup : MonoBehaviour
{
    private RectTransform root;
    private CanvasGroup group;
    private TextMeshProUGUI title;
    private RectTransform row;
    private Action onCancel;
    private bool built;
    private RectTransform panelRect;

    private void LateUpdate()
    {
        if (panelRect == null || root == null || !root.gameObject.activeSelf) return;
        float width = root.rect.width;
        panelRect.localScale = Vector3.one * Mathf.Min(ResponsiveCanvas.IsPortrait ? 1.8f : 1.2f, (width - 64f) / panelRect.sizeDelta.x);
    }

    public void Configure(Canvas canvas)
    {
        if (built || canvas == null) return;

        GameObject overlay = new GameObject("PlayChoicePopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
        overlay.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        root = overlay.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        Image dim = overlay.GetComponent<Image>();
        dim.color = new Color(MightyTheme.Table.r, MightyTheme.Table.g, MightyTheme.Table.b, 0.78f);
        dim.raycastTarget = true;
        Button dimBtn = overlay.GetComponent<Button>();
        dimBtn.transition = Selectable.Transition.None;
        dimBtn.onClick.AddListener(() => { Sfx.UiClick(); Cancel(); });

        group = overlay.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        panelRect = prt;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(680f, 320f);
        prt.anchoredPosition = new Vector2(0f, 40f);
        Image bg = panel.GetComponent<Image>();
        bg.color = MightyTheme.Panel;
        bg.raycastTarget = true;

        title = UiTmp.Create(panel.transform, "Title", 28, TextAnchor.MiddleCenter, Color.white);
        RectTransform trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-32f, UiFonts.Layout(48f));
        trt.anchoredPosition = new Vector2(0f, -12f);

        GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(panel.transform, false);
        row = rowGo.GetComponent<RectTransform>();
        row.anchorMin = new Vector2(0.5f, 0.5f);
        row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.sizeDelta = new Vector2(624f, 96f);
        row.anchoredPosition = new Vector2(0f, -8f);
        HorizontalLayoutGroup h = rowGo.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 12f;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        h.childControlWidth = true;
        h.childControlHeight = true;

        Button cancel = MakeTextButton(panel.transform, "취소", 140f, 72f, Cancel);
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
        LocalizedLabel.Bind(title, "조커 리드 — 따라낼 무늬");
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
        LocalizedLabel.Bind(title, "조커콜을 사용할까요?");
        ClearRow();
        Image preview = IconGui.MakeImage(row, "CallCard",
            IconSpriteAtlas.GetCard(cardId), IconSpriteAtlas.DisplayCard);
        LayoutElement ple = preview.gameObject.AddComponent<LayoutElement>();
        ple.preferredWidth = IconSpriteAtlas.DisplayCard.x;
        ple.preferredHeight = IconSpriteAtlas.DisplayCard.y;
        MakeTextButton(row, "조커콜 사용", 240f, 88f, () =>
        {
            Hide();
            if (onPick != null) onPick(true);
        });
        MakeTextButton(row, "일반으로 내기", 240f, 88f, () =>
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
        Sfx.UiClick();
    }

    private void Cancel()
    {
        Action cb = onCancel;
        Hide();
        if (cb != null) cb();
    }

    private void EnsureReady()
    {
        if (!built)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            Configure(canvas);
        }
        // TMP initializes its renderer/material in Awake. The parent must be
        // active before creating labels whose outline is configured by UiTmp.
        if (root != null) root.gameObject.SetActive(true);
    }

    private void ClearRow()
    {
        if (row == null) return;
        for (int i = row.childCount - 1; i >= 0; i--)
        {
            GameObject child = row.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static Button MakeTextButton(Transform parent, string label, float w, float h, Action click)
    {
        h = Mathf.Max(h, 72f);
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(w, h);
        Image img = go.GetComponent<Image>();
        img.color = MightyTheme.Primary;
        img.raycastTarget = true;
        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = w;
        le.preferredHeight = h;
        le.minWidth = w;
        le.minHeight = h;
        TextMeshProUGUI t = UiTmp.Create(
            go.transform, "Label", 24, TextAnchor.MiddleCenter, Color.white);
        RectTransform trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 10f);
        trt.offsetMax = new Vector2(-16f, -10f);
        LocalizedLabel.Bind(t, label);
        t.enableAutoSizing = true;
        t.fontSizeMin = 14;
        t.fontSizeMax = 24;
        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => { Sfx.UiClick(); if (click != null) click(); });
        return btn;
    }

    private static void MakeIconButton(Transform parent, IconSpriteAtlas.Slice slice, Vector2 size, Action click)
    {
        size *= Mathf.Max(1f, 80f / Mathf.Min(size.x, size.y));
        Image img = IconGui.MakeImage(parent, "IconBtn", slice, size);
        img.raycastTarget = true;
        LayoutElement le = img.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = size.x + 8f;
        le.preferredHeight = size.y + 8f;
        le.minWidth = size.x;
        le.minHeight = size.y;
        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => { Sfx.UiClick(); if (click != null) click(); });
    }
}
