using System.Collections.Generic;
using System.Text;
using UnityEngine;

// ============================================================================
// NetworkManager (03단계: 방 생성/입장)
//  - 타이틀과 공유하는 ServerConnection으로 서버 메시지 송수신
//  - 서버가 준 reconnectToken을 저장(PlayerPrefs) - 재접속 복구
//
//  사용법: 빈 GameObject에 이 스크립트를 붙이고 Play. (UI는 OnGUI로 자동 표시)
// ============================================================================
public partial class NetworkManager : MonoBehaviour
{
    [Header("서버 주소 (기본값 / 개발용)")]
    [Tooltip("타이틀 연결이 없을 때 사용할 기본 주소. WebGL에서는 같은 호스트/URL 쿼리 우선.")]
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

    private ServerConnection connection;
    private string status = "대기 중...";
    private Font uiFont; // WebGL/에디터 UI용 (Galmuri11)
    // OnGUI 패널 기준 2열 버튼 공통 폭 (행이 달라도 동일)
    private float guiColW;
    private float guiColGap = 8f;
    private float guiBtnH;

    // IMGUI does not use CanvasScaler, so controls need their own mobile touch size.
    private bool CompactChoice => ResponsiveCanvas.IsPortrait && inRoom && currentState != null
        && (currentState.status == "bidding" || currentState.status == "choosing_friend");
    private float GuiControlHeight => CompactChoice ? 40f : ResponsiveCanvas.IsPortrait ? 48f : 40f;
    private string biddingWaitClientId;

    // 로비 입력값
    private string nickname = "";
    private string roomIdInput = "";
    private string passwordInput = "";
    private bool inviteMode;
    private bool inviteNeedsPassword;
    private bool joinMode;
    private bool singlePlayerSession;
    private string lobbyFeedback = "";
    private string inviteFeedback = "";

    // 방 상태
    private bool inRoom = false;
    private string myRoomId = "";
    private GameState currentState;
    private string myClientId = "";   // 서버가 알려준 내 식별자 (방장/나 구분용)
    private bool singlePlayerStarting;
    // Temporary single-player deal test. Set false to restore normal dealing.
    private const bool DebugSinglePlayerJokerCall = false;
    private bool singlePlayerStartSent;
    private float singlePlayerDeadline;
    private GameFinishedData lastResult = null; // 10단계: game_finished 결과
    private CardData[] myHandCards = null;
    private readonly System.Collections.Generic.List<string> discardSelected =
        new System.Collections.Generic.List<string>();

    // 입찰 UI 입력값
    private string bidScoreInput = "13";
    private readonly string[] trumpOptions = { "SPADE", "HEART", "DIAMOND", "CLUB", "NT" };
    private string[] trumpLabels => new[] { "S", "H", "D", "C", L10n.Text("노기루") };
    private int trumpIndex = 0;
    private int friendSuitPick = 0;
    private FriendData pendingFriendChoice;
    private string pendingFriendLabel;
    private bool myCanDealMiss = false; // your_hand로 수신한 딜미스 가능 여부
    [Header("재접속")]
    [Tooltip("에디터에서 Play 시 PlayerPrefs 토큰으로 이전 게임에 자동 재입장. 테스트용(기본 꺼짐).")]
    public bool autoReconnectInEditor = false;

    private bool intentionalLeave = false;

    private Vector2 menuScroll;
    private float guiPanelHeight;
    private Vector2 finishedScroll;
    private Rect GuiSafeArea
    {
        get
        {
            Rect safe = ResponsiveCanvas.SafeArea;
            float scale = UiFonts.MenuScale;
            return new Rect(safe.x / scale, (ResponsiveCanvas.ViewHeight - safe.yMax) / scale,
                safe.width / scale, safe.height / scale);
        }
    }
    private Vector2 waitingPlayersScroll;
    private GUIStyle waitingPlayerStyle;
    private bool compactUi;
    private float finishedAutoLobbyAt = -1f; // realtimeSinceStartup 기준, <=0 이면 비활성
    private const float FinishedAutoLobbySec = 8f;

    // 현재 HUD 단계
    private string lastHudStatus = "";

    // 리드 시 추가 선택 (조커 무늬 선언 / 조커콜 활성화)
    private string pendingPlayCardId = null;

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
    private bool restoreTableSnapshot;

    // 바닥패: 입찰 중 중앙 표시 → 주공 확정 시 손/좌석으로 비행
    private string lastPhaseStatus = "";
    private CardData[] pendingHandAfterKitty = null;
    private bool kittyPickupStarted = false;
    private string lastElectionAnnounceKey = "";
    private bool choosingToastVisible = false;
    private int lastPassedCount = -1;
    private float passToastUntil;

    // 시작/재배분: 셔플+딜 애니 동안 손패 표시 보류
    private CardData[] pendingDealHand = null;
    private bool pendingDealAnim = false;
    private int dealAnimationId;
    private DealAnimator.SeatTarget[] dealSeatSnapshot = null;
    private int[] dealProgressCounts = null;
    private Coroutine redealCollectRoutine = null;
    private bool redealCollecting = false;
    private bool dealMissToastVisible = false;
    private string lastBidAnnounceKey = "";

    private void Start()
    {
        inviteMode = !string.IsNullOrEmpty(RoomNavigation.InviteCode);
        if (inviteMode)
        {
            roomIdInput = RoomNavigation.InviteCode;
            ClearReconnectPrefs();
            GameScenes.StartSinglePlayer = GameScenes.StartTutorial = false;
        }
        RoomNavigation.EntryPending = false;
        tutorialStarting = GameScenes.StartTutorial;
        tutorialRequested = tutorialStarting;
        tutorialRequestTime = Time.realtimeSinceStartup;
        GameScenes.StartTutorial = false;
        if (tutorialStarting) ClearReconnectPrefs();
        singlePlayerStarting = GameScenes.StartSinglePlayer;
        singlePlayerSession = singlePlayerStarting;
        GameScenes.StartSinglePlayer = false;
        if (singlePlayerStarting)
        {
            ClearReconnectPrefs();
            nickname = L10n.Text("플레이어");
            // Keep other people from replacing the four bots during startup.
            passwordInput = System.Guid.NewGuid().ToString("N");
            singlePlayerDeadline = Time.realtimeSinceStartup + 20f;
        }
        Canvas canvas = FindFirstObjectByType<Canvas>();
        ResponsiveCanvas.Ensure(canvas);
        EnsureTableBackdrop(canvas);
        if (Camera.main != null)
            Camera.main.backgroundColor = MightyTheme.Table;

        // WebGL 기본 폰트에는 한글 글리프가 없어 안 보임 → Noto Sans KR 사용
        uiFont = UiFonts.Primary;
        if (uiFont == null)
            Log("[font] Galmuri11 로드 실패 (Resources/Fonts/Galmuri11 확인)");
        else
            Log("[font] UI 폰트 로드됨: " + uiFont.name);

        Sfx.Ensure();

        // 손패 카드를 클릭하면 그 카드를 서버에 낸다.
        if (handView != null)
        {
            handView.onCardClicked = OnHandCardClicked;
            handView.ApplySelfHandDock();
        }

        EnsureOpponentHandsView();
        EnsurePlayAnimator();
        EnsureTrickWinAnimator();
        EnsureKittyView();
        EnsureDealAnimator();
        debugSeatMarkers = false; // 도킹/좌석 앵커 표식 (디버그용)
        EnsureSeatDebugOverlay();

#if UNITY_EDITOR
        // 에디터 Play 시작마다 이전 방으로 끌려가는 것 방지 (기본)
        if (!autoReconnectInEditor)
        {
            ClearReconnectPrefs();
            Log("[editor] 자동 재접속 OFF — 저장된 reconnectToken 무시/삭제 (로비부터 시작)");
        }
#endif

        connection = ServerConnection.Ensure(serverUrl);
        connection.Ready += OnConnectionReady;
        connection.Disconnected += OnConnectionLost;
        connection.Message += HandleMessage;
        GameSettings.LanguageChanged += RefreshLanguage;
        if (connection.IsReady) OnConnectionReady();
    }

