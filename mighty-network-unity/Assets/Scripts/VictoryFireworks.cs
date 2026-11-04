using System;
using UnityEngine;

// Results use IMGUI, which covers overlay canvases. Render particles after that panel.
[RequireComponent(typeof(RectTransform))]
public sealed class VictoryFireworks : MonoBehaviour
{
    private RectTransform rectTransform => (RectTransform)transform;
    private ParticleSystem particles;
    private readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[360];
    private Func<bool> visible;
    private float started, nextBurst;
    private int bursts;
    private static readonly Color[] Colors = {
        new Color(1f, 0.78f, 0.22f), new Color(0.3f, 0.85f, 1f),
        new Color(1f, 0.4f, 0.65f), new Color(0.6f, 1f, 0.55f)
    };

    public static VictoryFireworks Play(Canvas canvas, Func<bool> visible)
    {
        if (canvas == null) return null;
        var go = new GameObject("VictoryFireworks", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        var effect = go.AddComponent<VictoryFireworks>();
        effect.rectTransform.anchorMin = Vector2.zero;
        effect.rectTransform.anchorMax = Vector2.one;
        effect.rectTransform.offsetMin = effect.rectTransform.offsetMax = Vector2.zero;
        effect.visible = visible;
        effect.started = effect.nextBurst = Time.unscaledTime;
        effect.particles = go.AddComponent<ParticleSystem>();
        effect.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = effect.particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 5f;
        main.startLifetime = 1.35f;
        main.startSpeed = 0f;
        main.maxParticles = 360;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.useUnscaledTime = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = effect.particles.emission; emission.enabled = false;
        var shape = effect.particles.shape; shape.enabled = false;
        var force = effect.particles.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.Local;
        force.y = -140f;
        go.GetComponent<ParticleSystemRenderer>().enabled = false;
        effect.particles.Play();
        return effect;
    }

    private void Update()
    {
        if (visible == null || !visible() || Time.unscaledTime - started > 4.5f)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        if (particles == null) return;
        if (bursts < 6 && Time.unscaledTime >= nextBurst)
        {
            Rect area = rectTransform.rect;
            float side = bursts % 2 == 0 ? -1f : 1f;
            Vector3 origin = new Vector3(area.center.x + side * area.width * 0.34f,
                area.center.y + area.height * (0.12f + (bursts % 3) * 0.09f), 0f);
            float scale = Mathf.Min(area.width / 900f, area.height / 700f);
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                float speed = (i % 3 == 0 ? 220f : 145f) * scale;
                particles.Emit(new ParticleSystem.EmitParams {
                    position = origin,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * speed,
                    startColor = Colors[bursts % Colors.Length],
                    startSize = (i % 3 == 0 ? 6f : 4f) * scale,
                    startLifetime = 1.2f + (i % 4) * 0.08f
                }, 1);
            }
            bursts++;
            nextBurst = Time.unscaledTime + 0.48f;
        }
    }

    public void DrawForeground()
    {
        if (Event.current.type != EventType.Repaint || particles == null || visible == null || !visible()) return;
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        try
        {
        GUI.matrix = Matrix4x4.identity;
        int count = particles.GetParticles(buffer);
        for (int i = 0; i < count; i++)
        {
            var p = buffer[i];
            float life = Mathf.Clamp01(p.remainingLifetime / p.startLifetime);
            Color tint = p.startColor;
            tint.a *= life * life;
            float size = p.startSize * (0.4f + life * 0.6f);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, transform.TransformPoint(p.position));
            Vector2 edge = RectTransformUtility.WorldToScreenPoint(camera,
                transform.TransformPoint(p.position + Vector3.right * size));
            float pixels = Mathf.Max(1f, Vector2.Distance(center, edge));
            center.y = Screen.height - center.y;
            GUI.color = tint;
            GUI.matrix = Matrix4x4.identity;
            GUIUtility.RotateAroundPivot(45f, center);
            GUI.DrawTexture(new Rect(center.x - pixels, center.y - pixels, pixels * 2f, pixels * 2f),
                Texture2D.whiteTexture);
        }
        }
        finally { GUI.matrix = previousMatrix; GUI.color = previousColor; }
    }
}
