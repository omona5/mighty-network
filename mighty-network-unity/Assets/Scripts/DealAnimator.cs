using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// DealAnimator: 중앙 셔플 → 각 좌석/손패로 뒷면 딜 비행
//   비행은 flyLayer 로컬 anchoredPosition 사용 (CanvasScaler 월드 Y 붕괴 방지)
// ============================================================================
public class DealAnimator : MonoBehaviour
{
    public float shuffleDuration = 0.85f;
    public float flyDuration = 0.07f;  // 순차 딜 ~3배 빠르게
    public float dealStagger = 0.038f; // legacy
    public float dealGap = 0.005f;
    public float kittySpreadDuration = 0.45f;
    public int cardsPerPlayer = 10;
    public int kittyRemainCount = 3;

    public struct SeatTarget
    {
        public string nickname;
        public Vector2 normalizedAnchor;
        public Vector3 selfWorldPos;
        public Vector2 endSize;
        public bool isSelf;
    }

    public delegate void CardLandedHandler(int seatIndex, int cardsAtSeat);

    private CardView cardPrefab;
    private RectTransform root;
    private RectTransform flyLayer;
    private RectTransform deckRoot;
    private Text hintText;
    private Font hintFont;
    private readonly List<CardView> deckViews = new List<CardView>();
    private bool busy;
    private Coroutine idleShakeCo;
    private bool holdingCollectedDeck;

    public float collectFlyDuration = 0.06f;
    public float collectGap = 0.004f;

    public bool IsBusy { get { return busy; } }
    public bool IsHoldingCollectedDeck { get { return holdingCollectedDeck && deckViews.Count > 0; } }

    // 재배분 전: 각 좌석·바닥패 → 중앙 더미로 회수
    // onCardCollected(seatIndex, remainingAtSeat): 카드가 손패에서 떠나는 순간
    public void PlayCollectToCenter(
        SeatTarget[] seats,
        int[] handCounts,
        IList<Vector3> kittyWorldStarts,
        CardLandedHandler onCardCollected,
        Action onComplete)
    {
        if (busy)
        {
            if (onComplete != null) onComplete();
            return;
        }
        StartCoroutine(CoCollectToCenter(
            seats, handCounts, kittyWorldStarts, onCardCollected, onComplete));
    }

    private IEnumerator CoCollectToCenter(
        SeatTarget[] seats,
        int[] handCounts,
        IList<Vector3> kittyWorldStarts,
        CardLandedHandler onCardCollected,
        Action onComplete)
    {
        busy = true;
        EnsureConfigured();
        if (root != null) root.gameObject.SetActive(true);
        if (flyLayer != null)
        {
            flyLayer.gameObject.SetActive(true);
            flyLayer.SetAsLastSibling();
        }

        ClearDeck();
        StopIdleDeckShake();
        if (deckRoot != null)
        {
            deckRoot.anchoredPosition = new Vector2(0f, 40f);
            deckRoot.gameObject.SetActive(true);
            deckRoot.SetAsLastSibling();
        }
        SetHint("패 모으는 중...");

        yield return null;
        Canvas.ForceUpdateCanvases();
        RectTransform space = GetFlightSpace();

        Vector2 endSize = DeckCardSize() * 0.85f;
        Vector2 startSize = DeckCardSize();

        Coroutine shakeCo = StartCoroutine(CoShakeDeckLoop());

        // 라운드마다 5명(전원)이 한 장씩 동시에 중앙으로 쏨
        if (seats != null && handCounts != null)
        {
            int n = Mathf.Min(seats.Length, handCounts.Length);
            var remaining = new int[n];
            var seatLocals = new Vector2[n];
            int maxPer = 0;
            for (int s = 0; s < n; s++)
            {
                remaining[s] = Mathf.Clamp(handCounts[s], 0, 13);
                seatLocals[s] = ResolveSeatToFlightLocal(seats[s], space);
                if (remaining[s] > maxPer) maxPer = remaining[s];
            }

            float prevFly = flyDuration;
            flyDuration = collectFlyDuration;

            for (int round = 0; round < maxPer; round++)
            {
                Vector2 deckLocal = ResolveWorldToFlightLocal(
                    deckRoot != null ? deckRoot.position : Vector3.zero);
                int launched = 0;
                for (int s = 0; s < n; s++)
                {
                    if (remaining[s] <= 0) continue;
                    remaining[s]--;
                    int left = remaining[s];
                    float spread = left * 0.5f;
                    Vector2 start = seatLocals[s]
                        + new Vector2((left - spread) * 8f, 0f);
                    // 쏘는 순간 중앙 더미에 한 장 추가 → 모이는 패가 바로 보임
                    AppendDeckCardVisual();
                    StartCoroutine(CoFlyOneLocal(
                        start, deckLocal, startSize, endSize, 0f, -8f - (s % 3) * 2f));
                    if (onCardCollected != null)
                        onCardCollected(s, left);
                    launched++;
                }

                if (launched > 0)
                {
                    yield return new WaitForSecondsRealtime(
                        collectFlyDuration + Mathf.Max(0f, collectGap));
                }
            }

            flyDuration = prevFly;
        }

        if (kittyWorldStarts != null && kittyWorldStarts.Count > 0)
        {
            float prevFly = flyDuration;
            flyDuration = collectFlyDuration;
            Vector2 deckLocal = ResolveWorldToFlightLocal(
                deckRoot != null ? deckRoot.position : Vector3.zero);
            for (int i = 0; i < kittyWorldStarts.Count; i++)
            {
                Vector2 start = ResolveWorldToFlightLocal(kittyWorldStarts[i]);
                AppendDeckCardVisual();
                StartCoroutine(CoFlyOneLocal(
                    start, deckLocal, startSize, endSize, 0f, 8f));
            }
            yield return new WaitForSecondsRealtime(
                collectFlyDuration + Mathf.Max(0f, collectGap));
            flyDuration = prevFly;
        }

        if (shakeCo != null) StopCoroutine(shakeCo);
        ResetDeckStackLayout();
        yield return new WaitForSecondsRealtime(0.12f);

        // 회수 종료 후에도 중앙 더미를 흔들리게 유지 (재딜 셔플까지)
        SetHint("다시 섞는 중...");
        BeginIdleDeckShake();
        busy = false;
        if (onComplete != null) onComplete();
    }

