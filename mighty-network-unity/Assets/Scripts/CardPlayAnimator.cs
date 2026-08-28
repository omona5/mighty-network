using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// CardPlayAnimator: 제출 카드 → 테이블 슬롯 (같은 부모 로컬 보간)
// ============================================================================
public class CardPlayAnimator : MonoBehaviour
{
    public float duration = 0.38f;

    private struct FlyRequest
    {
        public CardData card;
        public Vector3 startWorld;
        public RectTransform destCard;
        public Vector2 startSize;
        public Vector2 endSize;
        public bool isDeclarer;
        public bool isFriend;
        public bool isFriendSecret;
        public string declaredSuit;
        public Action onComplete;
    }

    private CardView cardPrefab;
    private RectTransform flyLayer;
    private bool busy;
    private readonly Queue<FlyRequest> queue = new Queue<FlyRequest>();

    public bool IsBusy { get { return busy || queue.Count > 0; } }

    public void Configure(CardView prefab, Canvas canvas)
    {
        cardPrefab = prefab;
        if (flyLayer != null || canvas == null) return;

        GameObject go = new GameObject("CardFlyLayer", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        flyLayer = go.GetComponent<RectTransform>();
        flyLayer.anchorMin = Vector2.zero;
        flyLayer.anchorMax = Vector2.one;
        flyLayer.offsetMin = Vector2.zero;
        flyLayer.offsetMax = Vector2.zero;
        flyLayer.SetAsLastSibling();
    }

    public void AnimateToTable(
        CardData card,
        Vector3 startWorld,
        RectTransform destCard,
        Vector2 startSize,
        Vector2 endSize,
        bool isDeclarer,
        bool isFriend,
        bool isFriendSecret,
        string declaredSuit,
        Action onComplete)
    {
        if (cardPrefab == null || card == null || destCard == null)
        {
            if (onComplete != null) onComplete();
            return;
        }

        queue.Enqueue(new FlyRequest
        {
            card = card,
            startWorld = startWorld,
            destCard = destCard,
            startSize = startSize,
            endSize = endSize,
            isDeclarer = isDeclarer,
            isFriend = isFriend,
            isFriendSecret = isFriendSecret,
            declaredSuit = declaredSuit,
            onComplete = onComplete,
        });
        if (!busy)
            StartCoroutine(CoDrainQueue());
    }

    // 호환: 역할 아이콘 없이 비행
    public void AnimateToTable(
        CardData card,
        Vector3 startWorld,
        RectTransform destCard,
        Vector2 startSize,
        Vector2 endSize,
        Action onComplete)
    {
        AnimateToTable(card, startWorld, destCard, startSize, endSize, false, false, false, null, onComplete);
    }

    // 호환: friendSecret 없음
    public void AnimateToTable(
        CardData card,
        Vector3 startWorld,
        RectTransform destCard,
        Vector2 startSize,
        Vector2 endSize,
        bool isDeclarer,
        bool isFriend,
        string declaredSuit,
        Action onComplete)
    {
        AnimateToTable(card, startWorld, destCard, startSize, endSize, isDeclarer, isFriend, false, declaredSuit, onComplete);
    }

    private IEnumerator CoDrainQueue()
    {
        if (busy) yield break;
        busy = true;
        while (queue.Count > 0)
        {
            FlyRequest req = queue.Dequeue();
            yield return CoFlyOne(req);
        }
        busy = false;
    }

    private IEnumerator CoFlyOne(FlyRequest req)
    {
        Action onComplete = req.onComplete;
        RectTransform destCard = req.destCard;
        CardView flyView = null;

        try
        {
            if (destCard == null)
                yield break;

            Transform destParent = destCard.parent != null ? destCard.parent : destCard;

            // 목적 카드/닉네임: Graphic만 끄기 (CanvasGroup alpha 잔류 버그 회피)
            SetGraphicsVisible(destCard.gameObject, false);
            Transform destParentNick = destParent != null ? destParent.Find("Nick") : null;
            if (destParentNick != null) SetGraphicsVisible(destParentNick.gameObject, false);
            HideTopIcons(destParent);

            flyView = Instantiate(cardPrefab, destParent);
            flyView.Clicked = null;
            flyView.SetFlightMode(true);
            flyView.SetCard(req.card);
            flyView.SetPlayable(true);

            RectTransform rt = flyView.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.sizeDelta = req.startSize;
            rt.position = req.startWorld;
            Vector2 startLocal = rt.anchoredPosition;
            Vector2 endLocal = destCard.anchoredPosition;

            Graphic[] graphics = flyView.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;

            flyView.RefreshDropShadow();
            List<RectTransform> badgeRts = AttachFlyBadges(rt, req);
            rt.SetAsLastSibling();
            LayoutFlyBadges(badgeRts, rt.sizeDelta.y);
            Sfx.CardPlay();

            float t = 0f;
            while (t < duration)
            {
                if (flyView == null || destCard == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                float e = 1f - Mathf.Pow(1f - u, 3f);
                rt.anchoredPosition = Vector2.LerpUnclamped(startLocal, endLocal, e);
                rt.sizeDelta = Vector2.LerpUnclamped(req.startSize, req.endSize, e);
                LayoutFlyBadges(badgeRts, rt.sizeDelta.y);
                flyView.RefreshDropShadow();
                yield return null;
            }

            if (rt != null)
            {
                rt.anchoredPosition = endLocal;
                rt.sizeDelta = req.endSize;
            }
            Sfx.CardLand();
        }
        finally
        {
            if (flyView != null) Destroy(flyView.gameObject);

            // dest가 살아 있으면 반드시 다시 보이게
            if (destCard != null)
            {
                SetGraphicsVisible(destCard.gameObject, true);
                Transform nick = destCard.parent != null ? destCard.parent.Find("Nick") : null;
                if (nick != null) SetGraphicsVisible(nick.gameObject, true);
                ShowTopIcons(destCard.parent);
            }

            if (onComplete != null) onComplete();
        }
    }

    private static void SetGraphicsVisible(GameObject go, bool visible)
    {
        if (go == null) return;
        Graphic[] graphics = go.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] == null) continue;
            Color c = graphics[i].color;
            c.a = visible ? 1f : 0f;
            graphics[i].color = c;
        }
        // CanvasGroup 잔여 alpha도 정리
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = visible ? 1f : 0f;
    }

    private static void HideTopIcons(Transform parent)
    {
        SetTopIconsVisible(parent, false);
    }

    private static void ShowTopIcons(Transform parent)
    {
        SetTopIconsVisible(parent, true);
    }

    private static void SetTopIconsVisible(Transform parent, bool visible)
    {
        if (parent == null) return;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform c = parent.GetChild(i);
            if (c == null || c.name == null) continue;
            if (c.name.StartsWith("TopIcon"))
                SetGraphicsVisible(c.gameObject, visible);
        }
    }

    private static List<RectTransform> AttachFlyBadges(RectTransform cardRt, FlyRequest req)
    {
        var list = new List<RectTransform>();
        if (cardRt == null) return list;
        var slices = new List<IconSpriteAtlas.Slice>();
        if (req.isDeclarer) slices.Add(IconSpriteAtlas.GetDeclarer());
        if (req.isFriend) slices.Add(IconSpriteAtlas.GetFriend());
        else if (req.isFriendSecret) slices.Add(IconSpriteAtlas.GetFriendSecret());
        if (req.card != null && req.card.id == "JOKER" && !string.IsNullOrEmpty(req.declaredSuit))
            slices.Add(IconSpriteAtlas.GetSuit(req.declaredSuit));
        Vector2 sz = IconSpriteAtlas.DisplaySquare;
        float gap = 4f;
        float total = slices.Count * sz.x + Mathf.Max(0, slices.Count - 1) * gap;
        float x = slices.Count > 0 ? -total * 0.5f + sz.x * 0.5f : 0f;
        for (int i = 0; i < slices.Count; i++)
        {
            Image img = IconGui.MakeImage(cardRt, "FlyIcon" + i, slices[i], sz);
            RectTransform irt = img.rectTransform;
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(x, 0f);
            Graphic[] g = img.GetComponentsInChildren<Graphic>(true);
            for (int k = 0; k < g.Length; k++)
                g[k].raycastTarget = false;
            list.Add(irt);
            x += sz.x + gap;
        }
        return list;
    }

    private static void LayoutFlyBadges(List<RectTransform> badges, float cardH)
    {
        if (badges == null) return;
        Vector2 sz = IconSpriteAtlas.DisplaySquare;
        float y = cardH * 0.5f + sz.y * 0.5f + 4f;
        for (int i = 0; i < badges.Count; i++)
        {
            if (badges[i] == null) continue;
            Vector2 p = badges[i].anchoredPosition;
            p.y = y;
            badges[i].anchoredPosition = p;
        }
    }
}
