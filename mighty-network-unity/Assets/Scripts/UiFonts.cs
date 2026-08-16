using UnityEngine;

// ============================================================================
// UiFonts: UI 공통 폰트 (Resources/Fonts/Galmuri11)
// ============================================================================
public static class UiFonts
{
    public const string PrimaryResourcePath = "Fonts/Galmuri11";
    public const float Scale = 1.5f;

    public static int Size(int px)
    {
        return Mathf.Max(1, Mathf.RoundToInt(px * Scale));
    }

    private static Font primary;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        primary = null;
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
}
