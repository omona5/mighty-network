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
        public int score;
        public bool isTurn;
        public bool isBot;
        public bool disconnected;
        public bool isDeclarer;
        public bool isMightyPlayer;
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
    private readonly Dictionary<string, RectTransform> seatByNickname =
        new Dictionary<string, RectTransform>();
    private Font uiFont;

    public void Clear()
    {
        foreach (GameObject go in panels)
        {
            if (go != null) Destroy(go);
        }
        panels.Clear();
        seatByNickname.Clear();
    }

    // seats: 내 다음 자리부터 시계방향 순서 (최대 4)
    public void Show(SeatInfo[] seats)
    {
        EnsureRoot();
        Clear();
        if (seats == null || cardPrefab == null) return;

        int n = Mathf.Min(seats.Length, SeatAnchors.Length);
        for (int i = 0; i < n; i++)
            panels.Add(BuildPanel(seats[i], SeatAnchors[i], false));
    }

    // 내 정보(하단 손패 위): 닉네임 + 점수 + 주공/마이티
    public void ShowSelf(SeatInfo self)
    {
        EnsureRoot();
        for (int i = panels.Count - 1; i >= 0; i--)
        {
            GameObject go = panels[i];
            if (go == null || go.name == null || !go.name.StartsWith("Self_")) continue;
            Destroy(go);
            panels.RemoveAt(i);
        }
        panels.Add(BuildPanel(self, new Vector2(0.5f, 0.22f), true));
    }

    public bool TryGetSeatWorldPosition(string nickname, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (string.IsNullOrEmpty(nickname)) return false;
        RectTransform rt;
        if (!seatByNickname.TryGetValue(nickname, out rt) || rt == null) return false;
        worldPos = rt.position;
        return true;
    }

    public static string FormatStatusLine(SeatInfo seat, bool isSelf)
    {
        string prefix = seat.isBot ? "[봇] " : "";
        if (seat.disconnected) prefix = "[끊김] ";
        string badges = "";
        if (seat.isDeclarer) badges += " [주공]";
        if (seat.isMightyPlayer) badges += " [마이티]";
        string turn = seat.isTurn ? " <<" : "";
        string who = isSelf ? (seat.nickname + " (나)") : seat.nickname;
        return prefix + who + badges + turn
            + "\n" + seat.score + "점  ·  패 " + seat.handCount;
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

    private GameObject BuildPanel(SeatInfo seat, Vector2 anchor, bool isSelf)
    {
        string goName = (isSelf ? "Self_" : "Opp_") + (seat.nickname ?? "?");
        GameObject panel = new GameObject(goName, typeof(RectTransform));
        panel.transform.SetParent(root, false);
        RectTransform prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = anchor;
        prt.anchorMax = anchor;
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = isSelf ? new Vector2(360f, 56f) : new Vector2(300f, 160f);
        prt.anchoredPosition = Vector2.zero;
        if (!string.IsNullOrEmpty(seat.nickname))
            seatByNickname[seat.nickname] = prt;

        // 이름 + 점수/배지
        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        nameGo.transform.SetParent(panel.transform, false);
        RectTransform nrt = nameGo.GetComponent<RectTransform>();
        if (isSelf)
        {
            nrt.anchorMin = Vector2.zero;
            nrt.anchorMax = Vector2.one;
        }
        else
        {
            nrt.anchorMin = new Vector2(0f, 0.70f);
            nrt.anchorMax = new Vector2(1f, 1f);
        }
        nrt.offsetMin = Vector2.zero;
        nrt.offsetMax = Vector2.zero;
        Text nameText = nameGo.GetComponent<Text>();
        nameText.font = GetUiFont();
        nameText.fontSize = isSelf ? 18 : 15;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;
        nameText.color = seat.isTurn
            ? new Color(1f, 0.85f, 0.3f)
            : (seat.disconnected ? new Color(0.7f, 0.7f, 0.7f) : Color.white);
        nameText.text = FormatStatusLine(seat, isSelf);
        Shadow nsh = nameGo.AddComponent<Shadow>();
        nsh.effectColor = new Color(0f, 0f, 0f, 0.75f);
        nsh.effectDistance = new Vector2(1f, -1f);

        if (isSelf) return panel;

        // 카드 줄
        GameObject row = new GameObject("Cards", typeof(RectTransform));
        row.transform.SetParent(panel.transform, false);
        RectTransform rrt = row.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0.5f, 0f);
        rrt.anchorMax = new Vector2(0.5f, 0.68f);
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
            view.RefreshDropShadow();
        }

        return panel;
    }

    private Font GetUiFont()
    {
        if (uiFont == null)
            uiFont = UiFonts.Primary;
        if (uiFont == null)
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return uiFont;
    }
}
