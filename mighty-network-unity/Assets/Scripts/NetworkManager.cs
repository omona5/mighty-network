using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket; // 무료 패키지: 에디터/WebGL 모두에서 WebSocket 사용 가능

// ============================================================================
// NetworkManager (03단계: 방 생성/입장)
//  - 서버(ws://localhost:3000)에 순수 WebSocket으로 접속
//  - 닉네임 입력 후 방 만들기 / 방 코드로 입장
//  - 방 안 플레이어 목록을 실시간 표시
//  - 서버가 준 reconnectToken을 저장(PlayerPrefs) - 재접속(11단계) 대비
//
//  사용법: 빈 GameObject에 이 스크립트를 붙이고 Play. (UI는 OnGUI로 자동 표시)
// ============================================================================
public class NetworkManager : MonoBehaviour
{
    [Header("서버 주소 (개발용: localhost)")]
    public string serverUrl = "ws://localhost:3000";

    [Header("손패 표시 (Inspector에서 연결)")]
    public HandView handView; // 05단계: your_hand를 받아 카드를 그림

    [Header("테이블(낸 카드) 표시 (Inspector에서 연결)")]
    public HandView tableView; // 06단계: 낸 카드들을 화면 중앙에 그림

    private WebSocket websocket;
    private string status = "대기 중...";
    private readonly List<string> logLines = new List<string>();
    private Vector2 scroll;

    // 로비 입력값
    private string nickname = "";
    private string roomIdInput = "";
    private string passwordInput = "";

    // 방 상태
    private bool inRoom = false;
    private string myRoomId = "";
    private GameState currentState;
    private string myClientId = "";   // 서버가 알려준 내 식별자 (방장/나 구분용)
    private bool gameStarted = false;
    private GameFinishedData lastResult = null; // 10단계: game_finished 결과
    private CardData[] myHandCards = null;
    private readonly System.Collections.Generic.List<string> discardSelected =
        new System.Collections.Generic.List<string>();

    // 입찰 UI 입력값
    private string bidScoreInput = "13";
    private readonly string[] trumpOptions = { "SPADE", "HEART", "DIAMOND", "CLUB", "NT" };
    private readonly string[] trumpLabels = { "♠", "♥", "♦", "♣", "노기루" };
    private int trumpIndex = 0;
    private string friendCardInput = "";
    private bool myCanDealMiss = false; // your_hand로 수신한 딜미스 가능 여부

    // ---------- 서버와 주고받는 메시지 형식 (JSON) ----------
    // 공통: { "type": ..., "data": {...} }

    [System.Serializable] private class TypeOnly { public string type; }

    // 보내는 메시지들
    [System.Serializable] private class PingData { public string message; }
    [System.Serializable] private class PingMsg { public string type = "ping_from_client"; public PingData data; }

    [System.Serializable] private class CreateRoomData { public string nickname; public string password; }
    [System.Serializable] private class CreateRoomMsg { public string type = "create_room"; public CreateRoomData data; }

    [System.Serializable] private class JoinRoomData { public string roomId; public string nickname; public string password; }
    [System.Serializable] private class JoinRoomMsg { public string type = "join_room"; public JoinRoomData data; }

    [System.Serializable] private class LeaveRoomMsg { public string type = "leave_room"; public string data = ""; }
    [System.Serializable] private class ReadyMsg { public string type = "ready"; public string data = ""; }
    [System.Serializable] private class StartGameMsg { public string type = "start_game"; public string data = ""; }

    [System.Serializable] private class PlayCardData { public string cardId; }
    [System.Serializable] private class PlayCardMsg { public string type = "play_card"; public PlayCardData data; }

    [System.Serializable] private class BidData { public int targetScore; public string trumpSuit; public bool noTrump; }
    [System.Serializable] private class BidMsg { public string type = "bid"; public BidData data; }
    [System.Serializable] private class PassBidMsg { public string type = "pass_bid"; public string data = ""; }
    [System.Serializable] private class FriendData { public string friendCardId; public string friendClientId; }
    [System.Serializable] private class ChooseFriendMsg { public string type = "choose_friend"; public FriendData data; }
    [System.Serializable] private class ReturnLobbyMsg { public string type = "return_to_lobby"; public string data = ""; }
    [System.Serializable] private class DiscardKittyData { public string[] cardIds; }
    [System.Serializable] private class DiscardKittyMsg { public string type = "discard_kitty"; public DiscardKittyData data; }