    private void BeginIdleDeckShake()
    {
        StopIdleDeckShake();
        if (root != null) root.gameObject.SetActive(true);
        if (deckRoot != null)
        {
            deckRoot.gameObject.SetActive(true);
            deckRoot.anchoredPosition = new Vector2(0f, 40f);
            deckRoot.SetAsLastSibling();
        }
        ResetDeckStackLayout();
        if (deckViews.Count > 0)
        {
            holdingCollectedDeck = true;
            idleShakeCo = StartCoroutine(CoShakeDeckLoop());
        }
    }

    private void StopIdleDeckShake()
    {
        if (idleShakeCo != null)
        {
            StopCoroutine(idleShakeCo);
            idleShakeCo = null;
        }
        holdingCollectedDeck = false;
        ResetDeckStackLayout();
    }

    private void AppendDeckCardVisual()
    {
        if (cardPrefab == null || deckRoot == null) return;
        CardView view = Instantiate(cardPrefab, deckRoot);
        view.Clicked = null;
        view.SetFlightMode(true);
        view.SetFaceDown();
        RectTransform rt = view.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = DeckCardSize();
        rt.anchoredPosition = new Vector2(0f, deckViews.Count * 1.1f);
        rt.localRotation = Quaternion.identity;
        view.RefreshDropShadow();
        Graphic[] graphics = view.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
        deckViews.Add(view);
        if (deckRoot != null) deckRoot.SetAsLastSibling();
    }

