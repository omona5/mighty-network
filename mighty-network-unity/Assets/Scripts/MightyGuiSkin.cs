using UnityEngine;

// Runtime IMGUI skin used by NetworkManager's lobby and game controls.
public static class MightyGuiSkin
{
    private static GUISkin skin;
    private static Font cachedFont;
    private static int cachedFontSize;

    public static GUISkin Get(Font font, int fontSize)
    {
        if (skin == null || skin.button.normal.background == null
            || cachedFont != font || cachedFontSize != fontSize)
            Build(font, fontSize);
        return skin;
    }

    private static void Build(Font font, int fontSize)
    {
        skin = Object.Instantiate(GUI.skin);
        cachedFont = font;
        cachedFontSize = fontSize;

        skin.font = font;
        skin.label = TextStyle(MightyTheme.Ink, font, fontSize, TextAnchor.MiddleLeft);
        skin.box = BoxStyle(MightyTheme.Panel, MightyTheme.Ink, font, fontSize);
        skin.button = ButtonStyle(font, fontSize);
        skin.textField = FieldStyle(font, fontSize);
        skin.textArea = FieldStyle(font, fontSize);
        skin.verticalScrollbar = GUI.skin.verticalScrollbar;
        skin.horizontalScrollbar = GUI.skin.horizontalScrollbar;
    }

    private static GUIStyle TextStyle(Color color, Font font, int size, TextAnchor align)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.font = font;
        style.fontSize = size;
        style.alignment = align;
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.padding = new RectOffset(4, 4, 3, 3);
        style.margin = new RectOffset(0, 0, 4, 4);
        style.wordWrap = true;
        return style;
    }

    private static GUIStyle BoxStyle(Color background, Color text, Font font, int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.font = font;
        style.fontSize = size;
        style.normal.background = Texture(background);
        style.normal.textColor = text;
        int vertical = Mathf.Max(10, Mathf.RoundToInt(size * 0.38f));
        style.padding = new RectOffset(18, 18, vertical, vertical);
        style.border = new RectOffset(2, 2, 2, 2);
        return style;
    }

    private static GUIStyle ButtonStyle(Font font, int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.font = font;
        style.fontSize = size;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.background = Texture(MightyTheme.Primary);
        style.hover.background = Texture(MightyTheme.PrimaryHover);
        style.active.background = Texture(MightyTheme.Accent);
        style.onNormal.background = Texture(MightyTheme.Accent);
        style.normal.textColor = MightyTheme.Ink;
        style.hover.textColor = Color.white;
        style.active.textColor = MightyTheme.Panel;
        style.onNormal.textColor = MightyTheme.Panel;
        // Do not inherit the default grey textures for focused/toggled states.
        style.focused.background = style.hover.background;
        style.focused.textColor = style.hover.textColor;
        style.onHover.background = style.onActive.background = style.onFocused.background = style.onNormal.background;
        style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = style.onNormal.textColor;
        int vertical = Mathf.Max(8, Mathf.RoundToInt(size * 0.35f));
        style.padding = new RectOffset(12, 12, vertical, vertical);
        style.margin = new RectOffset(0, 0, 4, 4);
        style.wordWrap = true;
        return style;
    }

    private static GUIStyle FieldStyle(Font font, int size)
    {
        GUIStyle style = new GUIStyle(GUI.skin.textField);
        style.font = font;
        style.fontSize = size;
        style.normal.background = Texture(MightyTheme.PanelSoft);
        style.focused.background = Texture(new Color(0.08f, 0.23f, 0.25f, 1f));
        style.normal.textColor = Color.white;
        style.focused.textColor = Color.white;
        int vertical = Mathf.Max(8, Mathf.RoundToInt(size * 0.32f));
        style.padding = new RectOffset(12, 12, vertical, vertical);
        style.margin = new RectOffset(0, 0, 4, 4);
        return style;
    }

    private static Texture2D Texture(Color color)
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }
}
