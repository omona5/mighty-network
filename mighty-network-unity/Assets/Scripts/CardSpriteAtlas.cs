using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// CardSpriteAtlas: card_sheet.png (10×6, 셀 320×448)에서 카드 스프라이트를 꺼낸다.
//
// 시트 순서 (좌→우, 위→아래):
//   ♠ A..K (13) → ♦ A..K (13) → ♥ A..K (13) → ♣ A..K (13) → JOKER → BACK
//   나머지 6칸은 빈 셀.
//
// Resources 경로: Resources/Cards/card_sheet
//   ※ 원본을 Sprites/card.ss.png 에서 수정했다면 이 파일로 다시 복사해야 반영됨.
// ============================================================================
public static class CardSpriteAtlas
{
    public const int CellWidth = 320;
    public const int CellHeight = 448;
    public const int Columns = 10;
    public const int Rows = 6;
    public const string BackId = "BACK";

    // UI 표시 크기: 원본의 1/2 (정수 배율 축소)
    public const float DisplayWidth = 160f;
    public const float DisplayHeight = 224f;

    private const string ResourcePath = "Cards/card_sheet";

    private static readonly string[] RankOrder =
        { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

    // 시트 무늬 순서 (서버 HandView 정렬 순서와 다름: S→D→H→C)
    private static readonly string[] SuitOrder =
        { "SPADE", "DIAMOND", "HEART", "CLUB" };

    private static Dictionary<string, Sprite> byId;
    private static Sprite backSprite;
    private static bool loadAttempted;

    // Domain Reload 꺼둔 에디터 / 재Play 시 이전 스프라이트 캐시 제거
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        byId = null;
        backSprite = null;
        loadAttempted = false;
    }

    public static Sprite Get(string cardId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(cardId) || byId == null) return null;
        Sprite s;
        return byId.TryGetValue(cardId, out s) ? s : null;
    }

    public static Sprite GetBack()
    {
        EnsureLoaded();
        return backSprite;
    }

    public static bool IsReady
    {
        get
        {
            EnsureLoaded();
            return byId != null && byId.Count > 0;
        }
    }

    private static void EnsureLoaded()
    {
        if (loadAttempted) return;
        loadAttempted = true;

        Texture2D tex = Resources.Load<Texture2D>(ResourcePath);
        if (tex == null)
        {
            Debug.LogWarning("[CardSpriteAtlas] Resources/" + ResourcePath + " 로드 실패");
            return;
        }

        // 픽셀아트: Point 필터 (블러 방지). 1/2 표시(160×224)와 맞춤.
        tex.filterMode = FilterMode.Point;
        tex.anisoLevel = 0;
        tex.wrapMode = TextureWrapMode.Clamp;

        byId = new Dictionary<string, Sprite>(64);
        int index = 0;

        foreach (string suit in SuitOrder)
        {
            string prefix = SuitPrefix(suit);
            foreach (string rank in RankOrder)
            {
                string id = prefix + "_" + rank;
                byId[id] = CreateSprite(tex, index, id);
                index++;
            }
        }

        byId["JOKER"] = CreateSprite(tex, index, "JOKER");
        index++;
        backSprite = CreateSprite(tex, index, BackId);
        byId[BackId] = backSprite;

        Debug.Log("[CardSpriteAtlas] 로드 완료: " + byId.Count + "장 (텍스처 "
            + tex.width + "x" + tex.height + ")");
    }

    private static Sprite CreateSprite(Texture2D tex, int index, string name)
    {
        int col = index % Columns;
        int row = index / Columns; // 0 = 시트 맨 위
        // Unity Rect y는 텍스처 하단이 0
        float x = col * CellWidth;
        float y = (Rows - 1 - row) * CellHeight;
        var rect = new Rect(x, y, CellWidth, CellHeight);
        var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        return sprite;
    }

    private static string SuitPrefix(string suit)
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
