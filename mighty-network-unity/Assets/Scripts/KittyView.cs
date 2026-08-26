using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// KittyView: 중앙 바닥패(뒷면) 표시 + 주공 자리로 날아가는 애니
// ============================================================================
public class KittyView : MonoBehaviour
{
    public float flyDuration = 0.5f;
    public float flyStagger = 0.07f;

    public CardView cardPrefab;
    public RectTransform pileRoot;

    private readonly List<CardView> pileViews = new List<CardView>();
    private RectTransform flyLayer;
    private bool busy;
    private Font hintFont;
    private Text hintText;

    public bool IsBusy { get { return busy; } }
    public int VisibleCount { get { return pileViews.Count; } }

    public void Configure(CardView prefab, Canvas canvas)
    {
        cardPrefab = prefab;
        if (canvas == null) return;

        if (pileRoot == null)
        {
            GameObject go = new GameObject("KittyPile", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            pileRoot = go.GetComponent<RectTransform>();
            pileRoot.anchorMin = new Vector2(0.5f, 0.5f);
            pileRoot.anchorMax = new Vector2(0.5f, 0.5f);
            pileRoot.pivot = new Vector2(0.5f, 0.5f);
            pileRoot.anchoredPosition = new Vector2(0f, 40f);
            pileRoot.sizeDelta = new Vector2(
                CardSpriteAtlas.DisplayWidth * 3.2f,
                CardSpriteAtlas.DisplayHeight + 8f);
            // HLG 사용 안 함 — 딜 펼침과 동일 절대좌표 (전환 시 점프 방지)

            GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintGo.transform.SetParent(pileRoot, false);
            RectTransform hrt = hintGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -4f);
            hrt.sizeDelta = new Vector2(UiFonts.Layout(200f), UiFonts.Layout(32f));
            hintText = hintGo.GetComponent<Text>();
            hintText.font = GetHintFont();
            hintText.fontSize = UiFonts.Size(16);
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.9f, 0.9f, 0.85f, 0.9f);
            hintText.text = "바닥패";
            hintText.raycastTarget = false;
            hintGo.SetActive(false);
        }
        else
        {
            // 기존 세션에 HLG가 남아 있으면 제거
            HorizontalLayoutGroup hlg = pileRoot.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) Destroy(hlg);
            pileRoot.anchoredPosition = new Vector2(0f, 40f);
        }

        if (flyLayer == null)
        {
            GameObject fly = new GameObject("KittyFlyLayer", typeof(RectTransform));
            fly.transform.SetParent(canvas.transform, false);
            flyLayer = fly.GetComponent<RectTransform>();
            flyLayer.anchorMin = Vector2.zero;
            flyLayer.anchorMax = Vector2.one;
            flyLayer.offsetMin = Vector2.zero;
            flyLayer.offsetMax = Vector2.zero;
            flyLayer.SetAsLastSibling();
        }
    }

    public void ShowFaceDown(int count)
    {
        EnsureConfigured();
        ClearPileOnly();
        if (count <= 0 || cardPrefab == null || pileRoot == null) return;

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        HorizontalLayoutGroup hlg = pileRoot.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) Destroy(hlg);

        pileRoot.anchoredPosition = new Vector2(0f, 40f);
        pileRoot.sizeDelta = new Vector2(
            CardSpriteAtlas.DisplayWidth * 3.2f,
            CardSpriteAtlas.DisplayHeight + 8f);

        float w = CardSpriteAtlas.DisplayWidth;
        float h = CardSpriteAtlas.DisplayHeight;
        float spacing = w + 10f;
        float startX = -((count - 1) * spacing) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            CardView view = Instantiate(cardPrefab, pileRoot);
            view.Clicked = null;
            view.SetFlightMode(true); // raise Y=0 보정 끄기 (위치 고정)
            view.SetFaceDown();
            RectTransform rt = view.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(w, h);
                rt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
                rt.localRotation = Quaternion.identity;
            }
            view.RefreshDropShadow();
            pileViews.Add(view);
        }
        pileRoot.gameObject.SetActive(true);
    }

    // 딜 펼침 카드를 월드 위치 유지한 채 인수 (점프 없음)
    public void AdoptFaceDown(List<CardView> cards)
    {
        EnsureConfigured();
        ClearPileOnly();
        if (cards == null || cards.Count == 0 || pileRoot == null) return;

        if (hintText != null)
            hintText.gameObject.SetActive(false);

        HorizontalLayoutGroup hlg = pileRoot.GetComponent<HorizontalLayoutGroup>();
        if (hlg != null) Destroy(hlg);

        pileRoot.anchoredPosition = new Vector2(0f, 40f);
        pileRoot.sizeDelta = new Vector2(
            CardSpriteAtlas.DisplayWidth * 3.2f,
            CardSpriteAtlas.DisplayHeight + 8f);
        pileRoot.gameObject.SetActive(true);

        for (int i = 0; i < cards.Count; i++)
        {
            CardView view = cards[i];
            if (view == null) continue;
            view.SetFlightMode(true);
            view.Clicked = null;
            RectTransform rt = view.GetComponent<RectTransform>();
            if (rt != null)
                rt.SetParent(pileRoot, true); // worldPositionStays
            pileViews.Add(view);
        }
    }

    public void Clear()
    {
        ClearPileOnly();
        if (pileRoot != null) pileRoot.gameObject.SetActive(false);
    }

    private void ClearPileOnly()
    {
        foreach (CardView v in pileViews)
        {
            if (v != null) Destroy(v.gameObject);
        }
        pileViews.Clear();
    }

    public Vector3 GetPileCenterWorld()
    {
        if (pileRoot != null) return pileRoot.position;
        return Vector3.zero;
    }

    public void CollectPileWorldPositions(List<Vector3> outPositions)
    {
        if (outPositions == null) return;
        outPositions.Clear();
        Canvas.ForceUpdateCanvases();
        foreach (CardView v in pileViews)
        {
            if (v != null) outPositions.Add(v.transform.position);
        }
        if (outPositions.Count == 0 && pileRoot != null)
        {
            outPositions.Add(pileRoot.position);
            outPositions.Add(pileRoot.position);
            outPositions.Add(pileRoot.position);
        }
    }

    public void FlyToTarget(Vector3 targetWorld, Action onComplete)
    {
        if (busy)
        {
            if (onComplete != null) onComplete();
            return;
        }
        StartCoroutine(CoFly(targetWorld, onComplete));
    }

    private IEnumerator CoFly(Vector3 targetWorld, Action onComplete)
    {
        busy = true;
        EnsureConfigured();
        var starts = new List<Vector3>();
        CollectPileWorldPositions(starts);
        int n = Mathf.Max(starts.Count, 3);
        while (starts.Count < n) starts.Add(GetPileCenterWorld());

        ClearPileOnly();
        if (hintText != null) hintText.gameObject.SetActive(false);

        Canvas.ForceUpdateCanvases();
        Vector2 endLocal = OpponentHandsView.WorldToAnchored(flyLayer, targetWorld);
        Vector2 size = new Vector2(
            CardSpriteAtlas.DisplayWidth,
            CardSpriteAtlas.DisplayHeight);
        Vector2 endSize = size;

        for (int i = 0; i < n; i++)
        {
            Vector2 startLocal = OpponentHandsView.WorldToAnchored(flyLayer, starts[i]);
            StartCoroutine(CoFlyOneLocal(startLocal, endLocal, size, endSize, i * flyStagger));
        }

        float total = flyDuration + flyStagger * Mathf.Max(0, n - 1) + 0.05f;
        yield return new WaitForSecondsRealtime(total);

        if (pileRoot != null) pileRoot.gameObject.SetActive(false);
        busy = false;
        if (onComplete != null) onComplete();
    }

    private IEnumerator CoFlyOneLocal(
        Vector2 start, Vector2 end, Vector2 startSize, Vector2 endSize, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (cardPrefab == null || flyLayer == null) yield break;

        CardView view = Instantiate(cardPrefab, flyLayer);
        view.Clicked = null;
        view.SetFlightMode(true);
        view.SetFaceDown();
        RectTransform rt = view.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = startSize;
        rt.anchoredPosition = start;
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
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, e);
            rt.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, e);
            if (u > 0.65f && view.background != null)
            {
                Color c = view.background.color;
                c.a = Mathf.Lerp(1f, 0f, (u - 0.65f) / 0.35f);
                view.background.color = c;
            }
            view.RefreshDropShadow();
            yield return null;
        }
        if (view != null) Destroy(view.gameObject);
    }

    private void EnsureConfigured()
    {
        if (pileRoot != null && flyLayer != null) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Configure(cardPrefab, canvas);
    }

    private Font GetHintFont()
    {
        if (hintFont == null) hintFont = UiFonts.Primary;
        if (hintFont == null) hintFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return hintFont;
    }
}
