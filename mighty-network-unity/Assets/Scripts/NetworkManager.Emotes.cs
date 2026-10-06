using UnityEngine;

public partial class NetworkManager
{
    private EmoteChatView emoteChat;
    private float emoteReplyDeadline;
    private bool CanEmote => inRoom && !intentionalLeave && currentState != null
        && currentState.status == "playing" && connection != null && connection.IsReady;

    [System.Serializable] private class EmoteData { public string clientId; public int index; }
    [System.Serializable] private class EmoteMessage { public string type = "send_emote"; public EmoteData data; }

    private void UpdateEmotes()
    {
        if (!CanEmote) emoteReplyDeadline = 0f;
        if (emoteReplyDeadline > 0f && Time.unscaledTime >= emoteReplyDeadline)
        {
            emoteReplyDeadline = 0f;
            Debug.LogWarning("[Emote] No player_emote reply from " + connection.Url
                + ". Check that the connected Node server includes send_emote and was restarted after updating server.js.");
        }
        if (emoteChat == null && CanEmote)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            emoteChat = EmoteChatView.Create(canvas, index => {
                if (!CanEmote) return;
                emoteReplyDeadline = Time.unscaledTime + 5f;
                Log("[emote] send index=" + index + " server=" + connection.Url);
                Send(JsonUtility.ToJson(new EmoteMessage { data = new EmoteData { index = index } }));
            }, clientId => {
                if (opponentHandsView == null || currentState == null || currentState.players == null) return null;
                foreach (var player in currentState.players)
                    if (player.clientId == clientId) return opponentHandsView.GetEmoteAnchor(player.nickname);
                return null;
            });
        }
        if (emoteChat != null) emoteChat.SetAvailable(CanEmote);
    }

    private void ReceiveEmote(string json)
    {
        if (!CanEmote) return;
        UpdateEmotes();
        var message = JsonUtility.FromJson<EmoteMessage>(json);
        if (message.data != null && emoteChat != null)
        {
            if (message.data.clientId == myClientId) emoteReplyDeadline = 0f;
            Log("[emote] received index=" + message.data.index + " player=" + message.data.clientId);
            emoteChat.ShowEmote(message.data.clientId, message.data.index, message.data.clientId == myClientId);
        }
    }
}
