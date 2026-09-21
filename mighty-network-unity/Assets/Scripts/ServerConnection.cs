using System;
using System.Text;
using NativeWebSocket;
using UnityEngine;

// One transport survives title -> game -> title. Ready means the game server's
// welcome packet arrived, not just that a TCP/WebSocket handshake succeeded.
public sealed class ServerConnection : MonoBehaviour
{
    public const float RetryInterval = 5f;
    public static ServerConnection Instance { get; private set; }
    public event Action Changed;
    public event Action Ready;
    public event Action Disconnected;
    public event Action<string> Message;
    public bool IsReady { get; private set; }
    public bool IsConnecting { get; private set; }
    public string WelcomeJson { get; private set; }
    public string Url { get; private set; }
    public int AttemptCount { get; private set; }
    private WebSocket socket;
    private float nextAttemptAt;
    private float lastAttemptAt = -10f;
    private float nextPingAt;
    private float lastMessageAt;
    private bool stopping;
    private bool suspended;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public static ServerConnection Ensure(string defaultUrl = ServerUrlResolver.DefaultUrl)
    {
        Application.runInBackground = true;
        if (Instance != null) return Instance;
        var go = new GameObject("ServerConnection");
        Instance = go.AddComponent<ServerConnection>();
        DontDestroyOnLoad(go);
        Instance.Url = ServerUrlResolver.ResolveDefault(defaultUrl);
        Instance.RetryNow();
        return Instance;
    }

    public void RetryNow()
    {
        // A manual retry can retire a stuck handshake; a short debounce prevents
        // rapid clicks from spawning repeated attempts in the same frame.
        if (stopping || suspended || IsReady || Time.realtimeSinceStartup - lastAttemptAt < 0.5f) return;
        BeginAttempt();
    }

    private async void BeginAttempt()
    {
        RetireSocket();
        IsConnecting = true;
        lastAttemptAt = Time.realtimeSinceStartup;
        nextAttemptAt = lastAttemptAt + RetryInterval;
        AttemptCount++;
        Changed?.Invoke();
        WebSocket candidate = null;
        try
        {
            candidate = new WebSocket(Url);
            socket = candidate;
            candidate.OnMessage += bytes => Receive(candidate, bytes);
            candidate.OnError += _ => Fail(candidate);
            candidate.OnClose += _ => Fail(candidate);
            await candidate.Connect();
        }
        catch (Exception)
        {
            if (candidate == null) { IsConnecting = false; Changed?.Invoke(); }
            else Fail(candidate);
        }
    }

    private void Receive(WebSocket candidate, byte[] bytes)
    {
        if (stopping || socket != candidate) return;
        string json = Encoding.UTF8.GetString(bytes);
        try
        {
            var envelope = JsonUtility.FromJson<Envelope>(json);
            if (envelope == null) return;
            lastMessageAt = Time.realtimeSinceStartup;
            if (envelope.type == "welcome")
            {
                if (string.IsNullOrEmpty(envelope.data?.clientId)) return;
                WelcomeJson = json;
                IsReady = true;
                IsConnecting = false;
                nextPingAt = lastMessageAt + RetryInterval;
                Changed?.Invoke();
                Ready?.Invoke();
            }
            else if (IsReady)
            {
                // Reconnect restores the seat's original clientId on this socket.
                // New scenes must replay that identity, not the handshake ID.
                if (envelope.type == "reconnected" && !string.IsNullOrEmpty(envelope.data?.clientId))
                    WelcomeJson = JsonUtility.ToJson(new Envelope { type = "welcome", data = envelope.data });
                Message?.Invoke(json);
            }
        }
        catch (ArgumentException) { Fail(candidate); }
    }

    private void Fail(WebSocket candidate)
    {
        if (stopping || candidate != socket) return;
        bool wasReady = IsReady;
        RetireSocket();
        if (wasReady) nextAttemptAt = Time.realtimeSinceStartup + RetryInterval;
        Changed?.Invoke();
        if (wasReady) Disconnected?.Invoke();
    }

    private void Update()
    {
        if (stopping || suspended) return;
#if !UNITY_WEBGL || UNITY_EDITOR
        socket?.DispatchMessageQueue();
#endif
        float now = Time.realtimeSinceStartup;
        if (!IsReady && now >= nextAttemptAt) BeginAttempt();
        else if (IsReady && now - lastMessageAt >= RetryInterval * 3f) Fail(socket);
        else if (IsReady && now >= nextPingAt)
        {
            nextPingAt = now + RetryInterval;
            Send("{\"type\":\"ping_from_client\",\"data\":{\"message\":\"connection-check\"}}");
        }
    }

    public async void Send(string json)
    {
        var current = socket;
        if (!IsReady || current == null || current.State != WebSocketState.Open) return;
        try { await current.SendText(json); }
        catch (Exception) { Fail(current); }
    }

    private void RetireSocket()
    {
        var previous = socket;
        socket = null;
        IsReady = false;
        IsConnecting = false;
        WelcomeJson = null;
        if (previous != null) Close(previous);
    }

    private static async void Close(WebSocket previous)
    {
        try
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            previous.CancelConnection();
#endif
            await previous.Close();
        }
        catch (Exception) { /* A failed or cancelled socket may already be closed. */ }
    }

    private void OnApplicationPause(bool pause)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Browser visibility is not a disconnect. Keep the socket alive and let
        // the normal heartbeat/reconnect path handle an actual connection loss.
        suspended = false;
        if (!pause) nextAttemptAt = Time.realtimeSinceStartup;
#else
        suspended = pause;
        if (pause && socket != null) Fail(socket);
        if (!pause) nextAttemptAt = Time.realtimeSinceStartup;
#endif
    }

    private void OnApplicationQuit() { stopping = true; RetireSocket(); }
    private void OnDestroy()
    {
        stopping = true;
        RetireSocket();
        if (Instance == this) Instance = null;
    }

    [Serializable] private sealed class Envelope { public string type; public Welcome data; }
    [Serializable] private sealed class Welcome { public string clientId; }
}
