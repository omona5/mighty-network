using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// SeatDebugOverlay: 좌석 앵커 + 내 핸드 도킹 포인트 표식 (디버그용)
// ============================================================================
public class SeatDebugOverlay : MonoBehaviour
{
    public bool visible = true;

    private RectTransform root;
    private readonly RectTransform[] seatMarks = new RectTransform[5];
    private readonly RectTransform[] beyondMarks = new RectTransform[5];
    private readonly Text[] seatLabels = new Text[5];

    // 내 핸드 도킹: 하단 / 중심 / 상단
    private RectTransform dockBottom;
    private RectTransform dockCenter;
    private RectTransform dockTop;
    private Text dockBottomLabel;
    private Text dockCenterLabel;
    private Text dockTopLabel;

    private Font font;

    private static readonly string[] SeatNames = { "나", "왼", "상좌", "상우", "오" };
    private static readonly Color SeatColor = new Color(1f, 0.85f, 0.15f, 0.95f);
    private static readonly Color BeyondColor = new Color(0.2f, 0.95f, 1f, 0.9f);
    private static readonly Color DockBottomColor = new Color(1f, 0.35f, 0.2f, 0.95f);
    private static readonly Color DockCenterColor = new Color(0.3f, 1f, 0.45f, 0.95f);
    private static readonly Color DockTopColor = new Color(1f, 0.4f, 0.95f, 0.95f);

