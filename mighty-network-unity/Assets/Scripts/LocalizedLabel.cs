using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class LocalizedLabel : MonoBehaviour
{
    private System.Func<string> value;
    private TextMeshProUGUI label;

    public static void Bind(TextMeshProUGUI label, string source)
    {
        Bind(label, () => L10n.Text(source));
    }

    public static void Bind(TextMeshProUGUI label, System.Func<string> value)
    {
        var binding = label.GetComponent<LocalizedLabel>();
        if (binding == null) binding = label.gameObject.AddComponent<LocalizedLabel>();
        binding.label = label;
        binding.value = value;
        binding.Refresh();
    }

    private void OnEnable() { GameSettings.LanguageChanged += Refresh; Refresh(); }
    private void OnDisable() { GameSettings.LanguageChanged -= Refresh; }
    private void Refresh()
    {
        if (label != null && value != null) label.text = value();
    }
}