    [System.Serializable] private class TeamPlayerScore { public string clientId; public string nickname; public bool isBot; public int score; public int trickCount; }
    [System.Serializable] private class GameFinishedData {
        public string winner; public string winnerLabel; public int targetScore;
        public int declarerTeamScore; public int defenderTeamScore; public int kittyScore;
        public string declarerNickname; public string friendNickname;
        public string friendType; public string friendCardId; public bool friendRevealed;
        public string trumpSuit; public bool noTrump;
        public TeamPlayerScore[] declarerTeam; public TeamPlayerScore[] defenderTeam;
    }
    [System.Serializable] private class GameFinishedMsg { public string type; public GameFinishedData data; }

    // 받는 메시지들
    [System.Serializable] private class WelcomeData { public string clientId; }
    [System.Serializable] private class WelcomeMsg { public string type; public WelcomeData data; }

    [System.Serializable] private class RoomAckData { public string roomId; public string reconnectToken; }
    [System.Serializable] private class RoomAckMsg { public string type; public RoomAckData data; }

    [System.Serializable] private class PlayerInfo { public string clientId; public string nickname; public bool isReady; public bool connected; public bool isHost; public bool isBot; public int handCount; public int wonCount; public int trickCount; }
    [System.Serializable] private class TableCardInfo { public string playerNickname; public CardData card; }
    [System.Serializable] private class HighestBid { public string nickname; public int targetScore; public string trumpSuit; public bool noTrump; }
    [System.Serializable] private class GameState { public string roomId; public string status; public string hostClientId; public bool canStart; public string currentTurnClientId; public string currentTurnNickname; public string lastTrickWinnerNickname; public int trickNumber; public string trumpSuit; public bool noTrump; public string mightyCardId; public string jokerCallCardId; public int minBid; public string currentBidderClientId; public string currentBidderNickname; public HighestBid highestBid; public string declarerClientId; public string declarerNickname; public int targetScore; public int declarerTeamScore; public int defenderTeamScore; public int kittyScore; public int pointsNeeded; public bool friendChosen; public string friendType; public string friendCardId; public bool friendRevealed; public string friendNickname; public TableCardInfo[] tableCards; public PlayerInfo[] players; }
    [System.Serializable] private class GameStateMsg { public string type; public GameState data; }

    [System.Serializable] private class ErrorData { public string message; }
    [System.Serializable] private class ErrorMsg { public string type; public ErrorData data; }

    [System.Serializable] private class YourHandData { public CardData[] cards; public bool canDealMiss; }
    [System.Serializable] private class YourHandMsg { public string type; public YourHandData data; }
    [System.Serializable] private class DealMissMsg { public string type = "declare_deal_miss"; public string data = ""; }

