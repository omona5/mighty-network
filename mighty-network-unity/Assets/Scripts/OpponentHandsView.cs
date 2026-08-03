using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// OpponentHandsView: 나를 제외한 플레이어의 손패를 뒷면으로 표시.
//   - 닉네임 + 뒤집힌 카드(handCount장)
//   - 내 자리 기준 시계방향 상대 좌석 배치 (좌 / 상좌 / 상우 / 우)
// ============================================================================
public class OpponentHandsView : MonoBehaviour
{
    [Header("Inspector에서 연결 (비우면 런타임 생성)")]
    public CardView cardPrefab;
    public RectTransform root;

    public struct SeatInfo
    {
        public string nickname;
        public int handCount;
        public bool isTurn;
        public bool isBot;
        public bool disconnected;
    }

    // 상대 손패: 내 손패(160×224)의 1/2 → 원본 대비 1/4
    private static readonly Vector2 CardSize = new Vector2(
        CardSpriteAtlas.DisplayWidth * 0.5f,
        CardSpriteAtlas.DisplayHeight * 0.5f);
    private const float CardOverlap = 22f;

    // 상대 4석 앵커 (Canvas 정규화 좌표). 나=하단 손패 가정.
    private static readonly Vector2[] SeatAnchors =
    {
        new Vector2(0.07f, 0.48f), // +1 왼쪽
        new Vector2(0.28f, 0.90f), // +2 상단 왼쪽
        new Vector2(0.72f, 0.90f), // +3 상단 오른쪽
        new Vector2(0.93f, 0.48f), // +4 오른쪽
    };

    private readonly List<GameObject> panels = new List<GameObject>();
    private Font uiFont;

    public void Clear()
    {
        foreach (GameObject go in panels)
        {
            if (go != null) Destroy(go);
        }
        panels.Clear();
    }

    // seats: 내 다음 자리부터 시계방향 순서 (최대 4)
    public void Show(SeatInfo[] seats)
    {
        EnsureRoot();
        Clear();
        if (seats == null || cardPrefab == null) return;

        int n = Mathf.Min(seats.Length, SeatAnchors.Length);
        for (int i = 0; i < n; i++)
            panels.Add(BuildPanel(seats[i], SeatAnchors[i]));
    }

    private void EnsureRoot()
    {
        if (root != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[OpponentHandsView] Canvas를 찾을 수 없습니다.");
            return;
        }

        GameObject go = new GameObject("OpponentHandsRoot", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        root = go.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        // HandContainer보다 위, OnGUI보다 아래 — 형제 중 앞쪽
        root.SetAsFirstSibling();
    }

    private GameObject BuildPanel(SeatInfo seat, Vector2 anchor)
    {
        GameObject panel = new GameObject("Opp_" + seat.nickname, typeof(RectTransform));
        panel.transform.SetParent(root, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = anchor;
        prt.anchorMax = anchor;
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(280f, 140f);
        prt.anchoredPosition = Vector2.zero;

        // 이름
        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        nameGo.transform.SetParent(panel.transform, false);
        RectTransform nrt = nameGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.72f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = Vector2.zero;
        nrt.offsetMax = Vector2.zero;
        Text nameText = nameGo.GetComponent<Text>();
        nameText.font = GetUiFont();
        nameText.fontSize = 16;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Truncate;
        nameText.color = seat.isTurn
            ? new Color(1f, 0.85f, 0.3f)
            : (seat.disconnected ? new Color(0.7f, 0.7f, 0.7f) : Color.white);
        string prefix = seat.isBot ? "[봇] " : "";
        if (seat.disconnected) prefix = "[끊김] ";
        nameText.text = prefix + seat.nickname
            + (seat.isTurn ? " <<" : "")
            + " (" + seat.handCount + ")";

        // 카드 줄
        GameObject row = new GameObject("Cards", typeof(RectTransform));
        row.transform.SetParent(panel.transform, false);
        RectTransform rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(0.5f, 0.72f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        float rowWidth = Mathf.Max(CardSize.x, (Mathf.Max(seat.handCount, 1) - 1) * CardOverlap + CardSize.x);
        rrt.sizeDelta = new Vector2(rowWidth, CardSize.y + 4f);
        rrt.anchoredPosition = Vector2.zero;

        int count = Mathf.Clamp(seat.handCount, 0, 13);
        float startX = -((count - 1) * CardOverlap) * 0.5f;
        for (int c = 0; c < count; c++)
        {
            CardView view = Instantiate(cardPrefab, row.transform);
            view.SetFaceDown();
            view.Clicked = null;
            RectTransform crt = view.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.anchorMin = new Vector2(0.5f, 0.5f);
                crt.anchorMax = new Vector2(0.5f, 0.5f);
                crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = CardSize;
                crt.anchoredPosition = new Vector2(startX + c * CardOverlap, 0f);
                crt.localScale = Vector3.one;
            }
        }

        return panel;
    }

    private Font GetUiFont()
    {
        if (uiFont == null)
            uiFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return uiFont;
    }
}
