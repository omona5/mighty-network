using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// IconSpriteAtlas: UI 아이콘 시트 슬라이스
//   icon_card  128×64  — 13열 × 5행 (♠♦♥♣ + joker)
//   icon_shape 64×64   — ♠ ♦ ♥ ♣ No
//   icon_etc   64×64   — 주공(왕관) / 프렌드(F)
// ============================================================================
public static class IconSpriteAtlas
{
    public const int CardW = 128;
    public const int CardH = 64;
    public const int ShapeW = 64;
    public const int ShapeH = 64;
    public const int RoleW = 64;
    public const int RoleH = 64;

    // 표시 크기: 마이티 HUD(원본 128×64 → 96×48, 배율 0.75)에 맞춤
    public static readonly Vector2 DisplayCard = new Vector2(96f, 48f);
    public static readonly Vector2 DisplaySquare = new Vector2(48f, 48f);

    public struct Slice
    {
        public Texture2D tex;
        public Rect uv;
        public int pxW;
        public int pxH;
        public Sprite sprite;
        public bool IsValid { get { return tex != null && sprite != null; } }
    }

    private static readonly string[] RankOrder =
        { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
    private static readonly string[] CardSuitOrder =
        { "SPADE", "DIAMOND", "HEART", "CLUB" };
    private static readonly string[] ShapeOrder =
        { "SPADE", "DIAMOND", "HEART", "CLUB" };

    private static Dictionary<string, Slice> cards;
    private static Dictionary<string, Slice> shapes;
    private static Slice noTrump;
    private static Slice declarer;
    private static Slice friend;
    private static bool loadAttempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        cards = null;
        shapes = null;
        loadAttempted = false;
    }

    public static Slice GetCard(string cardId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(cardId) || cards == null) return default(Slice);
        Slice s;
        return cards.TryGetValue(cardId, out s) ? s : default(Slice);
    }

    public static Slice GetSuit(string suit)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(suit) || shapes == null) return default(Slice);
        Slice s;
        return shapes.TryGetValue(suit, out s) ? s : default(Slice);
    }

    public static Slice GetNoTrump()
    {
        EnsureLoaded();
        return noTrump;
    }

    public static Slice GetTrump(bool isNoTrump, string trumpSuit)
    {
        return isNoTrump ? GetNoTrump() : GetSuit(trumpSuit);
    }

    public static Slice GetDeclarer()
    {
        EnsureLoaded();
        return declarer;
    }

    public static Slice GetFriend()
    {
        EnsureLoaded();
        return friend;
    }

    public static string[] Ranks { get { return RankOrder; } }
    public static string[] CardSuits { get { return CardSuitOrder; } }

    public static string SuitPrefix(string suit)
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

    public static string CardId(string suit, string rank)
    {
        return SuitPrefix(suit) + "_" + rank;
    }

    private static void EnsureLoaded()
    {
        if (loadAttempted) return;
        loadAttempted = true;

        cards = new Dictionary<string, Slice>(64);
        shapes = new Dictionary<string, Slice>(8);

        Texture2D cardTex = LoadTex("Icons/icon_card");
        if (cardTex != null)
        {
            int index = 0;
            foreach (string suit in CardSuitOrder)
            {
                foreach (string rank in RankOrder)
                {
                    string id = CardId(suit, rank);
                    cards[id] = MakeSlice(cardTex, index % 13, index / 13, 13, 5, CardW, CardH, id);
                    index++;
                }
            }
            cards["JOKER"] = MakeSlice(cardTex, 0, 4, 13, 5, CardW, CardH, "JOKER");
        }

        Texture2D shapeTex = LoadTex("Icons/icon_shape");
        if (shapeTex != null)
        {
            for (int i = 0; i < ShapeOrder.Length; i++)
                shapes[ShapeOrder[i]] = MakeSlice(shapeTex, i, 0, 5, 1, ShapeW, ShapeH, ShapeOrder[i]);
            noTrump = MakeSlice(shapeTex, 4, 0, 5, 1, ShapeW, ShapeH, "NT");
        }

        Texture2D etcTex = LoadTex("Icons/icon_etc");
        if (etcTex != null)
        {
            declarer = MakeSlice(etcTex, 0, 0, 2, 1, RoleW, RoleH, "DECLARER");
            friend = MakeSlice(etcTex, 1, 0, 2, 1, RoleW, RoleH, "FRIEND");
        }
    }

    private static Texture2D LoadTex(string path)
    {
        Texture2D tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogWarning("[IconSpriteAtlas] Resources/" + path + " 로드 실패");
            return null;
        }
        tex.filterMode = FilterMode.Point;
        tex.anisoLevel = 0;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    private static Slice MakeSlice(
        Texture2D tex, int col, int row, int columns, int rows, int cellW, int cellH, string name)
    {
        float x = col * cellW;
        float y = (rows - 1 - row) * cellH;
        var rect = new Rect(x, y, cellW, cellH);
        Sprite sprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        float u0 = x / tex.width;
        float v0 = y / tex.height;
        return new Slice
        {
            tex = tex,
            uv = new Rect(u0, v0, (float)cellW / tex.width, (float)cellH / tex.height),
            pxW = cellW,
            pxH = cellH,
            sprite = sprite,
        };
    }
}
