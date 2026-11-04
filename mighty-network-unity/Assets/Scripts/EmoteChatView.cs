using System;
using System.Collections.Generic;
using UnityEngine;

// A separate overlay keeps bubbles above cards even when seat panels are rebuilt.
public sealed class EmoteChatView : MonoBehaviour
{
    private RectTransform root, picker, panel, trigger;
    private UnityEngine.UI.Image triggerIcon;
    private UnityEngine.UI.Button triggerButton;
    private readonly List<UnityEngine.UI.Button> choices = new List<UnityEngine.UI.Button>();
    private readonly Sprite[] sprites = new Sprite[8];
    private readonly Dictionary<string, Bubble> bubbles = new Dictionary<string, Bubble>();
    private readonly Vector3[] corners = new Vector3[4];
    private Action<int> send;
    private Func<string, RectTransform> anchor;
    private Func<RectTransform> handAnchor;
    private float nextSendAt;
    private bool available;
    private sealed class Bubble
    {
        public RectTransform rect;
        public EmoteBubbleGraphic background;
        public UnityEngine.UI.Image icon;
        public float until;
        public bool self;
    }

    public static EmoteChatView Create(Canvas canvas, Action<int> send, Func<string, RectTransform> anchor,
        Func<RectTransform> handAnchor)
    {
        var go = new GameObject("EmoteChat", typeof(RectTransform));
        go.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        var view = go.AddComponent<EmoteChatView>();
        view.root = (RectTransform)go.transform;
        Stretch(view.root);
        view.send = send;
        view.anchor = anchor;
        view.handAnchor = handAnchor;
        view.Build();
        return view;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        return rt;
    }

    private static UnityEngine.UI.Image Image(RectTransform rt, Color color, bool hit = false)
    {
        var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color; image.raycastTarget = hit;
        return image;
    }

    private static UnityEngine.UI.Button Button(RectTransform rt, Action click)
    {
        var button = rt.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = rt.GetComponent<UnityEngine.UI.Image>();
        button.onClick.AddListener(() => { Sfx.UiClick(); click(); });
        return button;
    }

    private UnityEngine.UI.Image Icon(RectTransform parent, int index, Vector2 size)
    {
        var image = Image(Rect("Emote", parent, size), Color.white);
        image.sprite = sprites[index]; image.preserveAspect = true;
        return image;
    }

    private void Build()
    {
        var texture = Resources.Load<Texture2D>("emote");
        if (texture == null) { Debug.LogError("Missing emote sprite sheet"); return; }
        // Top-left to bottom-right, 505 x 469 per cell in the source sheet.
        float w = texture.width / 4f, h = texture.height / 2f;
        for (int i = 0; i < 8; i++)
            sprites[i] = Sprite.Create(texture, new UnityEngine.Rect(i % 4 * w, (1 - i / 4) * h, w, h), Vector2.one * 0.5f);
        trigger = Rect("EmoteButton", root, new Vector2(100, 100));
        Image(trigger, MightyTheme.Panel, true);
        triggerIcon = Icon(trigger, 0, new Vector2(92, 86));
        triggerButton = Button(trigger, () => { picker.gameObject.SetActive(true); picker.SetAsLastSibling(); });

        picker = Rect("EmotePicker", root, Vector2.zero);
        Stretch(picker);
        Image(picker, new Color(0, 0, 0, 0.68f), true);
        Button(picker, () => picker.gameObject.SetActive(false));
        panel = Rect("Choices", picker, new Vector2(656, 344));
        Image(panel, MightyTheme.Panel, true);
        for (int i = 0; i < 8; i++)
        {
            int index = i;
            var cell = Rect("Choice" + i, panel, new Vector2(148, 148));
            cell.anchoredPosition = new Vector2((i % 4 - 1.5f) * 156, (0.5f - i / 4) * 156);
            Image(cell, MightyTheme.PanelSoft, true);
            Icon(cell, i, new Vector2(138, 128));
            choices.Add(Button(cell, () => Select(index)));
        }
        picker.gameObject.SetActive(false);
    }

    private void Select(int index)
    {
        if (!available || Time.unscaledTime < nextSendAt) return;
        nextSendAt = Time.unscaledTime + 1f;
        picker.gameObject.SetActive(false);
        send(index);
    }