    // The game scene is built at runtime, so put a durable table surface behind
    // cards and seat panels instead of relying on the editor's default colour.
    private static void EnsureTableBackdrop(Canvas canvas)
    {
        if (canvas == null || ResponsiveCanvas.Content(canvas).Find("MightyTableBackdrop") != null)
            return;

        GameObject go = new GameObject(
            "MightyTableBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(ResponsiveCanvas.Content(canvas), false);
        go.transform.SetAsFirstSibling();
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        UnityEngine.UI.Image image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = MightyTheme.Table;
        image.raycastTarget = false;
    }

    private void OnConnectionReady()
    {
        OnSocketOpen();
        if (!string.IsNullOrEmpty(connection.WelcomeJson)) HandleMessage(connection.WelcomeJson);
    }

    private void OnConnectionLost()
    {
        status = "끊김";
        if (inRoom && (currentState == null || currentState.status == "waiting"))
        {
            LeaveRoom();
            return;
        }
        // The shared connection retries every five seconds. Keep the current
        // room state until the saved reconnect token restores the seat.
    }

    private void RefreshLanguage()
    {
        UpdateGameRuleHud(currentState);
    }

    private void OnSocketOpen()
    {
        status = L10n.Text("접속됨");
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
        return inRoom || autoReconnectInEditor;
#else
        return true;
#endif
    }

    private void Update()
    {
        UpdateEmotes();
        UpdateTutorial();
        UpdateBiddingWait();
        if (singlePlayerStarting && Time.realtimeSinceStartup >= singlePlayerDeadline)
        {
            singlePlayerStarting = false;
            status = L10n.Text("싱글플레이 연결 시간 초과 — 서버 연결을 확인해 주세요.");
        }
        // 결과 화면: 버튼이 가려져도 대기방으로 복귀되도록 자동 전환
        if (inRoom && currentState != null && currentState.status == "finished"
            && finishedAutoLobbyAt > 0f
            && Time.realtimeSinceStartup >= finishedAutoLobbyAt)
        {
            finishedAutoLobbyAt = -1f;
            Log("[lobby] 결과 확인 시간 종료 → 자동 대기방 복귀");
            ReturnToLobby();
        }

    }

    private void HandleMessage(string json)
    {
        TypeOnly head = JsonUtility.FromJson<TypeOnly>(json);
        // Packets already queued when leaving must not restore the old room.
        if (intentionalLeave && head.type != "welcome" && head.type != "pong_from_server") return;
        if (HandleTutorialMessage(head.type, json)) return;
        switch (head.type)
        {
            case "player_emote":
                ReceiveEmote(json);
                break;
            case "welcome":
            {
                WelcomeMsg m = JsonUtility.FromJson<WelcomeMsg>(json);
                myClientId = m.data.clientId;
                Log("[welcome] 내 id: " + myClientId);
                if (tutorialStarting) { tutorialStarting = false; Send("{\"type\":\"start_tutorial\"}"); break; }
                if (singlePlayerStarting && !inRoom) CreateRoom();
                break;
            }

            case "pong_from_server":
                break;

            case "game_started":
                dealAnimationId = JsonUtility.FromJson<DealAnimationMsg>(json).data?.dealId ?? 0;
                singlePlayerStarting = false;
                BeginDealAnimRequest("game_started");
                Log("[game_started] 게임이 시작되었습니다!");
                break;

            case "deal_miss":
            {
                DealMissEventMsg m = JsonUtility.FromJson<DealMissEventMsg>(json);
                string who = (m.data != null && !string.IsNullOrEmpty(m.data.nickname))
                    ? m.data.nickname
                    : L10n.Text("누군가");
                ShowDealMissToast(who);
                RequestRedealWithCollect("deal_miss");
                Log("[deal_miss] " + who + " 재배분 (패 회수 후 재딜)");
                break;
            }

            case "redeal":
            {
                dealAnimationId = JsonUtility.FromJson<DealAnimationMsg>(json).data?.dealId ?? 0;
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
                lobbyFeedback = "";
                if (!singlePlayerSession && !tutorialRequested) RoomNavigation.SetRoom(myRoomId);
                intentionalLeave = false;
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
                if (singlePlayerStarting && head.type == "room_created") ToggleReady();
                break;
            }

            case "reconnected":
            {
                ReconnectedMsg m = JsonUtility.FromJson<ReconnectedMsg>(json);
                // Discard pre-disconnect animation work before applying the snapshot.
                StopAllCoroutines();
                pendingTableAnimStates.Clear();
                tableAnimPipelineRunning = false;
                redealCollectRoutine = null;
                redealCollecting = false;
                ResetLocalHandState();
                if (dealAnimator != null) dealAnimator.Cancel();
                if (playAnimator != null) playAnimator.Cancel();
                if (trickWinAnimator != null) trickWinAnimator.Cancel();
                if (kittyView != null) kittyView.Clear();
                kittyPickupStarted = false;
                restoreTableSnapshot = true;
                lastTableCardCount = 0;
                lastTrickResolveKey = "";
                myClientId = m.data.clientId;
                if (!string.IsNullOrEmpty(m.data.nickname)) nickname = m.data.nickname;
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
                currentState = null;
                ResetLocalHandState();
                break;
            }

            case "game_state":
            {
                GameStateMsg m = JsonUtility.FromJson<GameStateMsg>(json);
                if (!inRoom || m.data == null || m.data.roomId != myRoomId) break;
                string prevStatus = lastPhaseStatus;
                UpdateBidDefault(currentState, m.data);
                currentState = m.data;
                if (prevStatus != "choosing_friend" || currentState.status != "choosing_friend"
                    || currentState.declarerClientId != myClientId)
                    ClearFriendChoice();
                if (singlePlayerStarting && !singlePlayerStartSent && currentState != null
                    && currentState.roomId == myRoomId && currentState.canStart)
                {
                    singlePlayerStartSent = true;
                    StartGame();
                }

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

                MaybeAnnounceElection(prevStatus, m.data);
                if (!dealing)
                {
                    if (!electionPresenting) MaybeStartKittyPickup(prevStatus, m.data);
                    UpdateKittyPile(m.data);
                }
                else if (kittyView != null)
                {
                    // 딜 전/중에 바닥패·「바닥패」라벨이 먼저 뜨지 않게
                    kittyView.Clear();
                }
                UpdateChoosingToast(m.data);
                if (!dealing)
                    UpdateTable(m.data);
                UpdateGameRuleHud(m.data);
                if (m.data.status == "playing"
                    && (prevStatus == "choosing_friend" || prevStatus == "exchanging_kitty"))
                {
                    electionPresenting = false;
                    choosingToastVisible = false;
                    EnsureTrickWinAnimator();
                    playIntroUntil = Time.unscaledTime + 4.2f;
                    if (trickWinAnimator != null && gameRuleHud != null)
                        trickWinAnimator.AnnounceGameStart(gameRuleHud.CurrentInfo);
                }
                MaybeAnnounceBid(prevStatus, m.data);
                MaybeAnnouncePass(prevStatus, m.data);
                if (!dealing)
                    RefreshHandPlayability();
                if (m.data != null) lastPhaseStatus = m.data.status ?? "";
                // 새 입찰 라운드면 당선 토스트 키 리셋
                if (currentState != null && currentState.status == "bidding"
                    && prevStatus != "bidding")
                {
                    lastElectionAnnounceKey = "";
                    electionPresenting = false;
                    lastBidAnnounceKey = "";
                }
                if (currentState != null && currentState.status == "waiting")
                {
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
                    electionPresenting = false;
                    lastElectionAnnounceKey = "";
                    lastBidAnnounceKey = "";
                    lastPassedCount = -1;
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
                PlayFinishedSfx(m.data);
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
                singlePlayerStarting = false;
                ErrorMsg m = JsonUtility.FromJson<ErrorMsg>(json);
                lobbyFeedback = m.data.message;
                if (inviteMode && m.data.message.Contains("비밀번호")) inviteNeedsPassword = true;
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
    private void Send(string json)
    {
        if (connection == null || !connection.IsReady)
        {
            Log("아직 접속되지 않았습니다.");
            return;
        }
        connection.Send(json);
    }

    private void CreateRoom()
    {
        if (inviteMode) return;
        if (string.IsNullOrWhiteSpace(nickname)) { lobbyFeedback = "닉네임을 입력하세요."; return; }
        lobbyFeedback = "";
        intentionalLeave = false;
        Send(JsonUtility.ToJson(new CreateRoomMsg { data = new CreateRoomData {
            nickname = nickname, password = passwordInput,
            debugSinglePlayerJokerCall = singlePlayerStarting && DebugSinglePlayerJokerCall
        } }));
        Log("[create_room] 전송: " + nickname);
    }

    private void JoinRoom()
    {
        if (string.IsNullOrWhiteSpace(nickname)) { lobbyFeedback = "닉네임을 입력하세요."; return; }
        if (string.IsNullOrWhiteSpace(roomIdInput)) { lobbyFeedback = "방 코드를 입력하세요."; return; }
        lobbyFeedback = "";
        intentionalLeave = false;
        Send(JsonUtility.ToJson(new JoinRoomMsg { data = new JoinRoomData { roomId = roomIdInput.Trim().ToUpperInvariant(), nickname = nickname, password = passwordInput } }));
        Log("[join_room] 전송: " + roomIdInput.ToUpper());
    }

    private void LeaveRoom()
    {
        // Startup ends when cards are dealt; the session flag survives the
        // whole game, including results and a return to the waiting room.
        bool returnToTitle = singlePlayerSession;
        RoomNavigation.SetMode(returnToTitle ? "" : "multiplayer");
        inviteMode = inviteNeedsPassword = singlePlayerSession = false;
        lobbyFeedback = inviteFeedback = "";
        tutorialRequested = tutorialStarting = false;
        tutorialState = null;
        if (tutorialOverlay != null) tutorialOverlay.Hide();
        intentionalLeave = true;
        ClearReconnectPrefs();
        Send(JsonUtility.ToJson(new LeaveRoomMsg()));
        inRoom = false;
        currentState = null;
        myRoomId = "";
        roomIdInput = "";
        passwordInput = "";
        singlePlayerStarting = false;
        singlePlayerStartSent = false;
        lastResult = null;
        finishedAutoLobbyAt = -1f;
        pendingPlayCardId = null;
        StopAllCoroutines();
        redealCollectRoutine = null;
        redealCollecting = false;
        pendingTableAnimStates.Clear();
        tableAnimPipelineRunning = false;
        ResetLocalHandState();
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
        electionPresenting = false;
        playIntroUntil = 0f;
        lastPassedCount = -1;
        choosingToastVisible = false;
        if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
        Log("[leave_room] 전송");
        if (returnToTitle)
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Title);
    }

    public void OnInviteCopied(string result)
    {
        inviteFeedback = result == "success" ? "초대 링크를 복사했습니다."
            : "복사하지 못했습니다. 브라우저 주소창의 링크를 복사해 주세요.";
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
        if (TutorialBlocksInput) return;
        if (!TutorialAllowsCard(card)) return;
        if (SettingsPanel.IsOpen) return;
        if (card == null) return;
        if (electionPresenting || kittyPickupStarted || Time.unscaledTime < playIntroUntil) return;
        // 바닥패 교환: 카드 선택 토글
        if (currentState != null && currentState.status == "exchanging_kitty"
            && currentState.declarerClientId == myClientId)
        {
            if (discardSelected.Contains(card.id)) discardSelected.Remove(card.id);
            else if (discardSelected.Count < 3) discardSelected.Add(card.id);
            Sfx.UiClick();
            RefreshDiscardRaise();
            Log("[kitty] 선택 " + discardSelected.Count + "/3: " + string.Join(",", discardSelected));
            return;
        }
        if (currentState == null || currentState.status != "playing")
        {
            Log("지금은 카드를 낼 수 없습니다."
                + (currentState != null ? " (" + L10n.Text(currentState.status) + ")" : ""));
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
        if (playChoicePopup != null) playChoicePopup.Hide();
    }

    private void PlayCard(string cardId, string declaredSuit, bool activateJokerCall)
    {
        if (Time.unscaledTime < playIntroUntil) { ClearPendingPlay(); return; }
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
        ClearFriendChoice();
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
        }
        discardSelected.Clear();
        RefreshHandPlayability();
        RefreshDiscardRaise();
        RefreshSelfRoleBadges();
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
            if (tutorialState != null) handView.ApplyPlayability(TutorialAllowsCard);
            else handView.SetAllPlayable(true);
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
        if (dealAnimationId > 0)
            Send(JsonUtility.ToJson(new DealAnimationMsg {
                type = "deal_animation_complete", data = new DealAnimationData { dealId = dealAnimationId }
            }));
    }

    private void ShowDealMissToast(string nickname)
    {
        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        dealMissToastVisible = true;
        trickWinAnimator.ShowStickyToast(() => nickname + L10n.Text(" 딜미스!\n카드 재분배 중..."));
        Sfx.DealMiss();
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
        Vector2 oppSize = selfSize;

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
                relativeFromSelf = rel,
                nickname = p.nickname,
                normalizedAnchor = anchor,
                selfWorldPos = selfWorld,
                endSize = selfSize,
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
        normalizedAnchor = OpponentHandsView.GetSelfHandAnchor(GetSeatSpaceRect());
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
            normalizedAnchor = OpponentHandsView.GetSelfHandAnchor(GetSeatSpaceRect());
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
            if (canvas != null) space = ResponsiveCanvas.Content(canvas) as RectTransform;
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
            friendChosen = state.friendChosen,
            friendType = state.friendType,
            friendCardId = state.friendCardId,
            friendRevealed = state.friendRevealed,
            friendClientId = state.friendClientId,
            friendNickname = state.friendNickname,
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
        if (electionPresenting) return;
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
            RectTransform crt = ResponsiveCanvas.Content(canvas) as RectTransform;
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
        Vector2 beyondAp = seatAp;
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

        Sfx.Bid();
        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        trickWinAnimator.AnnounceBid(h.nickname, h.noTrump, h.trumpSuit, h.targetScore);
    }

    private void MaybeAnnouncePass(string prevStatus, GameState state)
    {
        if (state == null || state.status != "bidding")
        {
            lastPassedCount = -1;
            return;
        }
        int n = (state.passedClientIds != null) ? state.passedClientIds.Length : 0;
        if (prevStatus != "bidding")
        {
            lastPassedCount = n;
            return;
        }
        if (lastPassedCount >= 0 && n > lastPassedCount)
        {
            string passerId = state.passedClientIds[n - 1];
            PlayerInfo passer = state.players != null
                ? System.Array.Find(state.players, p => p != null && p.clientId == passerId) : null;
            Sfx.Pass();
            EnsureTrickWinAnimator();
            passToastUntil = Time.unscaledTime + 1f;
            trickWinAnimator.Announce((passer != null ? passer.nickname : L10n.Text("누군가")) + " PASS", 0.85f, 0.15f);
        }
        lastPassedCount = n;
    }

    private void PlayFinishedSfx(GameFinishedData r)
    {
        if (IWonFinished(r)) Sfx.GameWin();
        else Sfx.GameLose();
    }

    private bool IWonFinished(GameFinishedData r)
    {
        if (r == null) return false;
        bool onDeclarer = false;
        if (r.declarerTeam != null)
        {
            for (int i = 0; i < r.declarerTeam.Length; i++)
            {
                TeamPlayerScore p = r.declarerTeam[i];
                if (p != null && p.clientId == myClientId)
                {
                    onDeclarer = true;
                    break;
                }
            }
        }
        if (!onDeclarer && currentState != null && currentState.declarerClientId == myClientId)
            onDeclarer = true;
        bool declarerWon = r.winner == "declarer";
        return onDeclarer == declarerWon;
    }

    private void MaybeAnnounceElection(string prevStatus, GameState state)
    {
        if (state == null || state.status != "exchanging_kitty") return;
        if (prevStatus != "bidding") return;
        // bid_result가 먼저 왔으면 스킵
        AnnounceElection(state.declarerNickname, state.targetScore, state.declarerClientId,
            state.noTrump, state.trumpSuit);
    }

    private bool electionPresenting;
    private float playIntroUntil;

    private void AnnounceElection(string nickname, int targetScore, string declarerClientId,
        bool noTrump, string trumpSuit)
    {
        string key = (nickname ?? "") + "|" + targetScore;
        if (key == lastElectionAnnounceKey) return;
        lastElectionAnnounceKey = key;

        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        electionPresenting = true;
        // Hide the bidding pile immediately, including the final pass-toast wait.
        // The pickup animation recreates its three cards after the announcement.
        if (kittyView != null) kittyView.Clear();
        StartCoroutine(AfterPassToast(() =>
        {
            Sfx.Elected();
            trickWinAnimator.AnnounceElection(nickname, noTrump, trumpSuit, targetScore, 2f, 0.2f, () =>
            {
                electionPresenting = false;
                if (currentState == null || currentState.status != "exchanging_kitty") return;
                MaybeStartKittyPickup("bidding", currentState);
                ShowChoosingToast();
            });
        }));
    }

    private System.Collections.IEnumerator AfterPassToast(System.Action action)
    {
        yield return null; // bid_result arrives before the exchange state.
        while (inRoom && currentState != null && currentState.status == "bidding")
            yield return null;
        while (Time.unscaledTime < passToastUntil) yield return null;
        if (inRoom && currentState != null && currentState.status == "exchanging_kitty")
            action();
        else electionPresenting = false;
    }

    private void UpdateChoosingToast(GameState state)
    {
        if (electionPresenting) return;
        if (state == null || (state.status != "exchanging_kitty" && state.status != "choosing_friend"))
        {
            if (choosingToastVisible)
            {
                choosingToastVisible = false;
                if (trickWinAnimator != null) trickWinAnimator.ClearStickyToast();
            }
            return;
        }

        // 당선 토스트 애니 중이면 콜백에서 sticky로 전환
        if (state.status == "choosing_friend" && lastPhaseStatus != "choosing_friend")
        {
            ShowChoosingToast();
            return;
        }
        if (choosingToastVisible || Time.unscaledTime < passToastUntil) return;
        // 재접속 등으로 당선 토스트를 못 본 경우 바로 표시
        if (lastPhaseStatus == "exchanging_kitty" || lastElectionAnnounceKey == "")
        {
            ShowChoosingToast();
        }
    }

    private void ShowKittyPhaseToast(System.Func<string> message)
    {
        EnsureTrickWinAnimator();
        if (trickWinAnimator == null) return;
        choosingToastVisible = true;
        trickWinAnimator.ShowStickyToast(message);
    }

    // 호환용 별칭
    private void ShowChoosingToast()
    {
        ShowKittyPhaseToast(() => currentState != null
            && !string.IsNullOrEmpty(myClientId) && currentState.declarerClientId == myClientId
                ? (currentState.status == "choosing_friend" ? L10n.Text("프렌드를 선택하세요")
                    : L10n.Text("버릴 카드 3장을 고르세요"))
                : (currentState != null ? currentState.declarerNickname : "")
                    + (GameSettings.Language == "en"
                        ? " has been elected!\nDiscards and friend are being selected."
                        : "님이 당선되었습니다!\n버릴 카드와 프렌드를 설정하고 있습니다."));
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
        if (!CanChooseFriend()) return;
        if (string.IsNullOrWhiteSpace(friendCardId))
        {
            Log("프렌드 카드 id를 입력하세요. (노프렌드는 '노프렌드' 버튼)");
            return;
        }
        pendingFriendChoice = new FriendData { friendCardId = friendCardId };
        pendingFriendLabel = friendCardId == "NONE" ? L10n.Text("노프렌드")
            : friendCardId == currentState.mightyCardId ? L10n.Text("마이티") + " (" + CardKor(friendCardId) + ")"
            : CardKor(friendCardId);
    }

    private void ChooseFriendPlayer(string friendClientId)
    {
        if (!CanChooseFriend()) return;
        if (string.IsNullOrEmpty(friendClientId))
        {
            Log("프렌드 플레이어를 선택하세요.");
            return;
        }
        if (currentState.players == null) return;
        foreach (PlayerInfo player in currentState.players)
        {
            if (player == null || player.clientId != friendClientId || player.clientId == myClientId) continue;
            pendingFriendChoice = new FriendData { friendClientId = friendClientId };
            pendingFriendLabel = player.nickname;
            return;
        }
    }

    private bool CanChooseFriend()
    {
        return inRoom && currentState != null && currentState.status == "choosing_friend"
            && !string.IsNullOrEmpty(myClientId) && currentState.declarerClientId == myClientId;
    }

    private void ClearFriendChoice()
    {
        pendingFriendChoice = null;
        pendingFriendLabel = null;
    }

    private void ConfirmFriendChoice()
    {
        if (!CanChooseFriend() || pendingFriendChoice == null)
        {
            ClearFriendChoice();
            return;
        }
        FriendData choice = pendingFriendChoice;
        ClearFriendChoice();
        Send(JsonUtility.ToJson(new ChooseFriendMsg { data = choice }));
        Log("[choose_friend] " + (choice.friendCardId ?? choice.friendClientId));
    }

    // 테이블(낸 카드)을 화면 중앙에 갱신한다. — 낸 순서 그대로(정렬 없음)
    // 한 장 추가 시: 손패/좌석 → 테이블 슬롯 이동 애니 (순차 큐 — busy 중 Clear 금지)
    private void UpdateTable(GameState state)
    {
        if (tableView == null) return;
        EnsurePlayAnimator();
        EnsureTrickWinAnimator();

        if (restoreTableSnapshot)
        {
            restoreTableSnapshot = false;
            int slots = state != null && state.players != null && state.players.Length > 0
                ? state.players.Length : HandView.TableTrickSlots;
            var cards = state != null && state.status != "finished" && state.tableCards != null
                ? BuildTableEntries(state) : new HandView.TableCardEntry[0];
            tableView.ShowTableCards(cards, slots);
            lastTableCardCount = cards.Length;
            hasPendingPlayStart = false;
            MaybeBeginTrickWin(state);
            return;
        }

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
        // Disconnect/leave broadcasts can repeat a trick that is already being
        // collected (or has been collected). Do not resurrect its table slots.
        if (state.trickComplete && TrickResolveKey(state) == lastTrickResolveKey)
            return;
        HandView.TableCardEntry[] entries = BuildTableEntries(state);
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
            if (!tableAnimPipelineRunning && !trickWinAnimator.IsBusy)
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
            // The previous trick owns the table until its fade/collection ends.
            // Bot takeover can deliver the next play before that animation ends.
            while (trickWinAnimator != null && trickWinAnimator.IsBusy)
                yield return null;
            if (pendingTableAnimStates.Count == 0) break;
            GameState state = pendingTableAnimStates.Dequeue();
            if (state == null || state.tableCards == null || state.tableCards.Length == 0)
                continue;

            int slotCount = HandView.TableTrickSlots;
            if (state.players != null && state.players.Length > 0)
                slotCount = state.players.Length;

            int n = state.tableCards.Length;
            HandView.TableCardEntry[] entries = BuildTableEntries(state);
            CardData lastCard = entries[n - 1].card;
            if (lastCard == null) continue;

            TableCardInfo lastInfo = state.tableCards[n - 1];
            string who = lastInfo != null ? lastInfo.playerNickname : null;
            bool isMe = !string.IsNullOrEmpty(who)
                && !string.IsNullOrEmpty(nickname)
                && who == nickname;

            Sfx.PlayedCard(
                lastCard,
                lastInfo != null && lastInfo.jokerCallActivated,
                state.mightyCardId,
                state.friendRevealed ? state.friendCardId : null);

            // Append only the arrival slot. Existing cards retain their glow
            // until this queued card has actually landed.
            tableView.ShowTableCards(entries, slotCount, false);
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
                        RectTransform crt = ResponsiveCanvas.Content(canvas) as RectTransform;
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

            // Landing commits this snapshot's winner without rebuilding cards.
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

        string key = TrickResolveKey(state);
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
            seatAnchor = OpponentHandsView.GetSelfHandAnchor(GetSeatSpaceRect());
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

        Vector2 beyondAp = seatAp;
        Vector3 winnerWorld = space.TransformPoint(new Vector3(beyondAp.x, beyondAp.y, 0f));

        Log("[trick] " + state.lastTrickWinnerNickname + " 승리(rel=" + rel
            + " anchor=" + seatAnchor.x.ToString("F2") + "," + seatAnchor.y.ToString("F2")
            + ") 점수카드 " + pointCards.Count + "장 → 승자 핸드로 이동");
        trickWinAnimator.Play(
            state.lastTrickWinnerNickname,
            pointCards,
            centerWorld,
            winnerWorld,
            tableView,
            null);
    }

    private static string TrickResolveKey(GameState state)
    {
        return state.lastTrickWinnerNickname + ":" + SumPlayerScores(state) + ":" + state.trickNumber;
    }

    private static int SumPlayerScores(GameState state)
    {
        if (state == null || state.players == null) return 0;
        int s = 0;
        for (int i = 0; i < state.players.Length; i++)
            if (state.players[i] != null) s += state.players[i].score;
        return s;
    }

    private HandView.TableCardEntry[] BuildTableEntries(GameState state)
    {
        var tableCards = state != null ? state.tableCards : null;
        if (tableCards == null) return new HandView.TableCardEntry[0];
        var entries = new HandView.TableCardEntry[tableCards.Length];
        for (int i = 0; i < tableCards.Length; i++)
        {
            TableCardInfo t = tableCards[i];
            PlayerInfo p = null;
            if (t != null && state != null && state.players != null)
            {
                for (int k = 0; k < state.players.Length; k++)
                {
                    PlayerInfo cand = state.players[k];
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
                isWinning = tableCards.Length >= 2 && t != null && t.card != null && t.card.id == state.tableWinningCardId,
                playerNickname = t != null ? t.playerNickname : null,
                isDeclarer = p != null && IsDeclarerPlayer(p, state),
                isFriend = p != null && IsPublicFriendSeat(p, state),
                isFriendSecret = p != null && IsSecretFriendSeat(p, state),
                declaredSuit = t != null ? t.declaredSuit : null,
                jokerCallActivated = t != null && t.jokerCallActivated,
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
            // Bidding/friend choices require reading the hand, but not playing it.
            // Only dim unavailable cards once trick play has actually started.
            handView.SetAllPlayable(false, currentState != null && currentState.status == "playing");
            return;
        }

        TableCardSnapshot[] table = BuildTableSnapshots(currentState);
        string mighty = currentState.mightyCardId;
        string jokerCall = currentState.jokerCallCardId;
        CardData[] hand = myHandCards;
        handView.ApplyPlayability(c =>
            TutorialAllowsCard(c) && CardPlayLegality.CanPlay(c, hand, table, mighty, jokerCall));
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
            friendIsPlayer = state.friendChosen && state.friendType == "player",
            friendCardNone = state.friendChosen
                && state.friendType == "none",
            teamScoreLabel = teamScore,
            bidLabel = bid,
        });
    }

    // 마이티 공개 전: 주공 개인 점수만 / 공개 후: 주공팀 합산(서버 live)
    private string FormatDeclarerTeamScoreHud(GameState state)
    {
        if (state == null) return null;
        string st = state.status;
        if (st != "playing" && st != "finished" && st != "exchanging_kitty" && st != "choosing_friend")
            return null;

        string score = state.declarerTeamScore + L10n.Text("점");
        if (st != "finished" && state.declarerClientId == myClientId && state.kittyScore > 0)
            score += string.Format(L10n.Text(" (버린패 {0})"), state.kittyScore);
        return score;
    }

    private static string FormatBidHud(GameState state)
    {
        if (state == null) return null;
        if (state.targetScore > 0)
            return state.targetScore + L10n.Text("점");
        if (state.highestBid != null && state.highestBid.targetScore > 0)
            return state.highestBid.targetScore + L10n.Text("점");
        return null;
    }

    private string FormatFriendHud(GameState state)
    {
        if (state == null || !state.friendChosen) return L10n.Text("미정");
        if (state.friendType == "none") return L10n.Text("없음");
        if (state.friendType == "player")
            return string.IsNullOrEmpty(state.friendNickname) ? "-" : state.friendNickname;
        // card
        string decl = FriendDeclLabel(state.friendCardId, state.mightyCardId);
        if (state.friendRevealed && !string.IsNullOrEmpty(state.friendNickname))
            return state.friendNickname + " (" + decl + ")";
        return decl + L10n.Text(" (미공개)");
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
            OpponentHandsView.SeatInfo info = ToSeatInfo(p, state);
            // Bidding and active rounds always follow a completed deal. Preserve a
            // visible stack through an occasional count-less state update.
            info.retainHandWhenCountUnknown = true;
            seats.Add(info);
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
        // The role badge lives in the seat HUD. Hand updates can arrive after
        // game_state and also move the hand above that HUD in Canvas order.
        if (opponentHandsView == null || currentState == null || currentState.players == null)
            return;
        string phase = currentState.status;
        if (phase != "bidding" && phase != "exchanging_kitty"
            && phase != "choosing_friend" && phase != "playing") return;
        foreach (PlayerInfo player in currentState.players)
        {
            if (player == null || player.clientId != myClientId) continue;
            opponentHandsView.ShowSelf(ToSeatInfo(player, currentState));
            break;
        }
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
        return (state.friendType == "player" || state.friendRevealed)
            && !string.IsNullOrEmpty(state.friendClientId)
            && p.clientId == state.friendClientId;
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
            default: return L10n.Text("노기루");
        }
    }

    // 카드 id를 짧게 (S_A -> SA, JOKER -> 조커)
    private static string CardKor(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return "-";
        if (cardId == "JOKER") return L10n.Text("조커");
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
        if (string.IsNullOrEmpty(cardId)) return L10n.Text("카드 프렌드");
        if (cardId == "JOKER") return L10n.Text("조커 프렌드");
        if (!string.IsNullOrEmpty(mightyId) && cardId == mightyId)
            return L10n.Text("마이티 프렌드 (") + CardKor(cardId) + ")";
        return CardKor(cardId) + L10n.Text(" 프렌드");
    }

    private void Log(string line)
    {
        Debug.Log("[NetworkManager] " + line);
    }

    private void OnDestroy()
    {
        if (emoteChat != null) Destroy(emoteChat.gameObject);
        GameSettings.LanguageChanged -= RefreshLanguage;
        if (connection == null) return;
        connection.Ready -= OnConnectionReady;
        connection.Disconnected -= OnConnectionLost;
        connection.Message -= HandleMessage;
    }

    private void OpenGameMenu()
    {
        SettingsPanel.OpenMenu(FindFirstObjectByType<Canvas>(), LeaveRoom);
    }

    // ---------- 화면 UI (씬 세팅 없이 자동 표시) ----------
    private static float CornerHudHeight(bool showKittyDiscard) =>
        showKittyDiscard && !ResponsiveCanvas.IsPortrait ? 172f : 76f;

    private void DrawIdleCornerHud(bool showKittyDiscard)
    {
        // The tutorial owns the top bar and exit button. Keep the one required
        // exchange action below the table, clear of its persistent instruction.
        if (tutorialState != null)
        {
            if (showKittyDiscard)
            {
                Rect tutorialSafe = GuiSafeArea;
                float width = Mathf.Min(220f, tutorialSafe.width - 24f);
                GUI.enabled = discardSelected.Count == 3;
                if (UiButton.Rect(new Rect(tutorialSafe.center.x - width * 0.5f,
                    tutorialSafe.yMin + tutorialSafe.height * 0.68f, width, 44f),
                    L10n.Text("3장 버리기 (") + discardSelected.Count + "/3)")) DiscardKitty();
                GUI.enabled = true;
            }
            return;
        }
        float mw = showKittyDiscard ? 208f : 148f;
        float mh = CornerHudHeight(showKittyDiscard);
        Rect safe = GuiSafeArea;
        // A single portrait toolbar keeps the rule panel's reserved area stable
        // when the declarer starts discarding, instead of pushing it into seats.
        if (showKittyDiscard && ResponsiveCanvas.IsPortrait)
        {
            GUILayout.BeginArea(new Rect(safe.xMin + 8f, safe.yMin + 8f,
                Mathf.Max(1f, safe.width - 16f), mh), GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (UiButton.Layout(L10n.Text("메뉴"), GUILayout.Height(44f))) OpenGameMenu();
            GUILayout.Space(8f);
            GUI.enabled = discardSelected.Count == 3;
            if (UiButton.Layout(L10n.Text("버리기 (") + discardSelected.Count + "/3)", GUILayout.Height(44f)))
                DiscardKitty();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            return;
        }
        mw = Mathf.Min(mw, Mathf.Max(120f, safe.width - 16f));
        float mx = safe.xMax - mw - 8f;
        float my = safe.yMin + 8f;
        GUILayout.BeginArea(new Rect(mx, my, mw, mh), GUI.skin.box);
        if (UiButton.Layout(L10n.Text("메뉴"), GUILayout.Height(44f))) OpenGameMenu();
        if (showKittyDiscard)
        {
            GUILayout.Label(L10n.Text("선택 ") + discardSelected.Count + "/3");
            GUI.enabled = discardSelected.Count == 3;
            if (UiButton.Layout(L10n.Text("3장 버리기"), GUILayout.Height(44f)))
                DiscardKitty();
            GUI.enabled = true;
        }
        GUILayout.EndArea();
    }

    private void OnGUI()
    {
        if (NicknameInput.IsOpen) return;
        if (TutorialBlocksInput) return;
        if (SettingsPanel.IsOpen) return;
        Matrix4x4 previousMatrix = GUI.matrix;
        GUISkin previousSkin = GUI.skin;
        Color previousColor = GUI.color;
        bool previousEnabled = GUI.enabled;
        try
        {
            Rect viewport = ResponsiveCanvas.Viewport;
            GUI.matrix = Matrix4x4.TRS(
                new Vector3(viewport.xMin, viewport.yMin, 0f), Quaternion.identity,
                Vector3.one * UiFonts.MenuScale);
            GUI.skin = MightyGuiSkin.Get(uiFont, CompactChoice ? 13 : UiFonts.GameMenuSize, CompactChoice);
            DrawMenu();
        }
        finally
        {
            GUI.matrix = previousMatrix;
            GUI.skin = previousSkin;
            GUI.color = previousColor;
            GUI.enabled = previousEnabled;
        }
    }

    private void UpdateBiddingWait()
    {
        // A single toast owns both the pending bid and its submitted result.
        // Let bid/pass announcements finish before showing the next bidder.
        if (trickWinAnimator != null && trickWinAnimator.IsAnnouncing)
        {
            biddingWaitClientId = null;
            return;
        }
        bool show = inRoom && currentState != null && currentState.status == "bidding"
            && currentState.currentBidderClientId != myClientId && !IsDealInProgress()
            && !TutorialBlocksInput && !SettingsPanel.IsOpen
            && !string.IsNullOrEmpty(currentState.currentBidderNickname);
        if (!show)
        {
            if (biddingWaitClientId != null && trickWinAnimator != null && !choosingToastVisible && !dealMissToastVisible)
                trickWinAnimator.ClearStickyToast();
            biddingWaitClientId = null;
            return;
        }
        if (biddingWaitClientId == currentState.currentBidderClientId) return;
        EnsureTrickWinAnimator();
        if (trickWinAnimator == null || trickWinAnimator.IsBusy) return;
        biddingWaitClientId = currentState.currentBidderClientId;
        string bidder = currentState.currentBidderNickname;
        trickWinAnimator.ShowStickyToast(() => GameSettings.Language == "en"
            ? bidder + " is considering a bid."
            : bidder + "님이 공약을 검토하고 있습니다.");
    }

    private void DrawMenu()
    {
        string phase = currentState != null ? currentState.status : "waiting";
        bool inKittyExchange = inRoom && phase == "exchanging_kitty" && !electionPresenting && !kittyPickupStarted;
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

        if (gameRuleHud != null)
            gameRuleHud.SetTopClearance(!needChoiceMenu && ResponsiveCanvas.IsPortrait
                ? (8f + CornerHudHeight(iAmKittyDeclarer) + 12f) * UiFonts.MenuScale : 0f);

        // 선택이 필요 없으면 메뉴 비활성 (우상단 나가기만)
        if (!needChoiceMenu)
        {
            DrawIdleCornerHud(iAmKittyDeclarer);
            return;
        }

        Rect safe = GuiSafeArea;
        float safeTop = safe.yMin;
        float margin = ResponsiveCanvas.IsPortrait ? 12f : 24f;
        float availableW = Mathf.Max(1f, safe.width - margin * 2f);
        float availableH = Mathf.Max(1f, safe.height - margin * 2f);
        compactUi = ResponsiveCanvas.IsPortrait;
        bool lobbyPanel = !inRoom || phase == "waiting";
        float desiredW = compactUi ? (lobbyPanel ? 342f : 390f)
            : !inRoom ? 540f : phase == "waiting" ? 600f
            : isFinished ? 720f : myBidTurn ? 420f : 640f;
        float panelW = Mathf.Min(desiredW, availableW);
        float desiredH = !inRoom ? (compactUi ? 480f : 420f)
            : phase == "waiting" ? (compactUi ? 480f : 420f)
            : isFinished ? 680f
            : myBidTurn ? 360f : 560f;
        float panelH = Mathf.Min(availableH, desiredH);
        bool tableChoice = inGame && !isFinished;
        // Keep the bottom hand and the seat HUD visible while choosing a bid/friend.
        if (tableChoice) panelH = Mathf.Min(panelH,
            safe.height * (compactUi && phase == "choosing_friend" ? 0.60f : 0.48f));
        guiPanelHeight = panelH;
        float panelX = safe.xMin + (safe.width - panelW) * 0.5f;
        float panelY = safeTop + (safe.height - panelH) * 0.5f;
        if (tableChoice) panelY = safeTop + safe.height * 0.44f - panelH * 0.5f;
        // Reserve the actual box padding and a scrollbar gutter before sizing rows.
        guiColGap = 8f;
        float contentWidth = panelW - GUI.skin.box.padding.horizontal - 22f;
        guiColW = Mathf.Max(1f, (contentWidth - guiColGap) * 0.5f);
        guiBtnH = GuiControlHeight;
        // Full-screen backgrounds belong to menus; covering the table here hid
        // the just-dealt hand as soon as the bidding controls became visible.
        if (!tableChoice)
        {
            GUI.color = MightyTheme.Table;
            GUI.DrawTexture(new Rect(safe.xMin, safeTop, safe.width, safe.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        GUILayout.BeginArea(new Rect(panelX, panelY, panelW, panelH), GUI.skin.box);
        menuScroll = GUILayout.BeginScrollView(menuScroll, false, false);
        GUILayout.BeginVertical(GUILayout.Width(contentWidth));

        // 공약 선택 중·대기실에서는 서버 URL 숨김
        if (!myBidTurn && !tableChoice)
        {
            GUILayout.Label(inGame ? "MIGHTY  ·  TABLE" : "MIGHTY  ·  LOBBY");
            GUILayout.Space(6);
        }

        if (singlePlayerStarting)
        {
            GUILayout.Label(L10n.Text("싱글플레이 준비 중..."));
            GUILayout.Label(L10n.Text("봇 4명과 게임을 시작합니다."));
        }
        else if (!inRoom)
        {
            float labelW = GameSettings.Language == "en" ? 100f : (compactUi ? 76f : 96f);
            float fieldH = GuiControlHeight;
            float btnH = GuiControlHeight;
            // ---- 로비 화면 ----
            if (inviteMode) GUILayout.Label(L10n.Text("초대받은 방: ") + roomIdInput);
            else
            {
                GUILayout.BeginHorizontal();
                if (UiButton.Toggle(!joinMode, L10n.Text("방 만들기"), GUI.skin.button, GUILayout.Height(btnH))) joinMode = false;
                if (UiButton.Toggle(joinMode, L10n.Text("방 참여"), GUI.skin.button, GUILayout.Height(btnH))) joinMode = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(L10n.Text("닉네임:"), GUILayout.Width(labelW));
            if (GUILayout.Button(nickname, GUI.skin.textField,
                GUILayout.Width(guiColW * 2f + guiColGap - labelW), GUILayout.Height(fieldH)))
            {
                Sfx.UiClick();
                NicknameInput.Open(nickname, value => nickname = value);
            }
            GUILayout.EndHorizontal();

            if (!inviteMode && joinMode)
            {
                GUILayout.Label(L10n.Text("참여할 방 코드를 입력하세요"));
                GUILayout.BeginHorizontal();
                GUILayout.Label(L10n.Text("방 코드:"), GUILayout.Width(labelW));
                roomIdInput = GUILayout.TextField(roomIdInput, 4, GUILayout.Height(fieldH));
                GUILayout.EndHorizontal();
            }
            if (!inviteMode || inviteNeedsPassword)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(L10n.Text("비번:"), GUILayout.Width(labelW));
                passwordInput = GUILayout.PasswordField(passwordInput, '*', GUILayout.Height(fieldH));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            bool joining = inviteMode || joinMode;
            if (UiButton.Layout(L10n.Text(joining ? "입장" : "방 만들기"), GUILayout.Height(btnH)))
            {
                if (joining) JoinRoom(); else CreateRoom();
            }
            if (!string.IsNullOrEmpty(lobbyFeedback)) GUILayout.Label(L10n.ServerMessage(lobbyFeedback));
            if (UiButton.Layout(L10n.Text("타이틀로"), GUILayout.Height(btnH)))
            {
                LeaveRoom();
                RoomNavigation.SetMode("");
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Title);
            }
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

        GUILayout.EndVertical();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ---- 대기방 화면 (게임 시작 전) ----
    private void DrawWaitingRoom()
    {
        if (!singlePlayerSession && !tutorialRequested)
        {
            if (UiButton.Layout(L10n.Text("초대 링크 복사"), GUILayout.Height(GuiControlHeight)))
            {
                inviteFeedback = "초대 링크 복사 중...";
                RoomNavigation.CopyInvite(myRoomId, gameObject.name);
            }
            if (!string.IsNullOrEmpty(inviteFeedback)) GUILayout.Label(L10n.Text(inviteFeedback));
        }
        GUILayout.Label(compactUi
            ? (L10n.Text("방 ") + myRoomId + L10n.Text(" · 대기실"))
            : (L10n.Text("방 코드: ") + myRoomId
                + (currentState != null ? L10n.Text("   (상태: ") + L10n.Text(currentState.status) + ")" : "")));

        int count = (currentState != null && currentState.players != null) ? currentState.players.Length : 0;
        GUILayout.Label(L10n.Text("플레이어 (") + count + "/5):");
        if (currentState != null && currentState.players != null)
        {
            if (compactUi)
            {
                float listHeight = Mathf.Min(5, Mathf.Max(1, count)) * 32f + GUI.skin.box.padding.vertical;
                waitingPlayersScroll = GUILayout.BeginScrollView(
                    waitingPlayersScroll, GUI.skin.box, GUILayout.Height(listHeight));
                foreach (PlayerInfo p in currentState.players)
                    GUILayout.Label(CompactPlayerLabel(p), WaitingPlayerStyle(), GUILayout.MinHeight(28f));
                GUILayout.EndScrollView();
            }
            else
            {
                foreach (PlayerInfo p in currentState.players)
                {
                    GUILayout.Label("  " + (p.isHost ? L10n.Text("[방장] ") : "") + (p.isBot ? L10n.Text("[봇] ") : "") + p.nickname
                        + L10n.Text("  누적 ") + p.sessionScore
                        + (p.isReady ? " ●" : " ○")
                        + DisconnectLabel(p)
                        + (p.clientId == myClientId ? L10n.Text("  <- 나") : ""));
                }
            }
        }

        GUILayout.Space(6);
        float btnH = guiBtnH;
        float col = guiColW;
        float gap = guiColGap;
        if (IAmHost())
        {
            GUILayout.BeginHorizontal();
            if (UiButton.Layout(ReadyButtonLabel(), GUILayout.Width(col), GUILayout.Height(btnH)))
                ToggleReady();
            GUILayout.Space(gap);
            GUI.enabled = currentState != null && currentState.canStart;
            if (UiButton.Layout(compactUi ? L10n.Text("시작") : L10n.Text("게임 시작(방장)"), GUILayout.Width(col), GUILayout.Height(btnH)))
                StartGame();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (UiButton.Layout(compactUi ? L10n.Text("섞기") : L10n.Text("자리 섞기(방장)"), GUILayout.Width(col), GUILayout.Height(btnH)))
                ShuffleSeats();
            GUILayout.Space(gap);
            if (UiButton.Layout(compactUi ? L10n.Text("점수 초기화") : L10n.Text("점수 초기화(방장)"), GUILayout.Width(col), GUILayout.Height(btnH)))
                ResetScores();
            GUILayout.EndHorizontal();
        }
        else
        {
            if (UiButton.Layout(ReadyButtonLabel(), GUILayout.Width(col * 2f + gap), GUILayout.Height(btnH)))
                ToggleReady();
        }

        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        if (UiButton.Layout(L10n.Text("메뉴"), GUILayout.Width(col), GUILayout.Height(btnH)))
            OpenGameMenu();
        GUILayout.EndHorizontal();
    }

    private string ReadyButtonLabel()
    {
        bool ready = false;
        if (currentState != null && currentState.players != null)
            foreach (var player in currentState.players)
                if (player.clientId == myClientId) { ready = player.isReady; break; }
        return (ready ? "● " : "○ ") + L10n.Text(ready ? "준비 취소" : "준비");
    }

    private GUIStyle WaitingPlayerStyle()
    {
        if (waitingPlayerStyle == null)
            waitingPlayerStyle = new GUIStyle(GUI.skin.label);
        waitingPlayerStyle.font = uiFont != null ? uiFont : GUI.skin.font;
        waitingPlayerStyle.fontSize = GUI.skin.label.fontSize;
        waitingPlayerStyle.wordWrap = true;
        waitingPlayerStyle.clipping = TextClipping.Clip;
        return waitingPlayerStyle;
    }

    private string CompactPlayerLabel(PlayerInfo p)
    {
        if (p == null) return "-";
        string name = p.nickname ?? "?";
        if (name.Length > 7) name = name.Substring(0, 6) + "…";
        string role = p.isHost ? L10n.Text("[방] ") : (p.isBot ? L10n.Text("[봇] ") : "");
        string ready = p.isReady ? "●" : "○";
        string mine = p.clientId == myClientId ? L10n.Text(" 나") : "";
        return role + name + "  " + p.sessionScore + L10n.Text("점 ") + ready + mine + DisconnectLabel(p);
    }

    // ---- 입찰 단계 화면 ----
    private static int MinimumBid(GameState state)
    {
        if (state.nextMinBid > 0) return state.nextMinBid;
        return state.highestBid != null ? state.highestBid.targetScore + 1
            : state.minBid > 0 ? state.minBid : 13;
    }

    private void UpdateBidDefault(GameState previous, GameState next)
    {
        if (next.status != "bidding") return;
        // Reset on a new turn/round, including the 13 -> 12 -> 11 all-pass rounds.
        // Repeated snapshots in the same turn must preserve the user's choice.
        if (previous == null || previous.status != "bidding"
            || previous.roomId != next.roomId
            || previous.currentBidderClientId != next.currentBidderClientId
            || previous.minBid != next.minBid
            || MinimumBid(previous) != MinimumBid(next))
            bidScoreInput = MinimumBid(next).ToString();
    }

    private void DrawBidding()
    {
        if (currentState == null) return;

        bool dealing = (dealAnimator != null && dealAnimator.IsBusy)
            || pendingDealHand != null
            || pendingDealAnim;
        if (dealing)
        {
            GUILayout.Label(L10n.Text("카드를 섞고 나누는 중..."));
            return;
        }

        bool myBidTurn = currentState.currentBidderClientId == myClientId;
        int floorMin = currentState.minBid > 0 ? currentState.minBid : 13;
        int raiseMin = MinimumBid(currentState);

        if (currentState.highestBid != null)
        {
            HighestBid h = currentState.highestBid;
            GUILayout.Label(L10n.Text("최고 공약: ") + h.nickname + " " + h.targetScore
                + " " + (h.noTrump ? L10n.Text("노기루") : SuitKor(h.trumpSuit)));
        }
        else GUILayout.Label(L10n.Text("아직 공약 없음"));

        GUILayout.Label(L10n.Text("최소 ") + floorMin
            + (currentState.highestBid != null ? (L10n.Text("  · 올릴 최소 ") + raiseMin) : ""));

        if (myBidTurn)
        {
            int cur;
            if (!int.TryParse(bidScoreInput, out cur) || cur < raiseMin)
                bidScoreInput = raiseMin.ToString();
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Label(L10n.Text("공약:"), GUILayout.Width(60f));
            bidScoreInput = GUILayout.TextField(bidScoreInput, 2, GUILayout.Width(64f), GUILayout.Height(GuiControlHeight));
            GUILayout.Label(L10n.Text("기루다:"), GUILayout.Width(80f));
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
            if (UiButton.Layout(L10n.Text("공약 제출 (") + raiseMin + "+)", GUILayout.Width(guiColW), GUILayout.Height(GuiControlHeight))) SendBid();
            GUILayout.Space(guiColGap);
            if (UiButton.Layout(L10n.Text("패스"), GUILayout.Width(guiColW), GUILayout.Height(GuiControlHeight))) PassBid();
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label(L10n.Text("(다른 사람 입찰을 기다리는 중...)"));

        if (myCanDealMiss)
        {
            GUILayout.Space(4);
            GUI.color = new Color(1f, 0.5f, 0.5f);
            if (UiButton.Layout(L10n.Text("딜미스 (손패≤0.5점 · 다시 돌리기)"))) DeclareDealMiss();
            GUI.color = Color.white;
        }

        GUILayout.Space(6);
        if (UiButton.Layout(L10n.Text("메뉴"), GUILayout.Width(guiColW), GUILayout.Height(GuiControlHeight))) OpenGameMenu();
    }

    // ---- 바닥패 교환 화면 ----
    private void DrawKittyExchange()
    {
        GUILayout.Label(L10n.Text("바닥패 교환"));
        if (currentState == null) return;

        GUILayout.Label(L10n.Text("주공: ") + currentState.declarerNickname
            + L10n.Text(" / 승리 조건 ") + currentState.targetScore + L10n.Text("점 이상"));

        bool iAmDeclarer = currentState.declarerClientId == myClientId;
        if (iAmDeclarer)
        {
            GUILayout.Label(L10n.Text("손패에서 버릴 카드 3장을 고르세요. (다시 클릭=해제)"));
            GUILayout.Label(L10n.Text("선택: ") + discardSelected.Count + "/3"
                + (discardSelected.Count > 0 ? " [" + string.Join(", ", discardSelected) + "]" : ""));

            GUI.enabled = discardSelected.Count == 3;
            if (UiButton.Layout(L10n.Text("3장 버리기"), GUILayout.Height(UiFonts.Layout(36f)))) DiscardKitty();
            GUI.enabled = true;
        }
        else GUILayout.Label(L10n.Text("(주공이 바닥패 3장을 고르는 중...)"));

        GUILayout.Space(6);
        if (UiButton.Layout(L10n.Text("메뉴"))) OpenGameMenu();
    }

    private void DiscardKitty()
    {
        if (discardSelected.Count != 3) return;
        Send(JsonUtility.ToJson(new DiscardKittyMsg {
            data = new DiscardKittyData { cardIds = discardSelected.ToArray() }
        }));
        Sfx.Discard();
        Log("[discard_kitty] 전송: " + string.Join(",", discardSelected));
    }

    // ---- 프렌드 선택 단계 화면 ----
    private void DrawChoosingFriend()
    {
        GUILayout.Label(L10n.Text("프렌드 선택 단계"));
        if (currentState == null) return;

        GUILayout.Label(L10n.Text("주공: ") + currentState.declarerNickname
            + L10n.Text(" / 기루다 ") + (currentState.noTrump ? L10n.Text("노기루") : SuitKor(currentState.trumpSuit)));
        GUILayout.Label(L10n.Text("★ 주공팀 승리 조건: ") + currentState.targetScore + L10n.Text("점 이상"));

        bool iAmDeclarer = currentState.declarerClientId == myClientId;
        if (iAmDeclarer)
        {
            if (pendingFriendChoice != null)
            {
                GUILayout.Space(12);
                GUILayout.Label(pendingFriendChoice.friendCardId == "NONE"
                    ? L10n.Text("노프렌드로 선언하시겠습니까?")
                    : string.Format(L10n.Text("{0}을 프렌드로 선언하시겠습니까?"), pendingFriendLabel));
                GUILayout.BeginHorizontal();
                if (UiButton.Layout(L10n.Text("네"), GUILayout.Height(GuiControlHeight))) ConfirmFriendChoice();
                GUILayout.Space(guiColGap);
                if (UiButton.Layout(L10n.Text("아니오"), GUILayout.Height(GuiControlHeight))) ClearFriendChoice();
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.Label(L10n.Text("프렌드를 지정하세요:"));
            GUILayout.BeginHorizontal();
            if (UiButton.Layout(L10n.Text("마이티"), GUILayout.Height(GuiControlHeight)))
                ChooseFriend(currentState.mightyCardId);
            GUILayout.Space(guiColGap);
            if (UiButton.Layout(L10n.Text("조커"), GUILayout.Height(GuiControlHeight)))
                ChooseFriend("JOKER");
            GUILayout.Space(guiColGap);
            if (UiButton.Layout(L10n.Text("노프렌드"), GUILayout.Height(GuiControlHeight)))
                ChooseFriend("NONE");
            GUILayout.EndHorizontal();

            GUILayout.Label(L10n.Text("카드 프렌드 — 무늬 선택 후 카드 클릭:"));
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
            float cardWidth = compactUi ? (guiColW * 2f + guiColGap) / 5f : IconSpriteAtlas.DisplayCard.x;
            float cardHeight = compactUi ? 36f : IconSpriteAtlas.DisplayCard.y;
            int cardsPerRow = compactUi ? 5 : Mathf.Max(1, Mathf.FloorToInt(
                (guiColW * 2f + guiColGap) / cardWidth));
            for (int r = 0; r < ranks.Length; r++)
            {
                if (r > 0 && r % cardsPerRow == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                string id = IconSpriteAtlas.CardId(suit, ranks[r]);
                if (IconGui.Button(IconSpriteAtlas.GetCard(id), cardWidth, cardHeight))
                    ChooseFriend(id);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(L10n.Text("플레이어 프렌드 (즉시 공개):"));
            if (currentState.players != null)
            {
                int playerIndex = 0;
                foreach (PlayerInfo p in currentState.players)
                {
                    if (p.clientId == currentState.declarerClientId) continue;
                    if (compactUi && playerIndex % 2 == 0) GUILayout.BeginHorizontal();
                    if (compactUi && playerIndex % 2 == 1) GUILayout.Space(guiColGap);
                    string label = (p.isBot ? L10n.Text("[봇] ") : "") + p.nickname;
                    if (UiButton.Layout(label, GUILayout.Width(compactUi ? guiColW : guiColW * 2f + guiColGap),
                        GUILayout.Height(GuiControlHeight))) ChooseFriendPlayer(p.clientId);
                    playerIndex++;
                    if (compactUi && playerIndex % 2 == 0) GUILayout.EndHorizontal();
                }
                if (compactUi && playerIndex % 2 != 0) GUILayout.EndHorizontal();
            }
        }
        else GUILayout.Label(L10n.Text("(주공이 프렌드를 고르는 중...)"));

        GUILayout.Space(6);
        if (UiButton.Layout(L10n.Text("메뉴"))) OpenGameMenu();
    }

    // ---- 결과 화면 (한 판 종료) ----
    private void DrawFinished()
    {
        GUILayout.Label(L10n.Text("한 판 종료"));

        // 버튼은 위에 고정 — 상세 내용이 길어도 잘리지 않게
        if (finishedAutoLobbyAt > 0f)
        {
            float remain = Mathf.Max(0f, finishedAutoLobbyAt - Time.realtimeSinceStartup);
            GUILayout.Label(L10n.Text("자동 대기방 복귀: ") + remain.ToString("0") + L10n.Text("초"));
        }
        GUILayout.BeginHorizontal();
        if (UiButton.Layout(compactUi ? L10n.Text("대기실로") : L10n.Text("대기방으로 돌아가기"), GUILayout.Width(guiColW), GUILayout.Height(GuiControlHeight)))
        {
            finishedAutoLobbyAt = -1f;
            ReturnToLobby();
        }
        GUILayout.Space(guiColGap);
        if (UiButton.Layout(L10n.Text("메뉴"), GUILayout.Width(guiColW), GUILayout.Height(GuiControlHeight)))
        {
            OpenGameMenu();
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6);

        GameFinishedData r = lastResult;
        if (r == null)
        {
            GUILayout.Label(L10n.Text("(결과 수신 대기...)"));
            return;
        }

        // ExpandHeight 쓰지 않음 — 바깥 FlexibleSpace가 세로 가운데를 잡도록
        finishedScroll = GUILayout.BeginScrollView(
            finishedScroll, GUILayout.Height(Mathf.Max(100f, guiPanelHeight - 210f)));
        GUILayout.Label(L10n.Text("승자: ") + L10n.Text(r.winnerLabel));
        GUILayout.Label(L10n.Text("주공팀 목표였던 점수: ") + r.targetScore + L10n.Text("점"));
        GUILayout.Label(L10n.Text("주공: ") + r.declarerNickname
            + (string.IsNullOrEmpty(r.friendNickname) ? L10n.Text(" (단독)") : L10n.Text(" + 프렌드 ") + r.friendNickname));
        GUILayout.Label(L10n.Text("결과 — 주공팀 ") + r.declarerTeamScore + L10n.Text("점")
            + L10n.Text("  |  수비팀 ") + r.defenderTeamScore + L10n.Text("점"));
        GUILayout.Label(string.Format(L10n.Text("버린패 {0}점 (주공팀 합계에 포함)"), r.kittyScore));
        if (r.winner == "declarer")
            GUILayout.Label(L10n.Text("(목표 ") + r.targetScore + L10n.Text("점 달성)"));
        else
            GUILayout.Label(L10n.Text("(목표 ") + r.targetScore + L10n.Text("점까지 ")
                + Mathf.Max(0, r.targetScore - r.declarerTeamScore) + L10n.Text("점 부족)"));

        if (r.isRun) GUILayout.Label(L10n.Text("★ 런! (주공팀 20점 전부)"));
        if (r.isBackrun) GUILayout.Label(L10n.Text("★ 백런! (주공팀 10점 이하)"));
        if (r.multiplier > 1)
        {
            string tags = (r.multipliers != null && r.multipliers.Length > 0)
                ? string.Join(" + ", System.Array.ConvertAll(r.multipliers, L10n.Text)) : "";
            GUILayout.Label(L10n.Text("배수: ×") + r.multiplier
                + (string.IsNullOrEmpty(tags) ? "" : " (" + tags + ")"));
        }
        else GUILayout.Label(L10n.Text("배수: ×1"));
        GUILayout.Label(L10n.Text("정산 단위: ") + r.stakeBase + " × " + r.multiplier + " = " + r.stakeTotal);

        GUILayout.Space(4);
        GUILayout.Label(L10n.Text("이번 판 정산 / 누적 스코어:"));
        if (r.scoreboard != null)
        {
            foreach (ScoreboardEntry e in r.scoreboard)
            {
                string d = (e.delta >= 0 ? "+" : "") + e.delta;
                GUILayout.Label("  " + (e.isBot ? L10n.Text("[봇] ") : "") + e.nickname
                    + ": " + d + L10n.Text(" → 누적 ") + e.sessionScore);
            }
        }

        GUILayout.Space(4);
        GUILayout.Label(L10n.Text("주공팀:"));
        if (r.declarerTeam != null)
        {
            foreach (TeamPlayerScore p in r.declarerTeam)
                GUILayout.Label("  - " + p.nickname + ": " + p.score + L10n.Text("점"));
        }
        GUILayout.Label(L10n.Text("수비팀:"));
        if (r.defenderTeam != null)
        {
            foreach (TeamPlayerScore p in r.defenderTeam)
                GUILayout.Label("  - " + p.nickname + ": " + p.score + L10n.Text("점"));
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
        GUILayout.Label(L10n.Text("방 코드: ") + myRoomId);

        if (currentState != null)
        {
            // 기루다/마이티/조커콜/주공/프렌드는 좌상단 GameRuleHud
            GUILayout.Label(L10n.Text("★ 주공팀 목표: ") + currentState.targetScore + L10n.Text("점")
                + L10n.Text("  (현재 ") + FormatDeclarerTeamScoreHud(currentState) + L10n.Text(" / 남은 ")
                + currentState.pointsNeeded + L10n.Text("점)"));
            GUILayout.Label(L10n.Text("트릭 ") + currentState.trickNumber + " / 10");

            bool myTurn = currentState.currentTurnClientId == myClientId;
            GUILayout.Label(L10n.Text("현재 차례: ") + currentState.currentTurnNickname
                + (myTurn ? L10n.Text("  << 내 차례! (카드 클릭)") : ""));

            if (!string.IsNullOrEmpty(currentState.lastTrickWinnerNickname))
            {
                GUILayout.Label(L10n.Text("직전 트릭 승자: ") + currentState.lastTrickWinnerNickname);
            }

            if (currentState.tableCards != null && currentState.tableCards.Length > 0)
            {
                TableCardInfo lead = currentState.tableCards[0];
                if (lead != null && lead.card != null)
                {
                    if (lead.card.id == "JOKER" && !string.IsNullOrEmpty(lead.declaredSuit))
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(L10n.Text("조커 리드 무늬:"), GUILayout.Width(UiFonts.Layout(120f)));
                        IconGui.DrawLayout(IconSpriteAtlas.GetSuit(lead.declaredSuit), IconSpriteAtlas.DisplaySquare.x, IconSpriteAtlas.DisplaySquare.y);
                        GUILayout.EndHorizontal();
                    }
                    if (lead.jokerCallActivated)
                    {
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(L10n.Text("조커콜 활성"), GUILayout.Width(UiFonts.Layout(90f)));
                        IconGui.DrawLayout(IconSpriteAtlas.GetCard(currentState.jokerCallCardId), IconSpriteAtlas.DisplayCard.x, IconSpriteAtlas.DisplayCard.y);
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        GUILayout.Label(L10n.Text("플레이어 (남은/획득트릭):"));
        if (currentState != null && currentState.players != null)
        {
            foreach (PlayerInfo p in currentState.players)
            {
                string me = p.clientId == myClientId ? L10n.Text(" <- 나") : "";
                GUILayout.Label("  " + (p.isBot ? L10n.Text("[봇] ") : "") + p.nickname
                    + L10n.Text(" : 남은 ") + p.handCount + L10n.Text("장, 획득 ") + p.trickCount + L10n.Text("트릭")
                    + L10n.Text(", 누적 ") + p.sessionScore
                    + DisconnectLabel(p) + me);
            }
        }

        GUILayout.Space(6);
        if (UiButton.Layout(L10n.Text("메뉴"))) OpenGameMenu();
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
        if (expires <= 0) return L10n.Text(" (연결끊김·봇대타)");
        // Date.now()와 맞추기: UTC ms
        double nowMs = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMilliseconds;
        int sec = (int)System.Math.Ceiling(System.Math.Max(0, expires - nowMs) / 1000.0);
        return L10n.Text(" (연결끊김·봇대타 ") + (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00") + ")";
    }
}
