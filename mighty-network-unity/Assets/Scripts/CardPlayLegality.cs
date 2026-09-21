using UnityEngine;

// 서버 RuleEngine.canPlayCard 와 동일한 클라 판정 (하이라이트/클릭 차단용)
public static class CardPlayLegality
{
    public static bool IsLeadPosition(int tableCount)
    {
        return tableCount <= 0 || tableCount >= 5;
    }

    public static bool CanPlay(
        CardData card,
        CardData[] hand,
        TableCardSnapshot[] table,
        string mightyCardId,
        string jokerCallCardId)
    {
        if (card == null || hand == null) return false;
        int n = table != null ? table.Length : 0;
        if (IsLeadPosition(n)) return true;

        TableCardSnapshot lead = table[0];
        if (lead.card == null) return true;

        bool jokerCallActive = lead.jokerCallActivated
            && !string.IsNullOrEmpty(jokerCallCardId)
            && lead.card.id == jokerCallCardId;

        if (jokerCallActive)
        {
            bool hasJoker = HandHasJoker(hand);
            if (hasJoker)
            {
                if (IsJoker(card)) return true;
                return false;
            }
        }

        string leadSuit = LeadSuit(lead);
        if (string.IsNullOrEmpty(leadSuit)) return true;
        if (IsJoker(card) || IsMighty(card, mightyCardId)) return true;

        if (HandHasSuit(hand, leadSuit))
            return card.suit == leadSuit;
        return true;
    }

    public static string LeadSuit(TableCardSnapshot lead)
    {
        if (lead.card == null) return null;
        if (IsJoker(lead.card))
            return string.IsNullOrEmpty(lead.declaredSuit) ? null : lead.declaredSuit;
        return lead.card.suit;
    }

    private static bool IsJoker(CardData c)
    {
        return c != null && (c.id == "JOKER" || c.suit == "JOKER");
    }

    private static bool IsMighty(CardData c, string mightyCardId)
    {
        return c != null && !string.IsNullOrEmpty(mightyCardId) && c.id == mightyCardId;
    }

    private static bool HandHasJoker(CardData[] hand)
    {
        foreach (CardData c in hand)
            if (IsJoker(c)) return true;
        return false;
    }

    private static bool HandHasSuit(CardData[] hand, string suit)
    {
        foreach (CardData c in hand)
        {
            if (c == null || IsJoker(c)) continue;
            if (c.suit == suit) return true;
        }
        return false;
    }
}

// NetworkManager TableCardInfo 와 동일한 필드 (공용 판정용)
[System.Serializable]
public struct TableCardSnapshot
{
    public CardData card;
    public string declaredSuit;
    public bool jokerCallActivated;
}
