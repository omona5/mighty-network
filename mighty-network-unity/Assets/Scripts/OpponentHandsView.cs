using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// OpponentHandsView: 나를 제외한 플레이어의 손패를 뒷면으로 표시.
//   - 정보 박스(역할 아이콘 + 닉네임 + 점수)
//   - 좌·우 좌석은 카드를 90° 회전해 세로 배치 (중앙 공간 확보)
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

    // 상대 손패: 내 손패와 동일 크기
    private static Vector2 CardSize
    {
        get
        {
            return new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        }
    }
    // 뒷면 스택용 중심 간격 (풀사이즈 기준 촘촘히)
    private const float CardOverlap = 44f;

    // 상대 4석 앵커 (Canvas 정규화 0~1). 나=하단.
    // 카드 중심을 화면 가장자리에 두어 약 절반만 보이게 함.
    private static readonly Vector2[] SeatAnchors =
    {
        new Vector2(0.00f, 0.52f), // +1 왼쪽 (절반 화면 밖)
        new Vector2(0.28f, 1.00f), // +2 상단 왼쪽
        new Vector2(0.72f, 1.00f), // +3 상단 오른쪽
        new Vector2(1.00f, 0.52f), // +4 오른쪽
    };

    // 내 핸드: 상대처럼 화면 가장자리에 절반 걸친 뒤, 카드 높이×이 값만큼 위로
    public const float SelfHandLiftFromEdge = 0.3f;

    private static float cachedCanvasHeight = 1080f;
    private static float cachedSelfCenterYFromBottom = 67.2f;

    // HandView가 실제 Canvas rect 기준으로 갱신
    public static void RefreshSelfHandMetrics(float canvasHeight, float cardHeight)
    {
        cachedCanvasHeight = Mathf.Max(1f, canvasHeight);
        float h = Mathf.Max(1f, cardHeight);
        cachedSelfCenterYFromBottom = h * SelfHandLiftFromEdge;
    }

    public static void SetSelfHandCenterFromBottom(float centerYFromBottom)
    {
        cachedSelfCenterYFromBottom = centerYFromBottom;
    }

    public static float SelfHandCenterYFromBottom
    {
        get { return cachedSelfCenterYFromBottom; }
    }

    // stretch 부모(풀스크린)의 실제 높이로 정규화 — 캐시 높이와 어긋나면 마커/딜이 카드 아래로 뜸
    public static Vector2 SelfHandAnchor
    {
        get { return GetSelfHandAnchor(null); }
    }

    public static Vector2 GetSelfHandAnchor(RectTransform space)
    {
        float h = ResolveCanvasHeight(space);
        return new Vector2(0.5f, cachedSelfCenterYFromBottom / h);
    }

    // 화면 하단 기준 centerY → stretch 부모(중앙 피벗) 로컬
    public static Vector2 SelfHandLocalInStretch(RectTransform stretchParent)
    {
        float h = ResolveCanvasHeight(stretchParent);
        return new Vector2(0f, -h * 0.5f + cachedSelfCenterYFromBottom);
    }

    private static float ResolveCanvasHeight(RectTransform space)
    {
        if (space != null && space.rect.height > 1f)
            return space.rect.height;
        Canvas c = Object.FindFirstObjectByType<Canvas>();
        if (c != null)
        {
            RectTransform crt = c.transform as RectTransform;
            if (crt != null && crt.rect.height > 1f)
                return crt.rect.height;
        }
        return Mathf.Max(1f, cachedCanvasHeight);
    }

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

        // 내 손패 앵커 기준 — BuildPanel에서 카드 상단 바로 위에 정보 박스
        if (root != null)
        {
            Canvas.ForceUpdateCanvases();
            RefreshSelfHandMetrics(root.rect.height, CardSpriteAtlas.DisplayHeight);
        }
        GameObject panel = BuildPanel(self, GetSelfHandAnchor(root), true);
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

    // 좌상단 GameRuleHud body와 동일
    private static int StatusFontSize { get { return UiFonts.Size(14); } }

    // 스페이스 2칸 정도 (모든 좌석 박스 공통)
    private static float InfoEdgeInset
    {
        get { return StatusFontSize * 0.55f * 2f; }
    }

    private static float InfoRoleGap { get { return 4f; } }

    private static float InfoLineHeight
    {
        get { return StatusFontSize * 1.05f; }
    }

    private static float InfoLineGap { get { return 2f; } }

    // 텍스트 2줄 높이에 맞춘 역할 아이콘
    private static float InfoIconSide
    {
        get { return InfoLineHeight * 2f + InfoLineGap; }
    }

    private static float InfoMaxIconWidth
    {
        get { return 2f * InfoIconSide + InfoRoleGap; }
    }

    // 5좌석 InfoBox 동일 크기 (가로는 이전 대비 ~60%)
    private static Vector2 InfoBoxSize
    {
        get
        {
            float inset = InfoEdgeInset;
            float textW = UiFonts.Layout(180f);
            float midGap = inset;
            float fullW = inset + textW + midGap + InfoMaxIconWidth + inset;
            // 아이콘도 줄인 뒤 가로 60%
            float baseIconW = 2f * IconSpriteAtlas.DisplaySquare.x + InfoRoleGap;
            float legacyFullW = inset + textW + midGap + baseIconW + inset;
            float w = legacyFullW * 0.6f;
            float textBlockH = InfoLineHeight * 2f + InfoLineGap;
            float h = inset * 2f + Mathf.Max(textBlockH, InfoIconSide);
            return new Vector2(w, h);
        }
    }

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

    private static bool IsSideSeat(Vector2 anchor)
    {
        return anchor.x < 0.20f || anchor.x > 0.80f;
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
        prt.anchoredPosition = Vector2.zero;

        bool isSide = !isSelf && IsSideSeat(anchor);
        bool isLeftSeat = !isSelf && anchor.x < 0.5f;

        float nameGap = isSelf ? 12f : 8f;
        Vector2 boxSize = InfoBoxSize;
        float boxW = boxSize.x;
        float boxH = boxSize.y;

        int count = isSelf ? 0 : Mathf.Clamp(seat.handCount, 0, 13);
        float rowLen = Mathf.Max(CardSize.x, (Mathf.Max(count, 1) - 1) * CardOverlap + CardSize.x);
        // 회전 전 로컬: 가로 rowLen × 세로 CardSize.y
        // 90° 회전 후 화면: 가로 CardSize.y × 세로 rowLen
        float cardsVisualW = isSide ? CardSize.y : rowLen;
        float cardsVisualH = isSide ? rowLen : CardSize.y;

        if (isSelf)
        {
            // 핸드가 화면 아래로 잘리므로 정보 박스는 카드 상단 위쪽
            prt.sizeDelta = new Vector2(boxW, boxH);
            prt.anchoredPosition = new Vector2(
                0f,
                CardSize.y * 0.5f + nameGap + boxH * 0.5f);
        }
        else
        {
            // 카드 중심 = 좌석 앵커. 정보 박스는 화면 안쪽으로 넘침.
            prt.sizeDelta = new Vector2(cardsVisualW, cardsVisualH);
        }

        if (!string.IsNullOrEmpty(seat.nickname))
            seatByNickname[seat.nickname] = prt;

        // 상대 카드 줄
        if (!isSelf)
        {
            GameObject row = new GameObject("Cards", typeof(RectTransform));
            row.transform.SetParent(panel.transform, false);
            RectTransform rrt = row.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0.5f);
            rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.sizeDelta = new Vector2(rowLen, CardSize.y);
            rrt.anchoredPosition = Vector2.zero;
            // 좌 +90 / 우 -90 → 세로 스택, 짧은 변이 화면 가로
            if (isSide)
                rrt.localEulerAngles = new Vector3(0f, 0f, isLeftSeat ? 90f : -90f);

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

        // 정보 박스 (이름·점수 왼쪽 / 역할 아이콘 오른쪽)
        Vector2 boxPos;
        if (isSelf)
        {
            boxPos = Vector2.zero;
        }
        else if (isSide)
        {
            // 좌·우: 카드 안쪽(테이블 쪽)에 박스
            float inward = (cardsVisualW * 0.5f) + nameGap + (boxW * 0.5f);
            boxPos = new Vector2(isLeftSeat ? inward : -inward, 0f);
        }
        else
        {
            // 상단: 카드 바로 아래
            boxPos = new Vector2(0f, -(cardsVisualH * 0.5f + nameGap + boxH * 0.5f));
        }

        BuildInfoBox(panel.transform, seat, isSelf, boxW, boxH, boxPos);
        return panel;
    }

    private void BuildInfoBox(
        Transform parent,
        SeatInfo seat,
        bool isSelf,
        float boxW,
        float boxH,
        Vector2 anchoredPos)
    {
        float inset = InfoEdgeInset;
        float iconSide = InfoIconSide;
        int roleN = (seat.isDeclarer ? 1 : 0)
            + ((seat.isFriend || seat.isFriendSecret) ? 1 : 0);
        float iconW = roleN > 0
            ? roleN * iconSide + (roleN - 1) * InfoRoleGap
            : 0f;

        GameObject box = new GameObject("InfoBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(parent, false);
        RectTransform brt = box.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0.5f);
        brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(boxW, boxH);
        brt.anchoredPosition = anchoredPos;

        Image bg = box.GetComponent<Image>();
        bg.raycastTarget = false;
        if (seat.isTurn)
            bg.color = new Color(0.28f, 0.22f, 0.06f, 0.88f);
        else if (seat.disconnected)
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.75f);
        else
            bg.color = new Color(0f, 0f, 0f, 0.72f);

        Color textColor = seat.isTurn
            ? new Color(1f, 0.88f, 0.35f)
            : (seat.disconnected ? new Color(0.7f, 0.7f, 0.7f) : Color.white);

        // 텍스트: 왼쪽 정렬, 줄간격 타이트하게 세로 중앙
        float textRightReserve = inset + InfoMaxIconWidth + inset;
        float textW = Mathf.Max(8f, boxW - inset - textRightReserve);
        float lineH = InfoLineHeight;
        float lineGap = InfoLineGap;
        float nameY = (lineH + lineGap) * 0.5f;
        float scoreY = -nameY;

        TextMeshProUGUI nameText = UiTmp.Create(
            box.transform, "Name", StatusFontSize, TextAnchor.MiddleLeft, textColor);
        RectTransform nrt = nameText.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0.5f);
        nrt.anchorMax = new Vector2(0f, 0.5f);
        nrt.pivot = new Vector2(0f, 0.5f);
        nrt.sizeDelta = new Vector2(textW, lineH);
        nrt.anchoredPosition = new Vector2(inset, nameY);
        nameText.text = FormatStatusLine(seat, isSelf);

        TextMeshProUGUI scoreText = UiTmp.Create(
            box.transform, "Score", StatusFontSize, TextAnchor.MiddleLeft, textColor);
        RectTransform srt = scoreText.rectTransform;
        srt.anchorMin = new Vector2(0f, 0.5f);
        srt.anchorMax = new Vector2(0f, 0.5f);
        srt.pivot = new Vector2(0f, 0.5f);
        srt.sizeDelta = new Vector2(textW, lineH);
        srt.anchoredPosition = new Vector2(inset, scoreY);
        scoreText.text = FormatScoreLine(seat);

        // 주공·프렌드 아이콘: 오른쪽, 동일 여백 (있을 때만)
        if (roleN > 0)
        {
            GameObject roles = new GameObject("Roles", typeof(RectTransform));
            roles.transform.SetParent(box.transform, false);
            RectTransform roleRt = roles.GetComponent<RectTransform>();
            roleRt.anchorMin = new Vector2(1f, 0.5f);
            roleRt.anchorMax = new Vector2(1f, 0.5f);
            roleRt.pivot = new Vector2(1f, 0.5f);
            roleRt.sizeDelta = new Vector2(iconW, iconSide);
            roleRt.anchoredPosition = new Vector2(-inset, 0f);
            IconGui.PlaceRoleIcons(
                roles.transform, seat.isDeclarer, seat.isFriend, Vector2.zero,
                seat.isFriendSecret, iconSide);
        }
    }
}
