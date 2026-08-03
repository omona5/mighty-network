using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ============================================================================
// CardView: 카드 한 장(프리팹)에 붙는 스크립트.
//   - 카드 프리팹 구조(에디터에서 만들 것):
//       Card (RectTransform + Image + CardView)   <- 카드 스프라이트/배경
//         └ Label (Text)                          <- 스프라이트 없을 때 폴백 텍스트
//   - Inspector에서 background(자기 Image), label(자식 Text)을 연결한다.
//   - SetCard(...)로 스프라이트(또는 텍스트 폴백)를 세팅한다.
//   - SetFaceDown()으로 카드 뒷면을 표시한다.
// ============================================================================
public class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("Inspector에서 연결")]
    public Image background; // 카드 이미지 (보통 자기 자신의 Image)
    public Text label;       // 스프라이트 폴백용 텍스트 (있으면)

    // 이 카드가 담고 있는 데이터 (클릭 시 어떤 카드인지 알기 위함)
    public CardData Card { get; private set; }
    // 클릭 콜백 (HandView가 연결해준다). 클릭 불가 카드면 null로 둔다.
    public System.Action<CardData> Clicked;

    private static readonly Color Red = new Color(0.83f, 0.0f, 0.0f);
    private static readonly Color Black = new Color(0.10f, 0.10f, 0.10f);
    private static readonly Color JokerGold = new Color(0.95f, 0.70f, 0.10f);
    private static Font sharedUiFont;

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(Card);
    }

    // 카드 데이터에 맞춰 표시 내용을 세팅한다.
    public void SetCard(CardData card)
    {
        Card = card;
        if (card == null) return;

        Sprite sprite = CardSpriteAtlas.Get(card.id);
        if (sprite != null)
        {
            ApplySprite(sprite);
            return;
        }

        // 스프라이트 없으면 기존 텍스트 폴백
        ApplyTextFallback(card);
    }

    // 뒷면(비공개) 표시 — 상대 손패·바닥패 등에 사용
    public void SetFaceDown()
    {
        Card = null;
        Sprite back = CardSpriteAtlas.GetBack();
        if (back != null)
        {
            ApplySprite(back);
            return;
        }
        if (background != null)
        {
            background.sprite = null;
            background.color = new Color(0.15f, 0.25f, 0.55f);
        }
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = "?";
            label.color = Color.white;
        }
    }

    private void ApplySprite(Sprite sprite)
    {
        if (background != null)
        {
            background.sprite = sprite;
            background.color = Color.white;
            background.preserveAspect = true;
            background.type = Image.Type.Simple;
            // 뒷면(데이터 없음)은 클릭 가로채지 않음
            background.raycastTarget = Card != null;
        }
        if (label != null) label.gameObject.SetActive(false);
    }

    private void ApplyTextFallback(CardData card)
    {
        EnsureLabelFont();
        if (background != null)
        {
            background.sprite = null;
            background.color = card.suit == "JOKER"
                ? new Color(0.18f, 0.18f, 0.18f)
                : Color.white;
        }
        if (label == null) return;
        label.gameObject.SetActive(true);

        if (card.suit == "JOKER")
        {
            label.text = "JOKER";
            label.color = JokerGold;
            return;
        }

        // WebGL: ♠ 등 심볼 폰트 없을 수 있어 S/H/D/C 사용
        label.text = SuitSymbol(card.suit) + "\n" + card.rank;
        bool isRed = card.suit == "HEART" || card.suit == "DIAMOND";
        label.color = isRed ? Red : Black;
    }

    private void EnsureLabelFont()
    {
        if (label == null) return;
        if (sharedUiFont == null)
            sharedUiFont = Resources.Load<Font>("Fonts/NotoSansKR-Regular");
        if (sharedUiFont != null)
            label.font = sharedUiFont;
    }

    // WebGL 호환: 유니코드 슈트 대신 ASCII 약자 (빨강/검정은 색으로 구분)
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