    public void Configure(Canvas canvas)
    {
        if (canvas == null) return;
        if (root != null) return;

        GameObject go = new GameObject("SeatDebugOverlay", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(canvas.transform, false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        for (int i = 0; i < 5; i++)
        {
            seatMarks[i] = CreateMark("Seat_" + SeatNames[i], SeatColor, 28f);
            beyondMarks[i] = CreateMark("Beyond_" + SeatNames[i], BeyondColor, 18f);
            seatLabels[i] = CreateLabel(seatMarks[i], SeatNames[i]);
        }

        dockBottom = CreateMark("Dock_Bottom", DockBottomColor, 22f);
        dockCenter = CreateMark("Dock_Center", DockCenterColor, 26f);
        dockTop = CreateMark("Dock_Top", DockTopColor, 22f);
        dockBottomLabel = CreateLabel(dockBottom, "핸드하단");
        dockCenterLabel = CreateLabel(dockCenter, "핸드중심");
        dockTopLabel = CreateLabel(dockTop, "핸드상단");

        Refresh();
    }

    public void SetVisible(bool on)
    {
        visible = on;
        if (root != null) root.gameObject.SetActive(on);
        if (on) Refresh();
    }

    public void Refresh()
    {
        if (root == null || !visible) return;
        Canvas.ForceUpdateCanvases();
        root.SetAsLastSibling();

        Vector2 center = new Vector2(0.5f, 0.55f);
        for (int i = 0; i < 5; i++)
        {
            if (i == 0)
            {
                // 노랑 "나": HandContainer 기하 중심 (카드와 동일 방식의 GetWorldCorners)
                GameObject hc = GameObject.Find("HandContainer");
                RectTransform hrt = hc != null ? hc.GetComponent<RectTransform>() : null;
                if (hrt != null)
                {
                    Vector3[] hcCorners = new Vector3[4];
                    hrt.GetWorldCorners(hcCorners);
                    Vector3 hcMid = (hcCorners[0] + hcCorners[2]) * 0.5f;
                    PlaceAtWorld(seatMarks[0], hcMid);
                    if (seatLabels[0] != null)
                    {
                        float cy = OpponentHandsView.SelfHandCenterYFromBottom;
                        seatLabels[0].text = "나(컨테이너중심)\nbottom+" + cy.ToString("0.0")
                            + "\nlift " + (OpponentHandsView.SelfHandLiftFromEdge * 100f).ToString("0") + "%H";
                    }
                    Vector2 seatAp = OpponentHandsView.WorldToAnchored(root, hcMid);
                    Vector2 centerAp = OpponentHandsView.NormalizedToAnchored(root, center);
                    Vector2 beyondAp = OpponentHandsView.BeyondAnchored(centerAp, seatAp, 2.7f);
                    PlaceAtNormalized(beyondMarks[0], AnchoredToNormalizedClamped(root, beyondAp));
                    continue;
                }
            }

            Vector2 anchor = i == 0
                ? OpponentHandsView.GetSelfHandAnchor(root)
                : OpponentHandsView.GetRelativeSeatAnchor(i - 1);

            PlaceAtNormalized(seatMarks[i], anchor);
            if (seatLabels[i] != null)
            {
                seatLabels[i].text = SeatNames[i]
                    + "\n(" + anchor.x.ToString("F2") + "," + anchor.y.ToString("F2") + ")";
            }

            Vector2 seatAp2 = OpponentHandsView.NormalizedToAnchored(root, anchor);
            Vector2 centerAp2 = OpponentHandsView.NormalizedToAnchored(root, center);
            Vector2 beyondAp2 = OpponentHandsView.BeyondAnchored(centerAp2, seatAp2, 2.7f);
            PlaceAtNormalized(beyondMarks[i], AnchoredToNormalizedClamped(root, beyondAp2));
        }

        RefreshSelfHandDockMarks();
    }

    private void RefreshSelfHandDockMarks()
    {
        float cardH = CardSpriteAtlas.DisplayHeight;
        float cy = OpponentHandsView.SelfHandCenterYFromBottom;
        float canvasH = Mathf.Max(1f, root.rect.height);
        float lift = OpponentHandsView.SelfHandLiftFromEdge;

        // 1) 실제 손패 카드 Rect 기준 (컨테이너가 아니라 카드 이미지)
        RectTransform cardRt = FindFirstHandCardRect();
        if (cardRt != null)
        {
            Vector3[] corners = new Vector3[4];
            cardRt.GetWorldCorners(corners);
            // 0 BL, 1 TL, 2 TR, 3 BR
            Vector3 bottom = (corners[0] + corners[3]) * 0.5f;
            Vector3 top = (corners[1] + corners[2]) * 0.5f;
            Vector3 mid = (bottom + top) * 0.5f;
            PlaceAtWorld(dockBottom, bottom);
            PlaceAtWorld(dockCenter, mid);
            PlaceAtWorld(dockTop, top);

            float topLocalY = OpponentHandsView.WorldToAnchored(root, top).y;
            float midLocalY = OpponentHandsView.WorldToAnchored(root, mid).y;
            float botLocalY = OpponentHandsView.WorldToAnchored(root, bottom).y;
            if (dockBottomLabel != null)
                dockBottomLabel.text = "카드하단\nlocalY=" + botLocalY.ToString("0.0");
            if (dockCenterLabel != null)
                dockCenterLabel.text = "카드중심\nlocalY=" + midLocalY.ToString("0.0");
            if (dockTopLabel != null)
                dockTopLabel.text = "카드상단\nlocalY=" + topLocalY.ToString("0.0");
            return;
        }

        // 2) 카드 없을 때: 도킹 수식 기준 (화면 하단=0)
        PlaceAtNormalized(dockBottom, new Vector2(0.5f, (cy - cardH * 0.5f) / canvasH));
        PlaceAtNormalized(dockCenter, new Vector2(0.5f, cy / canvasH));
        PlaceAtNormalized(dockTop, new Vector2(0.5f, (cy + cardH * 0.5f) / canvasH));
        if (dockBottomLabel != null)
            dockBottomLabel.text = "하단(수식)\ny=" + (cy - cardH * 0.5f).ToString("0.0");
        if (dockCenterLabel != null)
            dockCenterLabel.text = "중심(가장자리+" + (lift * 100f).ToString("0") + "%H)\ny=" + cy.ToString("0.0");
        if (dockTopLabel != null)
            dockTopLabel.text = "상단(수식)\ny=" + (cy + cardH * 0.5f).ToString("0.0");
    }

    private static RectTransform FindFirstHandCardRect()
    {
        GameObject hc = GameObject.Find("HandContainer");
        if (hc == null) return null;
        // CardView가 붙은 첫 카드 (레이아웃 슬롯 제외)
        CardView[] cards = hc.GetComponentsInChildren<CardView>(false);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            RectTransform rt = cards[i].transform as RectTransform;
            if (rt != null && rt.rect.height > 1f)
                return rt;
        }
        return null;
    }

    private void LateUpdate()
    {
        if (!visible || root == null) return;
        // 도킹은 매 프레임 바뀔 수 있음
        Refresh();
    }

    private RectTransform CreateMark(string name, Color color, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(root, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        CreateBar(go.transform, "H", new Vector2(size * 2.2f, 4f), color);
        CreateBar(go.transform, "V", new Vector2(4f, size * 2.2f), color);
        return rt;
    }

    private static void CreateBar(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private Text CreateLabel(RectTransform parent, string text)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 18f);
        rt.sizeDelta = new Vector2(160f, 56f);
        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = UiFonts.Size(13);
        t.alignment = TextAnchor.LowerCenter;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;
        Shadow sh = go.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(1f, -1f);
        return t;
    }

    private void PlaceAtNormalized(RectTransform mark, Vector2 normalized)
    {
        if (mark == null || root == null) return;
        mark.anchoredPosition = OpponentHandsView.NormalizedToAnchored(root, normalized);
    }

    private void PlaceAtWorld(RectTransform mark, Vector3 world)
    {
        if (mark == null || root == null) return;
        mark.anchoredPosition = OpponentHandsView.WorldToAnchored(root, world);
    }

    private static Vector2 AnchoredToNormalizedClamped(RectTransform stretchParent, Vector2 anchored)
    {
        if (stretchParent == null) return new Vector2(0.5f, 0.5f);
        Rect r = stretchParent.rect;
        float w = Mathf.Max(1f, r.width);
        float h = Mathf.Max(1f, r.height);
        float nx = anchored.x / w + 0.5f;
        float ny = anchored.y / h + 0.5f;
        return new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(ny));
    }

    private Font GetFont()
    {
        if (font == null) font = UiFonts.Primary;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
