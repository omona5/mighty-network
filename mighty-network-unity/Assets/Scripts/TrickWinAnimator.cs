using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// TrickWinAnimator: 토스트(검정 박스·흰 글씨·Galmuri11 TMP) → 비전수 fade / 점수카드 중앙→승자
// ============================================================================
public class TrickWinAnimator : MonoBehaviour
{
    public float toastHold = 0.85f;
    public float toastFade = 0.35f;
    public float nonPointFade = 0.35f;
    public float flyDuration = 0.55f;
    public float flyStagger = 0.07f;

    private CardView cardPrefab;
    private RectTransform flyLayer;
    private RectTransform toastRoot;
    private Image toastBg;
    private TextMeshProUGUI toastText;
    private RectTransform bidRow;
    private TextMeshProUGUI bidLeft;
    private Image bidSuit;
    private TextMeshProUGUI bidRight;
    private CanvasGroup toastGroup;
    private bool busy;
    private bool stickyToast;
    private Coroutine announceRoutine;

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
            toastRoot.sizeDelta = new Vector2(UiFonts.Layout(560f), UiFonts.Layout(48f));
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

            toastText = UiTmp.Create(
                toastRoot, "Text", UiFonts.Size(14), TextAnchor.MiddleCenter, Color.white);
            RectTransform trt = toastText.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            // 좌우 여백을 넉넉히 (텍스트가 박스 끝에 붙지 않게)
            trt.offsetMin = new Vector2(UiFonts.Layout(16f), 6f);
            trt.offsetMax = new Vector2(-UiFonts.Layout(16f), -6f);

