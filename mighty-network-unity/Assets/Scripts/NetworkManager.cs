using System.Collections.Generic;
using System.Text;
using UnityEngine;
using NativeWebSocket; // 무료 패키지: 에디터/WebGL 모두에서 WebSocket 사용 가능

// ============================================================================
// NetworkManager (03단계: 방 생성/입장)
//  - 서버 WebSocket URL: Inspector 기본값 + PlayerPrefs + WebGL ?ws= 쿼리 (ServerUrlResolver)
//  - 서버가 준 reconnectToken을 저장(PlayerPrefs) - 재접속 복구
//
//  사용법: 빈 GameObject에 이 스크립트를 붙이고 Play. (UI는 OnGUI로 자동 표시)
// ============================================================================
public class NetworkManager : MonoBehaviour
{
    [Header("서버 주소 (기본값 / 개발용)")]
    [Tooltip("우선순위: URL ?ws= → PlayerPrefs → WebGL 같은 호스트 → 이 값")]
    public string serverUrl = "ws://localhost:3000";

    [Header("손패 표시 (Inspector에서 연결)")]
    public HandView handView; // 05단계: your_hand를 받아 카드를 그림

    [Header("테이블(낸 카드) 표시 (Inspector에서 연결)")]
    public HandView tableView; // 06단계: 낸 카드들을 화면 중앙에 그림

    [Header("상대 손패(뒷면) — 비우면 런타임 생성")]
    public OpponentHandsView opponentHandsView;

    [Header("좌상단 판 정보 — 비우면 런타임 생성")]
    public GameRuleHud gameRuleHud;

    [Header("셔플/딜 애니 — 비우면 런타임 생성")]
    public DealAnimator dealAnimator;

    [Header("디버그: 좌석 도착지 마커 (노랑=좌석, 청록=beyond)")]
    public bool debugSeatMarkers = false;
    public SeatDebugOverlay seatDebugOverlay;

    private WebSocket websocket;
    private string status = "대기 중...";
    private string activeServerUrl = ""; // 실제로 접속 중인 URL
    private Font uiFont; // WebGL/에디터 UI용 (Galmuri11)
    private float pingSentAt = -1f;
    private float lastPingRttMs = -1f;
    private float nextAutoPingAt = 0f;
    // OnGUI 패널 기준 2열 버튼 공통 폭 (행이 달라도 동일)
    private float guiColW;
    private float guiColGap = 8f;
    private float guiBtnH;

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
    private readonly string[] trumpLabels = { "S", "H", "D", "C", "노기루" };
    private int trumpIndex = 0;
    private int friendSuitPick = 0;
    private bool myCanDealMiss = false; // your_hand로 수신한 딜미스 가능 여부
    [Header("재접속")]
    [Tooltip("에디터에서 Play 시 PlayerPrefs 토큰으로 이전 게임에 자동 재입장. 테스트용(기본 꺼짐).")]
    public bool autoReconnectInEditor = false;

    private bool intentionalLeave = false;
    private bool reconnectInProgress = false;
    private int reconnectAttempt = 0;
    private bool suppressAutoReconnect = false; // 주소 변경 재연결 시 close 루프 방지

    private Vector2 finishedScroll;
    private float finishedAutoLobbyAt = -1f; // realtimeSinceStartup 기준, <=0 이면 비활성
    private const float FinishedAutoLobbySec = 8f;

    // 게임 중 중앙 OnGUI 패널 최소화 (우상단 토글)
    private bool hudCollapsed = false;
    private string lastHudStatus = "";

    // 리드 시 추가 선택 (조커 무늬 선언 / 조커콜 활성화)
    private string pendingPlayCardId = null;
    private bool pendingNeedSuit = false;
    private bool pendingNeedJokerCall = false;

    // 카드 제출 이동 애니
    private CardPlayAnimator playAnimator;
    private TrickWinAnimator trickWinAnimator;
    private PlayChoicePopup playChoicePopup;
    private KittyView kittyView;
    private int lastTableCardCount = 0;
    private bool hasPendingPlayStart;
    private Vector3 pendingPlayStartWorld;
    private string lastTrickResolveKey = "";
    // busy 중 도착한 테이블 갱신(애니용)을 순서대로 처리
    private readonly Queue<GameState> pendingTableAnimStates = new Queue<GameState>();
    private bool tableAnimPipelineRunning;

    // 바닥패: 입찰 중 중앙 표시 → 주공 확정 시 손/좌석으로 비행
    private string lastPhaseStatus = "";
    private CardData[] pendingHandAfterKitty = null;
    private bool kittyPickupStarted = false;
    private string lastElectionAnnounceKey = "";
    private bool choosingToastVisible = false;

    // 시작/재배분: 셔플+딜 애니 동안 손패 표시 보류
    private CardData[] pendingDealHand = null;
    private bool pendingDealAnim = false;
    private DealAnimator.SeatTarget[] dealSeatSnapshot = null;
    private int[] dealProgressCounts = null;
    private Coroutine redealCollectRoutine = null;
    private bool redealCollecting = false;
    private bool dealMissToastVisible = false;
    private string lastBidAnnounceKey = "";

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
    [System.Serializable] private class ReconnectData { public string reconnectToken; }
    [System.Serializable] private class ReconnectMsg { public string type = "reconnect"; public ReconnectData data; }

