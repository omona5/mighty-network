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
            onComplete = onComplete,
        });
        if (!busy)
            StartCoroutine(CoDrainQueue());
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
            Transform nick = destParent != null ? destParent.Find("Nick") : null;
            if (nick != null) SetGraphicsVisible(nick.gameObject, false);

            flyView = Instantiate(cardPrefab, destParent);
            flyView.Clicked = null;
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
            rt.SetAsLastSibling();

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
                flyView.RefreshDropShadow();
                yield return null;
            }

            if (rt != null)
            {
                rt.anchoredPosition = endLocal;
                rt.sizeDelta = req.endSize;
            }
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
}
