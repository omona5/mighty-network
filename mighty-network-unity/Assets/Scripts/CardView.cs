using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ============================================================================
// CardView: 카드 한 장(프리팹)에 붙는 스크립트.
//   - SetCard / SetFaceDown / SetPlayable(음영·클릭)
// ============================================================================
public class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("Inspector에서 연결")]
    public Image background; // 카드 이미지 (보통 자기 자신의 Image)
    public Text label;       // 스프라이트 폴백용 텍스트 (있으면)

    public CardData Card { get; private set; }
    public System.Action<CardData> Clicked;

    private static readonly Color Red = new Color(0.83f, 0.0f, 0.0f);
    private static readonly Color Black = new Color(0.10f, 0.10f, 0.10f);
    private static readonly Color JokerGold = new Color(0.95f, 0.70f, 0.10f);
    private static readonly Color DimMul = new Color(0.42f, 0.42f, 0.42f, 1f);
    private static Font sharedUiFont;

    private Color baseTint = Color.white;
    private bool playable = true;

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
        background.color = playable ? baseTint : new Color(
            baseTint.r * DimMul.r,
            baseTint.g * DimMul.g,
            baseTint.b * DimMul.b,
            baseTint.a);
    }

    private void EnsureLabelFont()
    {
        if (label == null) return;
        if (sharedUiFont == null)
            sharedUiFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
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
