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
            pileRoot.sizeDelta = new Vector2(400f, 240f);

            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 10f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintGo.transform.SetParent(pileRoot, false);
            RectTransform hrt = hintGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -4f);
            hrt.sizeDelta = new Vector2(200f, 28f);
            hintText = hintGo.GetComponent<Text>();
            hintText.font = GetHintFont();
            hintText.fontSize = 16;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.9f, 0.9f, 0.85f, 0.9f);
            hintText.text = "바닥패";
            hintText.raycastTarget = false;
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
        {
            hintText.gameObject.SetActive(true);
            hintText.text = "바닥패 " + count;
        }

        float w = CardSpriteAtlas.DisplayWidth * 0.75f;
        float h = CardSpriteAtlas.DisplayHeight * 0.75f;
        for (int i = 0; i < count; i++)
        {
            CardView view = Instantiate(cardPrefab, pileRoot);
            view.Clicked = null;
            view.SetFaceDown();
            RectTransform rt = view.GetComponent<RectTransform>();
            if (rt != null) rt.sizeDelta = new Vector2(w, h);
            view.RefreshDropShadow();
            // hint가 HorizontalLayout 형제이면 카드만 레이아웃에 들어가게 — hint는 레이아웃 무시
            pileViews.Add(view);
        }
        // hint를 레이아웃 밖으로: ignore layout
        if (hintText != null)
        {
            LayoutElement le = hintText.GetComponent<LayoutElement>();
            if (le == null) le = hintText.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }
        pileRoot.gameObject.SetActive(true);
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

        Vector2 size = new Vector2(
            CardSpriteAtlas.DisplayWidth * 0.75f,
            CardSpriteAtlas.DisplayHeight * 0.75f);
        Vector2 endSize = size * 0.55f;

        for (int i = 0; i < n; i++)
            StartCoroutine(CoFlyOne(starts[i], targetWorld, size, endSize, i * flyStagger));

        float total = flyDuration + flyStagger * Mathf.Max(0, n - 1) + 0.05f;
        yield return new WaitForSecondsRealtime(total);

        if (pileRoot != null) pileRoot.gameObject.SetActive(false);
        busy = false;
        if (onComplete != null) onComplete();
    }

    private IEnumerator CoFlyOne(
        Vector3 start, Vector3 end, Vector2 startSize, Vector2 endSize, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        if (cardPrefab == null || flyLayer == null) yield break;

        CardView view = Instantiate(cardPrefab, flyLayer);
        view.Clicked = null;
        view.SetFaceDown();
        RectTransform rt = view.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = startSize;
        rt.position = start;
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
            rt.position = Vector3.LerpUnclamped(start, end, e);
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
