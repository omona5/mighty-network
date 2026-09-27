using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;

// Browser paths are launch hints only. The server always validates admission.
public static class RoomNavigation
{
    public static string InviteCode { get; private set; } = "";
    public static string Mode { get; private set; } = "";
    public static bool EntryPending;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        InviteCode = "";
        Mode = "";
        if (Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var uri))
        {
            string path = uri.AbsolutePath.TrimEnd('/');
            var match = Regex.Match(path, @"^/room/([A-Za-z0-9]{4})$");
            if (match.Success) { InviteCode = match.Groups[1].Value.ToUpperInvariant(); Mode = "multiplayer"; }
            else if (path == "/singleplayer" || path == "/tutorial" || path == "/multiplayer")
                Mode = path.Substring(1);
        }
        EntryPending = Mode.Length > 0;
    }

    public static void SetMode(string mode)
    {
        Mode = mode;
        InviteCode = "";
        SetPath(mode.Length == 0 ? "/webgl/" : "/" + mode);
    }

    public static void SetRoom(string code)
    {
        Mode = "multiplayer";
        SetPath("/room/" + Uri.EscapeDataString(code));
    }

    private static void SetPath(string path)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        MightySetPath(path);
#endif
    }

    public static void CopyInvite(string code, string receiver)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        MightyCopyInvite(code, receiver);
#else
        GUIUtility.systemCopyBuffer = "http://localhost:3000/room/" + code;
        var target = GameObject.Find(receiver);
        if (target != null) target.SendMessage("OnInviteCopied", "success");
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void MightySetPath(string path);
    [DllImport("__Internal")] private static extern void MightyCopyInvite(string code, string receiver);
#endif
}
