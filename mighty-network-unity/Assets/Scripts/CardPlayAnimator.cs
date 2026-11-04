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
        public bool isMighty;
        public string declaredSuit;
        public Action onComplete;
    }

    private CardView cardPrefab;
    private RectTransform flyLayer;
    private bool busy;
    private CardView activeFlyView;
    private RectTransform activeDestination;
    private readonly Queue<FlyRequest> queue = new Queue<FlyRequest>();

    public bool IsBusy { get { return busy || queue.Count > 0; } }

    public void Cancel()
    {
        queue.Clear();
        StopAllCoroutines();
        CleanupActiveFlight();
        if (flyLayer != null)
            for (int i = flyLayer.childCount - 1; i >= 0; i--)
                Destroy(flyLayer.GetChild(i).gameObject);
        busy = false;
    }

    private void OnDisable() { Cancel(); }

    private void CleanupActiveFlight()
    {
        if (activeFlyView != null)
        {
            activeFlyView.gameObject.SetActive(false);
            Destroy(activeFlyView.gameObject);
        }
        activeFlyView = null;
        if (activeDestination != null)
        {
            SetGraphicsVisible(activeDestination.gameObject, true);
            Transform parent = activeDestination.parent;
            Transform nick = parent != null ? parent.Find("Nick") : null;
            if (nick != null) SetGraphicsVisible(nick.gameObject, true);
            ShowTopIcons(parent);
        }
        activeDestination = null;
    }

    public void Configure(CardView prefab, Canvas canvas)
    {
        cardPrefab = prefab;
        if (flyLayer != null || canvas == null) return;

        GameObject go = new GameObject("CardFlyLayer", typeof(RectTransform));
        go.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
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
        Action onComplete,
        bool isMighty = false)
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
            isMighty = isMighty,
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
            activeDestination = destCard;

            // 목적 카드/닉네임: Graphic만 끄기 (CanvasGroup alpha 잔류 버그 회피)
            SetGraphicsVisible(destCard.gameObject, false);
            Transform destParentNick = destParent != null ? destParent.Find("Nick") : null;
            if (destParentNick != null) SetGraphicsVisible(destParentNick.gameObject, false);
            HideTopIcons(destParent);

            flyView = Instantiate(cardPrefab, destParent);
            activeFlyView = flyView;
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
            bool isJoker = req.card.id == "JOKER" && !req.isMighty;
            CanvasGroup reveal = null;
            RectTransform[] echoes = null;
            CanvasGroup[] echoAlpha = null;
            if (isJoker)
            {
                reveal = flyView.GetComponent<CanvasGroup>();
                if (reveal == null) reveal = flyView.gameObject.AddComponent<CanvasGroup>();
                reveal.alpha = 0f;
                reveal.blocksRaycasts = false;
                reveal.interactable = false;
                rt.anchoredPosition = endLocal + Vector2.up * 12f;
                rt.sizeDelta = req.endSize;
                rt.localScale = Vector3.one * 0.96f;
                echoes = new RectTransform[2];
                echoAlpha = new CanvasGroup[2];
                for (int i = 0; i < 2; i++)
                {
                    // Children are automatically cleaned up with the flight on cancellation.
                    CardView echo = Instantiate(cardPrefab, rt);
                    echo.name = "JokerEcho" + i;
                    echo.Clicked = null;
                    echo.SetFlightMode(true);
                    echo.SetCard(req.card);
                    echo.SetPlayable(true);
                    echoes[i] = echo.GetComponent<RectTransform>();
                    echoes[i].anchorMin = echoes[i].anchorMax = echoes[i].pivot = Vector2.one * 0.5f;
                    echoes[i].sizeDelta = req.endSize;
                    echoes[i].localScale = Vector3.one;
                    echoes[i].anchoredPosition = Vector2.zero;
                    foreach (Graphic graphic in echo.GetComponentsInChildren<Graphic>(true))
                        graphic.raycastTarget = false;
                    echoAlpha[i] = echo.GetComponent<CanvasGroup>();
                    if (echoAlpha[i] == null) echoAlpha[i] = echo.gameObject.AddComponent<CanvasGroup>();
                    echoAlpha[i].alpha = 0f;
                    echoAlpha[i].blocksRaycasts = false;
                    echoAlpha[i].interactable = false;
                }
            }
            else Sfx.CardPlay();

            float t = 0f;
            float flightDuration = isJoker ? 0.7f : req.isMighty ? 0.42f : duration;
            while (t < flightDuration)
            {
                if (flyView == null || destCard == null)
                    yield break;
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / flightDuration);
                float e = 1f - Mathf.Pow(1f - u, 3f);
                if (isJoker)
                {
                    float split = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / 0.32f));
                    float merge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, u));
                    float spread = split * (1f - merge);
                    reveal.alpha = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(u / 0.25f));
                    rt.anchoredPosition = endLocal + Vector2.up * (req.endSize.y * 0.16f * (1f - merge));
                    rt.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, merge);
                    rt.sizeDelta = req.endSize;
                    for (int i = 0; i < echoes.Length; i++)
                    {
                        float side = i == 0 ? -1f : 1f;
                        echoes[i].anchoredPosition = new Vector2(side * req.endSize.x * 0.65f * spread,
                            req.endSize.y * 0.08f * spread);
                        echoes[i].localRotation = Quaternion.Euler(0, 0, -side * 14f * spread);
                        echoAlpha[i].alpha = 0.48f * spread;
                    }
                }
                else if (req.isMighty)
                {
                    // Gather above the table, briefly hang, then accelerate into impact.
                    Vector2 raised = endLocal + Vector2.up * req.endSize.y * 0.45f;
                    if (u < 0.65f)
                    {
                        float rise = 1f - Mathf.Pow(1f - u / 0.65f, 3f);
                        rt.anchoredPosition = Vector2.Lerp(startLocal, raised, rise);
                        rt.sizeDelta = Vector2.Lerp(req.startSize, req.endSize * 1.28f, rise);
                        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(rise * Mathf.PI) * -9f);
                    }
                    else
                    {
                        float slam = Mathf.Pow((u - 0.65f) / 0.35f, 3f);
                        rt.anchoredPosition = Vector2.Lerp(raised, endLocal, slam);
                        rt.sizeDelta = Vector2.Lerp(req.endSize * 1.28f, req.endSize, slam);
                        rt.localRotation = Quaternion.identity;
                    }
                }
                else
                {
                    rt.anchoredPosition = Vector2.LerpUnclamped(startLocal, endLocal, e);
                    rt.sizeDelta = Vector2.LerpUnclamped(req.startSize, req.endSize, e);
                }
                LayoutFlyBadges(badgeRts, rt.sizeDelta.y);
                flyView.RefreshDropShadow();
                yield return null;
            }

            if (req.isMighty && rt != null && destCard != null)
            {
                rt.anchoredPosition = endLocal;
                rt.sizeDelta = req.endSize;
                Sfx.CardLand(0.7f);
                MightyImpactEffect.Play(flyLayer, destCard.position, req.endSize.x);
                // Short squash and recoil, without moving the table or resetting its glow.
                for (float settle = 0f; settle < 0.14f; settle += Time.unscaledDeltaTime)
                {
                    if (rt == null || destCard == null) yield break;
                    float decay = 1f - settle / 0.14f;
                    float pulse = Mathf.Sin(settle / 0.14f * Mathf.PI * 3f) * decay;
                    rt.localScale = new Vector3(1f + 0.07f * pulse, 1f - 0.07f * pulse, 1f);
                    rt.anchoredPosition = endLocal + new Vector2(pulse * 3f, 0f);
                    yield return null;
                }
                rt.localScale = Vector3.one;
            }

            if (rt != null)
            {
                rt.anchoredPosition = endLocal;
                rt.sizeDelta = req.endSize;
            }
            if (!req.isMighty && !isJoker) Sfx.CardLand();
            else if (isJoker) Sfx.CardLand(1.2f);
        }
        finally
        {
            if (flyView != null) Destroy(flyView.gameObject);
            activeFlyView = null;
            activeDestination = null;

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
