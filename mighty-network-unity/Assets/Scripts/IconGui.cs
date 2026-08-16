using UnityEngine;
using UnityEngine.UI;

// ============================================================================
// IconGui: OnGUI 아이콘 버튼 / uGUI Image 헬퍼
// ============================================================================
public static class IconGui
{
    public static bool Button(IconSpriteAtlas.Slice slice, float w, float h)
    {
        Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
        bool hit = GUI.Button(r, GUIContent.none);
        Draw(r, slice);
        return hit;
    }

    public static bool ToggleButton(IconSpriteAtlas.Slice slice, bool selected, float w, float h)
    {
        Color prev = GUI.color;
        if (selected) GUI.color = new Color(1f, 1f, 0.65f, 1f);
        bool hit = Button(slice, w, h);
        GUI.color = prev;
        return hit;
    }

    public static void DrawLayout(IconSpriteAtlas.Slice slice, float w, float h)
    {
        Rect r = GUILayoutUtility.GetRect(w, h, GUILayout.Width(w), GUILayout.Height(h));
        Draw(r, slice);
    }

    public static void Draw(Rect r, IconSpriteAtlas.Slice slice)
    {
        if (!slice.IsValid) return;
        GUI.DrawTextureWithTexCoords(r, slice.tex, slice.uv, true);
    }

    public static Image MakeImage(Transform parent, string name, IconSpriteAtlas.Slice slice, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        Apply(img, slice);
        return img;
    }

    public static void Apply(Image img, IconSpriteAtlas.Slice slice)
    {
        if (img == null) return;
        if (!slice.IsValid)
        {
            img.enabled = false;
            img.sprite = null;
            return;
        }
        img.enabled = true;
        img.sprite = slice.sprite;
        img.color = Color.white;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
    }

    // 주공/프렌드 아이콘을 center 기준으로 가로 나란히 배치
    public static void PlaceRoleIcons(Transform parent, bool isDeclarer, bool isFriend, Vector2 center)
    {
        Vector2 sz = IconSpriteAtlas.DisplaySquare;
        int n = (isDeclarer ? 1 : 0) + (isFriend ? 1 : 0);
        if (n == 0) return;
        float gap = 4f;
        float total = n * sz.x + (n - 1) * gap;
        float x = center.x - total * 0.5f + sz.x * 0.5f;
        if (isDeclarer)
        {
            Image img = MakeImage(parent, "Declarer", IconSpriteAtlas.GetDeclarer(), sz);
            SetCenter(img.rectTransform, new Vector2(x, center.y));
            x += sz.x + gap;
        }
        if (isFriend)
        {
            Image img = MakeImage(parent, "Friend", IconSpriteAtlas.GetFriend(), sz);
            SetCenter(img.rectTransform, new Vector2(x, center.y));
        }
    }

    private static void SetCenter(RectTransform rt, Vector2 pos)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }
}
