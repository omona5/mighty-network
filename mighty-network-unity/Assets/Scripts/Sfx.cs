using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// Sfx: Resources/Sfx 클립을 PlayOneShot.
//   카드 동작은 변형(1~4) 중 무작위.
//   딜 틱은 쿨다운으로 겹침을 줄인다.
// ============================================================================
public static class Sfx
{
    private const string ResourceRoot = "Sfx/";
    private const float DealTickGap = 0.07f;

    private static AudioSource source;
    private static AudioSource electionSource;
    private static readonly Dictionary<string, AudioClip[]> cache =
        new Dictionary<string, AudioClip[]>();
    private static float lastDealTickAt = -10f;
    private static int lastUiClickFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        source = null;
        electionSource = null;
        cache.Clear();
        lastDealTickAt = -10f;
        lastUiClickFrame = -1;
    }

    public static void Ensure()
    {
        GameSettings.Initialize();
        if (source != null) return;

        GameObject go = new GameObject("SfxPlayer");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = false;
        source.volume = GameSettings.SfxVolume;
        source.mute = GameSettings.SfxMuted;
        electionSource = go.AddComponent<AudioSource>();
        electionSource.playOnAwake = false;
        electionSource.spatialBlend = 0f;
        electionSource.pitch = 0.75f;
        electionSource.volume = GameSettings.SfxVolume;
        electionSource.mute = GameSettings.SfxMuted;
        GameSettings.AudioChanged -= ApplyVolume;
        GameSettings.AudioChanged += ApplyVolume;
    }

    private static void ApplyVolume()
    {
        if (electionSource != null)
        {
            electionSource.volume = GameSettings.SfxVolume;
            electionSource.mute = GameSettings.SfxMuted;
        }
        if (source == null) return;
        source.volume = GameSettings.SfxVolume;
        source.mute = GameSettings.SfxMuted;
    }

    public static void UiClick()
    {
        // A button callback can also open a popup with its own click cue.
        if (lastUiClickFrame == Time.frameCount) return;
        lastUiClickFrame = Time.frameCount;
        Play("ui_click", 0.55f);
    }
    public static void Title() { Play("title", 0.8f); }
    public static void Shuffle() { Play("shuffle", 0.75f); }
    public static void KittyFan() { Play("kitty_fan", 0.7f); }
    public static void KittyTake() { Play("kitty_take", 0.75f); }
    public static void Discard() { Play("discard", 0.7f); }
    public static void Bid() { Play("bid", 0.7f); }
    public static void Pass() { Play("pass", 0.6f); }
    public static void Elected()
    {
        Ensure();
        AudioClip[] clips = Load("elected");
        if (electionSource != null && clips.Length > 0)
        {
            electionSource.Stop();
            electionSource.PlayOneShot(clips[0], 0.85f);
        }
    }
    public static void Mighty() { Play("mighty", 0.9f); }
    public static void Joker() { Play("joker", 0.85f); }
    public static void JokerCall() { Play("joker_call", 0.85f); }
    public static void Friend() { Play("friend", 0.85f); }
    public static void DealMiss() { Play("deal_miss", 0.85f); }
    public static void TrickWin() { Play("trick_win", 0.8f); }
    public static void GameWin() { Play("game_win", 0.9f); }
    public static void GameLose() { Play("game_lose", 0.85f); }

    public static void CardPlay() { Play("card_play", 0.72f); }
    public static void CardLand() { Play("card_land", 0.7f); }

    public static void DealTick()
    {
        float now = Time.unscaledTime;
        if (now - lastDealTickAt < DealTickGap) return;
        lastDealTickAt = now;
        Play("deal", 0.48f);
    }

    public static void PlayedCard(
        CardData card, bool jokerCall, string mightyCardId, string friendCardId)
    {
        if (card == null) return;

        if (!string.IsNullOrEmpty(mightyCardId) && card.id == mightyCardId)
        {
            Mighty();
            return;
        }
        if (card.id == "JOKER")
        {
            Joker();
            return;
        }
        if (jokerCall)
        {
            JokerCall();
            return;
        }
        if (!string.IsNullOrEmpty(friendCardId) && card.id == friendCardId)
            Friend();
    }

    private static void Play(string id, float volume)
    {
        Ensure();
        if (source == null) return;

        AudioClip[] clips = Load(id);
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        source.PlayOneShot(clip, volume);
    }

    private static AudioClip[] Load(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        AudioClip[] cached;
        if (cache.TryGetValue(id, out cached)) return cached;

        var list = new List<AudioClip>(4);
        AudioClip exact = Resources.Load<AudioClip>(ResourceRoot + id);
        if (exact != null) list.Add(exact);
        for (int i = 1; i <= 8; i++)
        {
            AudioClip c = Resources.Load<AudioClip>(ResourceRoot + id + "_" + i);
            if (c == null)
            {
                if (i == 1) continue;
                break;
            }
            list.Add(c);
        }

        cached = list.ToArray();
        cache[id] = cached;
        if (list.Count == 0)
            Debug.LogWarning("[Sfx] Resources/" + ResourceRoot + id + " 로드 실패");
        return cached;
    }
}
