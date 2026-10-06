using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Tables;

// Only authored UI strings enter this lookup. Player names and protocol values
// are never searched/replaced, even when a name happens to match a UI label.
public static class L10n
{
    private static Dictionary<string, string> english;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => english = null;

    public static string Text(string korean)
    {
        GameSettings.Initialize();
        if (korean == null || GameSettings.Language != "en") return korean;
        if (english == null)
        {
            english = new Dictionary<string, string>();
            // Resources keeps initial menus synchronous on WebGL; these are the
            // same String Tables used by Unity Localization's locale settings.
            var ko = Resources.Load<StringTable>("Localization/MightyUI_ko");
            var en = Resources.Load<StringTable>("Localization/MightyUI_en");
            if (ko != null && en != null)
                foreach (var entry in ko.Values)
                {
                    var translated = en.GetEntry(entry.KeyId);
                    if (translated != null) english[entry.Value] = translated.Value;
                }
        }
        return english.TryGetValue(korean, out var value) ? value : korean;
    }

    public static string ServerMessage(string message)
    {
        const string missingRoom = "방을 찾을 수 없습니다: ";
        if (message != null && message.StartsWith(missingRoom, System.StringComparison.Ordinal))
            return Text(missingRoom) + message.Substring(missingRoom.Length);
        return Text(message);
    }
}
