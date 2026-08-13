using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// SeatDebugOverlay: 딜/점수패 도착 앵커를 화면에 표시 (디버그용)
// ============================================================================
public class SeatDebugOverlay : MonoBehaviour
{
    public bool visible = true;

    private RectTransform root;
    private readonly RectTransform[] seatMarks = new RectTransform[5];
    private readonly RectTransform[] beyondMarks = new RectTransform[5];
    private readonly Text[] seatLabels = new Text[5];
    private Font font;

    private static readonly string[] SeatNames = { "나", "왼", "상좌", "상우", "오" };
    private static readonly Color SeatColor = new Color(1f, 0.85f, 0.15f, 0.95f);
    private static readonly Color BeyondColor = new Color(0.2f, 0.95f, 1f, 0.9f);

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
            Vector2 anchor = i == 0
                ? OpponentHandsView.SelfHandAnchor
                : OpponentHandsView.GetRelativeSeatAnchor(i - 1);

            PlaceAtNormalized(seatMarks[i], anchor);
            if (seatLabels[i] != null)
            {
                seatLabels[i].text = SeatNames[i]
                    + "\n(" + anchor.x.ToString("F2") + "," + anchor.y.ToString("F2") + ")";
            }

            Vector2 seatAp = OpponentHandsView.NormalizedToAnchored(root, anchor);
            Vector2 centerAp = OpponentHandsView.NormalizedToAnchored(root, center);
            Vector2 beyondAp = OpponentHandsView.BeyondAnchored(centerAp, seatAp, 2.7f);
            // beyond는 화면 밖일 수 있음 — 마커는 클램프해서라도 방향 보이게
            Vector2 beyondNorm = AnchoredToNormalizedClamped(root, beyondAp);
            PlaceAtNormalized(beyondMarks[i], beyondNorm);
        }
    }

    private void LateUpdate()
    {
        if (!visible || root == null) return;
        // 해상도/스케일 변경 대비 주기적 재배치
        if (Time.frameCount % 30 == 0)
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

        // 십자: 가로/세로 막대
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
        rt.sizeDelta = new Vector2(120f, 48f);
        Text t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = 14;
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

    private static Vector2 AnchoredToNormalizedClamped(RectTransform stretchParent, Vector2 anchored)
    {
        if (stretchParent == null) return new Vector2(0.5f, 0.5f);
        Rect r = stretchParent.rect;
        float w = Mathf.Max(1f, r.width);
        float h = Mathf.Max(1f, r.height);
        float nx = anchored.x / w + 0.5f;
        float ny = anchored.y / h + 0.5f;
        // 화면 밖으로 나간 beyond는 가장자리로 클램프해 방향만 표시
        return new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(ny));
    }

    private Font GetFont()
    {
        if (font == null) font = UiFonts.Primary;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }
}
