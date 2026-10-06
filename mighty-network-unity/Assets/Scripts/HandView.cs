using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// HandView: 손패/테이블 카드 나열.
//   ShowHand(cards): 손패 — 무늬·랭크 정렬
//   ShowCardsInOrder(cards): 테이블 — 낸 순서 그대로
//   ShowTableCards(entries): 테이블 + 제출자 닉네임
// ============================================================================
public class HandView : MonoBehaviour
{
    public struct TableCardEntry
    {
        public CardData card;
        public string playerNickname;
        public bool isDeclarer;
        public bool isFriend;
        public bool isFriendSecret;
        public string declaredSuit;
        public bool jokerCallActivated;
        public bool isWinning;
    }

    [Header("Inspector에서 연결")]
    public CardView cardPrefab;
    public Transform cardContainer;

    public System.Action<CardData> onCardClicked;

    private const float TableLabelHeight = 42f;
    private static int TableLabelFontSize => 30;
    private const int TableNickMaxChars = 8;
    // 5마: 트릭당 항상 5장 — 배치/비행 도착점을 이 슬롯 기준으로 고정
    public const int TableTrickSlots = 5;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<CardView> spawnedViews = new List<CardView>();
    private int tableLayoutSlots;

    private void Awake()
    {
        ApplySelfHandDock();
    }

    private void OnEnable()
    {
        ApplySelfHandDock();
    }

