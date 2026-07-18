// 서버가 보내는 카드 한 장의 데이터 구조와 동일하게 맞춘 클래스.
//   서버(Card.js): { id, suit, rank, point }
//
// JsonUtility로 파싱하려면 public 필드 + [System.Serializable] 필요.
[System.Serializable]
public class CardData
{
    public string id;    // 예: "S_A", "JOKER"
    public string suit;  // SPADE / HEART / DIAMOND / CLUB / JOKER
    public string rank;  // "2".."10", "J", "Q", "K", "A", "JOKER"
    public int point;    // 점수 카드면 1, 아니면 0
}
