using TMPro;
using UnityEngine;

// ============================================================================
// UiTmp: uGUI Text → TextMeshProUGUI 공통 생성 (스케일 변경에도 SDF로 또렷하게)
// ============================================================================
public static class UiTmp
{
    public static TMP_FontAsset FontAsset
    {
        get { return UiFonts.TmpPrimary; }
    }

    public static TextAlignmentOptions Align(TextAnchor anchor)
    {
        switch (anchor)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.MidlineLeft;
            case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.MidlineRight;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }

    public static TextMeshProUGUI Add(
        GameObject go,
        int fontSize,
        TextAnchor align,
        Color color,
        bool overflow = true,
        bool outline = true)
    {
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null)
            tmp = go.AddComponent<TextMeshProUGUI>();

        Apply(tmp, fontSize, align, color, overflow, outline);
        return tmp;
    }

    public static TextMeshProUGUI Create(
        Transform parent,
        string name,
        int fontSize,
        TextAnchor align,
        Color color,
        bool overflow = true,
        bool outline = true)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return Add(go, fontSize, align, color, overflow, outline);
    }

    public static void Apply(
        TextMeshProUGUI tmp,
        int fontSize,
        TextAnchor align,
        Color color,
        bool overflow = true,
        bool outline = true)
    {
        if (tmp == null) return;
        TMP_FontAsset font = FontAsset;
        if (font != null)
            tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.alignment = Align(align);
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = !overflow;
        tmp.overflowMode = overflow ? TextOverflowModes.Overflow : TextOverflowModes.Truncate;
        tmp.richText = false;
        if (outline)
        {
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = new Color32(0, 0, 0, 200);
        }
        else
        {
            tmp.outlineWidth = 0f;
        }
    }
}