    [System.Serializable] private class PlayCardData {
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
    [System.Serializable] private class GameFinishedData {
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

    // 받는 메시지들
    [System.Serializable] private class WelcomeData { public string clientId; }
    [System.Serializable] private class WelcomeMsg { public string type; public WelcomeData data; }

    [System.Serializable] private class RoomAckData { public string roomId; public string reconnectToken; public string nickname; }
    [System.Serializable] private class RoomAckMsg { public string type; public RoomAckData data; }
    [System.Serializable] private class ReconnectedData {
        public string roomId; public string clientId; public string reconnectToken; public string nickname;
    }
    [System.Serializable] private class ReconnectedMsg { public string type; public ReconnectedData data; }

    [System.Serializable] private class PlayerInfo { public string clientId; public string nickname; public bool isReady; public bool connected; public bool isHost; public bool isBot; public bool botControlled; public double disconnectedAt; public double reconnectExpiresAt; public int handCount; public int wonCount; public int trickCount; public int score; public int sessionScore; public bool isDeclarer; public bool isMightyPlayer; }
    [System.Serializable] private class TableCardInfo {
        public string playerNickname;
        public CardData card;
        public string declaredSuit;
        public bool jokerCallActivated;
    }
    [System.Serializable] private class HighestBid { public string nickname; public int targetScore; public string trumpSuit; public bool noTrump; }
    [System.Serializable] private class GameState { public string roomId; public string status; public string hostClientId; public bool canStart; public double reconnectGraceMs; public string currentTurnClientId; public string currentTurnNickname; public string lastTrickWinnerNickname; public bool trickComplete; public int trickNumber; public string trumpSuit; public bool noTrump; public string mightyCardId; public string jokerCallCardId; public bool jokerPlayed; public bool mightyRevealed; public string mightyPlayerNickname; public int minBid; public int nextMinBid; public string currentBidderClientId; public string currentBidderNickname; public string[] passedClientIds; public HighestBid highestBid; public string declarerClientId; public string declarerNickname; public int targetScore; public int declarerTeamScore; public int defenderTeamScore; public int kittyScore; public int pointsNeeded; public bool friendChosen; public string friendType; public string friendCardId; public bool friendRevealed; public string friendNickname; public int kittyCount; public TableCardInfo[] tableCards; public PlayerInfo[] players; }
    [System.Serializable] private class GameStateMsg { public string type; public GameState data; }

    [System.Serializable] private class BidResultData {
        public string declarerNickname; public int targetScore; public string trumpSuit; public bool noTrump;
    }
    [System.Serializable] private class BidResultMsg { public string type; public BidResultData data; }

    [System.Serializable] private class ErrorData { public string message; }
    [System.Serializable] private class ErrorMsg { public string type; public ErrorData data; }

    [System.Serializable] private class YourHandData { public CardData[] cards; public bool canDealMiss; }
    [System.Serializable] private class YourHandMsg { public string type; public YourHandData data; }
    [System.Serializable] private class DealMissMsg { public string type = "declare_deal_miss"; public string data = ""; }
    [System.Serializable] private class DealMissEventData { public string nickname; }
    [System.Serializable] private class DealMissEventMsg { public string type; public DealMissEventData data; }

    private async void Start()
    {
        // WebGL 기본 폰트에는 한글 글리프가 없어 안 보임 → Noto Sans KR 사용
        uiFont = UiFonts.Primary;
        if (uiFont == null)
            Log("[font] Galmuri11 로드 실패 (Resources/Fonts/Galmuri11 확인)");
        else
            Log("[font] UI 폰트 로드됨: " + uiFont.name);

        // 손패 카드를 클릭하면 그 카드를 서버에 낸다.
        if (handView != null) handView.onCardClicked = OnHandCardClicked;

        EnsureOpponentHandsView();
        EnsurePlayAnimator();
        EnsureTrickWinAnimator();
        EnsureKittyView();
        EnsureDealAnimator();
        debugSeatMarkers = false; // 좌석 도착 디버그 마커 비활성
        EnsureSeatDebugOverlay();

#if UNITY_EDITOR
        // 에디터 Play 시작마다 이전 방으로 끌려가는 것 방지 (기본)
        if (!autoReconnectInEditor)
        {
            ClearReconnectPrefs();
            Log("[editor] 자동 재접속 OFF — 저장된 reconnectToken 무시/삭제 (로비부터 시작)");
        }
#endif

        activeServerUrl = ServerUrlResolver.Resolve(serverUrl);
        serverUrl = activeServerUrl; // 로비 입력란에도 반영
        Log("[config] 서버 URL = " + activeServerUrl
            + " (우선순위: ?ws= → PlayerPrefs → WebGL호스트 → Inspector)");
        await ConnectSocket();
    }

    private async System.Threading.Tasks.Task ConnectSocket()
    {
        if (websocket != null)
        {
            try { websocket.OnOpen -= OnSocketOpen; } catch { /* ignore */ }
            try { websocket.OnError -= OnSocketError; } catch { /* ignore */ }
            try { websocket.OnClose -= OnSocketClose; } catch { /* ignore */ }
            try { websocket.OnMessage -= OnSocketMessage; } catch { /* ignore */ }
            try
            {
                if (websocket.State == WebSocketState.Open || websocket.State == WebSocketState.Connecting)
                {
                    suppressAutoReconnect = true;
                    await websocket.Close();
                    suppressAutoReconnect = false;
                }
            }
            catch { suppressAutoReconnect = false; }
        }

        string url = string.IsNullOrEmpty(activeServerUrl)
            ? ServerUrlResolver.Resolve(serverUrl)
            : activeServerUrl;
        activeServerUrl = url;
        Log("서버에 접속 시도: " + url);
        websocket = new WebSocket(url);
        websocket.OnOpen += OnSocketOpen;
        websocket.OnError += OnSocketError;
        websocket.OnClose += OnSocketClose;
        websocket.OnMessage += OnSocketMessage;
        await websocket.Connect();
    }

    // 로비에서 주소 바꾼 뒤 저장+재연결
    private async void ApplyServerUrlAndReconnect()
    {
        string next = ServerUrlResolver.Normalize(serverUrl);
        if (string.IsNullOrEmpty(next))
        {
            Log("[config] 서버 URL이 비어 있습니다.");
            return;
        }
        ServerUrlResolver.SavePrefs(next);
        activeServerUrl = next;
        serverUrl = next;
        Log("[config] 서버 URL 저장: " + next);
        status = "재연결 중...";
        reconnectInProgress = false;
        try
        {
            await ConnectSocket();
        }
        catch (System.Exception ex)
        {
            Log("[config] 재연결 실패: " + ex.Message);
        }
    }

    private void OnSocketOpen()
    {
        status = "접속됨";
        reconnectInProgress = false;
        reconnectAttempt = 0;
        Log("[open] 서버에 접속했습니다.");

        string token = PlayerPrefs.GetString("reconnectToken", "");
        if (!intentionalLeave && !string.IsNullOrEmpty(token) && ShouldAutoReconnectWithSavedToken())
        {
            Send(JsonUtility.ToJson(new ReconnectMsg { data = new ReconnectData { reconnectToken = token } }));
            Log("[reconnect] 토큰으로 재접속 요청");
        }
        else if (!string.IsNullOrEmpty(token) && !ShouldAutoReconnectWithSavedToken())
        {
            Log("[reconnect] 저장된 토큰 있음 — 에디터 자동 재접속 OFF라 무시");
        }
    }

    // WebGL/빌드: 항상 허용. 에디터: autoReconnectInEditor 일 때만.
    private bool ShouldAutoReconnectWithSavedToken()
    {
#if UNITY_EDITOR
        return autoReconnectInEditor;
#else
        return true;
#endif
    }

    private void OnSocketError(string e)
    {
        status = "오류";
        Log("[error] " + e);
    }

    private void OnSocketClose(WebSocketCloseCode code)
    {
        status = "끊김";
        Log("[close] 연결이 끊겼습니다.");
        if (intentionalLeave || suppressAutoReconnect)
        {
            if (intentionalLeave) inRoom = false;
            return;
        }
        string token = PlayerPrefs.GetString("reconnectToken", "");
        if (string.IsNullOrEmpty(token) || !ShouldAutoReconnectWithSavedToken())
        {
            inRoom = false;
            return;
        }
        // 좌석은 서버에 유지되고 봇이 대신 플레이 — 자동 재접속
        status = "재접속 중...";
        ScheduleReconnect();
    }

    private void OnSocketMessage(byte[] bytes)
    {
        HandleMessage(Encoding.UTF8.GetString(bytes));
    }

    private async void ScheduleReconnect()
    {
        if (reconnectInProgress) return;
        reconnectInProgress = true;
        reconnectAttempt++;
        int delayMs = Mathf.Min(1000 * (1 << Mathf.Min(reconnectAttempt - 1, 3)), 10000);
        Log("[reconnect] " + delayMs + "ms 후 재시도 (#" + reconnectAttempt + ")");
        await System.Threading.Tasks.Task.Delay(delayMs);
        if (intentionalLeave) { reconnectInProgress = false; return; }
        try
        {
            await ConnectSocket();
        }
        catch (System.Exception ex)
        {
            Log("[reconnect] 실패: " + ex.Message);
            reconnectInProgress = false;
            ScheduleReconnect();
        }
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
        // 결과 화면: 버튼이 가려져도 대기방으로 복귀되도록 자동 전환
        if (inRoom && currentState != null && currentState.status == "finished"
            && finishedAutoLobbyAt > 0f
            && Time.realtimeSinceStartup >= finishedAutoLobbyAt)
        {
            finishedAutoLobbyAt = -1f;
            Log("[lobby] 결과 확인 시간 종료 → 자동 대기방 복귀");
            ReturnToLobby();
        }

        // 대기실: 1초마다 핑 → 지연 시간 표시
        if (inRoom
            && currentState != null
            && currentState.status == "waiting"
            && websocket != null
            && websocket.State == WebSocketState.Open
            && Time.realtimeSinceStartup >= nextAutoPingAt)
        {
            nextAutoPingAt = Time.realtimeSinceStartup + 1f;
            SendPing();
        }
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
                if (pingSentAt > 0f)
                {
                    lastPingRttMs = (Time.realtimeSinceStartup - pingSentAt) * 1000f;
                    pingSentAt = -1f;
                }
                break;

            case "game_started":
                gameStarted = true;
                BeginDealAnimRequest("game_started");
                Log("[game_started] 게임이 시작되었습니다!");
                break;

            case "deal_miss":
            {
                DealMissEventMsg m = JsonUtility.FromJson<DealMissEventMsg>(json);
                string who = (m.data != null && !string.IsNullOrEmpty(m.data.nickname))
                    ? m.data.nickname
                    : "누군가";
                ShowDealMissToast(who);
                RequestRedealWithCollect("deal_miss");
                Log("[deal_miss] " + who + " 재배분 (패 회수 후 재딜)");
                break;
            }

            case "redeal":
            {
                RequestRedealWithCollect("redeal");
                Log("[redeal] 재배분 (패 회수 후 재딜)");
                break;
            }

            case "your_hand":
            {
                YourHandMsg m = JsonUtility.FromJson<YourHandMsg>(json);
                CardData[] cards = (m.data != null) ? m.data.cards : null;
                // 동일 id 중복 제거 (재전송·파싱 이상 시 방어)
                cards = DedupeCardsById(cards);
                int n = cards != null ? cards.Length : 0;
                Log("[your_hand] 손패 " + n + "장 받음");
                if (n > 13)
                    Log("[your_hand] 경고: 손패가 비정상적으로 많음 (" + n + ")");
                myCanDealMiss = (m.data != null && m.data.canDealMiss);

                // 셔플/딜 애니: game_started / deal_miss / redeal 로만 요청됨
                if (n == 10 && pendingDealAnim)
                {
                    pendingDealHand = cards != null ? HandView.SortCards(cards) : null;
                    if (handView != null && !redealCollecting) handView.Clear();
                    Log("[your_hand] 딜 애니 대기 — 손패 표시 보류");
                    if (!redealCollecting)
                        TryStartDealAnimation();
                    break;
                }

                // 입찰→주공 확정: 13장 손패는 바닥패 비행 애니 후에 반영
                bool deferForKitty = n == 13
                    && currentState != null
                    && (currentState.status == "bidding" || currentState.status == "exchanging_kitty");
                if (!deferForKitty && kittyView != null && kittyView.IsBusy)
                    deferForKitty = true;
                if (deferForKitty)
                {
                    pendingHandAfterKitty = cards != null ? HandView.SortCards(cards) : null;
                    Log("[your_hand] 바닥패 수령 애니 대기 중 — 손패 표시 보류");
                    break;
                }

                ApplyMyHand(cards);
                break;
            }

            case "room_created":
            case "room_joined":
            {
                RoomAckMsg m = JsonUtility.FromJson<RoomAckMsg>(json);
                myRoomId = m.data.roomId;
                inRoom = true;
                intentionalLeave = false;
                gameStarted = false;
                ResetLocalHandState();
                // 재접속 토큰 저장
                PlayerPrefs.SetString("reconnectToken", m.data.reconnectToken);
                PlayerPrefs.SetString("roomId", m.data.roomId);
                PlayerPrefs.Save();
                if (!string.IsNullOrEmpty(m.data.nickname) && m.data.nickname != nickname)
                {
                    nickname = m.data.nickname;
                    Log("[" + head.type + "] 닉네임이 '" + nickname + "'(으)로 변경됨 (중복)");
                }
                Log("[" + head.type + "] 방 코드: " + m.data.roomId + " (토큰 저장됨)");
                break;
            }

            case "reconnected":
            {
                ReconnectedMsg m = JsonUtility.FromJson<ReconnectedMsg>(json);
                myClientId = m.data.clientId;
                myRoomId = m.data.roomId;
                inRoom = true;
                intentionalLeave = false;
                if (!string.IsNullOrEmpty(m.data.reconnectToken))
                {
                    PlayerPrefs.SetString("reconnectToken", m.data.reconnectToken);
                    PlayerPrefs.SetString("roomId", m.data.roomId);
                    PlayerPrefs.Save();
                }
                Log("[reconnected] 복구됨 room=" + myRoomId + " id=" + myClientId);
                break;
            }

            case "reconnect_failed":
            {
                ErrorMsg m = JsonUtility.FromJson<ErrorMsg>(json);
                Log("[reconnect_failed] " + (m.data != null ? m.data.message : ""));
                ClearReconnectPrefs();
                inRoom = false;
                gameStarted = false;
                currentState = null;
                ResetLocalHandState();
                break;
            }

            case "game_state":
            {
                GameStateMsg m = JsonUtility.FromJson<GameStateMsg>(json);
                string prevStatus = lastPhaseStatus;
                currentState = m.data;

                // your_hand가 먼저 온 경우: 좌석 정보 생긴 뒤 딜 애니 시작
                if (pendingDealHand != null && pendingDealAnim)
                    TryStartDealAnimation();

                bool dealing = IsDealInProgress();
                if (dealing)
                {
                    if (dealProgressCounts != null)
                        RefreshDealProgressHands();
                    else
                        UpdateOpponentHandsZeroed(m.data);
                }
                else
                    UpdateOpponentHands(m.data);

                if (!dealing)
                {
                    MaybeStartKittyPickup(prevStatus, m.data);
                    UpdateKittyPile(m.data);
                }
                else if (kittyView != null)
                {
                    // 딜 전/중에 바닥패·「바닥패」라벨이 먼저 뜨지 않게
                    kittyView.Clear();
                }
                MaybeAnnounceElection(prevStatus, m.data);
                UpdateChoosingToast(m.data);
                if (!dealing)
                    UpdateTable(m.data);
                UpdateGameRuleHud(m.data);
                MaybeAnnounceBid(prevStatus, m.data);
                if (!dealing)
                    RefreshHandPlayability();
                if (m.data != null) lastPhaseStatus = m.data.status ?? "";
                // 새 입찰 라운드면 당선 토스트 키 리셋
                if (currentState != null && currentState.status == "bidding"
                    && prevStatus != "bidding")
                {
                    lastElectionAnnounceKey = "";
                    lastBidAnnounceKey = "";
                }
                if (currentState != null && currentState.status == "waiting")
                {
                    gameStarted = false;
                    lastResult = null;
                    finishedAutoLobbyAt = -1f;
                    lastTableCardCount = 0;
                    hasPendingPlayStart = false;
                    lastTrickResolveKey = "";
                    kittyPickupStarted = false;
                    pendingHandAfterKitty = null;
                    pendingDealHand = null;
                    pendingDealAnim = false;
                    if (dealAnimator != null) dealAnimator.Cancel();
                    lastPhaseStatus = "waiting";
                    lastElectionAnnounceKey = "";
                    lastBidAnnounceKey = "";
                    if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
                    if (handView != null) handView.Clear();
                    if (tableView != null) tableView.Clear();
                    if (opponentHandsView != null) opponentHandsView.Clear();
                    if (kittyView != null) kittyView.Clear();
                    if (gameRuleHud != null) gameRuleHud.Clear();
                    ResetLocalHandState();
                }
                break;
            }

            case "game_finished":
            {
                GameFinishedMsg m = JsonUtility.FromJson<GameFinishedMsg>(json);
                lastResult = m.data;
                finishedAutoLobbyAt = Time.realtimeSinceStartup + FinishedAutoLobbySec;
                // 결과 화면: 중앙 테이블 패·손패 연출 정리
                if (tableView != null) tableView.Clear();
                lastTableCardCount = 0;
                hasPendingPlayStart = false;
                if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
                Log("[game_finished] 승: " + (lastResult != null ? lastResult.winnerLabel : "?")
                    + "  주공팀 " + (lastResult != null ? lastResult.declarerTeamScore : 0)
                    + " / 목표 " + (lastResult != null ? lastResult.targetScore : 0)
                    + "  (" + FinishedAutoLobbySec + "초 후 자동 대기방)");
                break;
            }

            case "error_message":
            {
                ErrorMsg m = JsonUtility.FromJson<ErrorMsg>(json);
                Log("[error_message] " + m.data.message);
                break;
            }

            case "bid_result":
            {
                BidResultMsg m = JsonUtility.FromJson<BidResultMsg>(json);
                if (m.data != null)
                {
                    Log("[bid_result] 주공 " + m.data.declarerNickname
                        + " / 공약 " + m.data.targetScore);
                    AnnounceElection(
                        m.data.declarerNickname,
                        m.data.targetScore,
                        null,
                        m.data.noTrump,
                        m.data.trumpSuit);
                }
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
        pingSentAt = Time.realtimeSinceStartup;
        Send(JsonUtility.ToJson(new PingMsg { data = new PingData { message = "hello from Unity" } }));
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
        intentionalLeave = true;
        ClearReconnectPrefs();
        Send(JsonUtility.ToJson(new LeaveRoomMsg()));
        inRoom = false;
        gameStarted = false;
        currentState = null;
        lastTableCardCount = 0;
        hasPendingPlayStart = false;
        lastTrickResolveKey = "";
        if (handView != null) handView.Clear();
        if (tableView != null) tableView.Clear();
        if (opponentHandsView != null) opponentHandsView.Clear();
        if (kittyView != null) kittyView.Clear();
        if (gameRuleHud != null) gameRuleHud.Clear();
        kittyPickupStarted = false;
        pendingHandAfterKitty = null;
        pendingDealHand = null;
        pendingDealAnim = false;
        if (dealAnimator != null) dealAnimator.Cancel();
        lastPhaseStatus = "";
        lastElectionAnnounceKey = "";
        choosingToastVisible = false;
        if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
        Log("[leave_room] 전송");
    }

    private void ClearReconnectPrefs()
    {
        PlayerPrefs.DeleteKey("reconnectToken");
        PlayerPrefs.DeleteKey("roomId");
        PlayerPrefs.Save();
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

    private void ResetScores()
    {
        Send(JsonUtility.ToJson(new ResetScoresMsg()));
        Log("[reset_scores] 전송");
    }

    private void ShuffleSeats()
    {
        Send(JsonUtility.ToJson(new ShuffleSeatsMsg()));
        Log("[shuffle_seats] 전송");
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
            RefreshDiscardRaise();
            Log("[kitty] 선택 " + discardSelected.Count + "/3: " + string.Join(",", discardSelected));
            return;
        }
        if (currentState == null || currentState.status != "playing")
        {
            Log("지금은 카드를 낼 수 없습니다."
                + (currentState != null ? " (" + currentState.status + ")" : ""));
            return;
        }
        if (string.IsNullOrEmpty(myClientId)
            || currentState.currentTurnClientId != myClientId)
        {
            Log("아직 내 차례가 아닙니다. (현재: " + currentState.currentTurnNickname + ")");
            return;
        }
        if (!IsLegalToPlay(card))
        {
            Log("지금은 낼 수 없는 카드입니다: " + card.id);
            return;
        }
        BeginPlayCard(card.id);
    }

    // 리드이고 조커/조커콜이면 추가 선택 UI, 아니면 바로 전송
    private void BeginPlayCard(string cardId)
    {
        ClearPendingPlay();
        if (IsMyLeadTurn())
        {
            if (cardId == "JOKER")
            {
                pendingPlayCardId = cardId;
                pendingNeedSuit = true;
                Log("[play] 조커 리드 — 따라낼 무늬를 선택하세요");
                EnsurePlayChoicePopup();
                playChoicePopup.ShowSuitPick(
                    suit => PlayCard(cardId, suit, false),
                    ClearPendingPlay);
                return;
            }
            if (currentState != null && !string.IsNullOrEmpty(currentState.jokerCallCardId)
                && cardId == currentState.jokerCallCardId)
            {
                // 조커가 이미 나왔으면 조커콜 선택 없이 일반 제출
                if (currentState.jokerPlayed)
                {
                    Log("[play] 조커 이미 출현 — 조커콜 카드 일반 제출");
                    PlayCard(cardId, null, false);
                    return;
                }
                pendingPlayCardId = cardId;
                pendingNeedJokerCall = true;
                Log("[play] 조커콜 카드 — 조커콜 사용 여부를 선택하세요");
                EnsurePlayChoicePopup();
                playChoicePopup.ShowJokerCallPick(
                    cardId,
                    use => PlayCard(cardId, null, use),
                    ClearPendingPlay);
                return;
            }
        }
        PlayCard(cardId, null, false);
    }

    private bool IsMyLeadTurn()
    {
        if (currentState == null || currentState.currentTurnClientId != myClientId) return false;
        TableCardInfo[] t = currentState.tableCards;
        return t == null || t.Length == 0 || t.Length >= 5;
    }

    private void ClearPendingPlay()
    {
        pendingPlayCardId = null;
        pendingNeedSuit = false;
        pendingNeedJokerCall = false;
        if (playChoicePopup != null) playChoicePopup.Hide();
    }

    private void PlayCard(string cardId, string declaredSuit, bool activateJokerCall)
    {
        // 서버 전송 직전 재검증 (대기 중 턴이 바뀐 경우 차단)
        if (currentState == null || currentState.status != "playing"
            || string.IsNullOrEmpty(myClientId)
            || currentState.currentTurnClientId != myClientId)
        {
            Log("[play_card] 차단 — 내 차례가 아닙니다.");
            ClearPendingPlay();
            return;
        }

        // 손패에서 날아갈 시작 위치 스냅샷 후, 손패에서 낙관적으로 제거
        hasPendingPlayStart = false;
        if (handView != null && handView.TryGetCardWorldPosition(cardId, out Vector3 startWorld))
        {
            pendingPlayStartWorld = startWorld;
            hasPendingPlayStart = true;
        }
        OptimisticallyRemoveFromHand(cardId);

        var data = new PlayCardData { cardId = cardId, activateJokerCall = activateJokerCall };
        if (!string.IsNullOrEmpty(declaredSuit)) data.declaredSuit = declaredSuit;
        Send(JsonUtility.ToJson(new PlayCardMsg { data = data }));
        string extra = "";
        if (!string.IsNullOrEmpty(declaredSuit)) extra += " suit=" + declaredSuit;
        if (activateJokerCall) extra += " jokerCall";
        Log("[play_card] 전송: " + cardId + extra);
        ClearPendingPlay();
    }

    private void OptimisticallyRemoveFromHand(string cardId)
    {
        if (myHandCards == null || string.IsNullOrEmpty(cardId)) return;
        var kept = new List<CardData>();
        for (int i = 0; i < myHandCards.Length; i++)
        {
            CardData c = myHandCards[i];
            if (c != null && c.id != cardId) kept.Add(c);
        }
        myHandCards = kept.ToArray();
        if (handView != null)
        {
            handView.ShowHand(myHandCards);
            RefreshSelfRoleBadges();
        }
        RefreshHandPlayability();
    }

    private void ResetLocalHandState()
    {
        myHandCards = null;
        discardSelected.Clear();
        myCanDealMiss = false;
        hasPendingPlayStart = false;
        pendingHandAfterKitty = null;
        pendingDealHand = null;
        pendingDealAnim = false;
        if (handView != null) handView.Clear();
    }

    private void ApplyMyHand(CardData[] cards)
    {
        myHandCards = cards != null ? HandView.SortCards(cards) : null;
        if (handView != null)
        {
            handView.ShowHand(myHandCards);
            RefreshSelfRoleBadges();
        }
        discardSelected.Clear();
        RefreshHandPlayability();
        RefreshDiscardRaise();
    }

    private void RefreshDiscardRaise()
    {
        if (handView == null) return;
        if (currentState != null && currentState.status == "exchanging_kitty"
            && currentState.declarerClientId == myClientId)
        {
            // 손패가 토스트/오버레이보다 앞에 오도록
            if (handView.cardContainer != null)
                handView.cardContainer.SetAsLastSibling();
            handView.SetAllPlayable(true);
            handView.ApplyDiscardSelectionRaise(discardSelected);
        }
        else
            handView.ClearDiscardSelectionRaise();
    }

    private void EnsureKittyView()
    {
        if (kittyView == null)
            kittyView = GetComponent<KittyView>();
        if (kittyView == null)
            kittyView = gameObject.AddComponent<KittyView>();

        CardView prefab = handView != null ? handView.cardPrefab : null;
        if (prefab == null && opponentHandsView != null)
            prefab = opponentHandsView.cardPrefab;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        kittyView.Configure(prefab, canvas);
    }

    private void BeginDealAnimRequest(string reason)
    {
        pendingDealAnim = true;
        pendingDealHand = null;
        if (handView != null) handView.Clear();
        if (tableView != null) tableView.Clear();
        if (kittyView != null) kittyView.Clear();
        if (dealAnimator != null && dealAnimator.IsBusy)
            dealAnimator.Cancel();
        Log("[deal] 애니 요청 (" + reason + ")");
    }

    // 딜미스/재배분: 패를 가운데로 모은 뒤 다시 딜
    private void RequestRedealWithCollect(string reason)
    {
        pendingDealAnim = true;
        if (redealCollecting || redealCollectRoutine != null)
        {
            Log("[deal] 회수 애니 진행 중 — " + reason + " 병합");
            return;
        }
        if (dealAnimator != null && dealAnimator.IsBusy)
            dealAnimator.Cancel();
        redealCollectRoutine = StartCoroutine(CoRedealCollectThenDeal(reason));
    }

    private System.Collections.IEnumerator CoRedealCollectThenDeal(string reason)
    {
        redealCollecting = true;
        EnsureDealAnimator();

        DealAnimator.SeatTarget[] seats = BuildDealSeatTargets(currentState);
        int[] counts = null;
        if (seats != null)
        {
            counts = new int[seats.Length];
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i].isSelf)
                {
                    counts[i] = myHandCards != null ? myHandCards.Length : 0;
                    if (counts[i] == 0 && handView != null)
                        counts[i] = 10; // 폴백
                }
                else
                {
                    PlayerInfo p = FindPlayerByNickname(seats[i].nickname);
                    counts[i] = p != null ? Mathf.Max(0, p.handCount) : 10;
                }
            }
        }

        var kittyStarts = new List<Vector3>();
        if (kittyView != null && kittyView.VisibleCount > 0)
            kittyView.CollectPileWorldPositions(kittyStarts);
        else if (currentState != null && currentState.kittyCount > 0)
        {
            Vector3 c = kittyView != null
                ? kittyView.GetPileCenterWorld()
                : Vector3.zero;
            for (int i = 0; i < currentState.kittyCount; i++)
                kittyStarts.Add(c);
        }

        // 손패는 회수 중 한 장씩 줄어들게 유지 (바닥패·테이블만 즉시 숨김)
        if (tableView != null) tableView.Clear();
        if (kittyView != null) kittyView.Clear();

        dealSeatSnapshot = seats;
        if (counts != null)
        {
            dealProgressCounts = new int[counts.Length];
            for (int i = 0; i < counts.Length; i++)
                dealProgressCounts[i] = counts[i];
            RefreshDealProgressHands();
        }

        Log("[deal] 패 회수 시작 (" + reason + ")");
        bool collectDone = false;
        if (dealAnimator != null && seats != null)
        {
            dealAnimator.PlayCollectToCenter(
                seats, counts, kittyStarts, OnCollectCardShot, () => { collectDone = true; });
            while (!collectDone)
                yield return null;
        }
        else
            collectDone = true;

        if (handView != null) handView.Clear();
        if (opponentHandsView != null && currentState != null)
            UpdateOpponentHandsZeroed(currentState);
        dealProgressCounts = null;
        dealSeatSnapshot = null;
        myHandCards = null;
        redealCollecting = false;
        redealCollectRoutine = null;
        Log("[deal] 패 회수 완료 → 재딜 대기");
        TryStartDealAnimation();
    }

