using TMPro;
using UnityEngine;

// ============================================================================
// UiFonts: UI 공통 폰트 (Resources/Fonts/Galmuri11) + TMP SDF 에셋
// ============================================================================
public static class UiFonts
{
    public const string PrimaryResourcePath = "Fonts/Galmuri11";
    public const float Scale = 1.5f;

    public static int Size(int px)
    {
        return Mathf.Max(1, Mathf.RoundToInt(px * Scale));
    }

    public static float Layout(float px)
    {
        return px * Scale;
    }

    private static Font primary;
    private static TMP_FontAsset tmpPrimary;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        primary = null;
        tmpPrimary = null;
    }

    public static Font Primary
    {
        get
        {
            if (primary == null)
                primary = Resources.Load<Font>(PrimaryResourcePath);
            return primary;
        }
    }

    // 런타임 SDF — CanvasScaler 배율이 바뀌어도 또렷함
    public static TMP_FontAsset TmpPrimary
    {
        get
        {
            if (tmpPrimary != null) return tmpPrimary;
            Font src = Primary;
            if (src == null) return null;
            tmpPrimary = TMP_FontAsset.CreateFontAsset(src);
            if (tmpPrimary != null)
            {
                tmpPrimary.name = "Galmuri11_TMP_Runtime";
                // 한글 글리프 동적 추가
                tmpPrimary.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            }
            return tmpPrimary;
        }
    }
}
