using UnityEngine;

// IMGUI buttons report activation only after a valid enabled click/submit.
public static class UiButton
{
    public static bool Layout(string text, params GUILayoutOption[] options)
    {
        bool clicked = GUILayout.Button(text, options);
        if (clicked) Sfx.UiClick();
        return clicked;
    }

    public static bool Rect(Rect rect, string text)
    {
        bool clicked = GUI.Button(rect, text);
        if (clicked) Sfx.UiClick();
        return clicked;
    }

    public static bool Rect(Rect rect, GUIContent content)
    {
        bool clicked = GUI.Button(rect, content);
        if (clicked) Sfx.UiClick();
        return clicked;
    }

    public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options)
    {
        bool next = GUILayout.Toggle(value, text, style, options);
        if (next != value) Sfx.UiClick();
        return next;
    }
}
