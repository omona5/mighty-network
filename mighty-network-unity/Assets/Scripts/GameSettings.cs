using System;
using UnityEngine;
using UnityEngine.Localization.Settings;

public static class GameSettings
{
    private const string VolumeKey = "mighty.masterVolume";
    private const string LanguageKey = "mighty.language";
    private const string SfxVolumeKey = "mighty.sfxVolume";
    private const string MusicVolumeKey = "mighty.musicVolume";
    private const string SfxMutedKey = "mighty.sfxMuted";
    private const string MusicMutedKey = "mighty.musicMuted";
    public static event Action AudioChanged;
    public static float SfxVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 1f;
    public static bool SfxMuted { get; private set; }
    public static bool MusicMuted { get; private set; }
    public static event Action LanguageChanged;
    public static string Language { get; private set; } = "ko";
    public static float MasterVolume { get; private set; } = 1f;
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        initialized = false;
        LanguageChanged = null;
        AudioChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        Language = PlayerPrefs.GetString(LanguageKey, "ko") == "en" ? "en" : "ko";
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, MasterVolume));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, MasterVolume));
        // Preserve the old global mute preference when migrating to separate controls.
        int legacyMute = PlayerPrefs.GetInt("mighty.muted", 0);
        SfxMuted = PlayerPrefs.GetInt(SfxMutedKey, legacyMute) != 0;
        MusicMuted = PlayerPrefs.GetInt(MusicMutedKey, legacyMute) != 0;
        AudioListener.volume = 1f;
        var operation = LocalizationSettings.InitializationOperation;
        if (operation.IsDone) ApplyLocale();
        else operation.Completed += _ => ApplyLocale();
    }

    private static void ApplyLocale()
    {
        var locale = LocalizationSettings.AvailableLocales.GetLocale(Language);
        if (locale != null) LocalizationSettings.SelectedLocale = locale;
    }

    public static void SetVolume(float value)
    {
        Initialize();
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        SetSfxVolume(value);
        SetMusicVolume(value);
    }

    public static void SetSfxVolume(float value)
    {
        Initialize();
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        AudioChanged?.Invoke();
    }

    public static void SetMusicVolume(float value)
    {
        Initialize();
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        AudioChanged?.Invoke();
    }

    public static void SetSfxMuted(bool value)
    {
        Initialize();
        SfxMuted = value;
        PlayerPrefs.SetInt(SfxMutedKey, value ? 1 : 0);
        AudioChanged?.Invoke();
    }

    public static void SetMusicMuted(bool value)
    {
        Initialize();
        MusicMuted = value;
        PlayerPrefs.SetInt(MusicMutedKey, value ? 1 : 0);
        AudioChanged?.Invoke();
    }

    public static void SetLanguage(string language)
    {
        Initialize();
        if (language != "ko" && language != "en") return;
        if (Language == language) return;
        Language = language;
        PlayerPrefs.SetString(LanguageKey, language);
        PlayerPrefs.Save();
        if (LocalizationSettings.InitializationOperation.IsDone) ApplyLocale();
        LanguageChanged?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();
}
