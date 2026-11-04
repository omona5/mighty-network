using UnityEngine;
using UnityEngine.UI;

// Canvas-native particles: visible on overlay canvases without a particle camera/material.
public sealed class MightyImpactEffect : MaskableGraphic
{
    private const float Lifetime = 0.48f;
    private float born;
    private float radius;

    public static void Play(RectTransform parent, Vector3 position, float cardWidth)
    {
        if (parent == null) return;
        parent.SetAsLastSibling();
        var go = new GameObject("MightyImpact", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        var effect = go.AddComponent<MightyImpactEffect>();
        effect.raycastTarget = false;
        effect.maskable = false;
        effect.born = Time.unscaledTime;
        effect.radius = cardWidth;
        effect.rectTransform.sizeDelta = Vector2.one * cardWidth * 4f;
        effect.rectTransform.position = position;
    }

    private void Update()
    {
        if (Time.unscaledTime - born >= Lifetime) { Destroy(gameObject); return; }
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        float t = Mathf.Clamp01((Time.unscaledTime - born) / Lifetime);
        float ease = 1f - Mathf.Pow(1f - t, 3f);
        Color gold = new Color(1f, 0.76f, 0.18f, (1f - t) * 0.85f);
        float r = radius * Mathf.Lerp(0.42f, 1.55f, ease);
        float thickness = radius * 0.045f * (1f - t);
        // Elliptical shockwave suggests a strike on the tabletop.
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2f / 48f;
            float b = (i + 1) * Mathf.PI * 2f / 48f;
            Quad(mesh, Ring(a, r), Ring(b, r), Ring(b, r - thickness), Ring(a, r - thickness), gold);
        }
        for (int i = 0; i < 22; i++)
        {
            float a = i * 2.399963f;
            var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float speed = 0.85f + (i % 5) * 0.16f;
            Vector2 center = direction * radius * (0.45f + ease * speed);
            center.y = center.y * 0.78f - radius * t * t * 0.25f;
            Vector2 along = direction * radius * (0.065f + (i % 3) * 0.025f) * (1f - t);
            Vector2 across = new Vector2(-direction.y, direction.x) * radius * 0.018f * (1f - t);
            Color tint = i % 3 == 0 ? new Color(1f, 1f, 0.85f, 1f - t) : gold;
            Quad(mesh, center + along, center + across, center - along, center - across, tint);
        }
        // Brief four-point spark at the contact point; no full-screen flash.
        float flash = Mathf.Clamp01(1f - t * 5f);
        if (flash > 0f)
        {
            Color white = new Color(1f, 0.97f, 0.75f, flash * 0.7f);
            float size = radius * 0.72f;
            Quad(mesh, new Vector2(-size, 0), new Vector2(0, size * 0.15f),
                new Vector2(size, 0), new Vector2(0, -size * 0.15f), white);
        }
    }

    private static Vector2 Ring(float angle, float radius) =>
        new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.65f);

    private static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }
}
