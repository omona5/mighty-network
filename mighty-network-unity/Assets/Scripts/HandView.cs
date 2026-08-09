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

    // 테이블용: 카드 + 제출자 닉네임
    public void ShowTableCards(TableCardEntry[] entries)
    {
        Clear();
        if (entries == null || cardPrefab == null) return;

        Transform parent = cardContainer != null ? cardContainer : transform;
        for (int i = 0; i < entries.Length; i++)
        {
            TableCardEntry e = entries[i];
            if (e.card == null) continue;

            GameObject col = new GameObject("TableSlot", typeof(RectTransform));
            col.transform.SetParent(parent, false);
            RectTransform colRt = col.GetComponent<RectTransform>();
            colRt.anchorMin = new Vector2(0.5f, 0.5f);
            colRt.anchorMax = new Vector2(0.5f, 0.5f);
            colRt.pivot = new Vector2(0.5f, 0.5f);
            float cardW = CardSpriteAtlas.DisplayWidth;
            float cardH = CardSpriteAtlas.DisplayHeight;
            colRt.sizeDelta = new Vector2(cardW, cardH + TableLabelHeight);

            VerticalLayoutGroup vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 0f;
            vlg.padding = new RectOffset(0, 0, 0, 0);

            CardView view = Instantiate(cardPrefab, col.transform);
            ApplyHandCardSize(view);
            LayoutElement cardLe = view.gameObject.GetComponent<LayoutElement>();
            if (cardLe == null) cardLe = view.gameObject.AddComponent<LayoutElement>();
            cardLe.preferredWidth = cardW;
            cardLe.preferredHeight = cardH;
            cardLe.minHeight = cardH;
            view.SetCard(e.card);
            view.Clicked = null; // 테이블 카드는 클릭 없음

            GameObject labelGo = new GameObject("Nick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(col.transform, false);
            RectTransform lrt = labelGo.GetComponent<RectTransform>();
            lrt.sizeDelta = new Vector2(cardW, TableLabelHeight);
            LayoutElement labelLe = labelGo.AddComponent<LayoutElement>();
            labelLe.preferredHeight = TableLabelHeight;
            labelLe.minHeight = TableLabelHeight;

            Text label = labelGo.GetComponent<Text>();
            label.font = GetTableLabelFont();
            label.fontSize = TableLabelFontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = new Color(0.95f, 0.95f, 0.92f, 1f);
            label.raycastTarget = false;
            label.text = FormatTableNickname(e.playerNickname);

            // 가독성용 얇은 그림자
            Shadow sh = labelGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.75f);
            sh.effectDistance = new Vector2(1f, -1f);

            spawned.Add(col);
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

    // 점수카드 슬롯 즉시 숨김 (비행 복제본과 중복 방지)
    public void HidePointCardSlots()
    {
        for (int i = 0; i < spawnedViews.Count; i++)
        {
            CardView v = spawnedViews[i];
            if (v == null || v.Card == null || v.Card.point <= 0) continue;
            Transform slot = v.transform.parent != null ? v.transform.parent : v.transform;
            if (slot != null) slot.gameObject.SetActive(false);
        }
    }

    // 비전수 카드 슬롯 fade-out
    public IEnumerator CoFadeNonPointSlots(float duration)
    {
        var groups = new List<CanvasGroup>();
        for (int i = 0; i < spawnedViews.Count; i++)
        {
            CardView v = spawnedViews[i];
            if (v == null || v.Card == null || v.Card.point > 0) continue;
            Transform slot = v.transform.parent != null ? v.transform.parent : v.transform;
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
        float cardW = CardSpriteAtlas.DisplayWidth;
        float spacing = 8f;
        HorizontalLayoutGroup hlg = parent.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) spacing = hlg.spacing;
        int n = Mathf.Max(totalCount, 1);
        int i = Mathf.Clamp(index, 0, n - 1);
        float totalW = n * cardW + Mathf.Max(0, n - 1) * spacing;
        float x = -totalW * 0.5f + cardW * 0.5f + i * (cardW + spacing);
        // 슬롯 = 카드+라벨 컬럼; 카드 중심은 컬럼 중심보다 라벨 높이의 절반만큼 위
        float y = TableLabelHeight * 0.5f;
        return parent.TransformPoint(new Vector3(x, y, 0f));
    }

    public Vector2 HandCardSize
    {
        get
        {
            return new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        }
    }
}