    public void SetAvailable(bool value)
    {
        available = value;
        if (!value)
        {
            if (picker != null) picker.gameObject.SetActive(false);
            foreach (var bubble in bubbles.Values) if (bubble.rect != null) Destroy(bubble.rect.gameObject);
            bubbles.Clear();
        }
        gameObject.SetActive(value);
    }

    public void ShowEmote(string clientId, int index, bool self)
    {
        if (!available || string.IsNullOrEmpty(clientId) || index < 0 || index >= 8 || sprites[index] == null) return;
        // Local selection already plays a click; only peer arrivals add a cue.
        if (!self) Sfx.UiClick();
        if (!bubbles.TryGetValue(clientId, out var bubble))
        {
            bubble = new Bubble();
            bubble.rect = Rect("EmoteBubble", root, new Vector2(154, 154));
            bubble.background = bubble.rect.gameObject.AddComponent<EmoteBubbleGraphic>();
            bubble.background.color = new Color(1f, 0.98f, 0.91f, 0.15f);
            bubble.background.raycastTarget = false;
            bubble.icon = Icon(bubble.rect, index, new Vector2(142, 132));
            bubbles.Add(clientId, bubble);
        }
        bubble.icon.sprite = sprites[index];
        bubble.self = self;
        bubble.until = Time.unscaledTime + 3f;
        if (self) triggerIcon.sprite = sprites[index];
        // Keep the picker above newly created bubbles.
        picker.SetAsLastSibling();
    }

    private void LateUpdate()
    {
        if (trigger == null) return;
        root.SetAsLastSibling();
        bool portrait = ResponsiveCanvas.IsPortrait;
        var safe = ResponsiveCanvas.SafeArea;
        float width = Mathf.Max(1, ResponsiveCanvas.ViewWidth), height = Mathf.Max(1, ResponsiveCanvas.ViewHeight);
        trigger.anchorMin = trigger.anchorMax = new Vector2(safe.xMin / width, safe.yMin / height);
        trigger.pivot = Vector2.zero;
        trigger.anchoredPosition = new Vector2(16, 16);
        trigger.localScale = Vector3.one * (portrait ? 1.2f : 1f);
        if (portrait)
        {
            var hand = handAnchor?.Invoke();
            if (hand != null)
            {
                hand.GetWorldCorners(corners);
                float handTop = root.InverseTransformPoint((corners[1] + corners[2]) * 0.5f).y;
                float safeBottom = root.rect.yMin + root.rect.height * safe.yMin / height;
                float safeTop = root.rect.yMin + root.rect.height * safe.yMax / height;
                float buttonHeight = trigger.rect.height * trigger.localScale.y;
                trigger.anchoredPosition = new Vector2(16,
                    Mathf.Clamp(handTop + 16, safeBottom + 16,
                        Mathf.Max(safeBottom + 16, safeTop - buttonHeight - 16)) - safeBottom);
            }
        }
        panel.localScale = Vector3.one * Mathf.Min(portrait ? 1.4f : 1f, (root.rect.width - 40) / 656f);
        bool ready = available && Time.unscaledTime >= nextSendAt;
        triggerButton.interactable = ready;
        foreach (var button in choices) button.interactable = ready;
        foreach (var pair in bubbles)
        {
            Bubble bubble = pair.Value;
            var target = anchor(pair.Key);
            bool visible = Time.unscaledTime < bubble.until && target != null;
            bubble.rect.gameObject.SetActive(visible);
            if (!visible) continue;
            bool above = portrait || bubble.self;
            target.GetWorldCorners(corners);
            Vector3 edge = above ? (corners[1] + corners[2]) * 0.5f : (corners[0] + corners[3]) * 0.5f;
            Vector3 local = root.InverseTransformPoint(edge);
            float half = bubble.rect.sizeDelta.y * 0.5f;
            local.y += (above ? 1 : -1) * (half + 16);
            local.x = Mathf.Clamp(local.x, root.rect.xMin + half + 8, root.rect.xMax - half - 8);
            bubble.rect.anchoredPosition = new Vector2(local.x, local.y);
            bubble.background.SetTailBelow(above);
        }
        if (picker.gameObject.activeSelf)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) picker.gameObject.SetActive(false);
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) picker.gameObject.SetActive(false);
#endif
        }
    }

    private void OnDestroy()
    {
        foreach (var sprite in sprites) if (sprite != null) Destroy(sprite);
    }
}
