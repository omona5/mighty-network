using System;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Let the platform finish IME composition before enforcing the nickname limit.
public sealed class NicknameInput : MonoBehaviour
{
    private static NicknameInput active;
    public static bool IsOpen => active != null;
    private Action<string> accepted;
    private TMP_InputField input;
    private GameObject overlay;
    private Keyboard keyboard;
    private string composition = "";
    private UnityEngine.EventSystems.BaseInputModule inputModule;
    private UnityEngine.EventSystems.BaseInput previousInput;
    private NicknameImeInput imeInput;
#if UNITY_WEBGL && !UNITY_EDITOR
    private bool previousCapture;
    [DllImport("__Internal")] private static extern void MightyNicknameOpen(string receiver, string value, string title, string ok, string cancel);
    [DllImport("__Internal")] private static extern void MightyNicknameClose();
#endif

    public static void Open(string value, Action<string> onAccepted)
    {
        if (IsOpen) return;
        active = new GameObject("NicknameInput").AddComponent<NicknameInput>();
        active.accepted = onAccepted;
        active.Show(value);
    }

    private void Show(string value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        previousCapture = WebGLInput.captureAllKeyboardInput;
        WebGLInput.captureAllKeyboardInput = false;
        MightyNicknameOpen(gameObject.name, value, L10n.Text("닉네임:"), L10n.Text("확인"), L10n.Text("취소"));
#else
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) { Cancel(); return; }
        overlay = new GameObject("NicknameOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        overlay.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        var full = (RectTransform)overlay.transform;
        full.anchorMin = Vector2.zero;
        full.anchorMax = Vector2.one;
        full.offsetMin = full.offsetMax = Vector2.zero;
        overlay.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0.9f);
        float width = Mathf.Min(560f, full.rect.width - 40f);
        var title = UiTmp.Create(full, "Title", 28, TextAnchor.MiddleCenter, Color.white);
        Place(title.rectTransform, width, 48f, 90f);
        title.text = L10n.Text("닉네임:");
        var field = new GameObject("Field", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        field.transform.SetParent(full, false);
        Place((RectTransform)field.transform, width, 60f, 20f);
        field.GetComponent<UnityEngine.UI.Image>().color = new Color(0.16f, 0.18f, 0.22f);
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
        viewport.transform.SetParent(field.transform, false);
        var vr = (RectTransform)viewport.transform;
        vr.anchorMin = Vector2.zero; vr.anchorMax = Vector2.one;
        vr.offsetMin = new Vector2(12, 4); vr.offsetMax = new Vector2(-12, -4);
        var text = UiTmp.Create(vr, "Text", 28, TextAnchor.MiddleLeft, Color.white, false, false);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        text.richText = false;
        input = field.AddComponent<TMP_InputField>();
        input.textViewport = vr;
        input.textComponent = text;
        input.targetGraphic = field.GetComponent<UnityEngine.UI.Image>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterValidation = TMP_InputField.CharacterValidation.None;
        input.characterLimit = 0;
        input.richText = false;
        input.text = value;
        input.onSubmit.AddListener(_ => { if (composition.Length == 0) Accept(input.text); });
        Button(full, L10n.Text("확인"), -width / 4f, width / 2f - 8f, () => Accept(input.text));
        Button(full, L10n.Text("취소"), width / 4f, width / 2f - 8f, Cancel);
        keyboard = Keyboard.current;
        inputModule = UnityEngine.EventSystems.EventSystem.current?.currentInputModule;
        if (inputModule != null)
        {
            previousInput = inputModule.inputOverride;
            imeInput = gameObject.AddComponent<NicknameImeInput>();
            inputModule.inputOverride = imeInput;
        }
        if (keyboard != null)
        {
            keyboard.onIMECompositionChange += OnComposition;
            keyboard.SetIMEEnabled(true);
        }
        input.ActivateInputField();
#endif
    }

    private void OnComposition(IMECompositionString value)
    {
        composition = value.ToString();
        if (imeInput != null) imeInput.Composition = composition;
    }
    private static void Place(RectTransform rect, float width, float height, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0, y);
    }
    private void Button(Transform parent, string label, float x, float width, Action click)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, width, 54f, -60f);
        ((RectTransform)go.transform).anchoredPosition = new Vector2(x, -60f);
        go.GetComponent<UnityEngine.UI.Image>().color = new Color(0.25f, 0.29f, 0.34f);
        var text = UiTmp.Create(go.transform, "Label", 24, TextAnchor.MiddleCenter, Color.white);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
        go.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(() => { Sfx.UiClick(); click(); });
    }
    public void Accept(string value)
    {
        value = (value ?? "").Normalize(NormalizationForm.FormC).Trim();
        if (value.Length > 12) value = value.Substring(0, 12);
        accepted?.Invoke(value);
        Cancel();
    }
    public void Cancel() => Destroy(gameObject);
    private void OnDestroy()
    {
        if (inputModule != null && inputModule.inputOverride == imeInput)
            inputModule.inputOverride = previousInput;
        if (keyboard != null)
        {
            keyboard.onIMECompositionChange -= OnComposition;
            keyboard.SetIMEEnabled(false);
        }
        if (overlay != null) Destroy(overlay);
#if UNITY_WEBGL && !UNITY_EDITOR
        MightyNicknameClose();
        WebGLInput.captureAllKeyboardInput = previousCapture;
#endif
        if (active == this) active = null;
    }
}

// TMP queries BaseInput for composition; the project uses the new Input System.
public sealed class NicknameImeInput : UnityEngine.EventSystems.BaseInput
{
    public string Composition = "";
    private Vector2 cursor;
    private IMECompositionMode mode;
    public override string compositionString => Composition;
    public override bool touchSupported => false;
    public override IMECompositionMode imeCompositionMode
    {
        get => mode;
        set { mode = value; Keyboard.current?.SetIMEEnabled(value != IMECompositionMode.Off); }
    }
    public override Vector2 compositionCursorPos
    {
        get => cursor;
        set { cursor = value; Keyboard.current?.SetIMECursorPosition(value); }
    }
}