            toastGroup = root.GetComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toastRoot.SetAsLastSibling();
        }

        ApplyToastStyle();
    }

    private void ApplyToastStyle()
    {
        if (toastBg != null)
            toastBg.color = Color.black;
        if (toastRoot != null)
            toastRoot.sizeDelta = new Vector2(UiFonts.Layout(560f), UiFonts.Layout(48f));
        if (toastText != null)
        {
            RectTransform trt = toastText.rectTransform;
            if (trt != null)
            {
                trt.offsetMin = new Vector2(UiFonts.Layout(16f), 6f);
                trt.offsetMax = new Vector2(-UiFonts.Layout(16f), -6f);
            }
            UiTmp.Apply(toastText, UiFonts.Size(14), TextAnchor.MiddleCenter, Color.white);
        }
        int hudSize = UiFonts.Size(14);
        if (bidLeft != null) bidLeft.fontSize = hudSize;
        if (bidRight != null) bidRight.fontSize = hudSize;
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
        ClearStickyToast();
        StartCoroutine(CoPlay(
            winnerNickname, pointCards, tableCenterWorld, winnerWorld, tableView, onComplete));
    }

    // 검정 박스 + 흰 글씨 토스트 (당선 안내 등). hold 후 fade.
    public void Announce(string message, float holdSec = 1.35f, float fadeSec = 0.35f, Action onComplete = null)
    {
        EnsureToastReady();
        if (announceRoutine != null)
        {
            StopCoroutine(announceRoutine);
            announceRoutine = null;
        }
        stickyToast = false;
        ShowPlainToast(message);
        announceRoutine = StartCoroutine(CoAnnounceHoldFade(holdSec, fadeSec, onComplete));
    }

    // 공약 제출: "닉네임 [기루 아이콘] N장 공약 제출!"
    public void AnnounceBid(string nickname, bool noTrump, string trumpSuit, int score,
        float holdSec = 2.7f, float fadeSec = 0.35f)
    {
        EnsureToastReady();
        if (announceRoutine != null)
        {
            StopCoroutine(announceRoutine);
            announceRoutine = null;
        }
        stickyToast = false;
        string who = string.IsNullOrEmpty(nickname) ? "누군가" : nickname;
        ShowBidToast(who, noTrump, trumpSuit, score + "장 공약 제출!");
        announceRoutine = StartCoroutine(CoAnnounceHoldFade(holdSec, fadeSec, null));
    }

    // 당선: "닉네임가 당선되었습니다! [기루 아이콘] N장"
    public void AnnounceElection(string nickname, bool noTrump, string trumpSuit, int score,
        float holdSec = 2.35f, float fadeSec = 0.35f, Action onComplete = null)
    {
        EnsureToastReady();
        if (announceRoutine != null)
        {
            StopCoroutine(announceRoutine);
            announceRoutine = null;
        }
        stickyToast = false;
        string who = string.IsNullOrEmpty(nickname) ? "주공" : nickname;
        ShowBidToast(who + "가 당선되었습니다!", noTrump, trumpSuit, score + "장");
        announceRoutine = StartCoroutine(CoAnnounceHoldFade(holdSec, fadeSec, onComplete));
    }

    // 사라지지 않는 안내 (예: 카드 고르는 중...)
    public void ShowStickyToast(string message)
    {
        EnsureToastReady();
        if (announceRoutine != null)
        {
            StopCoroutine(announceRoutine);
            announceRoutine = null;
        }
        stickyToast = true;
        ShowPlainToast(message);
        ApplyToastStyle();
        if (toastGroup != null) toastGroup.alpha = 1f;
        if (toastRoot != null) toastRoot.SetAsLastSibling();
    }

    public void ClearStickyToast()
    {
        stickyToast = false;
        if (announceRoutine != null)
        {
            StopCoroutine(announceRoutine);
            announceRoutine = null;
        }
        if (toastGroup != null && !busy) toastGroup.alpha = 0f;
    }

    private IEnumerator CoAnnounceHoldFade(float holdSec, float fadeSec, Action onComplete)
    {
        if (toastGroup == null || toastRoot == null)
        {
            if (onComplete != null) onComplete();
            yield break;
        }

        ApplyToastStyle();
        toastGroup.alpha = 1f;
        toastRoot.SetAsLastSibling();

        float hold = 0f;
        while (hold < holdSec)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        if (stickyToast)
        {
            announceRoutine = null;
            if (onComplete != null) onComplete();
            yield break;
        }

        float fadeT = 0f;
        while (fadeT < fadeSec)
        {
            fadeT += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - Mathf.Clamp01(fadeT / fadeSec);
            yield return null;
        }
        toastGroup.alpha = 0f;
        announceRoutine = null;
        if (onComplete != null) onComplete();
    }

    private void ShowPlainToast(string message)
    {
        if (bidRow != null) bidRow.gameObject.SetActive(false);
        if (toastText != null)
        {
            toastText.gameObject.SetActive(true);
            toastText.text = message ?? "";
        }
    }

    private void ShowBidToast(string leftText, bool noTrump, string trumpSuit, string rightText)
    {
        if (toastText != null) toastText.gameObject.SetActive(false);
        EnsureBidRow();
        if (bidRow != null) bidRow.gameObject.SetActive(true);
        if (bidLeft != null) bidLeft.text = leftText ?? "";
        IconGui.Apply(bidSuit, IconSpriteAtlas.GetTrump(noTrump, trumpSuit));
        if (bidRight != null) bidRight.text = rightText ?? "";
    }

    private void EnsureBidRow()
    {
        if (bidRow != null || toastRoot == null) return;

        GameObject row = new GameObject("BidRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(toastRoot, false);
        bidRow = row.GetComponent<RectTransform>();
        bidRow.anchorMin = Vector2.zero;
        bidRow.anchorMax = Vector2.one;
        bidRow.offsetMin = new Vector2(24f, 12f);
        bidRow.offsetMax = new Vector2(-24f, -12f);
        HorizontalLayoutGroup h = row.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 10f;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        h.childControlWidth = true;
        h.childControlHeight = true;

        bidLeft = MakeBidText(row.transform, "Left", UiFonts.Size(14));
        bidSuit = IconGui.MakeImage(row.transform, "Suit", default(IconSpriteAtlas.Slice),
            IconSpriteAtlas.DisplaySquare);
        LayoutElement le = bidSuit.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = IconSpriteAtlas.DisplaySquare.x;
        le.preferredHeight = IconSpriteAtlas.DisplaySquare.y;
        le.minWidth = IconSpriteAtlas.DisplaySquare.x;
        le.minHeight = IconSpriteAtlas.DisplaySquare.y;
        bidRight = MakeBidText(row.transform, "Right", UiFonts.Size(14));
        bidRow.gameObject.SetActive(false);
    }

    private TextMeshProUGUI MakeBidText(Transform parent, string name, int fontSize)
    {
        TextMeshProUGUI t = UiTmp.Create(
            parent, name, fontSize, TextAnchor.MiddleCenter, Color.white);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth = 0f;
        le.minWidth = 20f;
        return t;
    }

    private void EnsureToastReady()
    {
        if (toastRoot != null) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Configure(cardPrefab, canvas);
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
            ShowPlainToast(string.IsNullOrEmpty(winnerNickname)
                ? "승리!"
                : (winnerNickname + " 승리!"));
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

        Canvas.ForceUpdateCanvases();
        Vector2 centerLocal = OpponentHandsView.WorldToAnchored(flyLayer, centerWorld);
        Vector2 winnerLocal = OpponentHandsView.WorldToAnchored(flyLayer, winnerWorld);

        Vector2 startSize = new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        Vector2 endSize = startSize;
        int n = 0;
        for (int i = 0; i < pointCards.Count; i++)
        {
            if (pointCards[i] == null) continue;
            Vector2 start = centerLocal + new Vector2((n - (pointCards.Count - 1) * 0.5f) * 12f, 0f);
            StartCoroutine(CoFlyOneLocal(pointCards[i], start, winnerLocal, startSize, endSize, n * flyStagger));
            n++;
        }
        float total = flyDuration + flyStagger * Mathf.Max(0, n - 1) + 0.05f;
        yield return new WaitForSecondsRealtime(total);
    }

    private IEnumerator CoFlyOneLocal(
        CardData card,
        Vector2 startLocal,
        Vector2 endLocal,
        Vector2 startSize,
        Vector2 endSize,
        float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        CardView view = Instantiate(cardPrefab, flyLayer);
        view.Clicked = null;
        view.SetFlightMode(true);
        view.SetCard(card);
        view.SetPlayable(true);
        RectTransform rt = view.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = startSize;
        rt.anchoredPosition = startLocal;
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
            rt.anchoredPosition = Vector2.LerpUnclamped(startLocal, endLocal, e);
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
