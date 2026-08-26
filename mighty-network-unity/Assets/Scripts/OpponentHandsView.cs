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
        public bool isFriend;
        public bool isFriendSecret; // 본인만 아는 미공개 프렌드(회색 F)
    }

    // 상대 손패: 내 손패(160×224)의 1/2 → 원본 대비 1/4
    private static readonly Vector2 CardSize = new Vector2(
        CardSpriteAtlas.DisplayWidth * 0.5f,
        CardSpriteAtlas.DisplayHeight * 0.5f);
    private const float CardOverlap = 22f;

    // 상대 4석 앵커 (Canvas 정규화 0~1). 나=하단.
    // Y를 좌·우(중) / 상단으로 명확히 분리.
    private static readonly Vector2[] SeatAnchors =
    {
        new Vector2(0.08f, 0.52f), // +1 왼쪽
        new Vector2(0.28f, 0.90f), // +2 상단 왼쪽
        new Vector2(0.72f, 0.90f), // +3 상단 오른쪽
        new Vector2(0.92f, 0.52f), // +4 오른쪽
    };

    public static Vector2 SelfHandAnchor = new Vector2(0.5f, 0.14f);

    // 딜/비행용: 시계방향 상대 좌석 정규화 앵커 (인덱스 0 = 내 다음)
    public static Vector2 GetRelativeSeatAnchor(int relativeIndex)
    {
        if (relativeIndex < 0 || relativeIndex >= SeatAnchors.Length)
            return new Vector2(0.5f, 0.5f);
        return SeatAnchors[relativeIndex];
    }

    // stretch 부모(앵커 풀스크린) 기준: 정규화 → 중앙 피벗 자식의 anchoredPosition
    // CanvasScaler/월드좌표 혼선으로 Y가 뭉개지는 문제를 피하기 위함.
    public static Vector2 NormalizedToAnchored(RectTransform stretchParent, Vector2 normalized)
    {
        if (stretchParent == null) return Vector2.zero;
        Canvas.ForceUpdateCanvases();
        Rect r = stretchParent.rect;
        if (r.width < 1f || r.height < 1f)
        {
            // 레이아웃 전: 스크린 기준으로라도 Y 분리
            return new Vector2(
                (normalized.x - 0.5f) * Screen.width,
                (normalized.y - 0.5f) * Screen.height);
        }
        return new Vector2(
            (normalized.x - 0.5f) * r.width,
            (normalized.y - 0.5f) * r.height);
    }

    public static Vector2 WorldToAnchored(RectTransform stretchParent, Vector3 worldPos)
    {
        if (stretchParent == null) return Vector2.zero;
        Vector3 local = stretchParent.InverseTransformPoint(worldPos);
        return new Vector2(local.x, local.y);
    }

    public static Vector2 BeyondAnchored(Vector2 from, Vector2 seat, float overshoot = 2.6f)
    {
        Vector2 delta = seat - from;
        if (delta.sqrMagnitude < 100f)
            return seat + new Vector2(0f, -600f);
        return Vector2.LerpUnclamped(from, seat, overshoot);
    }

    // 정규화 앵커 → 월드 (레거시/디버그용). 비행은 NormalizedToAnchored 권장.
    public bool TryGetAnchorWorldPosition(Vector2 normalizedAnchor, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        EnsureRoot();
        if (root == null) return false;
        Canvas.ForceUpdateCanvases();
        Vector2 ap = NormalizedToAnchored(root, normalizedAnchor);
        worldPos = root.TransformPoint(new Vector3(ap.x, ap.y, 0f));
        return true;
    }

    public bool TryGetRelativeSeatWorldPosition(int relativeFromSelf, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (relativeFromSelf <= 0) return false;
        return TryGetAnchorWorldPosition(GetRelativeSeatAnchor(relativeFromSelf - 1), out worldPos);
    }

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

    // 내 정보: 손패 아래 + Canvas 맨 앞에 표시
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

        // 내 손패 앵커 기준 — BuildPanel에서 카드 하단 바로 아래로 배치
        GameObject panel = BuildPanel(self, SelfHandAnchor, true);
        panels.Add(panel);

        // OpponentHandsRoot는 손패보다 뒤에 있음 → Self만 Canvas 맨 앞으로
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null && panel != null)
        {
            panel.transform.SetParent(canvas.transform, true);
            panel.transform.SetAsLastSibling();
        }
    }

    // from → seat 방향으로 seat를 지나 화면 밖까지 연장 (점수/바닥패 회수 애니)
    public static Vector3 BeyondSeatWorld(Vector3 fromWorld, Vector3 seatWorld, float overshoot = 2.6f)
    {
        Vector3 delta = seatWorld - fromWorld;
        if (delta.sqrMagnitude < 25f)
            return seatWorld + new Vector3(0f, -500f, 0f); // 내 자리 등 거의 동일 좌표면 아래로
        return Vector3.LerpUnclamped(fromWorld, seatWorld, overshoot);
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

    // 좌상단 GameRuleHud body(15)와 동일
    private const int StatusFontSize = 22;

    public static string FormatStatusLine(SeatInfo seat, bool isSelf)
    {
        string prefix = seat.isBot ? "[봇] " : "";
        if (seat.disconnected) prefix = "[끊김] ";
        string turn = seat.isTurn ? " <<" : "";
        string who = isSelf ? (seat.nickname + " (나)") : seat.nickname;
        return prefix + who + turn;
    }

    public static string FormatScoreLine(SeatInfo seat)
    {
        return seat.score + "점";
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
        bool isLeftSeat = !isSelf && anchor.x < 0.25f;
        // 패널 중심 = 핸드(또는 내 손패) 중심. 이름 UI는 카드 하단 바로 아래.
        prt.pivot = new Vector2(0.5f, 0.5f);
        float nameH = UiFonts.Layout(64f);
        float nameGap = isSelf ? 12f : 6f; // 내 이름은 손패와 살짝 더 띄움
        float panelW = isSelf ? UiFonts.Layout(420f) : UiFonts.Layout(360f);
        if (isSelf)
        {
            // 내 이름만: 손패 중심 앵커에서 카드 하단 + 여유만큼 아래로
            prt.sizeDelta = new Vector2(panelW, nameH);
            prt.anchoredPosition = new Vector2(
                0f,
                -(CardSpriteAtlas.DisplayHeight * 0.5f + nameGap + nameH * 0.5f));
        }
        else
        {
            // 상대: 카드가 좌석 앵커에 오도록 패널은 카드 크기, 이름은 아래로 넘침
            prt.sizeDelta = new Vector2(panelW, CardSize.y);
            prt.anchoredPosition = Vector2.zero;
        }
        if (!string.IsNullOrEmpty(seat.nickname))
            seatByNickname[seat.nickname] = prt;

        // 상대 카드 줄 — 좌석 앵커(패널 중앙)
        if (!isSelf)
        {
            GameObject row = new GameObject("Cards", typeof(RectTransform));
            row.transform.SetParent(panel.transform, false);
            RectTransform rrt = row.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0.5f);
            rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            float rowWidth = Mathf.Max(CardSize.x, (Mathf.Max(seat.handCount, 1) - 1) * CardOverlap + CardSize.x);
            rrt.sizeDelta = new Vector2(rowWidth, CardSize.y);
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
        }

        // 아이콘 + 이름/점수 — 핸드 카드 바로 아래
        GameObject nameRow = new GameObject("NameRow", typeof(RectTransform));
        nameRow.transform.SetParent(panel.transform, false);
        RectTransform nrrt = nameRow.GetComponent<RectTransform>();
        nrrt.anchorMin = new Vector2(0.5f, 0.5f);
        nrrt.anchorMax = new Vector2(0.5f, 0.5f);
        nrrt.pivot = new Vector2(0.5f, 0.5f);
        nrrt.sizeDelta = new Vector2(panelW, nameH);
        nrrt.anchoredPosition = isSelf
            ? Vector2.zero
            : new Vector2(0f, -(CardSize.y * 0.5f + nameGap + nameH * 0.5f));

        Vector2 sq = IconSpriteAtlas.DisplaySquare;
        int roleN = (seat.isDeclarer ? 1 : 0)
            + ((seat.isFriend || seat.isFriendSecret) ? 1 : 0);
        float roleGap = 4f;
        float iconW = roleN > 0 ? roleN * sq.x + (roleN - 1) * roleGap : 0f;
        float textW = isSelf ? UiFonts.Layout(240f) : UiFonts.Layout(200f);
        float clusterW = iconW + (roleN > 0 ? 8f : 0f) + textW;

        GameObject cluster = new GameObject("Cluster", typeof(RectTransform));
        cluster.transform.SetParent(nameRow.transform, false);
        RectTransform clusterRt = cluster.GetComponent<RectTransform>();
        clusterRt.anchorMin = new Vector2(0.5f, 0.5f);
        clusterRt.anchorMax = new Vector2(0.5f, 0.5f);
        clusterRt.pivot = new Vector2(0.5f, 0.5f);
        clusterRt.sizeDelta = new Vector2(clusterW, nameH);
        // 왼쪽 좌석만 이름·점수·역할 아이콘을 안쪽으로 (핸드 카드는 패널 중앙 유지)
        float nameShiftX = isLeftSeat ? Mathf.Max(UiFonts.Layout(48f), iconW * 0.5f + 28f) : 0f;
        clusterRt.anchoredPosition = new Vector2(nameShiftX, 0f);

        if (roleN > 0)
        {
            GameObject roles = new GameObject("Roles", typeof(RectTransform));
            roles.transform.SetParent(cluster.transform, false);
            RectTransform roleRt = roles.GetComponent<RectTransform>();
            roleRt.anchorMin = new Vector2(0f, 0.5f);
            roleRt.anchorMax = new Vector2(0f, 0.5f);
            roleRt.pivot = new Vector2(0f, 0.5f);
            roleRt.anchoredPosition = Vector2.zero;
            roleRt.sizeDelta = new Vector2(iconW, sq.y);
            IconGui.PlaceRoleIcons(
                roles.transform, seat.isDeclarer, seat.isFriend, Vector2.zero, seat.isFriendSecret);
        }

        float textX = iconW + (roleN > 0 ? 8f : 0f);

        GameObject nameGo = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        nameGo.transform.SetParent(cluster.transform, false);
        RectTransform nrt = nameGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(textX, 0f);
        nrt.offsetMax = Vector2.zero;
        Text nameText = nameGo.GetComponent<Text>();
        nameText.font = GetUiFont();
        nameText.fontSize = StatusFontSize;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;
        nameText.color = seat.isTurn
            ? new Color(1f, 0.85f, 0.3f)
            : (seat.disconnected ? new Color(0.7f, 0.7f, 0.7f) : Color.white);
        nameText.text = FormatStatusLine(seat, isSelf);
        nameText.raycastTarget = false;
        Shadow nsh = nameGo.AddComponent<Shadow>();
        nsh.effectColor = new Color(0f, 0f, 0f, 0.75f);
        nsh.effectDistance = new Vector2(1f, -1f);

        GameObject scoreGo = new GameObject("Score", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        scoreGo.transform.SetParent(cluster.transform, false);
        RectTransform srt = scoreGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0.5f);
        srt.offsetMin = new Vector2(textX, 0f);
        srt.offsetMax = Vector2.zero;
        Text scoreText = scoreGo.GetComponent<Text>();
        scoreText.font = GetUiFont();
        scoreText.fontSize = StatusFontSize;
        scoreText.alignment = TextAnchor.MiddleLeft;
        scoreText.color = nameText.color;
        scoreText.text = FormatScoreLine(seat);
        scoreText.raycastTarget = false;
        Shadow ssh = scoreGo.AddComponent<Shadow>();
        ssh.effectColor = new Color(0f, 0f, 0f, 0.75f);
        ssh.effectDistance = new Vector2(1f, -1f);

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
