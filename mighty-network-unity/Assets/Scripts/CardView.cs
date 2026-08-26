using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ============================================================================
// CardView: 카드 한 장(프리팹)에 붙는 스크립트.
//   - SetCard / SetFaceDown / SetPlayable(음영·클릭)
//   - 겹침 구분용 우측·하단 드롭 섀도 (UI Shadow)
// ============================================================================
public class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("Inspector에서 연결")]
    public Image background; // 카드 이미지 (보통 자기 자신의 Image)
    public Text label;       // 스프라이트 폴백용 텍스트 (있으면)

    [Header("겹침 그림자")]
    public Vector2 shadowOffset = new Vector2(10f, -10f); // 우하단 (기존)
    [Range(0f, 1f)] public float shadowAlpha = 0.65f;
    public Vector2 sideShadowOffset = new Vector2(5f, 5f); // 좌·상 각각 5
    [Range(0f, 1f)] public float sideShadowAlpha = 0.4f;

    public CardData Card { get; private set; }
    public System.Action<CardData> Clicked;

    private static readonly Color Red = new Color(0.83f, 0.0f, 0.0f);
    private static readonly Color Black = new Color(0.10f, 0.10f, 0.10f);
    private static readonly Color JokerGold = new Color(0.95f, 0.70f, 0.10f);
    private static readonly Color DimMul = new Color(0.42f, 0.42f, 0.42f, 1f);
    private static Font sharedUiFont;

    private Color baseTint = Color.white;
    private bool playable = true;

    // 바닥패 버리기 선택: LayoutGroup이 LateUpdate 이후 위치를 덮어쓰므로
    // willRenderCanvases에서 Y를 다시 맞춘다.
    // restAnchoredY = 손패 슬롯 기본 Y (HandView가 0으로 고정), raised 시 +SelectRaiseY.
    private const float SelectRaiseY = 36f;
    private bool raised;
    private bool subscribedToCanvas;
    private bool applyHandRaiseLayout = true;
    private float restAnchoredY;

    private void OnEnable()
    {
        EnsureDropShadow();
        SubscribeCanvas();
    }

    private void OnDisable()
    {
        UnsubscribeCanvas();
    }

    // 딜/트릭/키티 비행 카드: 손패 raise Y 보정 끄기
    public void SetFlightMode(bool flying)
    {
        applyHandRaiseLayout = !flying;
        if (flying) raised = false;
    }

    // HandView 수동 배치 시 슬롯 기본 Y (보통 0)
    public void SetRestAnchoredY(float y)
    {
        restAnchoredY = y;
        ApplyRaiseAfterLayout();
    }

    private void SubscribeCanvas()
    {
        if (subscribedToCanvas) return;
        Canvas.willRenderCanvases += ApplyRaiseAfterLayout;
        subscribedToCanvas = true;
    }

    private void UnsubscribeCanvas()
    {
        if (!subscribedToCanvas) return;
        Canvas.willRenderCanvases -= ApplyRaiseAfterLayout;
        subscribedToCanvas = false;
    }

    // Layout/수동배치 직후, 렌더 직전에 restY(+raise)로 Y를 고정한다.
    private void ApplyRaiseAfterLayout()
    {
        if (!applyHandRaiseLayout) return;
        RectTransform rt = transform as RectTransform;
        if (rt == null) return;
        Vector2 p = rt.anchoredPosition;
        float targetY = restAnchoredY + (raised ? SelectRaiseY : 0f);
        if (Mathf.Abs(p.y - targetY) < 0.01f) return;
        p.y = targetY;
        rt.anchoredPosition = p;
    }

    // 손패(160) / 상대(80) 등 sizeDelta 변경 시 그림자 비율도 맞춤
    private void OnRectTransformDimensionsChange()
    {
        EnsureDropShadow();
    }

    public void RefreshDropShadow()
    {
        EnsureDropShadow();
    }

    private void EnsureDropShadow()
    {
        if (background == null)
            background = GetComponent<Image>();
        if (background == null) return;

        float scale = ShadowScale();
        float side = Mathf.Abs(sideShadowOffset.x) > 0.01f
            ? Mathf.Abs(sideShadowOffset.x)
            : Mathf.Abs(sideShadowOffset.y);
        if (side < 0.01f) side = 5f;

        // 기준 손패 크기 대비 스케일 (상대 카드 0.5배 → 그림자도 절반)
        ApplyOrAddShadow(0, shadowOffset * scale, shadowAlpha);
        ApplyOrAddShadow(1, new Vector2(-side * scale, 0f), sideShadowAlpha); // 좌
        ApplyOrAddShadow(2, new Vector2(0f, side * scale), sideShadowAlpha);  // 상
    }

    private float ShadowScale()
    {
        RectTransform rt = background != null ? background.rectTransform : transform as RectTransform;
        if (rt == null || CardSpriteAtlas.DisplayWidth < 0.01f) return 1f;
        float s = rt.sizeDelta.x / CardSpriteAtlas.DisplayWidth;
        if (s < 0.01f)
        {
            // sizeDelta가 스트레치 모드일 때 rect 사용
            float w = rt.rect.width;
            if (w > 0.01f) s = w / CardSpriteAtlas.DisplayWidth;
        }
        return Mathf.Clamp(s, 0.2f, 2f);
    }

    private void ApplyOrAddShadow(int index, Vector2 offset, float alpha)
    {
        Shadow[] existing = background.GetComponents<Shadow>();
        Shadow s;
        if (index < existing.Length)
            s = existing[index];
        else
            s = background.gameObject.AddComponent<Shadow>();

        s.enabled = true;
        s.effectDistance = offset;
        s.effectColor = new Color(0f, 0f, 0f, alpha);
        s.useGraphicAlpha = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!playable || Card == null) return;
        Clicked?.Invoke(Card);
    }

    public void SetCard(CardData card)
    {
        Card = card;
        playable = true;
        if (card == null) return;

        Sprite sprite = CardSpriteAtlas.Get(card.id);
        if (sprite != null)
        {
            ApplySprite(sprite);
            return;
        }

        ApplyTextFallback(card);
    }

    public void SetFaceDown()
    {
        Card = null;
        playable = false;
        Sprite back = CardSpriteAtlas.GetBack();
        if (back != null)
        {
            ApplySprite(back);
            if (background != null) background.raycastTarget = false;
            return;
        }
        if (background != null)
        {
            background.sprite = null;
            background.color = new Color(0.15f, 0.25f, 0.55f);
            background.raycastTarget = false;
        }
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = "?";
            label.color = Color.white;
        }
    }

    // 내 차례에 못 내는 카드 음영. playable=false 면 클릭 불가.
    public void SetPlayable(bool canPlay)
    {
        if (Card == null) return;
        playable = canPlay;
        ApplyTint();
        if (background != null) background.raycastTarget = canPlay;
    }

    // 버리기 선택 표시: 카드를 위로 살짝 올림 / 다시 누르면 원위치
    public void SetSelectedRaised(bool selected)
    {
        raised = selected;
        SubscribeCanvas();
        ApplyRaiseAfterLayout();
        ApplyTint();
    }

    public bool IsSelectedRaised { get { return raised; } }

    private void ApplySprite(Sprite sprite)
    {
        baseTint = Color.white;
        if (background != null)
        {
            background.sprite = sprite;
            background.preserveAspect = true;
            background.type = Image.Type.Simple;
            background.raycastTarget = Card != null && playable;
        }
        if (label != null) label.gameObject.SetActive(false);
        ApplyTint();
    }

    private void ApplyTextFallback(CardData card)
    {
        EnsureLabelFont();
        baseTint = card.suit == "JOKER"
            ? new Color(0.18f, 0.18f, 0.18f)
            : Color.white;
        if (background != null)
        {
            background.sprite = null;
            background.raycastTarget = playable;
        }
        if (label != null)
        {
            label.gameObject.SetActive(true);
            if (card.suit == "JOKER")
            {
                label.text = "JOKER";
                label.color = JokerGold;
            }
            else
            {
                label.text = SuitSymbol(card.suit) + "\n" + card.rank;
                bool isRed = card.suit == "HEART" || card.suit == "DIAMOND";
                label.color = isRed ? Red : Black;
            }
        }
        ApplyTint();
    }

    private void ApplyTint()
    {
        if (background == null) return;
        Color c = playable ? baseTint : new Color(
            baseTint.r * DimMul.r,
            baseTint.g * DimMul.g,
            baseTint.b * DimMul.b,
            baseTint.a);
        // 선택 강조 (올림과 함께 살짝 밝게)
        if (raised && playable)
            c = Color.Lerp(c, Color.white, 0.18f);
        background.color = c;
    }

    private void EnsureLabelFont()
    {
        if (label == null) return;
        if (sharedUiFont == null)
            sharedUiFont = UiFonts.Primary;
        if (sharedUiFont != null)
            label.font = sharedUiFont;
    }

    public static string SuitSymbol(string suit)
    {
        switch (suit)
        {
            case "SPADE": return "S";
            case "HEART": return "H";
            case "DIAMOND": return "D";
            case "CLUB": return "C";
            default: return "?";
        }
    }
}
