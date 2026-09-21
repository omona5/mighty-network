// JSON message contracts shared by the NetworkManager partial class.
// Keeping transport DTOs here makes the connection/state code easier to navigate.
public partial class NetworkManager
{
    [System.Serializable] private class TypeOnly { public string type; }
    [System.Serializable] private class DealAnimationData { public int dealId; }
    [System.Serializable] private class DealAnimationMsg { public string type; public DealAnimationData data; }

    [System.Serializable] private class PingData { public string message; }
    [System.Serializable] private class PingMsg { public string type = "ping_from_client"; public PingData data; }
    [System.Serializable] private class CreateRoomData { public string nickname; public string password; public bool debugSinglePlayerJokerCall; }
    [System.Serializable] private class CreateRoomMsg { public string type = "create_room"; public CreateRoomData data; }
    [System.Serializable] private class JoinRoomData { public string roomId; public string nickname; public string password; }
    [System.Serializable] private class JoinRoomMsg { public string type = "join_room"; public JoinRoomData data; }
    [System.Serializable] private class LeaveRoomMsg { public string type = "leave_room"; public string data = ""; }
    [System.Serializable] private class ReadyMsg { public string type = "ready"; public string data = ""; }
    [System.Serializable] private class StartGameMsg { public string type = "start_game"; public string data = ""; }
    [System.Serializable] private class ReconnectData { public string reconnectToken; }
    [System.Serializable] private class ReconnectMsg { public string type = "reconnect"; public ReconnectData data; }

    [System.Serializable] private class PlayCardData
    {
        public string cardId;
        public string declaredSuit;
        public bool activateJokerCall;
    }

    [System.Serializable] private class PlayCardMsg { public string type = "play_card"; public PlayCardData data; }
    [System.Serializable] private class BidData { public int targetScore; public string trumpSuit; public bool noTrump; }
    [System.Serializable] private class BidMsg { public string type = "bid"; public BidData data; }
    [System.Serializable] private class PassBidMsg { public string type = "pass_bid"; public string data = ""; }
    [System.Serializable] private class FriendData { public string friendCardId; public string friendClientId; }
    [System.Serializable] private class ChooseFriendMsg { public string type = "choose_friend"; public FriendData data; }
    [System.Serializable] private class ReturnLobbyMsg { public string type = "return_to_lobby"; public string data = ""; }
    [System.Serializable] private class ResetScoresMsg { public string type = "reset_scores"; public string data = ""; }
    [System.Serializable] private class ShuffleSeatsMsg { public string type = "shuffle_seats"; public string data = ""; }
    [System.Serializable] private class DiscardKittyData { public string[] cardIds; }
    [System.Serializable] private class DiscardKittyMsg { public string type = "discard_kitty"; public DiscardKittyData data; }
    [System.Serializable] private class TeamPlayerScore { public string clientId; public string nickname; public bool isBot; public int score; public int trickCount; }
    [System.Serializable] private class ScoreboardEntry { public string clientId; public string nickname; public bool isBot; public int delta; public int sessionScore; }

    [System.Serializable] private class GameFinishedData
    {
        public string winner; public string winnerLabel; public int targetScore;
        public int declarerTeamScore; public int defenderTeamScore; public int kittyScore;
        public string declarerNickname; public string friendNickname;
        public string friendType; public string friendCardId; public bool friendRevealed;
        public string trumpSuit; public bool noTrump;
        public bool isRun; public bool isBackrun; public int multiplier;
        public string[] multipliers; public int stakeBase; public int stakeTotal;
        public ScoreboardEntry[] scoreboard;
        public TeamPlayerScore[] declarerTeam; public TeamPlayerScore[] defenderTeam;
    }

    [System.Serializable] private class GameFinishedMsg { public string type; public GameFinishedData data; }
    [System.Serializable] private class WelcomeData { public string clientId; }
    [System.Serializable] private class WelcomeMsg { public string type; public WelcomeData data; }
    [System.Serializable] private class RoomAckData { public string roomId; public string reconnectToken; public string nickname; }
    [System.Serializable] private class RoomAckMsg { public string type; public RoomAckData data; }

    [System.Serializable] private class ReconnectedData
    {
        public string roomId; public string clientId; public string reconnectToken; public string nickname;
    }

    [System.Serializable] private class ReconnectedMsg { public string type; public ReconnectedData data; }
    [System.Serializable] private class PlayerInfo { public string clientId; public string nickname; public bool isReady; public bool connected; public bool isHost; public bool isBot; public bool botControlled; public double disconnectedAt; public double reconnectExpiresAt; public int handCount; public int wonCount; public int trickCount; public int score; public int sessionScore; public bool isDeclarer; public bool isMightyPlayer; }

    [System.Serializable] private class TableCardInfo
    {
        public string playerNickname;
        public CardData card;
        public string declaredSuit;
        public bool jokerCallActivated;
    }

    [System.Serializable] private class HighestBid { public string nickname; public int targetScore; public string trumpSuit; public bool noTrump; }
    [System.Serializable] private class GameState { public string roomId; public string status; public string hostClientId; public bool canStart; public double reconnectGraceMs; public string currentTurnClientId; public string currentTurnNickname; public string lastTrickWinnerNickname; public bool trickComplete; public int trickNumber; public string trumpSuit; public bool noTrump; public string mightyCardId; public string jokerCallCardId; public bool jokerPlayed; public bool mightyRevealed; public string mightyPlayerNickname; public int minBid; public int nextMinBid; public string currentBidderClientId; public string currentBidderNickname; public string[] passedClientIds; public HighestBid highestBid; public string declarerClientId; public string declarerNickname; public int targetScore; public int declarerTeamScore; public int defenderTeamScore; public int kittyScore; public int pointsNeeded; public bool friendChosen; public string friendType; public string friendCardId; public bool friendRevealed; public string friendNickname; public int kittyCount; public TableCardInfo[] tableCards; public PlayerInfo[] players; }
    [System.Serializable] private class GameStateMsg { public string type; public GameState data; }
    [System.Serializable] private class BidResultData { public string declarerNickname; public int targetScore; public string trumpSuit; public bool noTrump; }
    [System.Serializable] private class BidResultMsg { public string type; public BidResultData data; }
    [System.Serializable] private class ErrorData { public string message; }
    [System.Serializable] private class ErrorMsg { public string type; public ErrorData data; }
    [System.Serializable] private class YourHandData { public CardData[] cards; public bool canDealMiss; }
    [System.Serializable] private class YourHandMsg { public string type; public YourHandData data; }
    [System.Serializable] private class DealMissMsg { public string type = "declare_deal_miss"; public string data = ""; }
    [System.Serializable] private class DealMissEventData { public string nickname; }
    [System.Serializable] private class DealMissEventMsg { public string type; public DealMissEventData data; }
}