    public void Configure(CardView prefab, Canvas canvas)
    {
        cardPrefab = prefab;
        if (canvas == null) return;

        if (root == null)
        {
            GameObject go = new GameObject("DealAnimatorRoot", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            root = go.GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.SetAsLastSibling();
        }

        if (deckRoot == null)
        {
            GameObject deck = new GameObject("DealDeck", typeof(RectTransform));
            deck.transform.SetParent(root, false);
            deckRoot = deck.GetComponent<RectTransform>();
            deckRoot.anchorMin = new Vector2(0.5f, 0.5f);
            deckRoot.anchorMax = new Vector2(0.5f, 0.5f);
            deckRoot.pivot = new Vector2(0.5f, 0.5f);
            deckRoot.anchoredPosition = new Vector2(0f, 40f);
            deckRoot.sizeDelta = new Vector2(480f, 280f);

            GameObject hintGo = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            hintGo.transform.SetParent(deckRoot, false);
            RectTransform hrt = hintGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 0f);
            hrt.anchorMax = new Vector2(0.5f, 0f);
            hrt.pivot = new Vector2(0.5f, 1f);
            hrt.anchoredPosition = new Vector2(0f, -8f);
            hrt.sizeDelta = new Vector2(240f, 28f);
            hintText = hintGo.GetComponent<Text>();
            hintText.font = GetFont();
            hintText.fontSize = UiFonts.Size(16);
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.95f, 0.95f, 0.88f, 0.95f);
            hintText.raycastTarget = false;
            Shadow sh = hintGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.7f);
            sh.effectDistance = new Vector2(1f, -1f);
        }

        if (flyLayer == null)
        {
            // 마커(SeatDebugOverlay)와 동일한 Canvas 직속 stretch 공간 사용
            // DealAnimatorRoot 하위에 두면 비활성→활성 직후 rect/Y가 어긋날 수 있음
            GameObject fly = new GameObject("DealFlyLayer", typeof(RectTransform));
            fly.transform.SetParent(canvas.transform, false);
            flyLayer = fly.GetComponent<RectTransform>();
            flyLayer.anchorMin = Vector2.zero;
            flyLayer.anchorMax = Vector2.one;
            flyLayer.offsetMin = Vector2.zero;
            flyLayer.offsetMax = Vector2.zero;
            flyLayer.SetAsLastSibling();
        }

        root.gameObject.SetActive(false);
    }

    public void Play(SeatTarget[] seats, Action onComplete)
    {
        Play(() => seats, null, onComplete);
    }

    public void Play(Func<SeatTarget[]> seatsProvider, Action onComplete)
    {
        Play(seatsProvider, null, onComplete);
    }

    public void Play(Func<SeatTarget[]> seatsProvider, CardLandedHandler onCardLanded, Action onComplete)
    {
        if (busy)
        {
            if (onComplete != null) onComplete();
            return;
        }
        StartCoroutine(CoPlay(seatsProvider, onCardLanded, onComplete));
    }

    public void Cancel()
    {
        StopAllCoroutines();
        idleShakeCo = null;
        holdingCollectedDeck = false;
        ClearDeck();
        ClearFlightEndMarkers();
        if (root != null) root.gameObject.SetActive(false);
        busy = false;
    }

    private IEnumerator CoPlay(
        Func<SeatTarget[]> seatsProvider, CardLandedHandler onCardLanded, Action onComplete)
    {
        busy = true;
        StopIdleDeckShake();
        EnsureConfigured();
        if (root != null) root.gameObject.SetActive(true);
        if (flyLayer != null)
        {
            flyLayer.gameObject.SetActive(true);
            flyLayer.SetAsLastSibling();
        }

        // 회수로 모인 더미가 있으면 그대로 사용, 없으면 새로 생성
        int visualDeck = Mathf.Max(kittyRemainCount + 8, 14);
        if (deckViews.Count == 0)
            BuildDeckPile(visualDeck);
        else
        {
            if (deckRoot != null)
            {
                deckRoot.anchoredPosition = new Vector2(0f, 40f);
                deckRoot.gameObject.SetActive(true);
                deckRoot.SetAsLastSibling();
            }
            ResetDeckStackLayout();
        }
        SetHint("카드 섞는 중...");
        yield return CoShuffle();

        yield return null;
        Canvas.ForceUpdateCanvases();

        SeatTarget[] seats = seatsProvider != null ? seatsProvider() : null;
        if (seats == null || seats.Length == 0)
        {
            ClearDeck();
            if (root != null) root.gameObject.SetActive(false);
            busy = false;
            if (onComplete != null) onComplete();
            yield break;
        }

        SetHint("패 나누는 중...");
        RectTransform space = GetFlightSpace();
        Canvas.ForceUpdateCanvases();

        Vector2 deckLocal = ResolveWorldToFlightLocal(deckRoot != null ? deckRoot.position : Vector3.zero);
        Vector2 startSize = DeckCardSize();
        int per = Mathf.Max(1, cardsPerPlayer);
        int seatN = seats.Length;
        int totalDeal = per * seatN;

        var ends = new Vector2[seatN];
        var endSizes = new Vector2[seatN];
        ClearFlightEndMarkers();
        for (int s = 0; s < seatN; s++)
        {
            ends[s] = ResolveSeatToFlightLocal(seats[s], space);
            endSizes[s] = seats[s].endSize.sqrMagnitude > 1f
                ? seats[s].endSize
                : startSize * 0.55f;
        }

        // 딜 내내 중앙 더미 흔들림
        Coroutine shakeCo = StartCoroutine(CoShakeDeckLoop());

        var counts = new int[seatN];
        int dealt = 0;
        for (int round = 0; round < per; round++)
        {
            for (int s = 0; s < seatN; s++)
            {
                // 흔들린 더미 꼭대기에서 출발
                deckLocal = ResolveWorldToFlightLocal(
                    deckRoot != null ? deckRoot.position : Vector3.zero);
                yield return CoFlyOneLocal(
                    deckLocal, ends[s], startSize, endSizes[s], 0f, 6f + (round % 3) * 4f);
                counts[s]++;
                dealt++;
                if (onCardLanded != null)
                    onCardLanded(s, counts[s]);

                // 더미가 줄어드는 느낌 (바닥패 3장은 남김)
                TrimDeckTowardRemain(kittyRemainCount, dealt, totalDeal);

                if (dealGap > 0f)
                    yield return new WaitForSecondsRealtime(dealGap);
            }
        }

        if (shakeCo != null) StopCoroutine(shakeCo);
        ResetDeckStackLayout();

        // 남은 더미 → 바닥패 3장 펼침 후 KittyView로 인계 (재생성 점프 방지)
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (deckRoot != null)
            deckRoot.anchoredPosition = new Vector2(0f, 40f);
        yield return CoSpreadKittyFromDeck(kittyRemainCount);

        if (hintText != null) hintText.gameObject.SetActive(false);
        ClearFlightEndMarkers();
        busy = false;
        if (onComplete != null) onComplete();
        // TakeKittyCards 이후 deckViews는 비어 있음
        if (root != null) root.gameObject.SetActive(false);
    }

    // 펼쳐 둔 바닥패 카드를 넘기고 목록에서 제거 (Destroy 하지 않음)
    public List<CardView> TakeKittyCards()
    {
        var list = new List<CardView>(deckViews);
        deckViews.Clear();
        return list;
    }

    private IEnumerator CoShakeDeckLoop()
    {
        Vector2 basePos = Vector2.zero;
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;
            float wobble = Mathf.Sin(t * 26f) * 7f;
            float lift = Mathf.Abs(Mathf.Sin(t * 17f)) * 4f;
            for (int i = 0; i < deckViews.Count; i++)
            {
                CardView v = deckViews[i];
                if (v == null) continue;
                RectTransform rt = v.GetComponent<RectTransform>();
                if (rt == null) continue;
                float layer = i * 1.1f;
                float side = (i % 2 == 0) ? 1f : -1f;
                float split = Mathf.Sin(t * 20f + i * 0.4f) * side * 8f;
                rt.anchoredPosition = basePos + new Vector2(wobble * 0.4f + split * 0.35f, layer + lift * 0.3f);
                rt.localRotation = Quaternion.Euler(0f, 0f, split * 0.25f + wobble * 0.06f);
            }
            yield return null;
        }
    }

    private void ResetDeckStackLayout()
    {
        for (int i = 0; i < deckViews.Count; i++)
        {
            CardView v = deckViews[i];
            if (v == null) continue;
            RectTransform rt = v.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.anchoredPosition = new Vector2(0f, i * 1.1f);
            rt.localRotation = Quaternion.identity;
        }
    }

    // 딜 진행에 맞춰 더미 장수 감소 (최소 remain장)
    private void TrimDeckTowardRemain(int remain, int dealt, int totalDeal)
    {
        if (deckViews.Count <= remain) return;
        int targetVisual = remain + Mathf.CeilToInt(
            (deckViews.Count - remain) * (1f - dealt / (float)Mathf.Max(1, totalDeal)));
        targetVisual = Mathf.Clamp(targetVisual, remain, deckViews.Count);
        while (deckViews.Count > targetVisual)
        {
            int last = deckViews.Count - 1;
            CardView v = deckViews[last];
            deckViews.RemoveAt(last);
            if (v != null) Destroy(v.gameObject);
        }
    }

    // 남은 더미를 가로로 펼쳐 바닥패처럼 보이기
    private IEnumerator CoSpreadKittyFromDeck(int count)
    {
        count = Mathf.Max(1, count);
        // 부족하면 채움
        while (deckViews.Count < count)
        {
            if (cardPrefab == null || deckRoot == null) break;
            CardView view = Instantiate(cardPrefab, deckRoot);
            view.Clicked = null;
            view.SetFlightMode(true);
            view.SetFaceDown();
            RectTransform rt = view.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = DeckCardSize();
            rt.anchoredPosition = new Vector2(0f, deckViews.Count * 1.1f);
            view.RefreshDropShadow();
            deckViews.Add(view);
        }
        // 초과분은 제거
        while (deckViews.Count > count)
        {
            int last = deckViews.Count - 1;
            CardView v = deckViews[last];
            deckViews.RemoveAt(last);
            if (v != null) Destroy(v.gameObject);
        }

        float spacing = CardSpriteAtlas.DisplayWidth * 0.75f + 10f;
        float startX = -((count - 1) * spacing) * 0.5f;
        Vector2 kittySize = new Vector2(
            CardSpriteAtlas.DisplayWidth * 0.75f,
            CardSpriteAtlas.DisplayHeight * 0.75f);

        var fromPos = new Vector2[count];
        var fromRot = new float[count];
        var fromSize = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            RectTransform rt = deckViews[i].GetComponent<RectTransform>();
            fromPos[i] = rt.anchoredPosition;
            fromRot[i] = rt.localEulerAngles.z;
            fromSize[i] = rt.sizeDelta;
        }

        float t = 0f;
        while (t < kittySpreadDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / kittySpreadDuration);
            float e = 1f - Mathf.Pow(1f - u, 3f);
            for (int i = 0; i < count; i++)
            {
                if (deckViews[i] == null) continue;
                RectTransform rt = deckViews[i].GetComponent<RectTransform>();
                Vector2 to = new Vector2(startX + i * spacing, 0f);
                rt.anchoredPosition = Vector2.LerpUnclamped(fromPos[i], to, e);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(fromRot[i], 0f, e));
                rt.sizeDelta = Vector2.LerpUnclamped(fromSize[i], kittySize, e);
                deckViews[i].RefreshDropShadow();
            }
            yield return null;
        }

        // 살짝 멈춰 보이게
        yield return new WaitForSecondsRealtime(0.2f);
    }

    private RectTransform GetFlightSpace()
    {
        if (flyLayer != null) return flyLayer;
        if (root != null && root.parent is RectTransform prt) return prt;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform as RectTransform : root;
    }

    private Vector2 ResolveSeatToFlightLocal(SeatTarget t, RectTransform space)
    {
        if (space == null) space = flyLayer;
        Vector2 spaceLocal;
        if (t.isSelf && t.selfWorldPos.sqrMagnitude > 0.01f)
            spaceLocal = OpponentHandsView.WorldToAnchored(space, t.selfWorldPos);
        else
            spaceLocal = OpponentHandsView.NormalizedToAnchored(
                space, t.isSelf ? OpponentHandsView.SelfHandAnchor : t.normalizedAnchor);

        if (space == flyLayer || flyLayer == null)
            return spaceLocal;

        Vector3 world = space.TransformPoint(new Vector3(spaceLocal.x, spaceLocal.y, 0f));
        return OpponentHandsView.WorldToAnchored(flyLayer, world);
    }

    private Vector2 ResolveWorldToFlightLocal(Vector3 world)
    {
        if (flyLayer == null) return Vector2.zero;
        return OpponentHandsView.WorldToAnchored(flyLayer, world);
    }

    private readonly System.Collections.Generic.List<GameObject> flightEndMarkers =
        new System.Collections.Generic.List<GameObject>();

    private void SpawnFlightEndMarker(Vector2 localPos, string nick, Vector2 anchor)
    {
        if (flyLayer == null) return;
        GameObject go = new GameObject(
            "FlyEnd_" + (nick ?? "?"),
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(flyLayer, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(36f, 36f);
        rt.anchoredPosition = localPos;
        Image img = go.GetComponent<Image>();
        img.color = new Color(1f, 0.2f, 0.85f, 0.95f); // 실제 비행 끝점 (마젠타)
        img.raycastTarget = false;

        GameObject labelGo = new GameObject("L", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        RectTransform lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0.5f, 0.5f);
        lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0f);
        lrt.anchoredPosition = new Vector2(0f, 20f);
        lrt.sizeDelta = new Vector2(140f, 40f);
        Text tx = labelGo.GetComponent<Text>();
        tx.font = UiFonts.Primary != null
            ? UiFonts.Primary
            : Resources.GetBuiltinResource<Font>("Arial.ttf");
        tx.fontSize = UiFonts.Size(13);
        tx.alignment = TextAnchor.LowerCenter;
        tx.color = Color.white;
        tx.text = (nick ?? "?") + "\nFLY " + anchor.x.ToString("F2") + "," + anchor.y.ToString("F2");
        tx.raycastTarget = false;
        flightEndMarkers.Add(go);
    }

    private void ClearFlightEndMarkers()
    {
        for (int i = 0; i < flightEndMarkers.Count; i++)
        {
            if (flightEndMarkers[i] != null) Destroy(flightEndMarkers[i]);
        }
        flightEndMarkers.Clear();
    }

    private IEnumerator CoShuffle()
    {
        if (deckViews.Count == 0) yield break;
        float t = 0f;
        Vector2 basePos = Vector2.zero;
        while (t < shuffleDuration)
        {
            t += Time.unscaledDeltaTime;
            float u = t / shuffleDuration;
            float wobble = Mathf.Sin(t * 22f) * (10f * (1f - u * 0.35f));
            float lift = Mathf.Abs(Mathf.Sin(t * 14f)) * 6f;
            for (int i = 0; i < deckViews.Count; i++)
            {
                CardView v = deckViews[i];
                if (v == null) continue;
                RectTransform rt = v.GetComponent<RectTransform>();
                if (rt == null) continue;
                float layer = i * 1.2f;
                float side = (i % 2 == 0) ? 1f : -1f;
                float split = Mathf.Sin(t * 18f + i * 0.35f) * side * (14f * (1f - u * 0.5f));
                rt.anchoredPosition = basePos + new Vector2(wobble * 0.35f + split, layer + lift * (i / (float)deckViews.Count));
                rt.localRotation = Quaternion.Euler(0f, 0f, split * 0.35f + wobble * 0.08f);
            }
            yield return null;
        }

        for (int i = 0; i < deckViews.Count; i++)
        {
            CardView v = deckViews[i];
            if (v == null) continue;
            RectTransform rt = v.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.anchoredPosition = new Vector2(0f, i * 1.1f);
            rt.localRotation = Quaternion.identity;
        }
        yield return new WaitForSecondsRealtime(0.08f);
    }

    private IEnumerator CoFlyOneLocal(
        Vector2 start, Vector2 end, Vector2 startSize, Vector2 endSize, float delay, float spin)
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
        rt.localRotation = Quaternion.identity;
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
            rt.anchoredPosition = Vector2.LerpUnclamped(start, end, e);
            rt.sizeDelta = Vector2.LerpUnclamped(startSize, endSize, e);
            rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(spin, 0f, e));
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

    private void BuildDeckPile(int count)
    {
        ClearDeck();
        if (cardPrefab == null || deckRoot == null) return;
        deckRoot.anchoredPosition = new Vector2(0f, 40f);
        deckRoot.gameObject.SetActive(true);
        Vector2 size = DeckCardSize();
        for (int i = 0; i < count; i++)
        {
            CardView view = Instantiate(cardPrefab, deckRoot);
            view.Clicked = null;
            view.SetFlightMode(true);
            view.SetFaceDown();
            RectTransform rt = view.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(0f, i * 1.1f);
            rt.localRotation = Quaternion.identity;
            view.RefreshDropShadow();
            Graphic[] graphics = view.GetComponentsInChildren<Graphic>(true);
            for (int g = 0; g < graphics.Length; g++)
                graphics[g].raycastTarget = false;
            deckViews.Add(view);
        }
        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            hintText.transform.SetAsLastSibling();
        }
    }

    private void ClearDeck()
    {
        foreach (CardView v in deckViews)
        {
            if (v != null) Destroy(v.gameObject);
        }
        deckViews.Clear();
    }

    private static Vector2 DeckCardSize()
    {
        return new Vector2(
            CardSpriteAtlas.DisplayWidth * 0.7f,
            CardSpriteAtlas.DisplayHeight * 0.7f);
    }

    private void SetHint(string msg)
    {
        if (hintText == null) return;
        hintText.gameObject.SetActive(true);
        hintText.text = msg ?? "";
    }

    private void EnsureConfigured()
    {
        if (root != null && flyLayer != null && deckRoot != null) return;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        Configure(cardPrefab, canvas);
    }

    private Font GetFont()
    {
        if (hintFont == null) hintFont = UiFonts.Primary;
        if (hintFont == null) hintFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return hintFont;
    }
}
