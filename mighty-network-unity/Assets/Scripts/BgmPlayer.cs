using UnityEngine;
using System.Collections.Generic;

public sealed class BgmPlayer : MonoBehaviour
{
    private AudioSource source;
    private AudioClip title;
    private readonly List<AudioClip> tracks = new List<AudioClip>();
    private bool focused = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        GameSettings.Initialize();
        var player = new GameObject("BgmPlayer");
        DontDestroyOnLoad(player);
        player.AddComponent<BgmPlayer>();
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        ApplyVolume();
        title = Resources.Load<AudioClip>("Bgm/title");
        foreach (string path in new[] { "Bgm/title", "Bgm/in-game1", "Bgm/in-game2" })
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip != null) tracks.Add(clip);
        }
        source.loop = false;
        GameSettings.AudioChanged += ApplyVolume;
    }

    private void Start()
    {
        // Start with the title track once; scene changes never interrupt playback.
        if (title != null) Play(title);
        else PlayRandomTrack();
    }

    private void Update()
    {
        // Muting leaves playback running. Focus loss / listener pause must not skip songs.
        if (!focused || AudioListener.pause || source.clip == null || source.isPlaying) return;
        PlayRandomTrack();
    }

    private void PlayRandomTrack()
    {
        if (tracks.Count == 0) return;
        int previous = tracks.IndexOf(source.clip);
        // All BGM tracks participate, without immediately repeating the same song.
        int next = Random.Range(0, tracks.Count > 1 && previous >= 0 ? tracks.Count - 1 : tracks.Count);
        if (tracks.Count > 1 && previous >= 0 && next >= previous) next++;
        Play(tracks[next]);
    }

    private void Play(AudioClip clip)
    {
        source.clip = clip;
        if (clip != null) source.Play();
        else Debug.LogWarning("[BgmPlayer] Missing BGM clip.");
    }

    private void ApplyVolume()
    {
        source.volume = GameSettings.MusicVolume;
        source.mute = GameSettings.MusicMuted;
    }
    private void OnApplicationFocus(bool value) => focused = value;

    private void OnDestroy()
    {
        GameSettings.AudioChanged -= ApplyVolume;
    }
}
