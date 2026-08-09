using System;
using System.Collections;
using System.Collections.Generic;
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
    }

    [Header("Inspector에서 연결")]
    public CardView cardPrefab;
    public Transform cardContainer;

    public System.Action<CardData> onCardClicked;

    private const float TableLabelHeight = 28f;
    private const int TableLabelFontSize = 20;
    private const int TableNickMaxChars = 8;
    // 5마: 트릭당 항상 5장 — 배치/비행 도착점을 이 슬롯 기준으로 고정
    public const int TableTrickSlots = 5;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<CardView> spawnedViews = new List<CardView>();
    private Font tableLabelFont;

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

    public void ShowTableCards(TableCardEntry[] entries, int slotCount)
    {
        Clear();
        EnsureTableContainerActive();
        if (entries == null || cardPrefab == null) return;

        Transform parent = cardContainer != null ? cardContainer : transform;
        int slots = Mathf.Max(slotCount, 1);
        // HLG는 장수에 따라 가운데로 다시 모으므로, 고정 슬롯은 수동 배치
        HorizontalLayoutGroup hlg = parent.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) hlg.enabled = false;

        float cardW = CardSpriteAtlas.DisplayWidth;
        float cardH = CardSpriteAtlas.DisplayHeight;

        for (int i = 0; i < entries.Length; i++)
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
            rootRt.anchoredPosition = new Vector2(slotX, 0f);

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

            GameObject labelGo = new GameObject("Nick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(root.transform, false);
            RectTransform lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(cardW, TableLabelHeight);
            // 카드 하단 바로 아래 (카드 피벗은 0,0 유지 → 비행 착지와 동일)
            lrt.anchoredPosition = new Vector2(0f, -(cardH * 0.5f + TableLabelHeight * 0.5f));

            Text label = labelGo.GetComponent<Text>();
            label.font = GetTableLabelFont();
            label.fontSize = TableLabelFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = new Color(0.95f, 0.95f, 0.92f, 1f);
            label.raycastTarget = false;
            label.text = FormatTableNickname(e.playerNickname);

            Shadow sh = labelGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.75f);
            sh.effectDistance = new Vector2(1f, -1f);
            labelGo.transform.SetAsLastSibling();

            spawned.Add(root);
            spawnedViews.Add(view);
        }
    }

    private static string FormatTableNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname)) return "";
        string s = nickname.Trim();
        if (s.Length <= TableNickMaxChars) return s;
        return s.Substring(0, TableNickMaxChars - 1) + "…";
    }

    private Font GetTableLabelFont()
    {
        if (tableLabelFont == null) tableLabelFont = UiFonts.Primary;
        return tableLabelFont != null ? tableLabelFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
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
    }

    public void SetAllPlayable(bool playable)
    {
        foreach (CardView v in spawnedViews)
        {
            if (v != null && v.Card != null) v.SetPlayable(playable);
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

    public void Clear()
    {
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
                if (child != null) Destroy(child.gameObject);
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
        return parent.position;
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
        float x = GetTableSlotLocalX(i, n);
        // 닉네임 OFF일 때 카드 중심 = y0 / ON이면 컬럼을 내려 카드 중심이 역시 y0
        float y = 0f;
        return parent.TransformPoint(new Vector3(x, y, 0f));
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
        float totalW = n * cardW + Mathf.Max(0, n - 1) * spacing;
        return -totalW * 0.5f + cardW * 0.5f + i * (cardW + spacing);
    }

    public Vector2 HandCardSize
    {
        get
        {
            return new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        }
    }
}
