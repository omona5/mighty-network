using UnityEngine;

// Tint the card's existing sprite-shaped shadows instead of drawing a box.
public sealed class WinningCardGlow : MonoBehaviour
{
    private UnityEngine.UI.Shadow[] shadows;
    private Color[] originalColors;
    private static readonly Color Amber = new Color(1f, 0.62f, 0.04f, 0.5f);
    private static readonly Color Gold = new Color(1f, 0.94f, 0.34f, 0.98f);

    public static void Attach(Transform card)
    {
        var glow = card.GetComponent<WinningCardGlow>();
        if (glow == null) glow = card.gameObject.AddComponent<WinningCardGlow>();
        glow.enabled = true;
    }

    private void LateUpdate()
    {
        if (shadows == null)
        {
            var card = GetComponent<CardView>();
            if (card == null || card.background == null) return;
            card.RefreshDropShadow();
            shadows = card.background.GetComponents<UnityEngine.UI.Shadow>();
            originalColors = new Color[shadows.Length];
            for (int i = 0; i < shadows.Length; i++)
                originalColors[i] = shadows[i].effectColor;
        }

        float pulse = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 4.5f);
        Color tint = Color.Lerp(Amber, Gold, pulse);
        foreach (var shadow in shadows)
            if (shadow != null) shadow.effectColor = tint;
    }

    private void OnDisable()
    {
        if (shadows == null) return;
        for (int i = 0; i < shadows.Length; i++)
            if (shadows[i] != null) shadows[i].effectColor = originalColors[i];
        shadows = null;
        originalColors = null;
    }
}