    private void OnCollectCardShot(int seatIndex, int cardsRemaining)
    {
        if (dealProgressCounts == null) return;
        if (seatIndex < 0 || seatIndex >= dealProgressCounts.Length) return;
        dealProgressCounts[seatIndex] = Mathf.Max(0, cardsRemaining);
        RefreshDealProgressHands();
    }

    private void EnsureDealAnimator()
    {
        if (dealAnimator == null)
            dealAnimator = GetComponent<DealAnimator>();
        if (dealAnimator == null)
            dealAnimator = gameObject.AddComponent<DealAnimator>();

        CardView prefab = handView != null ? handView.cardPrefab : null;
        if (prefab == null && opponentHandsView != null)
            prefab = opponentHandsView.cardPrefab;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        dealAnimator.Configure(prefab, canvas);
    }

    private void EnsureSeatDebugOverlay()
    {
        if (!debugSeatMarkers)
        {
            if (seatDebugOverlay != null) seatDebugOverlay.SetVisible(false);
            return;
        }
        if (seatDebugOverlay == null)
            seatDebugOverlay = GetComponent<SeatDebugOverlay>();
        if (seatDebugOverlay == null)
            seatDebugOverlay = gameObject.AddComponent<SeatDebugOverlay>();
        Canvas canvas = FindFirstObjectByType<Canvas>();
        seatDebugOverlay.Configure(canvas);
        seatDebugOverlay.SetVisible(true);
        seatDebugOverlay.Refresh();
    }

