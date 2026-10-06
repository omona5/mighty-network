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
    private Vector2 lastToastScreen;

    private float ToastUnit
    {
        get
        {
            Canvas canvas = toastRoot != null ? toastRoot.GetComponentInParent<Canvas>() : null;
            return UiFonts.MenuScale / (canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f);
        }
    }
    private int ToastFontSize => Mathf.RoundToInt((ResponsiveCanvas.IsPortrait ? 17f : 20f) * ToastUnit);

    public bool IsBusy { get { return busy; } }
    public bool IsAnnouncing => announceRoutine != null;

    private string lastToastLanguage;

    private void LateUpdate()
    {
        Vector2 screen = new Vector2(ResponsiveCanvas.ViewWidth, ResponsiveCanvas.ViewHeight);
        if (screen == lastToastScreen && lastToastLanguage == GameSettings.Language) return;
        lastToastLanguage = GameSettings.Language;
        lastToastScreen = screen;
        ApplyToastStyle();
    }

    public void Configure(CardView prefab, Canvas canvas)
    {
        cardPrefab = prefab;
        if (canvas == null) return;

        if (flyLayer == null)
        {
            GameObject go = new GameObject("TrickFlyLayer", typeof(RectTransform));
            go.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
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
            root.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
            toastRoot = root.GetComponent<RectTransform>();
            toastRoot.anchorMin = new Vector2(0.5f, 0.5f);
            toastRoot.anchorMax = new Vector2(0.5f, 0.5f);
            toastRoot.pivot = new Vector2(0.5f, 0.5f);
            toastRoot.sizeDelta = new Vector2(ResponsiveCanvas.IsPortrait ? 780f : 680f,
                ResponsiveCanvas.IsPortrait ? 104f : 76f);
            toastRoot.anchoredPosition = new Vector2(0f, ResponsiveCanvas.IsPortrait ? 150f : 100f);

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
                toastRoot, "Text", ToastFontSize, TextAnchor.MiddleCenter, Color.white);
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
        if (toastRoot == null || toastText == null) return;
        float unit = ToastUnit;
        // TMP's preferred width can land on a fractional pixel.  Reserve a small
        // extra gutter so a glyph never touches or exceeds the black background.
        float paddingX = 24f * unit;
        float paddingY = 14f * unit;
        RectTransform parent = toastRoot.parent as RectTransform;
        float maxWidth = Mathf.Min((ResponsiveCanvas.IsPortrait ? 342f : 620f) * unit,
            parent != null ? parent.rect.width - 32f * unit : 620f * unit);
        maxWidth = Mathf.Max(80f * unit, maxWidth);
        if (toastBg != null)
            toastBg.color = Color.black;
        UiTmp.Apply(toastText, ToastFontSize, TextAnchor.MiddleCenter, Color.white, false, false);
        toastText.rectTransform.offsetMin = new Vector2(paddingX, paddingY);
        toastText.rectTransform.offsetMax = new Vector2(-paddingX, -paddingY);
        float width;
        float height;
        if (bidRow != null && bidRow.gameObject.activeSelf)
        {
            UiTmp.Apply(bidLeft, ToastFontSize, TextAnchor.MiddleCenter, Color.white, false, false);
            UiTmp.Apply(bidRight, ToastFontSize, TextAnchor.MiddleCenter, Color.white, false, false);
            float icon = 24f * unit;
            float gap = 8f * unit;
            HorizontalLayoutGroup layout = bidRow.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = gap;
            LayoutElement iconLayout = bidSuit.GetComponent<LayoutElement>();
            iconLayout.minWidth = iconLayout.preferredWidth = icon;
            iconLayout.minHeight = iconLayout.preferredHeight = icon;
            float leftNatural = Mathf.Ceil(bidLeft.GetPreferredValues(bidLeft.text).x);
            float rightNatural = Mathf.Ceil(bidRight.GetPreferredValues(bidRight.text).x);
            float desiredWidth = leftNatural + rightNatural + icon + gap * 2f + paddingX * 2f;
            width = Mathf.Min(maxWidth, desiredWidth);
            float textWidth = Mathf.Max(1f, width - paddingX * 2f - icon - gap * 2f);
            // Fit the background to its actual contents. If it hits the screen
            // limit, preserve the short score and wrap only the name/announcement.
            float rightWidth = Mathf.Min(rightNatural, textWidth);
            float leftWidth = Mathf.Max(1f, textWidth - rightWidth);
            bidLeft.GetComponent<LayoutElement>().preferredWidth = leftWidth;
            bidRight.GetComponent<LayoutElement>().preferredWidth = rightWidth;
            height = Mathf.Max(icon, Mathf.Max(
                bidLeft.GetPreferredValues(bidLeft.text, leftWidth, Mathf.Infinity).y,
                bidRight.GetPreferredValues(bidRight.text, rightWidth, Mathf.Infinity).y)) + paddingY * 2f;
            bidRow.offsetMin = new Vector2(paddingX, paddingY);
            bidRow.offsetMax = new Vector2(-paddingX, -paddingY);
        }
        else
        {
            width = Mathf.Clamp(toastText.GetPreferredValues(toastText.text).x + paddingX * 2f,
                180f * unit, maxWidth);
            height = toastText.GetPreferredValues(toastText.text, width - paddingX * 2f, Mathf.Infinity).y
                + paddingY * 2f;
        }
        if (electionToast && bidRow != null && bidRow.gameObject.activeSelf)
        {
            width = Mathf.Min(maxWidth, Mathf.Max(width,
                toastText.GetPreferredValues(toastText.text).x + paddingX * 2f));
            float headingHeight = toastText.GetPreferredValues(toastText.text,
                Mathf.Max(1f, width - paddingX * 2f), Mathf.Infinity).y;
            float rowHeight = height - paddingY * 2f;
            float gap = 12f * unit;
            height += headingHeight + gap;
            toastText.rectTransform.offsetMin = new Vector2(paddingX, paddingY + rowHeight + gap);
            toastText.rectTransform.offsetMax = new Vector2(-paddingX, -paddingY);
            bidRow.offsetMin = new Vector2(paddingX, paddingY);
            bidRow.offsetMax = new Vector2(-paddingX, -(paddingY + headingHeight + gap));
        }
        if (startInfoRow != null && startInfoRow.gameObject.activeSelf)
        {
            width = Mathf.Min(maxWidth, 420f * unit);
            float rowHeight = 76f * unit;
            float headingHeight = toastText.GetPreferredValues(toastText.text,
                Mathf.Max(1f, width - paddingX * 2f), Mathf.Infinity).y;
            height = headingHeight + rowHeight + paddingY * 2f + 12f * unit;
            toastText.rectTransform.offsetMin = new Vector2(paddingX, paddingY + rowHeight + 12f * unit);
            startInfoRow.anchorMin = new Vector2(0f, 0f);
            startInfoRow.anchorMax = new Vector2(1f, 0f);
            startInfoRow.pivot = new Vector2(0.5f, 0f);
            startInfoRow.offsetMin = new Vector2(paddingX, paddingY);
            startInfoRow.offsetMax = new Vector2(-paddingX, paddingY + rowHeight);
            for (int i = 0; i < 4; i++)
            {
                var label = startInfoLabels[i];
                UiTmp.Apply(label, Mathf.RoundToInt(14f * unit), TextAnchor.MiddleCenter, Color.white, false, false);
                label.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                var icon = startInfoIcons[i].rectTransform;
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.25f);
                icon.pivot = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = Vector2.zero;
                float cellWidth = (width - paddingX * 2f) / 4f;
                icon.sizeDelta = i == 0 ? new Vector2(28f, 28f) * unit
                    : new Vector2(Mathf.Min(56f * unit, cellWidth - 4f * unit), 28f * unit);
            }
        }
        toastRoot.sizeDelta = new Vector2(width, Mathf.Max(52f * unit, height));
        toastRoot.anchoredPosition = electionToast ? Vector2.zero
            : new Vector2(0f, (ResponsiveCanvas.IsPortrait ? 56f : 72f) * unit);
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
        ShowPlainToast(() => message);
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
        string who = string.IsNullOrEmpty(nickname) ? L10n.Text("누군가") : nickname;
        ShowBidToast(() => string.IsNullOrEmpty(nickname) ? L10n.Text("누군가") : nickname, noTrump, trumpSuit, () => score + L10n.Text("장 공약 제출!"));
        announceRoutine = StartCoroutine(CoAnnounceHoldFade(holdSec, fadeSec, null));
    }

    private bool electionToast;
    private RectTransform startInfoRow;
    private readonly List<UnityEngine.UI.Image> startInfoIcons = new List<UnityEngine.UI.Image>();
    private readonly List<TextMeshProUGUI> startInfoLabels = new List<TextMeshProUGUI>();

    // 당선자와 이번 판의 기루다/목표 점수를 중앙에 표시.
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
        ShowPlainToast(() =>
        {
            string name = string.IsNullOrEmpty(nickname) ? L10n.Text("주공") : nickname;
            return GameSettings.Language == "en"
                ? name + " has been elected!\nDiscards and friend are being selected."
                : name + "님이 당선되었습니다!\n버릴 카드와 프렌드를 설정하고 있습니다.";
        });
        electionToast = true;
        announceRoutine = StartCoroutine(CoAnnounceHoldFade(holdSec, fadeSec, onComplete));
    }

    public void AnnounceGameStart(GameRuleHud.Info info)
    {
        EnsureToastReady();
        if (announceRoutine != null) StopCoroutine(announceRoutine);
        stickyToast = false;
        ShowPlainToast(() => GameSettings.Language == "en"
            ? "The game has started!\nTarget: " + info.bidLabel
            : "게임이 시작되었습니다!\n공약: " + info.bidLabel);
        if (startInfoRow == null)
        {
            var row = new GameObject("StartRuleIcons", typeof(RectTransform));
            row.transform.SetParent(toastRoot, false);
            startInfoRow = row.GetComponent<RectTransform>();
            for (int i = 0; i < 4; i++)
            {
                var cell = new GameObject("Rule" + i, typeof(RectTransform));
                cell.transform.SetParent(startInfoRow, false);
                var rect = cell.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / 4f, 0f);
                rect.anchorMax = new Vector2((i + 1) / 4f, 1f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                startInfoLabels.Add(UiTmp.Create(rect, "Label", ToastFontSize,
                    TextAnchor.MiddleCenter, Color.white));
                startInfoIcons.Add(IconGui.MakeImage(rect, "Icon", default(IconSpriteAtlas.Slice),
                    IconSpriteAtlas.DisplayCard));
            }
        }
        startInfoRow.gameObject.SetActive(true);
        string[] labels = { "기루다", "마이티", "조커콜", "프렌드카드" };
        for (int i = 0; i < 4; i++) LocalizedLabel.Bind(startInfoLabels[i], labels[i]);
        IconGui.Apply(startInfoIcons[0], IconSpriteAtlas.GetTrump(info.noTrump, info.trumpSuit));
        IconGui.Apply(startInfoIcons[1], IconSpriteAtlas.GetCard(info.mightyCardId));
        IconGui.Apply(startInfoIcons[2], IconSpriteAtlas.GetCard(info.jokerCallCardId));
        IconGui.Apply(startInfoIcons[3], IconSpriteAtlas.GetCard(info.friendCardId));
        if (info.friendCardNone || info.friendIsPlayer)
            LocalizedLabel.Bind(startInfoLabels[3], () => L10n.Text(" 프렌드").Trim() + "\n"
                + (info.friendCardNone ? L10n.Text("없음") : info.friendLabel));
        electionToast = true;
        announceRoutine = StartCoroutine(CoAnnounceHoldFade(4f, 0.2f, null));
    }

    // 사라지지 않는 안내 (예: 카드 고르는 중...)
    public void ShowStickyToast(Func<string> message)
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

    public void Cancel()
    {
        StopAllCoroutines();
        announceRoutine = null;
        busy = false;
        ClearStickyToast();
        if (flyLayer != null)
            for (int i = flyLayer.childCount - 1; i >= 0; i--)
                Destroy(flyLayer.GetChild(i).gameObject);
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

    private void ShowPlainToast(Func<string> message)
    {
        if (startInfoRow != null) startInfoRow.gameObject.SetActive(false);
        electionToast = false;
        if (bidRow != null) bidRow.gameObject.SetActive(false);
        if (toastText != null)
        {
            toastText.gameObject.SetActive(true);
            LocalizedLabel.Bind(toastText, message);
        }
        ApplyToastStyle();
    }

    private void ShowBidToast(Func<string> leftText, bool noTrump, string trumpSuit, Func<string> rightText)
    {
        if (startInfoRow != null) startInfoRow.gameObject.SetActive(false);
        electionToast = false;
        if (toastText != null) toastText.gameObject.SetActive(false);
        EnsureBidRow();
        if (bidRow != null) bidRow.gameObject.SetActive(true);
        if (bidLeft != null) LocalizedLabel.Bind(bidLeft, leftText);
        IconGui.Apply(bidSuit, IconSpriteAtlas.GetTrump(noTrump, trumpSuit));
        if (bidRight != null) LocalizedLabel.Bind(bidRight, rightText);
        ApplyToastStyle();
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
        int tableRevision = tableView != null ? tableView.ContentRevision : -1;

        // 1) 흰 배경 토스트 표시
        if (toastText != null && toastGroup != null && toastRoot != null)
        {
            ApplyToastStyle();
            ShowPlainToast(() => string.IsNullOrEmpty(winnerNickname)
                ? L10n.Text("승리!")
                : (winnerNickname + L10n.Text(" 승리!")));
            Sfx.TrickWin();
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
            if (tableView != null && tableView.ContentRevision == tableRevision)
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
            if (tableView != null && tableView.ContentRevision == tableRevision)
            {
                tableView.HidePointCardSlots();
                yield return tableView.CoFadeNonPointSlots(nonPointFade);
            }
            yield return CoFlyPointsFromCenter(pointCards, tableCenterWorld, winnerWorld);
        }

        if (tableView != null && tableView.ContentRevision == tableRevision)
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
