using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// HandView: 손패/테이블 카드 나열.
//   ShowHand(cards): 손패 — 무늬·랭크 정렬
//   ShowCardsInOrder(cards): 테이블 — 낸 순서 그대로
// ============================================================================
public class HandView : MonoBehaviour
{
    [Header("Inspector에서 연결")]
    public CardView cardPrefab;
    public Transform cardContainer;

    public System.Action<CardData> onCardClicked;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<CardView> spawnedViews = new List<CardView>();

    public void ShowHand(CardData[] cards)
    {
        ShowInternal(cards, sort: true);
    }

    // 테이블용: 정렬하지 않고 왼쪽→오른쪽 낸 순서
    public void ShowCardsInOrder(CardData[] cards)
    {
        ShowInternal(cards, sort: false);
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
    }
}