    private async void Start()
    {
        // 손패 카드를 클릭하면 그 카드를 서버에 낸다.
        if (handView != null) handView.onCardClicked = OnHandCardClicked;

        Log("서버에 접속 시도: " + serverUrl);
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => { status = "접속됨"; Log("[open] 서버에 접속했습니다."); };
        websocket.OnError += (e) => { status = "오류"; Log("[error] " + e); };
        websocket.OnClose += (e) => { status = "끊김"; inRoom = false; Log("[close] 연결이 끊겼습니다."); };
        websocket.OnMessage += (bytes) => HandleMessage(Encoding.UTF8.GetString(bytes));

        await websocket.Connect();
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private void HandleMessage(string json)
    {
        TypeOnly head = JsonUtility.FromJson<TypeOnly>(json);
        switch (head.type)
        {
            case "welcome":
            {
                WelcomeMsg m = JsonUtility.FromJson<WelcomeMsg>(json);
                myClientId = m.data.clientId;
                Log("[welcome] 내 id: " + myClientId);
                break;
            }

            case "pong_from_server":
                Log("[pong] " + json);
                break;

            case "game_started":
                gameStarted = true;
                Log("[game_started] 게임이 시작되었습니다!");
                break;

            case "your_hand":
            {
                YourHandMsg m = JsonUtility.FromJson<YourHandMsg>(json);
                int n = (m.data != null && m.data.cards != null) ? m.data.cards.Length : 0;
                Log("[your_hand] 손패 " + n + "장 받음");
                if (handView != null) handView.ShowHand(m.data.cards);
                myHandCards = (m.data != null) ? HandView.SortCards(m.data.cards) : null;
                discardSelected.Clear();
                myCanDealMiss = (m.data != null && m.data.canDealMiss);
                break;
            }

            case "room_created":
            case "room_joined":
            {
                RoomAckMsg m = JsonUtility.FromJson<RoomAckMsg>(json);
                myRoomId = m.data.roomId;
                inRoom = true;
                gameStarted = false;
                // 재접속 토큰 저장 (11단계에서 사용)
                PlayerPrefs.SetString("reconnectToken", m.data.reconnectToken);
                PlayerPrefs.SetString("roomId", m.data.roomId);
                PlayerPrefs.Save();
                Log("[" + head.type + "] 방 코드: " + m.data.roomId + " (토큰 저장됨)");
                break;
            }

            case "game_state":
            {
                GameStateMsg m = JsonUtility.FromJson<GameStateMsg>(json);
                currentState = m.data;
                UpdateTable(m.data);
                if (currentState != null && currentState.status == "waiting")
                {
                    gameStarted = false;
                    lastResult = null;
                    if (handView != null) handView.Clear();
                    if (tableView != null) tableView.Clear();
                }
                break;
            }

            case "game_finished":
            {
                GameFinishedMsg m = JsonUtility.FromJson<GameFinishedMsg>(json);
                lastResult = m.data;
                Log("[game_finished] 승: " + (lastResult != null ? lastResult.winnerLabel : "?")
                    + "  주공팀 " + (lastResult != null ? lastResult.declarerTeamScore : 0)
                    + " / 목표 " + (lastResult != null ? lastResult.targetScore : 0));
                break;
            }

            case "error_message":
            {
                ErrorMsg m = JsonUtility.FromJson<ErrorMsg>(json);
                Log("[error_message] " + m.data.message);
                break;
            }

            default:
                Log("[recv] " + json);
                break;
        }
    }

    // ---------- 서버로 보내기 ----------
    private async void Send(string json)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Log("아직 접속되지 않았습니다.");
            return;
        }
        await websocket.SendText(json);
    }

    private void SendPing()
    {
        Send(JsonUtility.ToJson(new PingMsg { data = new PingData { message = "hello from Unity" } }));
        Log("[ping] 전송");
    }

    private void CreateRoom()
    {
        if (string.IsNullOrWhiteSpace(nickname)) { Log("닉네임을 입력하세요."); return; }
        Send(JsonUtility.ToJson(new CreateRoomMsg { data = new CreateRoomData { nickname = nickname, password = passwordInput } }));
        Log("[create_room] 전송: " + nickname);
    }

    private void JoinRoom()
    {
        if (string.IsNullOrWhiteSpace(nickname)) { Log("닉네임을 입력하세요."); return; }
        if (string.IsNullOrWhiteSpace(roomIdInput)) { Log("방 코드를 입력하세요."); return; }
        Send(JsonUtility.ToJson(new JoinRoomMsg { data = new JoinRoomData { roomId = roomIdInput.ToUpper(), nickname = nickname, password = passwordInput } }));
        Log("[join_room] 전송: " + roomIdInput.ToUpper());
    }

    private void LeaveRoom()
    {
        Send(JsonUtility.ToJson(new LeaveRoomMsg()));
        inRoom = false;
        gameStarted = false;
        currentState = null;
        if (handView != null) handView.Clear();
        if (tableView != null) tableView.Clear();
        Log("[leave_room] 전송");
    }

    private void ToggleReady()
    {
        Send(JsonUtility.ToJson(new ReadyMsg()));
        Log("[ready] 전송");
    }

    private void StartGame()
    {
        Send(JsonUtility.ToJson(new StartGameMsg()));
        Log("[start_game] 전송");
    }

    // 손패 카드 클릭 시 호출됨
    private void OnHandCardClicked(CardData card)
    {
        if (card == null) return;
        // 바닥패 교환: 카드 선택 토글
        if (currentState != null && currentState.status == "exchanging_kitty"
            && currentState.declarerClientId == myClientId)
        {
            if (discardSelected.Contains(card.id)) discardSelected.Remove(card.id);
            else if (discardSelected.Count < 3) discardSelected.Add(card.id);
            Log("[kitty] 선택 " + discardSelected.Count + "/3: " + string.Join(",", discardSelected));
            return;
        }
        if (currentState != null && currentState.status != "playing")
        {
            Log("지금은 카드를 낼 수 없습니다. (" + currentState.status + ")");
            return;
        }
        // 내 차례가 아니면 서버가 거부하지만, 미리 안내만 한다.
        if (currentState != null && currentState.currentTurnClientId != myClientId)
        {
            Log("아직 내 차례가 아닙니다. (현재: " + currentState.currentTurnNickname + ")");
            return;
        }
        PlayCard(card.id);
    }

    private void PlayCard(string cardId)
    {
        Send(JsonUtility.ToJson(new PlayCardMsg { data = new PlayCardData { cardId = cardId } }));
        Log("[play_card] 전송: " + cardId);
    }

    private void SendBid()
    {
        int score;
        if (!int.TryParse(bidScoreInput, out score)) { Log("공약 숫자를 확인하세요."); return; }
        bool noTrump = trumpOptions[trumpIndex] == "NT";
        Send(JsonUtility.ToJson(new BidMsg { data = new BidData {
            targetScore = score,
            trumpSuit = noTrump ? null : trumpOptions[trumpIndex],
            noTrump = noTrump,
        } }));
        Log("[bid] 전송: " + score + " " + trumpLabels[trumpIndex]);
    }

    private void PassBid()
    {
        Send(JsonUtility.ToJson(new PassBidMsg()));
        Log("[pass_bid] 전송");
    }

    private void DeclareDealMiss()
    {
        Send(JsonUtility.ToJson(new DealMissMsg()));
        Log("[declare_deal_miss] 전송");
    }

    private void ChooseFriend(string friendCardId)
    {
        if (string.IsNullOrWhiteSpace(friendCardId))
        {
            Log("프렌드 카드 id를 입력하세요. (노프렌드는 '노프렌드' 버튼)");
            return;
        }
        Send(JsonUtility.ToJson(new ChooseFriendMsg { data = new FriendData { friendCardId = friendCardId } }));
        Log("[choose_friend] 카드: " + friendCardId);
    }

    private void ChooseFriendPlayer(string friendClientId)
    {
        if (string.IsNullOrEmpty(friendClientId))
        {
            Log("프렌드 플레이어를 선택하세요.");
            return;
        }
        Send(JsonUtility.ToJson(new ChooseFriendMsg { data = new FriendData { friendClientId = friendClientId } }));
        Log("[choose_friend] 플레이어: " + friendClientId);
    }

    // 테이블(낸 카드)을 화면 중앙에 갱신한다.
    private void UpdateTable(GameState state)
    {
        if (tableView == null) return;
        if (state == null || state.tableCards == null)
        {
            tableView.ShowHand(new CardData[0]);
            return;
        }
        CardData[] cards = new CardData[state.tableCards.Length];
        for (int i = 0; i < state.tableCards.Length; i++) cards[i] = state.tableCards[i].card;
        tableView.ShowHand(cards);
    }

    private bool IAmHost()
    {
        return currentState != null && currentState.hostClientId == myClientId;
    }

    // 무늬 코드를 기호로 (SPADE -> ♠)
    private static string SuitKor(string suit)
    {
        switch (suit)
        {
            case "SPADE": return "♠";
            case "HEART": return "♥";
            case "DIAMOND": return "♦";
            case "CLUB": return "♣";
            default: return "노기루";
        }
    }

    // 카드 id를 짧게 (S_A -> ♠A, JOKER -> 조커)
    private static string CardKor(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return "-";
        if (cardId == "JOKER") return "조커";
        int us = cardId.IndexOf('_');
        if (us < 0) return cardId;
        string suit = cardId.Substring(0, us);
        string rank = cardId.Substring(us + 1);
        string sym = suit == "S" ? "♠" : suit == "H" ? "♥" : suit == "D" ? "♦" : suit == "C" ? "♣" : suit;
        return sym + rank;
    }

    // 프렌드 선언 표시용 (소유자 비공개여도 "무슨 프렌드"는 공개)
    private static string FriendDeclLabel(string cardId, string mightyId)
    {
        if (string.IsNullOrEmpty(cardId)) return "카드 프렌드";
        if (cardId == "JOKER") return "조커 프렌드";
        if (!string.IsNullOrEmpty(mightyId) && cardId == mightyId)
            return "마이티 프렌드 (" + CardKor(cardId) + ")";
        return CardKor(cardId) + " 프렌드";
    }

    private void Log(string line)
    {
        Debug.Log("[NetworkManager] " + line);
        logLines.Add(line);
        if (logLines.Count > 100) logLines.RemoveAt(0);
        scroll.y = float.MaxValue;
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null) await websocket.Close();
    }

    // ---------- 화면 UI (씬 세팅 없이 자동 표시) ----------
    private void OnGUI()
    {
        GUI.skin.label.fontSize = 15;
        GUI.skin.button.fontSize = 15;
        GUI.skin.textField.fontSize = 15;

        string phase = currentState != null ? currentState.status : "waiting";
        bool inGame = inRoom && phase != "waiting";
        // 게임 중에는 오버레이를 작게(왼쪽 위 HUD), 대기/로비에서는 넓게 표시
        float panelW = inGame ? 400f : 500f;
        float panelH = phase == "finished" ? 520f : (inGame ? 470f : 560f);
        GUILayout.BeginArea(new Rect(20, 20, panelW, panelH), GUI.skin.box);

        GUILayout.Label(inGame ? "Mighty - 게임 중" : "Mighty - 방 테스트");
        GUILayout.Label("연결 상태: " + status);
        GUILayout.Space(6);

        if (!inRoom)
        {
            // ---- 로비 화면 ----
            GUILayout.BeginHorizontal();
            GUILayout.Label("닉네임:", GUILayout.Width(70));
            nickname = GUILayout.TextField(nickname, 12, GUILayout.Width(180));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("방 코드:", GUILayout.Width(70));
            roomIdInput = GUILayout.TextField(roomIdInput, 4, GUILayout.Width(100));
            GUILayout.Label("비번:", GUILayout.Width(45));
            passwordInput = GUILayout.TextField(passwordInput, GUILayout.Width(120));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("방 만들기", GUILayout.Height(38))) CreateRoom();
            if (GUILayout.Button("입장", GUILayout.Height(38))) JoinRoom();
            GUILayout.EndHorizontal();
        }
        else
        {
            switch (phase)
            {
                case "bidding": DrawBidding(); break;
                case "exchanging_kitty": DrawKittyExchange(); break;
                case "choosing_friend": DrawChoosingFriend(); break;
                case "playing": DrawGameHud(); break;
                case "finished": DrawFinished(); break;
                default: DrawWaitingRoom(); break;
            }
        }

        GUILayout.Space(8);
        GUILayout.Label("로그:");
        scroll = GUILayout.BeginScrollView(scroll, GUI.skin.box, GUILayout.Height(inGame ? 90 : 240));
        foreach (string line in logLines) GUILayout.Label(line);
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    // ---- 대기방 화면 (게임 시작 전) ----
    private void DrawWaitingRoom()
    {
        GUILayout.Label("방 코드: " + myRoomId
            + (currentState != null ? "   (상태: " + currentState.status + ")" : ""));

        int count = (currentState != null && currentState.players != null) ? currentState.players.Length : 0;
        GUILayout.Label("플레이어 (" + count + "/5):");
        if (currentState != null && currentState.players != null)
        {
            foreach (PlayerInfo p in currentState.players)
            {
                GUILayout.Label("  " + (p.isHost ? "[방장] " : "") + (p.isBot ? "[봇] " : "") + p.nickname
                    + (p.isReady ? " [준비]" : " [대기]")
                    + (p.connected ? "" : " (연결끊김)")
                    + (p.clientId == myClientId ? "  <- 나" : ""));
            }
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("준비 / 취소")) ToggleReady();

        // 방장에게만 시작 버튼 표시. (빈자리는 서버가 자동으로 봇으로 채움)
        if (IAmHost())
        {
            GUI.enabled = currentState != null && currentState.canStart; // 5명 전원 준비 시 활성화
            if (GUILayout.Button("게임 시작(방장)")) StartGame();
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Ping")) SendPing();
        if (GUILayout.Button("방 나가기")) LeaveRoom();
        GUILayout.EndHorizontal();
    }

    // ---- 입찰 단계 화면 ----
    private void DrawBidding()
    {
        GUILayout.Label("입찰 단계");
        if (currentState == null) return;

        if (currentState.highestBid != null)
        {
            HighestBid h = currentState.highestBid;
            GUILayout.Label("최고 공약: " + h.nickname + " " + h.targetScore
                + " " + (h.noTrump ? "노기루" : SuitKor(h.trumpSuit)));
        }
        else GUILayout.Label("아직 공약 없음");

        int minBid = currentState.minBid > 0 ? currentState.minBid : 13;
        GUILayout.Label("최소 공약: " + minBid);

        bool myBidTurn = currentState.currentBidderClientId == myClientId;
        GUILayout.Label("입찰 차례: " + currentState.currentBidderNickname
            + (myBidTurn ? "  << 내 차례!" : ""));

        if (myBidTurn)
        {
            int cur;
            if (!int.TryParse(bidScoreInput, out cur) || cur < minBid) bidScoreInput = minBid.ToString();
            GUILayout.BeginHorizontal();
            GUILayout.Label("공약:", GUILayout.Width(45));
            bidScoreInput = GUILayout.TextField(bidScoreInput, 2, GUILayout.Width(50));
            GUILayout.Label("기루다:", GUILayout.Width(55));
            trumpIndex = GUILayout.Toolbar(trumpIndex, trumpLabels, GUILayout.Width(220));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("공약 제출")) SendBid();
            if (GUILayout.Button("패스")) PassBid();
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label("(다른 사람 입찰을 기다리는 중...)");

        if (myCanDealMiss)
        {
            GUILayout.Space(4);
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("딜미스 (노게임 · 다시 돌리기)")) DeclareDealMiss();
            GUI.color = Color.white;
        }

        GUILayout.Space(6);
        if (GUILayout.Button("방 나가기")) LeaveRoom();
    }

    // ---- 바닥패 교환 화면 ----
    private void DrawKittyExchange()
    {
        GUILayout.Label("바닥패 교환");
        if (currentState == null) return;

        GUILayout.Label("주공: " + currentState.declarerNickname
            + " / 승리 조건 " + currentState.targetScore + "점 이상");

        bool iAmDeclarer = currentState.declarerClientId == myClientId;
        if (iAmDeclarer)
        {
            GUILayout.Label("바닥패 3장을 받았습니다. 버릴 카드 3장을 고르세요.");
            GUILayout.Label("선택: " + discardSelected.Count + "/3"
                + (discardSelected.Count > 0 ? " [" + string.Join(", ", discardSelected) + "]" : ""));
            GUILayout.Label("(손패 카드를 클릭해 선택/해제)");

            if (myHandCards != null)
            {
                GUILayout.BeginHorizontal();
                int shown = 0;
                foreach (CardData c in myHandCards)
                {
                    if (c == null) continue;
                    bool sel = discardSelected.Contains(c.id);
                    string label = (sel ? "[V] " : "") + CardKor(c.id);
                    if (GUILayout.Button(label, GUILayout.Width(52), GUILayout.Height(36)))
                    {
                        if (sel) discardSelected.Remove(c.id);
                        else if (discardSelected.Count < 3) discardSelected.Add(c.id);
                    }
                    shown++;
                    if (shown % 7 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                }
                GUILayout.EndHorizontal();
            }

            GUI.enabled = discardSelected.Count == 3;
            if (GUILayout.Button("3장 버리기", GUILayout.Height(36))) DiscardKitty();
            GUI.enabled = true;
        }
        else GUILayout.Label("(주공이 바닥패 3장을 고르는 중...)");

        GUILayout.Space(6);
        if (GUILayout.Button("방 나가기")) LeaveRoom();
    }

    private void DiscardKitty()
    {
        if (discardSelected.Count != 3) return;
        Send(JsonUtility.ToJson(new DiscardKittyMsg {
            data = new DiscardKittyData { cardIds = discardSelected.ToArray() }
        }));
        Log("[discard_kitty] 전송: " + string.Join(",", discardSelected));
    }

    // ---- 프렌드 선택 단계 화면 ----
    private void DrawChoosingFriend()
    {
        GUILayout.Label("프렌드 선택 단계");
        if (currentState == null) return;

        GUILayout.Label("주공: " + currentState.declarerNickname
            + " / 기루다 " + (currentState.noTrump ? "노기루" : SuitKor(currentState.trumpSuit)));
        GUILayout.Label("★ 주공팀 승리 조건: " + currentState.targetScore + "점 이상");

        bool iAmDeclarer = currentState.declarerClientId == myClientId;
        if (iAmDeclarer)
        {
            GUILayout.Label("프렌드를 지정하세요:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("마이티 프렌드 (" + CardKor(currentState.mightyCardId) + ")"))
                ChooseFriend(currentState.mightyCardId);
            if (GUILayout.Button("조커 프렌드")) ChooseFriend("JOKER");
            if (GUILayout.Button("노프렌드")) ChooseFriend("NONE");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("카드(id):", GUILayout.Width(70));
            friendCardInput = GUILayout.TextField(friendCardInput, 6, GUILayout.Width(80));
            if (GUILayout.Button("카드 지정")) ChooseFriend(friendCardInput.Trim().ToUpper());
            GUILayout.EndHorizontal();

            GUILayout.Label("플레이어 프렌드 (즉시 공개):");
            if (currentState.players != null)
            {
                foreach (PlayerInfo p in currentState.players)
                {
                    if (p.clientId == currentState.declarerClientId) continue;
                    string label = (p.isBot ? "[봇] " : "") + p.nickname;
                    if (GUILayout.Button(label)) ChooseFriendPlayer(p.clientId);
                }
            }
        }
        else GUILayout.Label("(주공이 프렌드를 고르는 중...)");

        GUILayout.Space(6);
        if (GUILayout.Button("방 나가기")) LeaveRoom();
    }

    // ---- 결과 화면 (한 판 종료) ----
    private void DrawFinished()
    {
        GUILayout.Label("한 판 종료");
        GameFinishedData r = lastResult;
        if (r == null)
        {
            GUILayout.Label("(결과 수신 대기...)");
        }
        else
        {
            GUILayout.Label("승자: " + r.winnerLabel);
            GUILayout.Label("주공팀 목표였던 점수: " + r.targetScore + "점");
            GUILayout.Label("주공: " + r.declarerNickname
                + (string.IsNullOrEmpty(r.friendNickname) ? " (단독)" : " + 프렌드 " + r.friendNickname));
            GUILayout.Label("결과 — 주공팀 " + r.declarerTeamScore + "점"
                + "  |  수비팀 " + r.defenderTeamScore + "점"
                + "  |  바닥패 " + r.kittyScore + "점");
            if (r.winner == "declarer")
                GUILayout.Label("(목표 " + r.targetScore + "점 달성)");
            else
                GUILayout.Label("(목표 " + r.targetScore + "점까지 "
                    + Math.Max(0, r.targetScore - r.declarerTeamScore) + "점 부족)");

            GUILayout.Space(4);
            GUILayout.Label("주공팀:");
            if (r.declarerTeam != null)
            {
                foreach (TeamPlayerScore p in r.declarerTeam)
                    GUILayout.Label("  - " + p.nickname + ": " + p.score + "점");
            }
            GUILayout.Label("수비팀:");
            if (r.defenderTeam != null)
            {
                foreach (TeamPlayerScore p in r.defenderTeam)
                    GUILayout.Label("  - " + p.nickname + ": " + p.score + "점");
            }
        }

        GUILayout.Space(8);
        if (GUILayout.Button("대기방으로 돌아가기", GUILayout.Height(36))) ReturnToLobby();
        if (GUILayout.Button("방 나가기")) LeaveRoom();
    }

    private void ReturnToLobby()
    {
        Send(JsonUtility.ToJson(new ReturnLobbyMsg()));
        Log("[return_to_lobby] 전송");
    }

    // ---- 게임 화면 HUD (게임 시작 후) ----
    private void DrawGameHud()
    {
        GUILayout.Label("방 코드: " + myRoomId);

        if (currentState != null)
        {
            // 주공/공약/프렌드
            string teamLabel = "주공: " + currentState.declarerNickname;
            if (currentState.friendRevealed && !string.IsNullOrEmpty(currentState.friendNickname))
                teamLabel += " + " + currentState.friendNickname;
            GUILayout.Label(teamLabel);
            // 승리 조건 — 눈에 띄게
            GUILayout.Label("★ 주공팀 승리 조건: " + currentState.targetScore + "점 이상"
                + "  (현재 " + currentState.declarerTeamScore + "점 / 앞으로 "
                + currentState.pointsNeeded + "점)");
            if (currentState.friendChosen)
            {
                if (currentState.friendType == "none")
                    GUILayout.Label("프렌드: 없음(노프렌드)");
                else if (currentState.friendType == "player")
                    GUILayout.Label("프렌드: " + currentState.friendNickname + " (플레이어)");
                else
                {
                    string decl = FriendDeclLabel(currentState.friendCardId, currentState.mightyCardId);
                    if (currentState.friendRevealed)
                        GUILayout.Label("프렌드: " + decl + " → " + currentState.friendNickname);
                    else
                        GUILayout.Label("프렌드: " + decl + " (소유자 비공개)");
                }
            }
            // 룰 정보 (기루다/마이티/조커콜)
            GUILayout.Label("기루다: " + (currentState.noTrump ? "노기루" : SuitKor(currentState.trumpSuit))
                + "  |  마이티: " + CardKor(currentState.mightyCardId)
                + "  |  조커콜: " + CardKor(currentState.jokerCallCardId));
            GUILayout.Label("트릭 " + currentState.trickNumber + " / 10");

            bool myTurn = currentState.currentTurnClientId == myClientId;
            GUILayout.Label("현재 차례: " + currentState.currentTurnNickname
                + (myTurn ? "  << 내 차례! (카드 클릭)" : ""));

            if (!string.IsNullOrEmpty(currentState.lastTrickWinnerNickname))
            {
                GUILayout.Label("직전 트릭 승자: " + currentState.lastTrickWinnerNickname);
            }
        }

        GUILayout.Label("플레이어 (남은/획득트릭):");
        if (currentState != null && currentState.players != null)
        {
            foreach (PlayerInfo p in currentState.players)
            {
                string me = p.clientId == myClientId ? " <- 나" : "";
                GUILayout.Label("  " + (p.isBot ? "[봇] " : "") + p.nickname
                    + " : 남은 " + p.handCount + "장, 획득 " + p.trickCount + "트릭" + me);
            }
        }

        GUILayout.Space(6);
        if (GUILayout.Button("방 나가기")) LeaveRoom();
    }
}
