using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// TrickWinAnimator: 토스트(검정 박스·흰 글씨·Galmuri11) → 비전수 fade / 점수카드 중앙→승자
// ============================================================================
public class TrickWinAnimator : MonoBehaviour
{
    public float toastHold = 0.85f;
    public float toastFade = 0.35f;
    public float nonPointFade = 0.35f;
    public float flyDuration = 0.5f;
    public float flyStagger = 0.07f;

    private CardView cardPrefab;
    private RectTransform flyLayer;
    private RectTransform toastRoot;
    private Image toastBg;
    private Text toastText;
    private CanvasGroup toastGroup;
    private bool busy;

    public bool IsBusy { get { return busy; } }

    public void Configure(CardView prefab, Canvas canvas)
    {
        cardPrefab = prefab;
        if (canvas == null) return;

        if (flyLayer == null)
        {
            GameObject go = new GameObject("TrickFlyLayer", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            flyLayer = go.GetComponent<RectTransform>();
            flyLayer.anchorMin = Vector2.zero;
            flyLayer.anchorMax = Vector2.one;
            flyLayer.offsetMin = Vector2.zero;
            flyLayer.offsetMax = Vector2.zero;
            flyLayer.SetAsLastSibling();
        }

        if (toastRoot == null)
        {
            GameObject root = new GameObject("TrickToast", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas.transform, false);
            toastRoot = root.GetComponent<RectTransform>();
            toastRoot.anchorMin = new Vector2(0.5f, 0.5f);
            toastRoot.anchorMax = new Vector2(0.5f, 0.5f);
            toastRoot.pivot = new Vector2(0.5f, 0.5f);
            toastRoot.sizeDelta = new Vector2(520f, 96f);
            toastRoot.anchoredPosition = new Vector2(0f, 90f);

            GameObject bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(toastRoot, false);
            RectTransform bgrt = bgGo.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;
            toastBg = bgGo.GetComponent<Image>();
            toastBg.raycastTarget = false;

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(toastRoot, false);
            RectTransform trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16f, 8f);
            trt.offsetMax = new Vector2(-16f, -8f);
            toastText = textGo.GetComponent<Text>();
            toastText.fontSize = 40;
            toastText.alignment = TextAnchor.MiddleCenter;
            toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
            toastText.verticalOverflow = VerticalWrapMode.Overflow;
            toastText.raycastTarget = false;

            toastGroup = root.GetComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastRoot.SetAsLastSibling();
        }

        ApplyToastStyle();
    }

    private void ApplyToastStyle()
    {
        if (toastBg != null)
            toastBg.color = Color.black;
        if (toastText == null) return;

        Font font = UiFonts.Primary;
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        toastText.font = font;
        toastText.color = Color.white;
    }

    public void Play(
        string winnerNickname,
        IList<CardData> pointCards,
        Vector3 tableCenterWorld,
        Vector3 winnerWorld,
        HandView tableView,
        Action onComplete)
    {
        if (busy)
        {
            if (onComplete != null) onComplete();
            return;
        }
        StartCoroutine(CoPlay(
            winnerNickname, pointCards, tableCenterWorld, winnerWorld, tableView, onComplete));
    }

    private IEnumerator CoPlay(
        string winnerNickname,
        IList<CardData> pointCards,
        Vector3 tableCenterWorld,
        Vector3 winnerWorld,
        HandView tableView,
        Action onComplete)
    {
        busy = true;

        // 1) 흰 배경 토스트 표시
        if (toastText != null && toastGroup != null && toastRoot != null)
        {
            ApplyToastStyle();
            toastText.text = string.IsNullOrEmpty(winnerNickname)
                ? "승리!"
                : (winnerNickname + " 승리!");
            toastGroup.alpha = 1f;
            toastRoot.SetAsLastSibling();
            if (flyLayer != null) flyLayer.SetAsLastSibling();

            float hold = 0f;
            while (hold < toastHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            // 2) 토스트 fade-out 과 동시에: 비전수 fade / 점수카드 중앙→승자
            if (tableView != null)
            {
                tableView.HidePointCardSlots();
                StartCoroutine(tableView.CoFadeNonPointSlots(nonPointFade));
            }

            Coroutine flyCo = StartCoroutine(
                CoFlyPointsFromCenter(pointCards, tableCenterWorld, winnerWorld));

            float fadeT = 0f;
            while (fadeT < toastFade)
            {
                fadeT += Time.unscaledDeltaTime;
                toastGroup.alpha = 1f - Mathf.Clamp01(fadeT / toastFade);
                yield return null;
            }
            toastGroup.alpha = 0f;

            if (flyCo != null)
                yield return flyCo;
        }
        else
        {
            if (tableView != null)
            {
                tableView.HidePointCardSlots();
                yield return tableView.CoFadeNonPointSlots(nonPointFade);
            }
            yield return CoFlyPointsFromCenter(pointCards, tableCenterWorld, winnerWorld);
        }

        if (tableView != null)
            tableView.Clear();

        busy = false;
        if (onComplete != null) onComplete();
    }

    private IEnumerator CoFlyPointsFromCenter(
        IList<CardData> pointCards,
        Vector3 centerWorld,
        Vector3 winnerWorld)
    {
        if (pointCards == null || pointCards.Count == 0 || cardPrefab == null || flyLayer == null)
            yield break;

        Vector2 startSize = new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        Vector2 endSize = startSize * 0.55f;
        int n = 0;
        for (int i = 0; i < pointCards.Count; i++)
        {
            if (pointCards[i] == null) continue;
            // 중앙에서 살짝 겹쳐 쌓인 뒤 출발
            Vector3 start = centerWorld + new Vector3((n - (pointCards.Count - 1) * 0.5f) * 12f, 0f, 0f);
            StartCoroutine(CoFlyOne(pointCards[i], start, winnerWorld, startSize, endSize, n * flyStagger));
            n++;
        }
        float total = flyDuration + flyStagger * Mathf.Max(0, n - 1) + 0.05f;
        yield return new WaitForSecondsRealtime(total);
    }

    private IEnumerator CoFlyOne(
        CardData card,
        Vector3 startWorld,
        Vector3 endWorld,
        Vector2 startSize,
        Vector2 endSize,
        float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

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
        Graphic[] graphics = view.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        float t = 0f;
        while (t < flyDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / flyDuration);
            float e = 1f - Mathf.Pow(1f - u, 3f);
            rt.position = Vector3.LerpUnclamped(startWorld, endWorld, e);
            rt.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, e);
            if (u > 0.7f && view.background != null)
            {
                Color c = view.background.color;
                c.a = Mathf.Lerp(1f, 0f, (u - 0.7f) / 0.3f);
                view.background.color = c;
            }
            view.RefreshDropShadow();
            yield return null;
        }
        if (view != null) Destroy(view.gameObject);
    }
}
