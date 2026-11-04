using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One continuous silhouette: body and tail share edges, never transparent layers.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class EmoteBubbleGraphic : MaskableGraphic
{
    private bool tailBelow = true;
    private readonly List<Vector2> contour = new List<Vector2>(31);

    public void SetTailBelow(bool value)
    {
        if (tailBelow == value) return;
        tailBelow = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        contour.Clear();
        Rect r = rectTransform.rect;
        float radius = Mathf.Min(16f, Mathf.Min(r.width, r.height) * 0.2f);
        Arc(new Vector2(r.xMax - radius, r.yMax - radius), radius, 0f);
        Arc(new Vector2(r.xMin + radius, r.yMax - radius), radius, 90f);
        Arc(new Vector2(r.xMin + radius, r.yMin + radius), radius, 180f);
        float halfTail = Mathf.Min(12f, r.width * 0.15f);
        contour.Add(new Vector2(r.center.x - halfTail, r.yMin));
        contour.Add(new Vector2(r.center.x, r.yMin - 14f));
        contour.Add(new Vector2(r.center.x + halfTail, r.yMin));
        Arc(new Vector2(r.xMax - radius, r.yMin + radius), radius, 270f);
        if (!tailBelow)
        {
            for (int i = 0; i < contour.Count; i++)
                contour[i] = new Vector2(contour[i].x, 2f * r.center.y - contour[i].y);
            contour.Reverse();
        }
        // The center sees every boundary edge, so the triangle fan has no overlap.
        mesh.AddVert(r.center, color, Vector2.zero);
        foreach (Vector2 point in contour) mesh.AddVert(point, color, Vector2.zero);
        for (int i = 0; i < contour.Count; i++)
            mesh.AddTriangle(0, i + 1, (i + 1) % contour.Count + 1);
    }

    private void Arc(Vector2 center, float radius, float start)
    {
        for (int i = 0; i <= 6; i++)
        {
            float angle = (start + i * 15f) * Mathf.Deg2Rad;
            contour.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