    private void LateUpdate()
    {
        // 테이블용 HandView는 스킵 — 손패 컨테이너만 매 프레임 하단 도킹
        if (IsSelfHandContainer())
            ApplySelfHandDock();
        else if (cardContainer != null && cardContainer.name == "TableContainer")
        {
            RectTransform table = cardContainer as RectTransform;
            table.anchorMin = table.anchorMax = new Vector2(0.5f, 0.51f);
            table.anchoredPosition = Vector2.zero;
            table.sizeDelta = new Vector2(ResponsiveCanvas.IsPortrait ? 540f : 960f, 560f);
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null)
                    ((RectTransform)spawned[i].transform).anchoredPosition = GetTableSlotLocalPosition(i, TableTrickSlots);
        }
    }

    private bool IsSelfHandContainer()
    {
        return cardContainer != null && cardContainer.name == "HandContainer";
    }

    // 상대 핸드처럼 화면 하단에 절반 걸치게 한 뒤, 카드 높이×0.3 만큼 위로
    public void ApplySelfHandDock()
    {
        if (!IsSelfHandContainer()) return;
        RectTransform rt = cardContainer as RectTransform;
        if (rt == null) return;

        Canvas canvas = rt.GetComponentInParent<Canvas>();
        RectTransform canvasRt = canvas != null ? ResponsiveCanvas.Content(canvas) as RectTransform : null;
        if (canvasRt != null)
            Canvas.ForceUpdateCanvases();

        float canvasH = (canvasRt != null && canvasRt.rect.height > 1f)
            ? canvasRt.rect.height
            : Mathf.Max(1f, ResponsiveCanvas.ViewHeight);

        float cardH = CardSpriteAtlas.DisplayHeight;
        // 1) 중심을 화면 하단(y=0)에 두면 상대처럼 절반만 보임
        // 2) 그다음 카드 높이의 SelfHandLiftFromEdge 만큼 위로
        float scale = canvas != null ? Mathf.Max(0.001f, canvas.scaleFactor) : 1f;
        float centerY = ResponsiveCanvas.SafeArea.yMin / scale + cardH * 0.5f + 20f;

        OpponentHandsView.RefreshSelfHandMetrics(canvasH, cardH);
        OpponentHandsView.SetSelfHandCenterFromBottom(centerY);

        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2((ResponsiveCanvas.SafeArea.center.x - ResponsiveCanvas.ViewWidth * 0.5f) / scale, centerY);
        rt.sizeDelta = new Vector2(Mathf.Min(1700f, ResponsiveCanvas.SafeArea.width / scale - 40f), cardH);

        // HLG는 Y를 매 프레임 다시 쓰기 때문에 손패는 수동 배치로 통일
        HorizontalLayoutGroup hlg = rt.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        RelayoutHandCards();
    }

    // HandContainer 중심(Y=0)에 카드 중심을 맞추고 X만 펼침 (구 HLG spacing -40과 동일)
    private void RelayoutHandCards()
    {
        if (!IsSelfHandContainer() || cardContainer == null) return;
        RectTransform parent = cardContainer as RectTransform;
        if (parent == null) return;

        HorizontalLayoutGroup hlg = parent.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        float cardW = CardSpriteAtlas.DisplayWidth;
        float cardH = CardSpriteAtlas.DisplayHeight;
        float step = cardW - 40f; // 기존 m_Spacing: -40
        int n = 0;
        for (int i = 0; i < spawnedViews.Count; i++)
        {
            if (spawnedViews[i] != null) n++;
        }
        if (n <= 0) return;

        float availableW = parent.rect.width > 1f
            ? Mathf.Max(cardW, parent.rect.width - 32f)
            : Mathf.Max(cardW, ResponsiveCanvas.ViewWidth - 32f);
        if (n > 1)
            step = Mathf.Min(step, Mathf.Max(cardW * 0.24f, (availableW - cardW) / (n - 1)));

        float startX = -((n - 1) * step) * 0.5f;
        int slot = 0;
        for (int i = 0; i < spawnedViews.Count; i++)
        {
            CardView view = spawnedViews[i];
            if (view == null) continue;
            RectTransform crt = view.GetComponent<RectTransform>();
            if (crt == null) continue;
            crt.anchorMin = new Vector2(0.5f, 0.5f);
            crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(cardW, cardH);
            crt.anchoredPosition = new Vector2(startX + slot * step, 0f);
            view.SetRestAnchoredY(0f);
            slot++;
        }
    }

    public void ShowHand(CardData[] cards)
    {
        ShowInternal(cards, sort: true);
    }

    // 테이블용: 정렬하지 않고 왼쪽→오른쪽 낸 순서
    public void ShowCardsInOrder(CardData[] cards)
    {
        if (cards == null || cards.Length == 0)
        {
            Clear();
            return;
        }
        var entries = new TableCardEntry[cards.Length];
        for (int i = 0; i < cards.Length; i++)
            entries[i] = new TableCardEntry { card = cards[i], playerNickname = null };
        ShowTableCards(entries);
    }

    // 테이블용: 카드 + 제출자 닉네임 (항상 TableTrickSlots 기준 고정 슬롯)
    public void ShowTableCards(TableCardEntry[] entries)
    {
        ShowTableCards(entries, TableTrickSlots);
    }

    public void ShowTableCards(TableCardEntry[] entries, int slotCount, bool highlightWinner = true)
    {
        int slots = Mathf.Max(slotCount, 1);
        // Keep landed cards (and their running shadow pulse) when appending a
        // card or refreshing the same trick after its flight finishes.
        bool reuse = entries != null && entries.Length > 0 && tableLayoutSlots == slots
            && spawnedViews.Count == spawned.Count && spawnedViews.Count <= entries.Length;
        for (int i = 0; reuse && i < spawnedViews.Count; i++)
            reuse = spawned[i] != null && spawnedViews[i] != null && spawnedViews[i].Card != null
                && entries[i].card != null && spawnedViews[i].Card.id == entries[i].card.id;
        if (!reuse) Clear();
        else if (spawnedViews.Count < entries.Length) ContentRevision++;
        EnsureTableContainerActive();
        if (entries == null || cardPrefab == null) return;
        tableLayoutSlots = slots;

        Transform parent = cardContainer != null ? cardContainer : transform;
        // HLG는 장수에 따라 가운데로 다시 모으므로, 고정 슬롯은 수동 배치
        HorizontalLayoutGroup hlg = parent.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        float cardW = CardSpriteAtlas.DisplayWidth;
        float cardH = CardSpriteAtlas.DisplayHeight;

        for (int i = spawnedViews.Count; i < entries.Length; i++)
        {
            TableCardEntry e = entries[i];
            if (e.card == null) continue;

            float slotX = GetTableSlotLocalX(i, slots);

            // 슬롯 루트(숨김 단위). 카드 피벗 = 루트 원점 → 비행 도착점과 동일.
            // VerticalLayoutGroup 사용 금지: 생성 직후 ForceUpdate 전 좌표가 어긋나 착지 점프 발생.
            GameObject root = new GameObject("TableSlot", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = Vector2.zero;
            rootRt.anchoredPosition = GetTableSlotLocalPosition(i, slots);

            CardView view = Instantiate(cardPrefab, root.transform);
            ApplyHandCardSize(view);
            RectTransform vrt = view.GetComponent<RectTransform>();
            if (vrt != null)
            {
                vrt.anchorMin = new Vector2(0.5f, 0.5f);
                vrt.anchorMax = new Vector2(0.5f, 0.5f);
                vrt.pivot = new Vector2(0.5f, 0.5f);
                vrt.anchoredPosition = Vector2.zero;
                vrt.sizeDelta = new Vector2(cardW, cardH);
            }
            view.SetCard(e.card);
            if (e.jokerCallActivated) AddJokerCallOverlay(view.transform);
            view.Clicked = null;
            // 이전 애니 CanvasGroup/alpha 잔여 방지
            CanvasGroup leftover = view.GetComponent<CanvasGroup>();
            if (leftover != null) leftover.alpha = 1f;
            if (view.background != null)
            {
                Color bc = view.background.color;
                bc.a = 1f;
                view.background.color = bc;
            }

            TextMeshProUGUI label = UiTmp.Create(
                root.transform, "Nick", TableLabelFontSize, TextAnchor.MiddleCenter,
                new Color(0.95f, 0.95f, 0.92f, 1f), overflow: false);
            RectTransform lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(cardW, TableLabelHeight);
            // 카드 하단 바로 아래 (카드 피벗은 0,0 유지 → 비행 착지와 동일)
            lrt.anchoredPosition = new Vector2(0f, -(cardH * 0.5f + TableLabelHeight * 0.5f));
            label.text = FormatTableNickname(e.playerNickname);
            label.transform.SetAsLastSibling();

            spawned.Add(root);
            spawnedViews.Add(view);
        }

        for (int i = 0; i < spawnedViews.Count && i < entries.Length; i++)
        {
            CardView view = spawnedViews[i];
            if (view == null || spawned[i] == null) continue;
            TableCardEntry entry = entries[i];
            Transform slot = spawned[i].transform;
            ((RectTransform)slot).anchoredPosition = GetTableSlotLocalPosition(i, slots);
            Transform nick = slot.Find("Nick");
            if (nick != null) nick.GetComponent<TextMeshProUGUI>().text = FormatTableNickname(entry.playerNickname);

            // Refresh roles without replacing the landed card or its glow.
            for (int c = slot.childCount - 1; c >= 0; c--)
            {
                Transform child = slot.GetChild(c);
                if (!child.name.StartsWith("TopIcon")) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            var above = new List<IconSpriteAtlas.Slice>();
            if (entry.isDeclarer) above.Add(IconSpriteAtlas.GetDeclarer());
            if (entry.isFriend) above.Add(IconSpriteAtlas.GetFriend());
            else if (entry.isFriendSecret) above.Add(IconSpriteAtlas.GetFriendSecret());
            if (entry.card != null && entry.card.id == "JOKER" && !string.IsNullOrEmpty(entry.declaredSuit))
                above.Add(IconSpriteAtlas.GetSuit(entry.declaredSuit));
            PlaceIconsAbove(slot, above, cardH * 0.5f + IconSpriteAtlas.DisplaySquare.y * 0.5f + 4f);

            // During flight, leave the previous landed winner untouched.
            // The queued state supplies the new winner only once landing ends.
            if (!highlightWinner) continue;
            var glow = view.GetComponent<WinningCardGlow>();
            bool winning = entries.Length >= 2 && entry.isWinning;
            if (winning) WinningCardGlow.Attach(view.transform);
            else if (glow != null) glow.enabled = false;

            CanvasGroup group = view.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
            foreach (Graphic graphic in slot.GetComponentsInChildren<Graphic>(true))
            {
                Color color = graphic.color;
                color.a = 1f;
                graphic.color = color;
            }
        }
    }

    private static void PlaceIconsAbove(Transform parent, List<IconSpriteAtlas.Slice> slices, float y)
    {
        if (slices == null || slices.Count == 0) return;
        Vector2 sz = IconSpriteAtlas.DisplaySquare;
        float gap = 4f;
        float total = slices.Count * sz.x + (slices.Count - 1) * gap;
        float x = -total * 0.5f + sz.x * 0.5f;
        for (int i = 0; i < slices.Count; i++)
        {
            Image img = IconGui.MakeImage(parent, "TopIcon" + i, slices[i], sz);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            x += sz.x + gap;
        }
    }

    private static void AddJokerCallOverlay(Transform parent)
    {
        var box = new GameObject("JokerCallOverlay", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(parent, false);
        var rect = box.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(-12f, 42f);
        rect.anchoredPosition = Vector2.zero;
        var background = box.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = false;
        var text = UiTmp.Create(box.transform, "Label", 22, TextAnchor.MiddleCenter, Color.white);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(6f, 4f);
        text.rectTransform.offsetMax = new Vector2(-6f, -4f);
        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = 22;
        LocalizedLabel.Bind(text, () => GameSettings.Language == "en" ? "Joker Call" : "조커콜");
    }

    private GameObject selfRoleRoot;

    public void SetSelfRoleBadges(bool isDeclarer, bool isFriend, bool friendSecret = false)
    {
        if (selfRoleRoot != null)
        {
            Destroy(selfRoleRoot);
            selfRoleRoot = null;
        }
        if (!isDeclarer && !isFriend && !friendSecret) return;

        selfRoleRoot = new GameObject("SelfRoles", typeof(RectTransform));
        selfRoleRoot.transform.SetParent(transform, false);
        RectTransform rt = selfRoleRoot.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 6f);
        rt.sizeDelta = new Vector2(UiFonts.Layout(120f), IconSpriteAtlas.DisplaySquare.y);
        IconGui.PlaceRoleIcons(selfRoleRoot.transform, isDeclarer, isFriend, Vector2.zero, friendSecret);
    }

    private static string FormatTableNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return "";
        string s = nickname.Trim();
        if (s.Length <= TableNickMaxChars) return s;
        return s.Substring(0, TableNickMaxChars - 1) + "…";
    }

    private void ShowInternal(CardData[] cards, bool sort)
    {
        Clear();
        if (cards == null) return;

        CardData[] list = sort ? SortCards(cards) : cards;
        Transform parent = cardContainer != null ? cardContainer : transform;
        foreach (CardData card in list)
        {
            CardView view = Instantiate(cardPrefab, parent);
            ApplyHandCardSize(view);
            view.SetCard(card);
            view.Clicked = onCardClicked;
            spawned.Add(view.gameObject);
            spawnedViews.Add(view);
        }
        RelayoutHandCards();
    }

    public void ShowFaceDown(int count)
    {
        Clear();
        if (count <= 0 || cardPrefab == null) return;
        Transform parent = cardContainer != null ? cardContainer : transform;
        for (int i = 0; i < count; i++)
        {
            CardView view = Instantiate(cardPrefab, parent);
            ApplyHandCardSize(view);
            view.SetFaceDown();
            view.Clicked = null;
            spawned.Add(view.gameObject);
            spawnedViews.Add(view);
        }
        RelayoutHandCards();
    }

    public void SetAllPlayable(bool playable, bool dimDisabled = true)
    {
        foreach (CardView v in spawnedViews)
        {
            if (v != null && v.Card != null) v.SetPlayable(playable, dimDisabled);
        }
    }

    public void ApplyPlayability(Func<CardData, bool> canPlay)
    {
        foreach (CardView v in spawnedViews)
        {
            if (v == null || v.Card == null) continue;
            bool ok = canPlay == null || canPlay(v.Card);
            v.SetPlayable(ok);
        }
    }

    // 바닥패 버리기: 선택된 카드 id는 위로 올림
    public void ApplyDiscardSelectionRaise(ICollection<string> selectedIds)
    {
        foreach (CardView v in spawnedViews)
        {
            if (v == null || v.Card == null) continue;
            bool sel = selectedIds != null && selectedIds.Contains(v.Card.id);
            v.SetSelectedRaised(sel);
        }
    }

    public void ClearDiscardSelectionRaise()
    {
        foreach (CardView v in spawnedViews)
        {
            if (v != null) v.SetSelectedRaised(false);
        }
    }

    private static void ApplyHandCardSize(CardView view)
    {
        if (view == null) return;
        RectTransform rt = view.GetComponent<RectTransform>();
        if (rt != null)
            rt.sizeDelta = new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        view.RefreshDropShadow();
    }

    public static CardData[] SortCards(CardData[] cards)
    {
        if (cards == null || cards.Length == 0) return cards;
        CardData[] copy = (CardData[])cards.Clone();
        Array.Sort(copy, CompareCards);
        return copy;
    }

    private static int CompareCards(CardData a, CardData b)
    {
        int sa = SuitOrder(a != null ? a.suit : null);
        int sb = SuitOrder(b != null ? b.suit : null);
        if (sa != sb) return sa.CompareTo(sb);
        return RankOrder(b != null ? b.rank : null).CompareTo(RankOrder(a != null ? a.rank : null));
    }

    private static int SuitOrder(string suit)
    {
        switch (suit)
        {
            case "SPADE": return 0;
            case "HEART": return 1;
            case "DIAMOND": return 2;
            case "CLUB": return 3;
            case "JOKER": return 4;
            default: return 9;
        }
    }

    private static int RankOrder(string rank)
    {
        switch (rank)
        {
            case "2": return 0;
            case "3": return 1;
            case "4": return 2;
            case "5": return 3;
            case "6": return 4;
            case "7": return 5;
            case "8": return 6;
            case "9": return 7;
            case "10": return 8;
            case "J": return 9;
            case "Q": return 10;
            case "K": return 11;
            case "A": return 12;
            case "JOKER": return 13;
            default: return -1;
        }
    }

    public int ContentRevision { get; private set; }

    public void Clear()
    {
        tableLayoutSlots = 0;
        ContentRevision++;
        EnsureTableContainerActive();
        foreach (GameObject go in spawned)
        {
            if (go != null) Destroy(go);
        }
        spawned.Clear();
        spawnedViews.Clear();

        // 추적 목록 밖 고아(재접속·이전 라운드 잔여)도 컨테이너에서 제거
        Transform parent = cardContainer != null ? cardContainer : transform;
        if (parent != null)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name != "SelfRoles") Destroy(child.gameObject);
            }
        }
    }

    // 손패에서 카드 월드 위치 (제출 애니 시작점)
    public bool TryGetCardWorldPosition(string cardId, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (string.IsNullOrEmpty(cardId)) return false;
        foreach (CardView v in spawnedViews)
        {
            if (v == null || v.Card == null) continue;
            if (v.Card.id == cardId)
            {
                worldPos = v.transform.position;
                return true;
            }
        }
        return false;
    }

    // 테이블에 이미 배치된 index번째 카드의 실제 월드 좌표 (비행 도착점용)
    public bool TryGetSpawnedCardWorldPosition(int index, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        RectTransform rt;
        if (!TryGetSpawnedCardRect(index, out rt)) return false;
        worldPos = rt.position;
        return true;
    }

    public bool TryGetSpawnedCardRect(int index, out RectTransform cardRt)
    {
        cardRt = null;
        if (index < 0 || index >= spawnedViews.Count) return false;
        CardView v = spawnedViews[index];
        if (v == null) return false;
        cardRt = v.transform as RectTransform;
        return cardRt != null;
    }

    public void SetTableSlotVisible(int index, bool visible)
    {
        if (index < 0 || index >= spawned.Count) return;
        GameObject go = spawned[index];
        if (go != null) go.SetActive(visible);
    }

    // 테이블에 보이는 점수카드(point>0) 월드 좌표 목록
    public void CollectPointCardWorldPositions(List<CardData> cardsOut, List<Vector3> positionsOut)
    {
        if (cardsOut != null) cardsOut.Clear();
        if (positionsOut != null) positionsOut.Clear();
        if (cardsOut == null || positionsOut == null) return;
        foreach (CardView v in spawnedViews)
        {
            if (v == null || v.Card == null || v.Card.point <= 0) continue;
            cardsOut.Add(v.Card);
            positionsOut.Add(v.transform.position);
        }
    }

    public void CollectPointCards(List<CardData> cardsOut)
    {
        if (cardsOut == null) return;
        cardsOut.Clear();
        foreach (CardView v in spawnedViews)
        {
            if (v == null || v.Card == null || v.Card.point <= 0) continue;
            cardsOut.Add(v.Card);
        }
    }

    public Vector3 GetLayoutCenterWorldPosition()
    {
        Transform parent = cardContainer != null ? cardContainer : transform;
        RectTransform rt = parent as RectTransform;
        if (rt == null) return parent.position;
        // pivot이 하단이어도 카드 영역 중심을 반환
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        return (corners[0] + corners[2]) * 0.5f;
    }

    // 슬롯 루트: 닉네임 컬럼이면 그 컬럼, 카드가 컨테이너 직속이면 카드 자신
    // (직속일 때 parent를 끄면 TableContainer 전체가 비활성화되어 이후 패가 안 보임)
    private Transform GetTableSlotRoot(CardView v)
    {
        if (v == null) return null;
        Transform parent = cardContainer != null ? cardContainer : transform;
        Transform t = v.transform;
        if (t.parent != null && t.parent != parent)
            return t.parent;
        return t;
    }

    private void EnsureTableContainerActive()
    {
        Transform parent = cardContainer != null ? cardContainer : transform;
        if (parent != null && !parent.gameObject.activeSelf)
            parent.gameObject.SetActive(true);
    }

    // 점수카드 슬롯 즉시 숨김 (비행 복제본과 중복 방지)
    public void HidePointCardSlots()
    {
        EnsureTableContainerActive();
        for (int i = 0; i < spawnedViews.Count; i++)
        {
            CardView v = spawnedViews[i];
            if (v == null || v.Card == null || v.Card.point <= 0) continue;
            Transform slot = GetTableSlotRoot(v);
            if (slot != null) slot.gameObject.SetActive(false);
        }
    }

    // 비전수 카드 슬롯 fade-out
    public IEnumerator CoFadeNonPointSlots(float duration)
    {
        EnsureTableContainerActive();
        var groups = new List<CanvasGroup>();
        for (int i = 0; i < spawnedViews.Count; i++)
        {
            CardView v = spawnedViews[i];
            if (v == null || v.Card == null || v.Card.point > 0) continue;
            Transform slot = GetTableSlotRoot(v);
            if (slot == null || !slot.gameObject.activeInHierarchy) continue;
            CanvasGroup cg = slot.GetComponent<CanvasGroup>();
            if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            groups.Add(cg);
        }

        if (groups.Count == 0 || duration <= 0.01f)
        {
            for (int i = 0; i < groups.Count; i++)
                if (groups[i] != null) groups[i].alpha = 0f;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / duration);
            for (int i = 0; i < groups.Count; i++)
                if (groups[i] != null) groups[i].alpha = a;
            yield return null;
        }
        for (int i = 0; i < groups.Count; i++)
            if (groups[i] != null) groups[i].alpha = 0f;
    }

    // 테이블 n장일 때 index번째 슬롯의 카드 중심 월드 좌표
    public Vector3 GetTableSlotWorldPosition(int index, int totalCount)
    {
        Transform parent = cardContainer != null ? cardContainer : transform;
        int n = Mathf.Max(totalCount, 1);
        int i = Mathf.Clamp(index, 0, n - 1);
        return parent.TransformPoint(GetTableSlotLocalPosition(i, n));
    }

    private Vector2 GetTableSlotLocalPosition(int index, int totalCount)
    {
        if (!ResponsiveCanvas.IsPortrait)
            return new Vector2(GetTableSlotLocalX(index, totalCount), 0f);
        // Fixed 3 + 2 slots keep submitted cards and flight destinations identical.
        int i = Mathf.Clamp(index, 0, 4);
        return i < 3 ? new Vector2((i - 1) * 174f, 146f)
            : new Vector2((i - 3.5f) * 174f, -146f);
    }

    private float GetTableSlotLocalX(int index, int totalCount)
    {
        Transform parent = cardContainer != null ? cardContainer : transform;
        float cardW = CardSpriteAtlas.DisplayWidth;
        float spacing = 8f;
        HorizontalLayoutGroup hlg = parent != null ? parent.GetComponent<HorizontalLayoutGroup>() : null;
        if (hlg != null) spacing = hlg.spacing;
        int n = Mathf.Max(totalCount, 1);
        int i = Mathf.Clamp(index, 0, n - 1);
        float step = cardW + spacing;
        RectTransform parentRt = parent as RectTransform;
        float availableW = parentRt != null && parentRt.rect.width > 1f
            ? parentRt.rect.width - 32f
            : ResponsiveCanvas.ViewWidth - 32f;
        if (n > 1 && n * cardW + (n - 1) * spacing > availableW)
            step = Mathf.Max(cardW * 0.5f, (availableW - cardW) / (n - 1));
        float totalW = cardW + Mathf.Max(0, n - 1) * step;
        return -totalW * 0.5f + cardW * 0.5f + i * step;
    }

    public Vector2 HandCardSize
    {
        get
        {
            return new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        }
    }
}