    private void TryStartDealAnimation()
    {
        EnsureDealAnimator();
        if (dealAnimator == null || dealAnimator.IsBusy) return;
        if (pendingDealHand == null || !pendingDealAnim) return;
        if (currentState == null || currentState.players == null || currentState.players.Length == 0)
            return;

        pendingDealAnim = false;
        if (handView != null) handView.Clear();
        if (tableView != null) tableView.Clear();
        if (kittyView != null) kittyView.Clear();

        // 닉/좌석만 먼저 (손패 0장) → 착지마다 뒷면 추가
        dealSeatSnapshot = BuildDealSeatTargets(currentState);
        if (dealSeatSnapshot == null || dealSeatSnapshot.Length == 0)
        {
            OnDealAnimComplete();
            return;
        }
        dealProgressCounts = new int[dealSeatSnapshot.Length];
        RefreshDealProgressHands();
        Canvas.ForceUpdateCanvases();

        Log("[deal] 셔플/딜 애니 시작 (순차 1장씩)");
        dealAnimator.Play(BuildDealSeatTargets, OnDealCardLanded, OnDealAnimComplete);
    }

    private void OnDealCardLanded(int seatIndex, int cardsAtSeat)
    {
        if (dealProgressCounts == null) return;
        if (seatIndex < 0 || seatIndex >= dealProgressCounts.Length) return;
        dealProgressCounts[seatIndex] = cardsAtSeat;
        RefreshDealProgressHands();
    }

    // 딜 중: 각 좌석 handCount만큼 뒷면 표시 (나는 handView)
    private void RefreshDealProgressHands()
    {
        if (dealSeatSnapshot == null || dealProgressCounts == null) return;
        EnsureOpponentHandsView();

        int selfCount = dealProgressCounts.Length > 0 ? dealProgressCounts[0] : 0;
        if (handView != null)
        {
            if (selfCount <= 0) handView.Clear();
            else handView.ShowFaceDown(selfCount);
        }

        if (opponentHandsView == null || currentState == null) return;
        if (opponentHandsView.cardPrefab == null && handView != null)
            opponentHandsView.cardPrefab = handView.cardPrefab;

        var opp = new List<OpponentHandsView.SeatInfo>();
        for (int i = 1; i < dealSeatSnapshot.Length; i++)
        {
            DealAnimator.SeatTarget t = dealSeatSnapshot[i];
            PlayerInfo p = FindPlayerByNickname(t.nickname);
            OpponentHandsView.SeatInfo info = p != null
                ? ToSeatInfo(p, currentState)
                : new OpponentHandsView.SeatInfo { nickname = t.nickname };
            info.handCount = dealProgressCounts[i];
            opp.Add(info);
        }
        opponentHandsView.Show(opp.ToArray());

        if (dealSeatSnapshot.Length > 0)
        {
            PlayerInfo me = FindPlayerByNickname(dealSeatSnapshot[0].nickname);
            if (me == null && currentState.players != null)
            {
                for (int i = 0; i < currentState.players.Length; i++)
                {
                    if (currentState.players[i] != null
                        && currentState.players[i].clientId == myClientId)
                    {
                        me = currentState.players[i];
                        break;
                    }
                }
            }
            if (me != null)
            {
                OpponentHandsView.SeatInfo selfInfo = ToSeatInfo(me, currentState);
                selfInfo.handCount = selfCount;
                opponentHandsView.ShowSelf(selfInfo);
            }
        }
    }

    private PlayerInfo FindPlayerByNickname(string nick)
    {
        if (currentState == null || currentState.players == null || string.IsNullOrEmpty(nick))
            return null;
        for (int i = 0; i < currentState.players.Length; i++)
        {
            PlayerInfo p = currentState.players[i];
            if (p != null && p.nickname == nick) return p;
        }
        return null;
    }

    private void OnDealAnimComplete()
    {
        dealProgressCounts = null;
        dealSeatSnapshot = null;
        if (pendingDealHand != null)
        {
            ApplyMyHand(pendingDealHand);
            pendingDealHand = null;
        }
        pendingDealAnim = false;

        // 딜에서 펼친 바닥패를 그대로 인수 (재생성으로 위로 점프하던 문제 해결)
        EnsureKittyView();
        if (dealAnimator != null && kittyView != null)
        {
            List<CardView> kittyCards = dealAnimator.TakeKittyCards();
            if (kittyCards != null && kittyCards.Count > 0)
                kittyView.AdoptFaceDown(kittyCards);
            else if (currentState != null)
                UpdateKittyPile(currentState);
        }
        else if (currentState != null)
            UpdateKittyPile(currentState);

        if (currentState != null)
        {
            UpdateOpponentHands(currentState);
            RefreshHandPlayability();
        }
        Log("[deal] 딜 애니 완료 — 손패 공개");
        ClearDealMissToast();
    }

    private void ShowDealMissToast(string nickname)
    {
        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        dealMissToastVisible = true;
        trickWinAnimator.ShowStickyToast(nickname + " 딜미스!\n카드 재분배 중...");
    }

    private void ClearDealMissToast()
    {
        if (!dealMissToastVisible) return;
        dealMissToastVisible = false;
        if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
    }

    private DealAnimator.SeatTarget[] BuildDealSeatTargets()
    {
        return BuildDealSeatTargets(currentState);
    }

    private DealAnimator.SeatTarget[] BuildDealSeatTargets(GameState state)
    {
        if (state == null || state.players == null) return null;

        Vector2 selfSize = handView != null
            ? handView.HandCardSize
            : new Vector2(CardSpriteAtlas.DisplayWidth, CardSpriteAtlas.DisplayHeight);
        Vector2 oppSize = new Vector2(selfSize.x * 0.5f, selfSize.y * 0.5f);

        EnsureOpponentHandsView();
        Canvas.ForceUpdateCanvases();

        int myIndex = IndexOfPlayer(myClientId, null);
        if (myIndex < 0) myIndex = 0;

        var list = new List<DealAnimator.SeatTarget>();
        int n = state.players.Length;
        for (int offset = 0; offset < n; offset++)
        {
            PlayerInfo p = state.players[(myIndex + offset) % n];
            if (p == null) continue;

            Vector2 anchor;
            Vector3 selfWorld;
            int rel;
            if (!TryResolveSeatAnchor(p.nickname, p.clientId, out anchor, out selfWorld, out rel))
                continue;

            bool isSelf = rel == 0;
            list.Add(new DealAnimator.SeatTarget
            {
                nickname = p.nickname,
                normalizedAnchor = anchor,
                selfWorldPos = selfWorld,
                endSize = isSelf ? selfSize * 0.85f : oppSize,
                isSelf = isSelf,
            });
        }

        if (list.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("[deal] 좌석앵커 ");
            for (int i = 0; i < list.Count; i++)
            {
                DealAnimator.SeatTarget t = list[i];
                sb.Append(t.nickname).Append('(')
                    .Append(t.normalizedAnchor.x.ToString("F2")).Append(',')
                    .Append(t.normalizedAnchor.y.ToString("F2")).Append(") ");
            }
            Log(sb.ToString());
        }
        return list.ToArray();
    }

    private int IndexOfPlayer(string clientId, string playerNickname)
    {
        if (currentState == null || currentState.players == null) return -1;
        for (int i = 0; i < currentState.players.Length; i++)
        {
            PlayerInfo p = currentState.players[i];
            if (p == null) continue;
            if (!string.IsNullOrEmpty(clientId) && p.clientId == clientId) return i;
            if (!string.IsNullOrEmpty(playerNickname) && p.nickname == playerNickname) return i;
        }
        return -1;
    }

    // 시계방향: 0=나, 1=왼, 2=상좌, 3=상우, 4=오 — 정규화 앵커 반환 (비행은 로컬 변환)
    private bool TryResolveSeatAnchor(
        string playerNickname,
        string playerClientId,
        out Vector2 normalizedAnchor,
        out Vector3 selfWorldPos,
        out int relativeFromSelf)
    {
        normalizedAnchor = OpponentHandsView.SelfHandAnchor;
        selfWorldPos = Vector3.zero;
        relativeFromSelf = -1;
        if (currentState == null || currentState.players == null) return false;

        int myIndex = -1;
        int targetIndex = -1;
        for (int i = 0; i < currentState.players.Length; i++)
        {
            PlayerInfo p = currentState.players[i];
            if (p == null) continue;
            if (myIndex < 0 && !string.IsNullOrEmpty(myClientId) && p.clientId == myClientId)
                myIndex = i;
            if (targetIndex < 0)
            {
                if (!string.IsNullOrEmpty(playerClientId) && p.clientId == playerClientId)
                    targetIndex = i;
                else if (!string.IsNullOrEmpty(playerNickname) && p.nickname == playerNickname)
                    targetIndex = i;
            }
        }
        if (myIndex < 0) myIndex = 0;
        if (targetIndex < 0) return false;

        int n = currentState.players.Length;
        relativeFromSelf = (targetIndex - myIndex + n) % n;

        if (relativeFromSelf == 0)
        {
            normalizedAnchor = OpponentHandsView.SelfHandAnchor;
            selfWorldPos = handView != null
                ? handView.GetLayoutCenterWorldPosition()
                : Vector3.zero;
            return true;
        }

        normalizedAnchor = OpponentHandsView.GetRelativeSeatAnchor(relativeFromSelf - 1);
        return true;
    }

