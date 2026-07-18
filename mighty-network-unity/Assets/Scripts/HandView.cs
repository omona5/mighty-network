using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// HandView: 내 손패 전체를 화면에 그리는 매니저.
//   - Inspector 연결:
//       cardPrefab    : 위에서 만든 Card 프리팹 (CardView가 붙어 있음)
//       cardContainer : 카드들이 나열될 부모 오브젝트
//                       (HorizontalLayoutGroup을 붙이면 자동 정렬됨)
//   - ShowHand(cards): 기존 카드를 지우고 받은 카드만큼 프리팹을 생성한다.
//   - Clear(): 카드를 모두 제거 (방 나갈 때 등)
// ============================================================================
public class HandView : MonoBehaviour
{
    [Header("Inspector에서 연결")]
    public CardView cardPrefab;     // 카드 프리팹
    public Transform cardContainer; // 카드가 놓일 부모 (레이아웃 그룹 권장)

    // 카드 클릭 콜백. NetworkManager가 연결한다. (null이면 클릭해도 반응 없음)
    public System.Action<CardData> onCardClicked;

    private readonly List<GameObject> spawned = new List<GameObject>();

    // 손패를 다시 그린다.
    public void ShowHand(CardData[] cards)
    {
        Clear();
        if (cards == null) return;

        Transform parent = cardContainer != null ? cardContainer : transform;
        foreach (CardData card in cards)
        {
            CardView view = Instantiate(cardPrefab, parent);
            view.SetCard(card);
            view.Clicked = onCardClicked; // 클릭하면 콜백 호출
            spawned.Add(view.gameObject);
        }
    }

    // 화면의 카드를 모두 제거한다.
    public void Clear()
    {
        foreach (GameObject go in spawned)
        {
            if (go != null) Destroy(go);
        }
        spawned.Clear();
    }
}
