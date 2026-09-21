using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public static class MightyLocalizationBuilder
{
    [Serializable] public sealed class Entry { public string ko; public string en; }
    [Serializable] public sealed class Catalog { public Entry[] entries; }

    [MenuItem("Mighty/Rebuild Localization Tables")]
    public static void Build()
    {
        const string folder = "Assets/Resources/Localization";
        Directory.CreateDirectory(folder);
        AssetDatabase.Refresh();
        var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, folder + "/LocalizationSettings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        foreach (string code in new[] { "ko", "en" })
        {
            if (LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == code)) continue;
            var locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, folder + "/" + code + ".asset");
            LocalizationEditorSettings.AddLocale(locale);
        }
        var collection = LocalizationEditorSettings.GetStringTableCollection("MightyUI")
            ?? LocalizationEditorSettings.CreateStringTableCollection("MightyUI", folder);
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Editor/MightyLocalizationSource.json"));
        if (catalog.entries.Select(e => e.ko).Distinct().Count() != catalog.entries.Length)
            throw new Exception("Duplicate source translation");
        int count = 0;
        foreach (var table in collection.StringTables)
        {
            string code = table.LocaleIdentifier.Code;
            if (code != "ko" && code != "en") continue;
            for (int i = 0; i < catalog.entries.Length; i++)
            {
                var source = catalog.entries[i];
                string value = code == "ko" ? source.ko : source.en;
                if (string.IsNullOrWhiteSpace(value)) throw new Exception("Missing translation: " + source.ko);
                table.AddEntry("ui." + i.ToString("D3"), value);
                count++;
            }
            EditorUtility.SetDirty(table);
        }
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(settings);
        LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null, collection);
        AssetDatabase.SaveAssets();
        Debug.Log("Localization complete: " + count + " entries, no gaps.");
    }
}
