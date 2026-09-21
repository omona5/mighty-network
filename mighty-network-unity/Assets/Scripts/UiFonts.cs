using TMPro;
using UnityEngine;

// ============================================================================
// UiFonts: UI 공통 폰트 (Resources/Fonts/Galmuri11) + TMP SDF 에셋
// ============================================================================
public static class UiFonts
{
    public const string PrimaryResourcePath = "Fonts/Galmuri11";
    public static int PlayerStatusSize => ResponsiveCanvas.IsPortrait ? 36 : 26;

    // Match the table's physical text size despite IMGUI's separate menu scale.
    // Keep portrait touch UI and the existing desktop upper bound unchanged.
    public static int GameMenuSize => ResponsiveCanvas.IsPortrait ? 16
        : Mathf.Clamp(Mathf.RoundToInt(PlayerStatusSize
            * (ResponsiveCanvas.ViewWidth / ResponsiveCanvas.LandscapeReference.x)
            / MenuScale), 1, 18);
    // Menus use a 390px phone viewport or a bounded desktop scale. Apply once:
    // via GUI.matrix for IMGUI, or divided by Canvas.scaleFactor for TMP.
    public static float MenuScale => ResponsiveCanvas.IsPortrait
        ? Mathf.Max(1f, ResponsiveCanvas.ViewWidth) / 390f
        : Mathf.Clamp(ResponsiveCanvas.ViewHeight / 900f, 0.85f, 1.5f);

    // These sizes are authored in logical UI pixels.  CanvasScaler handles the
    // physical enlargement; IMGUI does not, so keep this multiplier deliberately
    // conservative on narrow displays.
    private const float BaseScale = 1.0f;

    // IMGUI는 CanvasScaler를 거치지 않으므로 화면 크기와 방향을 직접 반영한다.
    // uGUI/TMP도 생성 시 이 값을 사용하고, 이후에는 ResponsiveCanvas가 물리 해상도를 맞춘다.
    public static float Scale
    {
        get
        {
            float shortSide = Mathf.Max(1f, Mathf.Min(ResponsiveCanvas.ViewWidth, ResponsiveCanvas.ViewHeight));
            float density = Mathf.Lerp(0.78f, 1.05f, Mathf.InverseLerp(360f, 1080f, shortSide));
            bool portrait = ResponsiveCanvas.ViewHeight > ResponsiveCanvas.ViewWidth;
            float orientation = portrait ? 0.88f : 1.0f;
            return Mathf.Clamp(BaseScale * density * orientation, 0.78f, 1.12f);
        }
    }

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
                tmpPrimary.isMultiAtlasTexturesEnabled = true;
            }
            return tmpPrimary;
        }
    }
}
