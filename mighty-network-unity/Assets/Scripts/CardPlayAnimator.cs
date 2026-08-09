using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// CardPlayAnimator: 제출 카드를 시작 위치 → 테이블 슬롯으로 이동
// ============================================================================
public class CardPlayAnimator : MonoBehaviour
{
    public float duration = 0.38f;

    private CardView cardPrefab;
    private RectTransform flyLayer;
    private bool busy;

    public bool IsBusy { get { return busy; } }

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
        Vector3 endWorld,
        Vector2 startSize,
        Vector2 endSize,
        Action onComplete)
    {
        if (cardPrefab == null || flyLayer == null || card == null)
        {
            if (onComplete != null) onComplete();
            return;
        }
        StartCoroutine(CoFly(card, startWorld, endWorld, startSize, endSize, onComplete));
    }

    private IEnumerator CoFly(
        CardData card,
        Vector3 startWorld,
        Vector3 endWorld,
        Vector2 startSize,
        Vector2 endSize,
        Action onComplete)
    {
        busy = true;
        CardView view = Instantiate(cardPrefab, flyLayer);
        view.Clicked = null;
        view.SetCard(card);
        view.SetPlayable(true);

        RectTransform rt = view.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = startSize;
        rt.position = startWorld;
        rt.localScale = Vector3.one;
        view.RefreshDropShadow();

        // 클릭 가로채지 않음
        Graphic[] graphics = view.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            // ease-out cubic
            float e = 1f - Mathf.Pow(1f - u, 3f);
            rt.position = Vector3.LerpUnclamped(startWorld, endWorld, e);
            rt.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, e);
            view.RefreshDropShadow();
            yield return null;
        }

        rt.position = endWorld;
        rt.sizeDelta = endSize;
        if (view != null) Destroy(view.gameObject);
        busy = false;
        if (onComplete != null) onComplete();
    }
}