    // 레거시 월드 해석 — 가능하면 앵커→같은 Canvas 로컬로 변환
    private bool TryResolveSeatWorld(
        string playerNickname, string playerClientId, out Vector3 worldPos, out int relativeFromSelf)
    {
        worldPos = Vector3.zero;
        Vector2 anchor;
        Vector3 selfWorld;
        if (!TryResolveSeatAnchor(playerNickname, playerClientId, out anchor, out selfWorld, out relativeFromSelf))
            return false;

        if (relativeFromSelf == 0)
        {
            worldPos = selfWorld;
            return true;
        }

        EnsureOpponentHandsView();
        RectTransform space = null;
        if (opponentHandsView != null && opponentHandsView.root != null)
            space = opponentHandsView.root;
        else
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) space = canvas.transform as RectTransform;
        }
        if (space == null) return false;

        Vector2 ap = OpponentHandsView.NormalizedToAnchored(space, anchor);
        worldPos = space.TransformPoint(new Vector3(ap.x, ap.y, 0f));
        return true;
    }

    private void UpdateOpponentHandsZeroed(GameState state)
    {
        if (state == null || state.players == null) return;
        EnsureOpponentHandsView();
        if (opponentHandsView == null) return;

        // handCount만 0으로 복사해 닉 패널/좌석만 유지
        var copy = new GameState
        {
            status = state.status,
            currentTurnClientId = state.currentTurnClientId,
            declarerClientId = state.declarerClientId,
            mightyRevealed = state.mightyRevealed,
            mightyPlayerNickname = state.mightyPlayerNickname,
            players = new PlayerInfo[state.players.Length],
        };
        for (int i = 0; i < state.players.Length; i++)
        {
            PlayerInfo src = state.players[i];
            if (src == null) continue;
            copy.players[i] = new PlayerInfo
            {
                clientId = src.clientId,
                nickname = src.nickname,
                isReady = src.isReady,
                connected = src.connected,
                isHost = src.isHost,
                isBot = src.isBot,
                botControlled = src.botControlled,
                handCount = 0,
                wonCount = src.wonCount,
                trickCount = src.trickCount,
                score = src.score,
                sessionScore = src.sessionScore,
                isDeclarer = src.isDeclarer,
                isMightyPlayer = src.isMightyPlayer,
            };
        }
        UpdateOpponentHands(copy);
    }

    private bool IsDealInProgress()
    {
        return pendingDealAnim
            || pendingDealHand != null
            || redealCollecting
            || (dealAnimator != null && dealAnimator.IsBusy);
    }

    private void UpdateKittyPile(GameState state)
    {
        EnsureKittyView();
        if (kittyView == null) return;
        if (kittyView.IsBusy || kittyPickupStarted) return;
        // 셔플/딜 끝나기 전에는 바닥패(및 라벨) 숨김
        if (IsDealInProgress())
        {
            kittyView.Clear();
            return;
        }

        if (state != null && state.status == "bidding" && state.kittyCount > 0)
        {
            if (kittyView.VisibleCount != state.kittyCount)
                kittyView.ShowFaceDown(state.kittyCount);
            return;
        }

        // 입찰이 아니면 중앙 바닥패 숨김 (비행 중엔 위에서 early-return)
        if (state == null || state.status != "bidding")
            kittyView.Clear();
    }

    private void MaybeStartKittyPickup(string prevStatus, GameState state)
    {
        if (state == null || state.status != "exchanging_kitty") return;
        if (kittyPickupStarted) return;
        // 재접속 등으로 이미 exchanging이면 애니 생략, 보류 손패만 반영
        bool fromBidding = prevStatus == "bidding";
        if (!fromBidding)
        {
            if (pendingHandAfterKitty != null)
            {
                ApplyMyHand(pendingHandAfterKitty);
                pendingHandAfterKitty = null;
            }
            return;
        }

        EnsureKittyView();
        if (kittyView == null) return;
        kittyPickupStarted = true;

        // kittyCount=0 이라 Clear되기 전에 비행 소스 확보
        if (kittyView.VisibleCount < 3)
            kittyView.ShowFaceDown(3);

        Vector3 target = ResolveKittyPickupTarget(state);
        Log("[kitty] 바닥패 → " + (state.declarerNickname ?? "?") + " 비행");
        kittyView.FlyToTarget(target, OnKittyPickupComplete);
    }

    private RectTransform GetSeatSpaceRect()
    {
        // 딜 비행/디버그 마커와 동일한 Canvas 좌표계 사용
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null)
        {
            RectTransform crt = canvas.transform as RectTransform;
            if (crt != null) return crt;
        }
        EnsureOpponentHandsView();
        if (opponentHandsView != null && opponentHandsView.root != null)
            return opponentHandsView.root;
        return null;
    }

    private Vector3 ResolveKittyPickupTarget(GameState state)
    {
        Vector3 from = kittyView != null ? kittyView.GetPileCenterWorld() : Vector3.zero;

        string nick = state != null ? state.declarerNickname : null;
        string cid = state != null ? state.declarerClientId : null;
        Vector2 seatAnchor;
        Vector3 selfWorld;
        int rel;
        if (!TryResolveSeatAnchor(nick, cid, out seatAnchor, out selfWorld, out rel))
            return from;

        RectTransform space = GetSeatSpaceRect();
        if (space == null)
            return OpponentHandsView.BeyondSeatWorld(from, selfWorld.sqrMagnitude > 0.01f ? selfWorld : from, 2.7f);

        Vector2 fromAp = OpponentHandsView.WorldToAnchored(space, from);
        Vector2 seatAp = rel == 0 && selfWorld.sqrMagnitude > 0.01f
            ? OpponentHandsView.WorldToAnchored(space, selfWorld)
            : OpponentHandsView.NormalizedToAnchored(space, seatAnchor);
        Vector2 beyondAp = OpponentHandsView.BeyondAnchored(fromAp, seatAp, 2.7f);
        return space.TransformPoint(new Vector3(beyondAp.x, beyondAp.y, 0f));
    }

    private void OnKittyPickupComplete()
    {
        kittyPickupStarted = false;
        if (pendingHandAfterKitty != null)
        {
            ApplyMyHand(pendingHandAfterKitty);
            pendingHandAfterKitty = null;
        }
        else if (myHandCards != null && handView != null)
        {
            // 비주공: 손패 장수는 그대로지만 상대 handCount는 game_state로 이미 갱신됨
            RefreshDiscardRaise();
        }
        if (kittyView != null) kittyView.Clear();
        // 비행 후 비주공에게 "카드 고르는 중..." (당선 토스트가 이미 sticky로 바뀌었을 수도)
        UpdateChoosingToast(currentState);
    }

    private void MaybeAnnounceBid(string prevStatus, GameState state)
    {
        if (state == null || state.status != "bidding") return;
        if (IsDealInProgress()) return;
        HighestBid h = state.highestBid;
        if (h == null || h.targetScore <= 0 || string.IsNullOrEmpty(h.nickname)) return;

        string key = h.nickname + "|" + h.targetScore + "|" + (h.noTrump ? "NT" : (h.trumpSuit ?? ""));
        if (key == lastBidAnnounceKey) return;
        lastBidAnnounceKey = key;

        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        trickWinAnimator.AnnounceBid(h.nickname, h.noTrump, h.trumpSuit, h.targetScore);
    }

    private void MaybeAnnounceElection(string prevStatus, GameState state)
    {
        if (state == null || state.status != "exchanging_kitty") return;
        if (prevStatus != "bidding") return;
        // bid_result가 먼저 왔으면 스킵
        AnnounceElection(state.declarerNickname, state.targetScore, state.declarerClientId,
            state.noTrump, state.trumpSuit);
    }

    private void AnnounceElection(string nickname, int targetScore, string declarerClientId,
        bool noTrump, string trumpSuit)
    {
        string key = (nickname ?? "") + "|" + targetScore;
        if (key == lastElectionAnnounceKey) return;
        lastElectionAnnounceKey = key;

        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;

        string declId = declarerClientId;
        if (string.IsNullOrEmpty(declId) && currentState != null)
            declId = currentState.declarerClientId;

        trickWinAnimator.AnnounceElection(nickname, noTrump, trumpSuit, targetScore, 2.35f, 0.35f, () =>
        {
            if (currentState == null || currentState.status != "exchanging_kitty")
                return;
            bool iAmDeclarer = !string.IsNullOrEmpty(declId) && declId == myClientId;
            if (iAmDeclarer)
                ShowKittyPhaseToast("버릴 카드 3장을 선택하세요.");
            else
                ShowKittyPhaseToast("카드 고르는 중...");
        });
    }

    private void UpdateChoosingToast(GameState state)
    {
        if (state == null || state.status != "exchanging_kitty")
        {
            if (choosingToastVisible)
            {
                choosingToastVisible = false;
                if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
            }
            return;
        }

        // 당선 토스트 애니 중이면 콜백에서 sticky로 전환
        if (choosingToastVisible) return;
        // 재접속 등으로 당선 토스트를 못 본 경우 바로 표시
        if (lastPhaseStatus == "exchanging_kitty" || lastElectionAnnounceKey == "")
        {
            bool iAmDeclarer = state.declarerClientId == myClientId;
            ShowKittyPhaseToast(iAmDeclarer
                ? "버릴 카드 3장을 선택하세요."
                : "카드 고르는 중...");
        }
    }

    private void ShowKittyPhaseToast(string message)
    {
        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        choosingToastVisible = true;
        trickWinAnimator.ShowStickyToast(message);
        // 손패 선택에 집중 — 메뉴 최소화
        hudCollapsed = true;
    }

    // 호환용 별칭
    private void ShowChoosingToast()
    {
        ShowKittyPhaseToast("카드 고르는 중...");
    }

    private static CardData[] DedupeCardsById(CardData[] cards)
    {
        if (cards == null || cards.Length == 0) return cards;
        var seen = new HashSet<string>();
        var list = new List<CardData>(cards.Length);
        for (int i = 0; i < cards.Length; i++)
        {
            CardData c = cards[i];
            if (c == null || string.IsNullOrEmpty(c.id)) continue;
            if (!seen.Add(c.id)) continue;
            list.Add(c);
        }
        return list.ToArray();
    }

    private void EnsurePlayAnimator()
    {
        if (playAnimator == null)
            playAnimator = GetComponent<CardPlayAnimator>();
        if (playAnimator == null)
            playAnimator = gameObject.AddComponent<CardPlayAnimator>();

        CardView prefab = handView != null ? handView.cardPrefab : null;
        if (prefab == null && opponentHandsView != null)
            prefab = opponentHandsView.cardPrefab;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        playAnimator.Configure(prefab, canvas);
    }

    private void EnsureTrickWinAnimator()
    {
        if (trickWinAnimator == null)
            trickWinAnimator = GetComponent<TrickWinAnimator>();
        if (trickWinAnimator == null)
            trickWinAnimator = gameObject.AddComponent<TrickWinAnimator>();

        CardView prefab = handView != null ? handView.cardPrefab : null;
        if (prefab == null && opponentHandsView != null)
            prefab = opponentHandsView.cardPrefab;
        Canvas canvas = FindFirstObjectByType<Canvas>();
        trickWinAnimator.Configure(prefab, canvas);
    }

    private void EnsurePlayChoicePopup()
    {
        if (playChoicePopup == null)
            playChoicePopup = GetComponent<PlayChoicePopup>();
        if (playChoicePopup == null)
            playChoicePopup = gameObject.AddComponent<PlayChoicePopup>();
        Canvas canvas = FindFirstObjectByType<Canvas>();
        playChoicePopup.Configure(canvas);
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
        hudCollapsed = true;
    }

    private void PassBid()
    {
        Send(JsonUtility.ToJson(new PassBidMsg()));
        Log("[pass_bid] 전송");
        hudCollapsed = true;
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

    // 테이블(낸 카드)을 화면 중앙에 갱신한다. — 낸 순서 그대로(정렬 없음)
    // 한 장 추가 시: 손패/좌석 → 테이블 슬롯 이동 애니 (순차 큐 — busy 중 Clear 금지)
    private void UpdateTable(GameState state)
    {
        if (tableView == null) return;
        EnsurePlayAnimator();
        EnsureTrickWinAnimator();

        if (state != null && state.status == "finished")
        {
            pendingTableAnimStates.Clear();
            tableAnimPipelineRunning = false;
            tableView.Clear();
            lastTableCardCount = 0;
            hasPendingPlayStart = false;
            return;
        }

        if (state == null || state.tableCards == null || state.tableCards.Length == 0)
        {
            pendingTableAnimStates.Clear();
            tableAnimPipelineRunning = false;
            tableView.ShowTableCards(new HandView.TableCardEntry[0]);
            lastTableCardCount = 0;
            return;
        }

        int slotCount = HandView.TableTrickSlots;
        if (state.players != null && state.players.Length > 0)
            slotCount = state.players.Length;

        int n = state.tableCards.Length;
        HandView.TableCardEntry[] entries = BuildTableEntries(state.tableCards);
        CardData lastCard = entries[n - 1].card;

        bool grewByOne = n == lastTableCardCount + 1;
        bool newTrickLead = lastTableCardCount >= slotCount && n == 1;
        bool wantAnim = (grewByOne || newTrickLead)
            && playAnimator != null
            && handView != null
            && handView.cardPrefab != null
            && lastCard != null;

        if (!wantAnim)
        {
            // 애니 파이프라인 도중 Clear 하면 비행 dest가 파괴됨 → 대기 중엔 스킵
            if (!tableAnimPipelineRunning)
            {
                tableView.ShowTableCards(entries, slotCount);
                lastTableCardCount = n;
                hasPendingPlayStart = false;
                MaybeBeginTrickWin(state);
            }
            return;
        }

        lastTableCardCount = n;
        pendingTableAnimStates.Enqueue(state);
        if (!tableAnimPipelineRunning)
            StartCoroutine(CoProcessTableAnimQueue());
    }

    private System.Collections.IEnumerator CoProcessTableAnimQueue()
    {
        if (tableAnimPipelineRunning) yield break;
        tableAnimPipelineRunning = true;

        while (pendingTableAnimStates.Count > 0)
        {
            GameState state = pendingTableAnimStates.Dequeue();
            if (state == null || state.tableCards == null || state.tableCards.Length == 0)
                continue;

            int slotCount = HandView.TableTrickSlots;
            if (state.players != null && state.players.Length > 0)
                slotCount = state.players.Length;

            int n = state.tableCards.Length;
            HandView.TableCardEntry[] entries = BuildTableEntries(state.tableCards);
            CardData lastCard = entries[n - 1].card;
            if (lastCard == null) continue;

            TableCardInfo lastInfo = state.tableCards[n - 1];
            string who = lastInfo != null ? lastInfo.playerNickname : null;
            bool isMe = !string.IsNullOrEmpty(who)
                && !string.IsNullOrEmpty(nickname)
                && who == nickname;

            tableView.ShowTableCards(entries, slotCount);
            Canvas.ForceUpdateCanvases();

            RectTransform destCardRt;
            if (!tableView.TryGetSpawnedCardRect(n - 1, out destCardRt) || destCardRt == null)
            {
                MaybeBeginTrickWin(state);
                continue;
            }

            Vector2 tableSize = new Vector2(
                CardSpriteAtlas.DisplayWidth,
                CardSpriteAtlas.DisplayHeight);
            Vector3 startPos;
            Vector2 startSize;

            if (isMe && hasPendingPlayStart)
            {
                startPos = pendingPlayStartWorld;
                startSize = handView.HandCardSize;
                hasPendingPlayStart = false;
            }
            else if (isMe && handView.TryGetCardWorldPosition(lastCard.id, out startPos))
            {
                startSize = handView.HandCardSize;
            }
            else
            {
                startSize = tableSize;
                int rel;
                if (!TryResolveSeatWorld(who, null, out startPos, out rel))
                {
                    startPos = destCardRt.position + new Vector3(0f, 250f, 0f);
                    Canvas canvas = FindFirstObjectByType<Canvas>();
                    if (canvas != null)
                    {
                        RectTransform crt = canvas.transform as RectTransform;
                        if (crt != null)
                            startPos = crt.TransformPoint(new Vector3(0f, crt.rect.height * 0.35f, 0f));
                    }
                }
            }

            bool done = false;
            playAnimator.AnimateToTable(
                lastCard,
                startPos,
                destCardRt,
                startSize,
                tableSize,
                entries[n - 1].isDeclarer,
                entries[n - 1].isFriend,
                entries[n - 1].isFriendSecret,
                entries[n - 1].declaredSuit,
                () => { done = true; });

            float timeout = Time.unscaledTime + 3f;
            while (!done && Time.unscaledTime < timeout)
                yield return null;

            // 애니 후 CanvasGroup/alpha 잔여로 안 보이는 경우 방지 — 테이블 재배치
            if (tableView != null)
                tableView.ShowTableCards(entries, slotCount);

            MaybeBeginTrickWin(state);
        }

        tableAnimPipelineRunning = false;
    }

    private void MaybeBeginTrickWin(GameState state)
    {
        if (state == null || !state.trickComplete) return;
        if (string.IsNullOrEmpty(state.lastTrickWinnerNickname)) return;
        if (trickWinAnimator != null && trickWinAnimator.IsBusy) return;

        string key = state.lastTrickWinnerNickname + ":" + SumPlayerScores(state) + ":" + state.trickNumber;
        if (key == lastTrickResolveKey) return;
        lastTrickResolveKey = key;

        EnsureTrickWinAnimator();
        if (tableView != null)
            Canvas.ForceUpdateCanvases();

        var pointCards = new List<CardData>();
        if (tableView != null)
            tableView.CollectPointCards(pointCards);

        Vector3 centerWorld = tableView != null
            ? tableView.GetLayoutCenterWorldPosition()
            : Vector3.zero;

        Vector2 seatAnchor;
        Vector3 selfWorld;
        int rel;
        if (!TryResolveSeatAnchor(
                state.lastTrickWinnerNickname, null, out seatAnchor, out selfWorld, out rel))
        {
            seatAnchor = OpponentHandsView.SelfHandAnchor;
            rel = 0;
            if (handView != null)
                selfWorld = handView.GetLayoutCenterWorldPosition();
        }

        // 점수패 비행: Canvas 로컬 앵커 공간에서 beyond 계산 → 월드로 (Y 붕괴 방지)
        RectTransform space = GetSeatSpaceRect();
        if (space == null)
        {
            Log("[trick] seat space 없음 — 폴백");
            Vector3 seatWorld = rel == 0 && selfWorld.sqrMagnitude > 0.01f
                ? selfWorld
                : centerWorld + new Vector3(0f, -200f, 0f);
            Vector3 winnerWorldFallback = OpponentHandsView.BeyondSeatWorld(centerWorld, seatWorld, 2.7f);
            trickWinAnimator.Play(
                state.lastTrickWinnerNickname,
                pointCards,
                centerWorld,
                winnerWorldFallback,
                tableView,
                null);
            return;
        }

        Vector2 centerAp = OpponentHandsView.WorldToAnchored(space, centerWorld);
        Vector2 seatAp = rel == 0 && selfWorld.sqrMagnitude > 0.01f
            ? OpponentHandsView.WorldToAnchored(space, selfWorld)
            : OpponentHandsView.NormalizedToAnchored(space, seatAnchor);

        Vector2 beyondAp = OpponentHandsView.BeyondAnchored(centerAp, seatAp, 2.7f);
        Vector3 winnerWorld = space.TransformPoint(new Vector3(beyondAp.x, beyondAp.y, 0f));

        Log("[trick] " + state.lastTrickWinnerNickname + " 승리(rel=" + rel
            + " anchor=" + seatAnchor.x.ToString("F2") + "," + seatAnchor.y.ToString("F2")
            + ") 점수카드 " + pointCards.Count + "장 → 화면 밖으로 이동");
        trickWinAnimator.Play(
            state.lastTrickWinnerNickname,
            pointCards,
            centerWorld,
            winnerWorld,
            tableView,
            null);
    }

    private static int SumPlayerScores(GameState state)
    {
        if (state == null || state.players == null) return 0;
        int s = 0;
        for (int i = 0; i < state.players.Length; i++)
            if (state.players[i] != null) s += state.players[i].score;
        return s;
    }

    private HandView.TableCardEntry[] BuildTableEntries(TableCardInfo[] tableCards)
    {
        if (tableCards == null) return new HandView.TableCardEntry[0];
        var entries = new HandView.TableCardEntry[tableCards.Length];
        for (int i = 0; i < tableCards.Length; i++)
        {
            TableCardInfo t = tableCards[i];
            PlayerInfo p = null;
            if (t != null && currentState != null && currentState.players != null)
            {
                for (int k = 0; k < currentState.players.Length; k++)
                {
                    PlayerInfo cand = currentState.players[k];
                    if (cand != null && cand.nickname == t.playerNickname)
                    {
                        p = cand;
                        break;
                    }
                }
            }
            entries[i] = new HandView.TableCardEntry
            {
                card = t != null ? t.card : null,
                playerNickname = t != null ? t.playerNickname : null,
                isDeclarer = p != null && IsDeclarerPlayer(p, currentState),
                isFriend = p != null && IsPublicFriendSeat(p, currentState),
                isFriendSecret = p != null && IsSecretFriendSeat(p, currentState),
                declaredSuit = t != null ? t.declaredSuit : null,
            };
        }
        return entries;
    }

    private static bool IsDeclarerPlayer(PlayerInfo p, GameState state)
    {
        if (p == null || state == null) return false;
        return p.isDeclarer
            || (!string.IsNullOrEmpty(state.declarerClientId) && p.clientId == state.declarerClientId);
    }

    // 내 차례일 때 못 내는 카드 음영 / 내 차례 아니면 클릭 자체 불가
    private void RefreshHandPlayability()
    {
        if (handView == null) return;

        if (currentState != null && currentState.status == "exchanging_kitty"
            && currentState.declarerClientId == myClientId)
        {
            handView.SetAllPlayable(true);
            RefreshDiscardRaise();
            return;
        }

        handView.ClearDiscardSelectionRaise();

        bool myTurnPlaying = currentState != null
            && currentState.status == "playing"
            && !string.IsNullOrEmpty(myClientId)
            && currentState.currentTurnClientId == myClientId
            && myHandCards != null
            && myHandCards.Length > 0;

        if (!myTurnPlaying)
        {
            // 내 차례 아님·다른 단계: 클릭/레이캐스트 차단 + 음영
            handView.SetAllPlayable(false);
            return;
        }

        TableCardSnapshot[] table = BuildTableSnapshots(currentState);
        string mighty = currentState.mightyCardId;
        string jokerCall = currentState.jokerCallCardId;
        CardData[] hand = myHandCards;
        handView.ApplyPlayability(c =>
            CardPlayLegality.CanPlay(c, hand, table, mighty, jokerCall));
    }

    private static TableCardSnapshot[] BuildTableSnapshots(GameState state)
    {
        if (state == null || state.tableCards == null) return new TableCardSnapshot[0];
        var arr = new TableCardSnapshot[state.tableCards.Length];
        for (int i = 0; i < state.tableCards.Length; i++)
        {
            TableCardInfo t = state.tableCards[i];
            arr[i] = new TableCardSnapshot
            {
                card = t != null ? t.card : null,
                declaredSuit = t != null ? t.declaredSuit : null,
                jokerCallActivated = t != null && t.jokerCallActivated,
            };
        }
        return arr;
    }

    private bool IsLegalToPlay(CardData card)
    {
        if (card == null || myHandCards == null || currentState == null) return false;
        return CardPlayLegality.CanPlay(
            card,
            myHandCards,
            BuildTableSnapshots(currentState),
            currentState.mightyCardId,
            currentState.jokerCallCardId);
    }

    private void EnsureOpponentHandsView()
    {
        if (opponentHandsView == null)
        {
            GameObject go = new GameObject("OpponentHandsView");
            opponentHandsView = go.AddComponent<OpponentHandsView>();
        }
        if (opponentHandsView.cardPrefab == null && handView != null)
            opponentHandsView.cardPrefab = handView.cardPrefab;
    }

    private void EnsureGameRuleHud()
    {
        if (gameRuleHud == null)
            gameRuleHud = GetComponent<GameRuleHud>();
        if (gameRuleHud == null)
            gameRuleHud = gameObject.AddComponent<GameRuleHud>();
    }

    private void UpdateGameRuleHud(GameState state)
    {
        EnsureGameRuleHud();
        if (gameRuleHud == null) return;
        if (state == null)
        {
            gameRuleHud.Clear();
            return;
        }

        string st = state.status;
        bool show = st == "exchanging_kitty" || st == "choosing_friend"
            || st == "playing" || st == "finished";
        // 입찰 중에는 확정 전일 수 있음 — 최고 공약 기루다만 있으면 표시
        if (st == "bidding" && state.highestBid != null)
            show = true;

        if (!show)
        {
            gameRuleHud.Clear();
            return;
        }

        string trumpSuit = null;
        bool noTrump = false;
        string mightyId = state.mightyCardId;
        string jokerCallId = state.jokerCallCardId;
        if (st == "bidding" && state.highestBid != null)
        {
            noTrump = state.highestBid.noTrump;
            trumpSuit = state.highestBid.trumpSuit;
        }
        else
        {
            noTrump = state.noTrump;
            trumpSuit = state.trumpSuit;
        }

        string friend = FormatFriendHud(state);
        string teamScore = FormatDeclarerTeamScoreHud(state);
        string bid = FormatBidHud(state);

        gameRuleHud.Set(new GameRuleHud.Info
        {
            visible = true,
            noTrump = noTrump,
            trumpSuit = trumpSuit,
            mightyCardId = mightyId,
            jokerCallCardId = jokerCallId,
            declarerLabel = state.declarerNickname,
            friendLabel = friend,
            friendCardId = state.friendType == "card" ? state.friendCardId : null,
            friendCardNone = state.friendChosen
                && (state.friendType == "player" || state.friendType == "none"),
            teamScoreLabel = teamScore,
            bidLabel = bid,
        });
    }

    // 마이티 공개 전: 주공 개인 점수만 / 공개 후: 주공팀 합산(서버 live)
    private static string FormatDeclarerTeamScoreHud(GameState state)
    {
        if (state == null) return null;
        string st = state.status;
        if (st != "playing" && st != "finished" && st != "exchanging_kitty" && st != "choosing_friend")
            return null;

        if (!state.mightyRevealed)
        {
            if (state.players == null || string.IsNullOrEmpty(state.declarerClientId))
                return "0점";
            for (int i = 0; i < state.players.Length; i++)
            {
                PlayerInfo p = state.players[i];
                if (p != null && p.clientId == state.declarerClientId)
                    return p.score + "점";
            }
            return "0점";
        }

        return state.declarerTeamScore + "점";
    }

    private static string FormatBidHud(GameState state)
    {
        if (state == null) return null;
        if (state.targetScore > 0)
            return state.targetScore + "점";
        if (state.highestBid != null && state.highestBid.targetScore > 0)
            return state.highestBid.targetScore + "점";
        return null;
    }

    private string FormatFriendHud(GameState state)
    {
        if (state == null || !state.friendChosen) return "미정";
        if (state.friendType == "none") return "없음";
        if (state.friendType == "player")
            return string.IsNullOrEmpty(state.friendNickname) ? "-" : state.friendNickname;
        // card
        string decl = FriendDeclLabel(state.friendCardId, state.mightyCardId);
        if (state.friendRevealed && !string.IsNullOrEmpty(state.friendNickname))
            return state.friendNickname + " (" + decl + ")";
        return decl + " (미공개)";
    }

    // 상대 손패를 뒷면 + 닉네임으로 표시 (나는 handView로 앞면)
    private void UpdateOpponentHands(GameState state)
    {
        EnsureOpponentHandsView();
        if (opponentHandsView == null) return;
        if (opponentHandsView.cardPrefab == null && handView != null)
            opponentHandsView.cardPrefab = handView.cardPrefab;

        if (state == null || state.players == null || state.players.Length == 0)
        {
            opponentHandsView.Clear();
            return;
        }

        // 손패가 있는 단계만 표시
        string st = state.status;
        bool show = st == "bidding" || st == "exchanging_kitty"
            || st == "choosing_friend" || st == "playing";
        if (!show)
        {
            opponentHandsView.Clear();
            return;
        }

        int myIndex = -1;
        for (int i = 0; i < state.players.Length; i++)
        {
            if (state.players[i] != null && state.players[i].clientId == myClientId)
            {
                myIndex = i;
                break;
            }
        }
        if (myIndex < 0) myIndex = 0;

        var seats = new System.Collections.Generic.List<OpponentHandsView.SeatInfo>();
        int n = state.players.Length;
        for (int offset = 1; offset < n; offset++)
        {
            PlayerInfo p = state.players[(myIndex + offset) % n];
            if (p == null) continue;
            seats.Add(ToSeatInfo(p, state));
        }
        opponentHandsView.Show(seats.ToArray());

        // 내 상태 (하단)
        PlayerInfo me = state.players[myIndex];
        if (me != null)
        {
            OpponentHandsView.SeatInfo selfInfo = ToSeatInfo(me, state);
            opponentHandsView.ShowSelf(selfInfo);
        }
    }

    private void RefreshSelfRoleBadges()
    {
        if (handView != null) handView.SetSelfRoleBadges(false, false);
    }

    private OpponentHandsView.SeatInfo ToSeatInfo(PlayerInfo p, GameState state)
    {
        return new OpponentHandsView.SeatInfo
        {
            nickname = p.nickname,
            handCount = p.handCount,
            score = p.score,
            isTurn = !string.IsNullOrEmpty(state.currentTurnClientId)
                && p.clientId == state.currentTurnClientId,
            isBot = p.isBot,
            disconnected = !p.connected && !p.isBot,
            isDeclarer = p.isDeclarer
                || (!string.IsNullOrEmpty(state.declarerClientId) && p.clientId == state.declarerClientId),
            isMightyPlayer = p.isMightyPlayer
                || (state.mightyRevealed
                    && !string.IsNullOrEmpty(state.mightyPlayerNickname)
                    && p.nickname == state.mightyPlayerNickname),
            isFriend = IsPublicFriendSeat(p, state),
            isFriendSecret = IsSecretFriendSeat(p, state),
        };
    }

    private bool IsPublicFriendSeat(PlayerInfo p, GameState state)
    {
        if (p == null || state == null || !state.friendChosen) return false;
        if (state.friendType == "none") return false;
        if (state.friendType == "player")
            return !string.IsNullOrEmpty(state.friendNickname) && p.nickname == state.friendNickname;
        // 카드 프렌드: 공개된 뒤에만 보라 F
        return state.friendRevealed
            && !string.IsNullOrEmpty(state.friendNickname)
            && p.nickname == state.friendNickname;
    }

    // 미공개 카드 프렌드 — 본인 손패에 지정 카드가 있을 때만 회색 F
    private bool IsSecretFriendSeat(PlayerInfo p, GameState state)
    {
        if (p == null || state == null || !state.friendChosen) return false;
        if (state.friendType != "card" || state.friendRevealed) return false;
        if (p.clientId != myClientId) return false;
        return HandContains(myHandCards, state.friendCardId);
    }

    private bool IsFriendSeat(PlayerInfo p, GameState state)
    {
        return IsPublicFriendSeat(p, state) || IsSecretFriendSeat(p, state);
    }

    private static bool HandContains(CardData[] hand, string cardId)
    {
        if (hand == null || string.IsNullOrEmpty(cardId)) return false;
        for (int i = 0; i < hand.Length; i++)
        {
            if (hand[i] != null && hand[i].id == cardId) return true;
        }
        return false;
    }

    private bool IAmHost()
    {
        return currentState != null && currentState.hostClientId == myClientId;
    }

    // 무늬 코드를 표시용 문자로 (WebGL: ♠♥♦♣ 폰트 미포함 → S/H/D/C)
    private static string SuitKor(string suit)
    {
        switch (suit)
        {
            case "SPADE": return "S";
            case "HEART": return "H";
            case "DIAMOND": return "D";
            case "CLUB": return "C";
            default: return "노기루";
        }
    }

    // 카드 id를 짧게 (S_A -> SA, JOKER -> 조커)
    private static string CardKor(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return "-";
        if (cardId == "JOKER") return "조커";
        int us = cardId.IndexOf('_');
        if (us < 0) return cardId;
        string suit = cardId.Substring(0, us);
        string rank = cardId.Substring(us + 1);
        string sym = suit == "S" ? "S" : suit == "H" ? "H" : suit == "D" ? "D" : suit == "C" ? "C" : suit;
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
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null) await websocket.Close();
    }

    private async void OnApplicationPause(bool pause)
    {
        // 모바일/WebGL에서 백그라운드 전환 시 소켓을 닫아 서버가 바로 감지하도록
        if (pause && websocket != null && websocket.State == WebSocketState.Open)
        {
            Log("[pause] 백그라운드 → 소켓 종료 (재접속 대기)");
            await websocket.Close();
        }
    }

    private async void OnDestroy()
    {
        if (websocket != null)
        {
            try { await websocket.Close(); } catch { /* ignore */ }
        }
    }

    // ---------- 화면 UI (씬 세팅 없이 자동 표시) ----------
    private void DrawIdleCornerHud(bool showKittyDiscard)
    {
        float mw = showKittyDiscard ? UiFonts.Layout(200f) : UiFonts.Layout(168f);
        float mh = showKittyDiscard ? UiFonts.Layout(108f) : UiFonts.Layout(48f);
        float mx = Screen.width - mw - 14f;
        float my = 12f;
        GUILayout.BeginArea(new Rect(mx, my, mw, mh), GUI.skin.box);
        if (GUILayout.Button("방 나가기", GUILayout.Height(UiFonts.Layout(36f)))) LeaveRoom();
        if (showKittyDiscard)
        {
            GUILayout.Label("선택 " + discardSelected.Count + "/3");
            GUI.enabled = discardSelected.Count == 3;
            if (GUILayout.Button("3장 버리기", GUILayout.Height(UiFonts.Layout(32f))))
                DiscardKitty();
            GUI.enabled = true;
        }
        GUILayout.EndArea();
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = UiFonts.Size(15);
        GUI.skin.button.fontSize = UiFonts.Size(15);
        GUI.skin.textField.fontSize = UiFonts.Size(15);

        string phase = currentState != null ? currentState.status : "waiting";
        bool inKittyExchange = inRoom && phase == "exchanging_kitty";
        bool myBidTurn = inRoom && phase == "bidding"
            && currentState != null
            && currentState.currentBidderClientId == myClientId;
        bool inGame = inRoom && phase != "waiting";
        bool isFinished = phase == "finished";
        bool iAmDeclarer = currentState != null
            && currentState.declarerClientId == myClientId;
        bool iAmKittyDeclarer = inKittyExchange && iAmDeclarer;
        bool dealing = IsDealInProgress();
        bool needChoiceMenu = !dealing && (
            !inRoom
            || phase == "waiting"
            || phase == "finished"
            || (phase == "choosing_friend" && iAmDeclarer)
            || myBidTurn);

        lastHudStatus = phase;

        // WebGL/고해상도에서 글씨가 너무 작지 않게
        int fontSize = UiFonts.Size(Screen.height >= 900 ? 18 : 15);
        if (uiFont != null)
        {
            GUI.skin.font = uiFont;
            GUI.skin.label.font = uiFont;
            GUI.skin.button.font = uiFont;
            GUI.skin.textField.font = uiFont;
            GUI.skin.textArea.font = uiFont;
            GUI.skin.box.font = uiFont;
        }
        GUI.skin.label.fontSize = fontSize;
        GUI.skin.button.fontSize = fontSize;
        GUI.skin.textField.fontSize = fontSize;

        // 선택이 필요 없으면 메뉴 비활성 (우상단 나가기만)
        if (!needChoiceMenu)
        {
            DrawIdleCornerHud(iAmKittyDeclarer);
            return;
        }

        // 화면 크기에 맞춰 HUD 패널 (로비·대기실은 콘텐츠에 가깝게 짧게)
        float panelW = Mathf.Clamp(Screen.width * 0.7f, 720f, 1000f);
        if (inGame) panelW = Mathf.Clamp(Screen.width * 0.55f, 560f, 820f);
        if (isFinished) panelW = Mathf.Clamp(Screen.width * 0.62f, 640f, 920f);

        float panelH;
        if (!inRoom)
            panelH = Mathf.Min(Screen.height - 40f, UiFonts.Layout(360f));
        else if (phase == "waiting")
        {
            int pc = (currentState != null && currentState.players != null)
                ? currentState.players.Length : 5;
            panelH = Mathf.Min(
                Screen.height - 40f,
                UiFonts.Layout(220f) + pc * UiFonts.Layout(28f) + UiFonts.Layout(150f));
        }
        else if (isFinished)
            panelH = Mathf.Clamp(Screen.height - 40f, 560f, Screen.height - 40f);
        else
            panelH = Mathf.Clamp(Screen.height - 40f, 360f, 540f);

        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;
        // 박스 안쪽 여백 24(좌우 12) + 열 간격 → 2열 버튼 폭 고정
        float boxInset = 24f;
        guiColGap = 8f;
        guiColW = (panelW - boxInset - guiColGap) * 0.5f;
        guiBtnH = UiFonts.Layout(36f);
        GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH), GUI.skin.box);

        // 공약 선택 중·대기실에서는 서버 URL 숨김
        if (!myBidTurn)
        {
            GUILayout.Label(inGame ? "Mighty - 게임 중" : "Mighty - 방 테스트");
            GUILayout.Label("연결 상태: " + status);
            if (!inRoom && !string.IsNullOrEmpty(activeServerUrl))
                GUILayout.Label("서버: " + activeServerUrl);
            GUILayout.Space(6);
        }

        if (!inRoom)
        {
            float labelW = UiFonts.Layout(70f);
            float fieldH = UiFonts.Layout(32f);
            float btnH = UiFonts.Layout(38f);
            // ---- 로비 화면 ----
            GUILayout.BeginHorizontal();
            GUILayout.Label("서버URL:", GUILayout.Width(labelW));
            serverUrl = GUILayout.TextField(serverUrl, GUILayout.Width(guiColW * 2f + guiColGap - labelW));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("주소 저장·재연결", GUILayout.Width(guiColW), GUILayout.Height(fieldH)))
                ApplyServerUrlAndReconnect();
            GUILayout.Space(guiColGap);
            if (GUILayout.Button("기본값", GUILayout.Width(guiColW), GUILayout.Height(fieldH)))
            {
                ServerUrlResolver.ClearPrefs();
                serverUrl = "ws://localhost:3000";
                Log("[config] PlayerPrefs 서버 URL 삭제, 입력란을 localhost로");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("(WebGL: ?ws=wss://호스트 로도 지정 가능)");

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("닉네임:", GUILayout.Width(labelW));
            nickname = GUILayout.TextField(nickname, 12, GUILayout.Width(guiColW * 2f + guiColGap - labelW));
            GUILayout.EndHorizontal();

            float roomFieldW = (guiColW * 2f + guiColGap - labelW - UiFonts.Layout(50f) - guiColGap) * 0.5f;
            GUILayout.BeginHorizontal();
            GUILayout.Label("방 코드:", GUILayout.Width(labelW));
            roomIdInput = GUILayout.TextField(roomIdInput, 4, GUILayout.Width(roomFieldW));
            GUILayout.Space(guiColGap);
            GUILayout.Label("비번:", GUILayout.Width(UiFonts.Layout(50f)));
            passwordInput = GUILayout.TextField(passwordInput, GUILayout.Width(roomFieldW));
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("방 만들기", GUILayout.Width(guiColW), GUILayout.Height(btnH))) CreateRoom();
            GUILayout.Space(guiColGap);
            if (GUILayout.Button("입장", GUILayout.Width(guiColW), GUILayout.Height(btnH))) JoinRoom();
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
                    + "  누적 " + p.sessionScore
                    + (p.isReady ? " [준비]" : " [대기]")
                    + DisconnectLabel(p)
                    + (p.clientId == myClientId ? "  <- 나" : ""));
            }
        }

        GUILayout.Space(6);
        float btnH = guiBtnH;
        float col = guiColW;
        float gap = guiColGap;
        if (IAmHost())
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("준비 / 취소", GUILayout.Width(col), GUILayout.Height(btnH)))
                ToggleReady();
            GUILayout.Space(gap);
            GUI.enabled = currentState != null && currentState.canStart;
            if (GUILayout.Button("게임 시작(방장)", GUILayout.Width(col), GUILayout.Height(btnH)))
                StartGame();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("자리 섞기(방장)", GUILayout.Width(col), GUILayout.Height(btnH)))
                ShuffleSeats();
            GUILayout.Space(gap);
            if (GUILayout.Button("점수 초기화(방장)", GUILayout.Width(col), GUILayout.Height(btnH)))
                ResetScores();
            GUILayout.EndHorizontal();
        }
        else
        {
            if (GUILayout.Button("준비 / 취소", GUILayout.Width(col * 2f + gap), GUILayout.Height(btnH)))
                ToggleReady();
        }

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        string pingLabel = lastPingRttMs >= 0f
            ? ("지연 " + lastPingRttMs.ToString("0") + " ms")
            : "지연 측정 중...";
        GUILayout.Label(pingLabel, GUILayout.Width(col), GUILayout.Height(btnH));
        GUILayout.Space(gap);
        if (GUILayout.Button("방 나가기", GUILayout.Width(col), GUILayout.Height(btnH)))
            LeaveRoom();
        GUILayout.EndHorizontal();
    }

    // ---- 입찰 단계 화면 ----
    private void DrawBidding()
    {
        if (currentState == null) return;

        bool dealing = (dealAnimator != null && dealAnimator.IsBusy)
            || pendingDealHand != null
            || pendingDealAnim;
        if (dealing)
        {
            GUILayout.Label("카드를 섞고 나누는 중...");
            return;
        }

        bool myBidTurn = currentState.currentBidderClientId == myClientId;
        int floorMin = currentState.minBid > 0 ? currentState.minBid : 13;
        int raiseMin = currentState.nextMinBid > 0
            ? currentState.nextMinBid
            : (currentState.highestBid != null
                ? currentState.highestBid.targetScore + 1
                : floorMin);

        if (currentState.highestBid != null)
        {
            HighestBid h = currentState.highestBid;
            GUILayout.Label("최고 공약: " + h.nickname + " " + h.targetScore
                + " " + (h.noTrump ? "노기루" : SuitKor(h.trumpSuit)));
        }
        else GUILayout.Label("아직 공약 없음");

        GUILayout.Label("최소 " + floorMin
            + (currentState.highestBid != null ? ("  · 올릴 최소 " + raiseMin) : ""));

        if (myBidTurn)
        {
            int cur;
            if (!int.TryParse(bidScoreInput, out cur) || cur < raiseMin)
                bidScoreInput = raiseMin.ToString();
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label("공약:", GUILayout.Width(UiFonts.Layout(50f)));
            bidScoreInput = GUILayout.TextField(bidScoreInput, 2, GUILayout.Width(UiFonts.Layout(60f)));
            GUILayout.Label("기루다:", GUILayout.Width(UiFonts.Layout(60f)));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 0; i < trumpOptions.Length; i++)
            {
                IconSpriteAtlas.Slice slice = trumpOptions[i] == "NT"
                    ? IconSpriteAtlas.GetNoTrump()
                    : IconSpriteAtlas.GetSuit(trumpOptions[i]);
                if (IconGui.ToggleButton(slice, trumpIndex == i, IconSpriteAtlas.DisplaySquare.x, IconSpriteAtlas.DisplaySquare.y))
                    trumpIndex = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("공약 제출 (" + raiseMin + "+)")) SendBid();
            if (GUILayout.Button("패스")) PassBid();
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label("(다른 사람 입찰을 기다리는 중...)");

        if (myCanDealMiss)
        {
            GUILayout.Space(4);
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("딜미스 (손패≤0.5점 · 다시 돌리기)")) DeclareDealMiss();
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
            GUILayout.Label("손패에서 버릴 카드 3장을 고르세요. (다시 클릭=해제)");
            GUILayout.Label("선택: " + discardSelected.Count + "/3"
                + (discardSelected.Count > 0 ? " [" + string.Join(", ", discardSelected) + "]" : ""));

            GUI.enabled = discardSelected.Count == 3;
            if (GUILayout.Button("3장 버리기", GUILayout.Height(UiFonts.Layout(36f)))) DiscardKitty();
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
            if (IconGui.Button(IconSpriteAtlas.GetCard(currentState.mightyCardId), IconSpriteAtlas.DisplayCard.x, IconSpriteAtlas.DisplayCard.y))
                ChooseFriend(currentState.mightyCardId);
            GUILayout.Label("마이티", GUILayout.Width(UiFonts.Layout(56f)));
            if (IconGui.Button(IconSpriteAtlas.GetCard("JOKER"), IconSpriteAtlas.DisplayCard.x, IconSpriteAtlas.DisplayCard.y))
                ChooseFriend("JOKER");
            GUILayout.Label("조커", GUILayout.Width(UiFonts.Layout(48f)));
            if (GUILayout.Button("노프렌드", GUILayout.Height(IconSpriteAtlas.DisplayCard.y), GUILayout.Width(UiFonts.Layout(100f))))
                ChooseFriend("NONE");
            GUILayout.EndHorizontal();

            GUILayout.Label("카드 프렌드 — 무늬 선택 후 카드 클릭:");
            GUILayout.BeginHorizontal();
            string[] pickSuits = IconSpriteAtlas.CardSuits;
            for (int i = 0; i < pickSuits.Length; i++)
            {
                if (IconGui.ToggleButton(IconSpriteAtlas.GetSuit(pickSuits[i]), friendSuitPick == i, IconSpriteAtlas.DisplaySquare.x, IconSpriteAtlas.DisplaySquare.y))
                    friendSuitPick = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            string suit = pickSuits[Mathf.Clamp(friendSuitPick, 0, pickSuits.Length - 1)];
            string[] ranks = IconSpriteAtlas.Ranks;
            for (int r = 0; r < ranks.Length; r++)
            {
                if (r == 7)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                string id = IconSpriteAtlas.CardId(suit, ranks[r]);
                if (IconGui.Button(IconSpriteAtlas.GetCard(id), IconSpriteAtlas.DisplayCard.x, IconSpriteAtlas.DisplayCard.y))
                    ChooseFriend(id);
            }
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

        // 버튼은 위에 고정 — 상세 내용이 길어도 잘리지 않게
        if (finishedAutoLobbyAt > 0f)
        {
            float remain = Mathf.Max(0f, finishedAutoLobbyAt - Time.realtimeSinceStartup);
            GUILayout.Label("자동 대기방 복귀: " + remain.ToString("0") + "초");
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("대기방으로 돌아가기", GUILayout.Height(UiFonts.Layout(40f))))
        {
            finishedAutoLobbyAt = -1f;
            ReturnToLobby();
        }
        if (GUILayout.Button("방 나가기", GUILayout.Height(UiFonts.Layout(40f))))
        {
            finishedAutoLobbyAt = -1f;
            LeaveRoom();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        GameFinishedData r = lastResult;
        if (r == null)
        {
            GUILayout.Label("(결과 수신 대기...)");
            return;
        }

        finishedScroll = GUILayout.BeginScrollView(finishedScroll, GUILayout.ExpandHeight(true));
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
                + Mathf.Max(0, r.targetScore - r.declarerTeamScore) + "점 부족)");

        if (r.isRun) GUILayout.Label("★ 런! (주공팀 20점 전부)");
        if (r.isBackrun) GUILayout.Label("★ 백런! (주공팀 10점 이하)");
        if (r.multiplier > 1)
        {
            string tags = (r.multipliers != null && r.multipliers.Length > 0)
                ? string.Join(" + ", r.multipliers) : "";
            GUILayout.Label("배수: ×" + r.multiplier
                + (string.IsNullOrEmpty(tags) ? "" : " (" + tags + ")"));
        }
        else GUILayout.Label("배수: ×1");
        GUILayout.Label("정산 단위: " + r.stakeBase + " × " + r.multiplier + " = " + r.stakeTotal);

        GUILayout.Space(4);
        GUILayout.Label("이번 판 정산 / 누적 스코어:");
        if (r.scoreboard != null)
        {
            foreach (ScoreboardEntry e in r.scoreboard)
            {
                string d = (e.delta >= 0 ? "+" : "") + e.delta;
                GUILayout.Label("  " + (e.isBot ? "[봇] " : "") + e.nickname
                    + ": " + d + " → 누적 " + e.sessionScore);
            }
        }

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
        GUILayout.EndScrollView();
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
            // 기루다/마이티/조커콜/주공/프렌드는 좌상단 GameRuleHud
            GUILayout.Label("★ 주공팀 목표: " + currentState.targetScore + "점"
                + "  (현재 " + currentState.declarerTeamScore + "점 / 남은 "
                + currentState.pointsNeeded + "점)");
            GUILayout.Label("트릭 " + currentState.trickNumber + " / 10");

            bool myTurn = currentState.currentTurnClientId == myClientId;
            GUILayout.Label("현재 차례: " + currentState.currentTurnNickname
                + (myTurn ? "  << 내 차례! (카드 클릭)" : ""));

            if (!string.IsNullOrEmpty(currentState.lastTrickWinnerNickname))
            {
                GUILayout.Label("직전 트릭 승자: " + currentState.lastTrickWinnerNickname);
            }

            if (currentState.tableCards != null && currentState.tableCards.Length > 0)
            {
                TableCardInfo lead = currentState.tableCards[0];
                if (lead != null && lead.card != null)
                {
                    if (lead.card.id == "JOKER" && !string.IsNullOrEmpty(lead.declaredSuit))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("조커 리드 무늬:", GUILayout.Width(UiFonts.Layout(120f)));
                        IconGui.DrawLayout(IconSpriteAtlas.GetSuit(lead.declaredSuit), IconSpriteAtlas.DisplaySquare.x, IconSpriteAtlas.DisplaySquare.y);
                        GUILayout.EndHorizontal();
                    }
                    if (lead.jokerCallActivated)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("조커콜 활성", GUILayout.Width(UiFonts.Layout(90f)));
                        IconGui.DrawLayout(IconSpriteAtlas.GetCard(currentState.jokerCallCardId), IconSpriteAtlas.DisplayCard.x, IconSpriteAtlas.DisplayCard.y);
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        GUILayout.Label("플레이어 (남은/획득트릭):");
        if (currentState != null && currentState.players != null)
        {
            foreach (PlayerInfo p in currentState.players)
            {
                string me = p.clientId == myClientId ? " <- 나" : "";
                GUILayout.Label("  " + (p.isBot ? "[봇] " : "") + p.nickname
                    + " : 남은 " + p.handCount + "장, 획득 " + p.trickCount + "트릭"
                    + ", 누적 " + p.sessionScore
                    + DisconnectLabel(p) + me);
            }
        }

        GUILayout.Space(6);
        if (GUILayout.Button("방 나가기")) LeaveRoom();
    }

    // 끊긴 플레이어 남은 재접속 시간 (서버 Unix ms 기준)
    private string DisconnectLabel(PlayerInfo p)
    {
        if (p == null || p.connected || p.isBot) return "";
        double expires = p.reconnectExpiresAt;
        if (expires <= 0 && p.disconnectedAt > 0)
        {
            double grace = (currentState != null && currentState.reconnectGraceMs > 0)
                ? currentState.reconnectGraceMs : (5 * 60 * 1000);
            expires = p.disconnectedAt + grace;
        }
        if (expires <= 0) return " (연결끊김·봇대타)";
        // Date.now()와 맞추기: UTC ms
        double nowMs = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMilliseconds;
        int sec = (int)System.Math.Ceiling(System.Math.Max(0, expires - nowMs) / 1000.0);
        return " (연결끊김·봇대타 " + (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00") + ")";
    }
}
