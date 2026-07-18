using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// ============================================================================
// CardView: 카드 한 장(프리팹)에 붙는 스크립트.
//   - 카드 프리팹 구조(에디터에서 만들 것):
//       Card (RectTransform + Image + CardView)   <- 흰색 배경
//         └ Label (Text)                          <- 무늬+숫자 텍스트
//   - Inspector에서 background(자기 Image), label(자식 Text)을 연결한다.
//   - SetCard(...)로 무늬/숫자에 맞게 텍스트와 색을 바꾼다.
//   - 카드를 클릭하면 Clicked 콜백이 호출된다. (06단계: 카드 내기)
// ============================================================================
public class CardView : MonoBehaviour, IPointerClickHandler
{
    [Header("Inspector에서 연결")]
    public Image background; // 카드 배경 이미지 (보통 자기 자신의 Image)
    public Text label;       // 무늬+숫자를 보여줄 자식 Text

    // 이 카드가 담고 있는 데이터 (클릭 시 어떤 카드인지 알기 위함)
    public CardData Card { get; private set; }
    // 클릭 콜백 (HandView가 연결해준다). 클릭 불가 카드면 null로 둔다.
    public System.Action<CardData> Clicked;

    private static readonly Color Red = new Color(0.83f, 0.0f, 0.0f);
    private static readonly Color Black = new Color(0.10f, 0.10f, 0.10f);
    private static readonly Color JokerGold = new Color(0.95f, 0.70f, 0.10f);

    public void OnPointerClick(PointerEventData eventData)
    {
        Clicked?.Invoke(Card);
    }

    // 카드 데이터에 맞춰 표시 내용을 세팅한다.
    public void SetCard(CardData card)
    {
        Card = card;
        if (card == null) return;

        if (card.suit == "JOKER")
        {
            label.text = "JOKER";
            label.color = JokerGold;
            if (background != null) background.color = new Color(0.18f, 0.18f, 0.18f);
            return;
        }

        // 무늬 기호 + 숫자 (줄바꿈으로 위: 기호, 아래: 숫자)
        label.text = SuitSymbol(card.suit) + "\n" + card.rank;
        bool isRed = card.suit == "HEART" || card.suit == "DIAMOND";
        label.color = isRed ? Red : Black;
        if (background != null) background.color = Color.white;
    }

    private static string SuitSymbol(string suit)
    {
        switch (suit)
        {
            case "SPADE": return "\u2660";   // ♠
            case "HEART": return "\u2665";   // ♥
            case "DIAMOND": return "\u2666"; // ♦
            case "CLUB": return "\u2663";    // ♣
            default: return "?";
        }
    }
}
