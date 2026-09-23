// ============================================================================
// GameScenes: 빌드에 등록된 씬 이름
// ============================================================================
public static class GameScenes
{
    public const string Title = "TitleScene";
    public const string Game = "SampleScene";

    // One-shot launch request; direct Game scene launches retain the lobby.
    public static bool StartSinglePlayer;
    public static bool StartTutorial;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLaunchMode() { StartSinglePlayer = false; StartTutorial = false; }
}
